// Quick Actions — iOS native layer.
//
// Implements home-screen quick actions (UIApplicationShortcutItem) and bridges
// taps back to Unity. It hooks Unity's app delegate (UnityAppController) at
// load time via the Objective-C runtime, so the integrating project needs no
// manual AppDelegate edits.
//
// Delivery model (single pull channel, mirrors the C# side):
//   * Cold launch  -> captured in didFinishLaunchingWithOptions, queued, stored as "last".
//   * Warm resume  -> performActionForShortcutItem, queued, stored as "last".
// Both paths enqueue; C# drains the queue on first frame and on focus gain.
// performActionForShortcutItem runs before applicationDidBecomeActive, so the
// focus poll reliably catches a warm tap. No UnitySendMessage needed.

#import <UIKit/UIKit.h>
#import <objc/runtime.h>
#import <string.h>

// Unity compiles plugin .mm with ARC. The static NSString assignments below rely
// on ARC for retain/release; fail loudly rather than corrupt memory under MRC.
#if !__has_feature(objc_arc)
#error "QuickActions.mm requires ARC (Unity enables it for plugins by default)."
#endif

// Entire native layer is gated by QUICKACTIONS_ENABLED (set on the Xcode
// target by the iOS build post-processor only when the package is enabled).
// When the define is absent (production), this file compiles to nothing —
// no +load swizzle, no symbols.
#if QUICKACTIONS_ENABLED

// Runs `block` on the main thread, synchronously if already there. Used for
// UIKit writes so a same-frame read-back (GetShortcutsJson) sees them.
static void QARunOnMain(dispatch_block_t block) {
    if ([NSThread isMainThread]) block();
    else dispatch_async(dispatch_get_main_queue(), block);
}

// Per-session state, mirroring the Android side: the "last performed" id lives
// only for this process run (a cold launch sets it before Unity reads it), so a
// later normal launch never reports a stale shortcut.
static NSString *gQALastPerformed = nil;
// Queue of action ids awaiting delivery to the C# Performed event (cold launch).
static NSMutableArray<NSString *> *gQAPending = nil;
static id gQALock = nil;

static void QAEnsureState(void) {
    static dispatch_once_t once;
    dispatch_once(&once, ^{
        gQAPending = [NSMutableArray array];
        gQALock = [[NSObject alloc] init];
    });
}

// Records the tapped action: stores it as "last" and, when `queue` is YES,
// enqueues it for the single C# poll channel. Both cold and warm taps pass YES;
// `copy` pins the (possibly autoreleased) type string.
static void QAStorePerformed(NSString *type, BOOL queue) {
    if (type.length == 0) return;
    QAEnsureState();
    @synchronized (gQALock) {
        gQALastPerformed = [type copy];
        if (queue) [gQAPending addObject:[type copy]];
    }
}

// Returns a malloc'd copy of `s` (freed on the C# side via _QuickActions_FreeString,
// which calls free()), or NULL for nil/empty.
static char *QACopyCString(NSString *s) {
    if (s.length == 0) return NULL;
    const char *utf8 = s.UTF8String;
    if (utf8 == NULL) return NULL;
    size_t len = strlen(utf8) + 1;
    char *out = (char *)malloc(len);
    if (out != NULL) memcpy(out, utf8, len);
    return out;
}

// Marks the shortcuts THIS package created — dynamic items get it here, and static
// plist entries carry the same key in their UIApplicationShortcutItemUserInfo — so
// the app-delegate hooks below intercept ONLY ours and leave a host app's / another
// plugin's quick actions to their own routing.
static NSString *const kQAManagedMarkerKey = @"com.emindeniz99.quickactions.managed";

static BOOL QAIsOurShortcut(UIApplicationShortcutItem *item) {
    if (![item isKindOfClass:[UIApplicationShortcutItem class]]) return NO;
    id marker = item.userInfo[kQAManagedMarkerKey];
    return [marker isKindOfClass:[NSNumber class]] && [marker boolValue];
}

