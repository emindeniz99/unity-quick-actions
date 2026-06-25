// Quick Actions — iOS native layer.
//
// Implements home-screen quick actions (UIApplicationShortcutItem) and bridges
// taps back to Unity. It hooks Unity's app delegate (UnityAppController) at
// load time via the Objective-C runtime, so the integrating project needs no
// manual AppDelegate edits.
//
// Delivery model (mirrors the C# side):
//   * Cold launch  -> captured in didFinishLaunchingWithOptions; queued for the
//                     Performed event (polled by C#) and stored as "last".
//   * Warm resume  -> performActionForShortcutItem; pushed immediately via
//                     UnitySendMessage and stored as "last" (NOT queued, so the
//                     C# focus-poll cannot double-deliver it).

#import <UIKit/UIKit.h>
#import <objc/runtime.h>
#import <string.h>

extern "C" void UnitySendMessage(const char *obj, const char *method, const char *msg);

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

// Records the tapped action. `queue` is YES for cold launch (the event is
// delivered by the C# poll) and NO for warm resume (UnitySendMessage handles it).
static void QAStorePerformed(NSString *type, BOOL queue) {
    if (type.length == 0) return;
    QAEnsureState();
    @synchronized (gQALock) {
        gQALastPerformed = type;
        if (queue) [gQAPending addObject:type];
    }
}

// Returns a malloc'd copy of `s` (freed on the C# side via Marshal.FreeHGlobal),
// or NULL for nil/empty.
static char *QACopyCString(NSString *s) {
    if (s.length == 0) return NULL;
    const char *utf8 = s.UTF8String;
    if (utf8 == NULL) return NULL;
    size_t len = strlen(utf8) + 1;
    char *out = (char *)malloc(len);
    if (out != NULL) memcpy(out, utf8, len);
    return out;
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
                                                   userInfo:nil];
        [result addObject:shortcut];
    }
    return result;
}

#pragma mark - UnityAppController hooks (installed via the ObjC runtime)

static BOOL (*gQAOrigDidFinishLaunching)(id, SEL, UIApplication *, NSDictionary *) = NULL;

static BOOL QADidFinishLaunching(id self, SEL _cmd, UIApplication *application, NSDictionary *launchOptions) {
    UIApplicationShortcutItem *launchItem = launchOptions[UIApplicationLaunchOptionsShortcutItemKey];
    BOOL launchedFromShortcut = NO;
    if ([launchItem isKindOfClass:[UIApplicationShortcutItem class]]) {
        QAStorePerformed(launchItem.type, YES);
        launchedFromShortcut = YES;
    }

    BOOL result = YES;
    if (gQAOrigDidFinishLaunching != NULL) {
        result = gQAOrigDidFinishLaunching(self, _cmd, application, launchOptions);
    }

    // Returning NO when launched from a shortcut tells iOS not to also invoke
    // performActionForShortcutItem for this same item (we already captured it).
    return launchedFromShortcut ? NO : result;
}

static void QAPerformActionForShortcutItem(id self, SEL _cmd, UIApplication *application,
                                           UIApplicationShortcutItem *shortcutItem,
                                           void (^completionHandler)(BOOL)) {
    if ([shortcutItem isKindOfClass:[UIApplicationShortcutItem class]]) {
        QAStorePerformed(shortcutItem.type, NO);
        UnitySendMessage("QuickActionsRuntime", "OnPerformed", shortcutItem.type.UTF8String);
    }
    if (completionHandler != nil) completionHandler(YES);
}

@interface QuickActionsAppControllerHook : NSObject
@end

@implementation QuickActionsAppControllerHook

+ (void)load {
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
        class_addMethod(cls, didFinishSel, (IMP)QADidFinishLaunching, "B@:@@");
    }

    // Install application:performActionForShortcutItem:completionHandler:
    // (Unity does not implement it, so add it; replace defensively if present).
    SEL performSel = @selector(application:performActionForShortcutItem:completionHandler:);
    const char *performTypes = "v@:@@@?";
    Method perform = class_getInstanceMethod(cls, performSel);
    if (perform != NULL) {
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
    dispatch_async(dispatch_get_main_queue(), ^{
        [UIApplication sharedApplication].shortcutItems = QABuildItems(value);
    });
}

void _QuickActions_RemoveAll(void) {
    dispatch_async(dispatch_get_main_queue(), ^{
        [UIApplication sharedApplication].shortcutItems = @[];
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

} // extern "C"