// Builds UIApplicationShortcutItems from {"items":[{Id,Title,Subtitle,Icon}]}.
static NSArray<UIApplicationShortcutItem *> *QABuildItems(NSString *json) {
    NSData *data = [json dataUsingEncoding:NSUTF8StringEncoding];
    if (data == nil) return @[];

    NSError *error = nil;
    NSDictionary *root = [NSJSONSerialization JSONObjectWithData:data options:0 error:&error];
    if (error != nil || ![root isKindOfClass:[NSDictionary class]]) return @[];

    NSArray *items = root[@"items"];
    if (![items isKindOfClass:[NSArray class]]) return @[];

    NSMutableArray<UIApplicationShortcutItem *> *result = [NSMutableArray array];
    for (NSDictionary *item in items) {
        if (![item isKindOfClass:[NSDictionary class]]) continue;

        NSString *identifier = item[@"Id"];
        NSString *title = item[@"Title"];
        if (identifier.length == 0 || title.length == 0) continue;

        NSString *subtitle = [item[@"Subtitle"] isKindOfClass:[NSString class]] ? item[@"Subtitle"] : nil;

        // IconType enum: 0 = None; 1..N map to UIApplicationShortcutIconType
        // (which starts at 0), so subtract 1. The C# enum is ordered to match.
        UIApplicationShortcutIcon *icon = nil;
        NSNumber *iconNumber = item[@"Icon"];
        if ([iconNumber isKindOfClass:[NSNumber class]] && iconNumber.integerValue > 0) {
            icon = [UIApplicationShortcutIcon iconWithType:(UIApplicationShortcutIconType)(iconNumber.integerValue - 1)];
        }

        UIApplicationShortcutItem *shortcut =
            [[UIApplicationShortcutItem alloc] initWithType:identifier
                                             localizedTitle:title
                                          localizedSubtitle:subtitle
                                                       icon:icon
                                                   userInfo:@{ kQAManagedMarkerKey: @YES }];
        [result addObject:shortcut];
    }
    return result;
}

#pragma mark - UnityAppController hooks (installed via the ObjC runtime)

static BOOL (*gQAOrigDidFinishLaunching)(id, SEL, UIApplication *, NSDictionary *) = NULL;

static BOOL QADidFinishLaunching(id self, SEL _cmd, UIApplication *application, NSDictionary *launchOptions) {
    UIApplicationShortcutItem *launchItem = launchOptions[UIApplicationLaunchOptionsShortcutItemKey];
    BOOL launchedFromOurShortcut = QAIsOurShortcut(launchItem);
    if (launchedFromOurShortcut) {
        QAStorePerformed(launchItem.type, YES);
    }

    BOOL result = YES;
    if (gQAOrigDidFinishLaunching != NULL) {
        result = gQAOrigDidFinishLaunching(self, _cmd, application, launchOptions);
    }

    // Return NO ONLY for OUR shortcut (we already captured it), so iOS doesn't also
    // call performActionForShortcutItem for the same item. For a HOST shortcut we must
    // NOT intercept — return the delegate's own result so the host's cold-launch
    // routing (its own performActionForShortcutItem path) still runs. This dedup relies
    // on the UIApplicationDelegate lifecycle Unity's trampoline uses by default; under
    // the UIScene lifecycle the cold shortcut arrives via the scene delegate — see ROADMAP.
    // NOTE: if a host UnityAppController subclass overrides this selector, calls super,
    // then returns YES unconditionally (ignoring our NO), iOS will ALSO call
    // performActionForShortcutItem and the cold tap is delivered twice. Host subclasses
    // should return the value from [super application:didFinishLaunchingWithOptions:].
    // (Known limitation — see ROADMAP.)
    return launchedFromOurShortcut ? NO : result;
}

static void (*gQAOrigPerformAction)(id, SEL, UIApplication *, UIApplicationShortcutItem *, void (^)(BOOL)) = NULL;

static void QAPerformActionForShortcutItem(id self, SEL _cmd, UIApplication *application,
                                           UIApplicationShortcutItem *shortcutItem,
                                           void (^completionHandler)(BOOL)) {
    if (QAIsOurShortcut(shortcutItem)) {
        // Enqueue for the single C# poll channel. This runs before
        // applicationDidBecomeActive, so the focus poll drains it on resume. Only
        // OUR shortcuts — a host shortcut is left entirely to its own handler below.
        QAStorePerformed(shortcutItem.type, YES);
    }
    // If UnityAppController already had an implementation (a host app or another
    // native plugin), chain to it and let it own the completion handler so the
    // existing warm-tap handler still runs — mirrors the didFinish path. Only
    // complete ourselves when there was no prior implementation (avoids a
    // double completionHandler call).
    if (gQAOrigPerformAction != NULL) {
        gQAOrigPerformAction(self, _cmd, application, shortcutItem, completionHandler);
    } else if (completionHandler != nil) {
        completionHandler(YES);
    }
}

@interface QuickActionsAppControllerHook : NSObject
@end

@implementation QuickActionsAppControllerHook

+ (void)load {
    // Install once. Guards against a duplicated +load capturing our own hook as
    // the "original" IMP, which would recurse infinitely on launch.
    static BOOL installed = NO;
    if (installed) return;
    installed = YES;

    Class cls = NSClassFromString(@"UnityAppController");
    if (cls == Nil) return;

    // Swizzle application:didFinishLaunchingWithOptions: (Unity implements it).
    SEL didFinishSel = @selector(application:didFinishLaunchingWithOptions:);
    Method didFinish = class_getInstanceMethod(cls, didFinishSel);
    if (didFinish != NULL) {
        gQAOrigDidFinishLaunching =
            (BOOL (*)(id, SEL, UIApplication *, NSDictionary *))method_getImplementation(didFinish);
        class_replaceMethod(cls, didFinishSel, (IMP)QADidFinishLaunching, method_getTypeEncoding(didFinish));
    } else {
        // Defensive fallback (Unity always implements the selector). BOOL is
        // 'c' (signed char) in the ObjC ABI.
        class_addMethod(cls, didFinishSel, (IMP)QADidFinishLaunching, "c@:@@");
    }

    // Install application:performActionForShortcutItem:completionHandler:
    // (Unity does not implement it, so normally we add it; if something already
    // implements it we preserve and chain to that IMP instead of dropping it).
    SEL performSel = @selector(application:performActionForShortcutItem:completionHandler:);
    const char *performTypes = "v@:@@@?";
    Method perform = class_getInstanceMethod(cls, performSel);
    if (perform != NULL) {
        // Preserve the existing implementation so QAPerformActionForShortcutItem
        // can chain to it (host app / another plugin also handling quick actions).
        gQAOrigPerformAction =
            (void (*)(id, SEL, UIApplication *, UIApplicationShortcutItem *, void (^)(BOOL)))method_getImplementation(perform);
        class_replaceMethod(cls, performSel, (IMP)QAPerformActionForShortcutItem, method_getTypeEncoding(perform));
    } else {
        class_addMethod(cls, performSel, (IMP)QAPerformActionForShortcutItem, performTypes);
    }
}

@end

#pragma mark - C API consumed by C# (DllImport "__Internal")

extern "C" {

void _QuickActions_SetShortcuts(const char *json) {
    NSString *value = json != NULL ? [NSString stringWithUTF8String:json] : @"";
    QARunOnMain(^{
        // Replace only OUR items: keep any host / other-plugin dynamic shortcuts
        // (unmarked) and append our current set. A plain assignment would wipe the
        // host's live UIApplicationShortcutItems on the first Add/RemoveById — mirror
        // the marker-scoped RemoveAll and read-back paths.
        UIApplication *app = [UIApplication sharedApplication];
        NSMutableArray<UIApplicationShortcutItem *> *merged = [NSMutableArray array];
        for (UIApplicationShortcutItem *item in app.shortcutItems)
            if (!QAIsOurShortcut(item)) [merged addObject:item];
        [merged addObjectsFromArray:QABuildItems(value)];
        app.shortcutItems = merged;
    });
}

void _QuickActions_RemoveAll(void) {
    QARunOnMain(^{
        // Remove only OUR shortcuts (marked) — preserve a host app's / another
        // plugin's dynamic UIApplicationShortcutItems instead of wiping everything.
        UIApplication *app = [UIApplication sharedApplication];
        NSMutableArray<UIApplicationShortcutItem *> *kept = [NSMutableArray array];
        for (UIApplicationShortcutItem *item in app.shortcutItems)
            if (!QAIsOurShortcut(item)) [kept addObject:item];
        app.shortcutItems = kept;
    });
}

char *_QuickActions_GetLastPerformed(void) {
    QAEnsureState();
    @synchronized (gQALock) { return QACopyCString(gQALastPerformed); }
}

void _QuickActions_ResetLastPerformed(void) {
    QAEnsureState();
    @synchronized (gQALock) { gQALastPerformed = nil; }
}

char *_QuickActions_ConsumePendingPerformed(void) {
    QAEnsureState();
    @synchronized (gQALock) {
        if (gQAPending.count == 0) return NULL;
        NSString *value = gQAPending.firstObject;
        [gQAPending removeObjectAtIndex:0];
        return QACopyCString(value);
    }
}

// Builds {"items":[...]} from the OS's current *dynamic* shortcut items (static
// Info.plist items are not surfaced by shortcutItems). Icons can't be read back,
// so Icon is reported as 0 (None). Must run on the main thread (UIApplication).
static char *QABuildShortcutsJson(void) {
    NSArray<UIApplicationShortcutItem *> *items = [UIApplication sharedApplication].shortcutItems;
    NSMutableArray *out = [NSMutableArray array];
    for (UIApplicationShortcutItem *item in items) {
        // Only OUR shortcuts (marked) — never absorb a host's / another plugin's items
        // into the managed set (which a later Add would then re-stamp as ours).
        if (!QAIsOurShortcut(item)) continue;
        [out addObject:@{
            @"Id": item.type ?: @"",
            @"Title": item.localizedTitle ?: @"",
            @"Subtitle": item.localizedSubtitle ?: @"",
            @"Icon": @0,
            @"AndroidDrawable": @"",
        }];
    }
    NSString *json = @"{\"items\":[]}";
    NSData *data = [NSJSONSerialization dataWithJSONObject:@{@"items": out} options:0 error:nil];
    if (data != nil)
        json = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding];
    return QACopyCString(json);
}

// Reads the dynamic shortcuts, marshalling onto the main thread (UIApplication is
// main-thread-only). Off the main thread we hop to it asynchronously and wait on a
// bounded semaphore — NOT dispatch_sync — so that if the caller violates the
// "call on the Unity main thread" contract while the main thread is blocked on this
// work (e.g. Task.Result), we time out and report a failed read (NULL) instead of
// hard-deadlocking the process.
char *_QuickActions_GetShortcutsJson(void) {
    if ([NSThread isMainThread]) return QABuildShortcutsJson();
    __block char *result = NULL;
    dispatch_semaphore_t sem = dispatch_semaphore_create(0);
    dispatch_async(dispatch_get_main_queue(), ^{
        result = QABuildShortcutsJson();
        dispatch_semaphore_signal(sem);
    });
    if (dispatch_semaphore_wait(sem, dispatch_time(DISPATCH_TIME_NOW, (int64_t)(2 * NSEC_PER_SEC))) != 0)
        return NULL; // main thread didn't drain in time — failed read, don't hang
    return result;
}

// Frees a string returned by the getters above (paired with malloc in
// QACopyCString). Called from C# so the alloc/free use the same allocator.
void _QuickActions_FreeString(char *ptr) {
    if (ptr != NULL) free(ptr);
}

} // extern "C"

#endif // QUICKACTIONS_ENABLED
