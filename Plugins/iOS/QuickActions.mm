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
// A host app that adopts the UIScene lifecycle gets both taps routed to its SCENE
// delegate instead (cold in scene:willConnectToSession:options:, warm in
// windowScene:performActionForShortcutItem:completionHandler:), so we learn that
// delegate class at runtime — from the UISceneConfiguration for the CONNECTING
// SESSION, which is the manifest entry UIKit resolves or whatever configuration a host
// implementation of the selector returns — and install the same two hooks there. Only
// an app that declares a scene manifest can adopt that lifecycle, and we add the
// configuration hook only in one — a default Unity project keeps the exact app-delegate
// launch path it had without this package.
// A host UnityAppController SUBCLASS that owns the configuration selector shadows that
// hook, so a UISceneWillConnectNotification observer installs the same hooks from the
// connecting scene's own configuration as a fallback — covering warm taps and every
// later connection, but NOT necessarily the first cold tap of such an install, and only
// for a scene we can tell is Unity's (see QAInstallSceneHooksFromScene for why, for the
// scenes it deliberately ignores, and for the one-line host-side fix).
// Both paths enqueue; C# drains the queue on first frame and on focus gain.
// performActionForShortcutItem runs before applicationDidBecomeActive, so the
// focus poll reliably catches a warm tap. No UnitySendMessage needed. A cold tap
// that also arrives through a warm hook before the app first becomes active (iOS
// re-delivering a scene launch, or a host subclass discarding the NO we return from
// didFinishLaunchingWithOptions) collapses to ONE queue entry — see gQAColdDeliveredId.

#import <UIKit/UIKit.h>
#import <objc/runtime.h>
#import <stdio.h>
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
// Consume-once marker holding the id a COLD source already queued this launch
// (didFinishLaunchingWithOptions or scene:willConnectToSession:). The same tap can
// reach a warm hook a second time — iOS delivering a scene cold tap through both
// paths, or a host UnityAppController subclass discarding the NO we return from
// didFinishLaunchingWithOptions — and that duplicate must not become a second
// Performed event. Cleared the first time the app/scene becomes active, so the
// window only ever covers launch and no genuine warm tap is ever dropped later.
static NSString *gQAColdDeliveredId = nil;
static id gQALock = nil;

static void QAEnsureState(void) {
    static dispatch_once_t once;
    dispatch_once(&once, ^{
        gQAPending = [NSMutableArray array];
        gQALock = [[NSObject alloc] init];
    });
}

// Records the tapped action: stores it as "last" and, when `queue` is YES,
// enqueues it for the single C# poll channel. Reached through the cold/warm wrappers
// below, which both pass YES; `copy` pins the (possibly autoreleased) type string.
static void QAStorePerformed(NSString *type, BOOL queue) {
    if (type.length == 0) return;
    QAEnsureState();
    @synchronized (gQALock) {
        gQALastPerformed = [type copy];
        if (queue) [gQAPending addObject:[type copy]];
    }
}

// Records a tap that came from a COLD source and arms the dedup marker with its id.
// The marker is armed inside the same lock hold as the store (@synchronized is
// recursive, so the nested acquisition in QAStorePerformed is safe) — a warm hook on
// another thread must never observe the enqueue without the marker that shields it.
static void QAStorePerformedCold(NSString *type) {
    if (type.length == 0) return;
    QAEnsureState();
    @synchronized (gQALock) {
        gQAColdDeliveredId = [type copy];
        QAStorePerformed(type, YES);
    }
}

// Records a tap that came from a WARM source, unless it is the launch tap a cold
// source already queued: that one duplicate is skipped and the marker consumed, so a
// later warm tap of the SAME id (the user tapping the shortcut again) still enqueues.
// "last" needs no update on the skip path — the cold store already set it to this id.
static void QAStorePerformedWarm(NSString *type) {
    if (type.length == 0) return;
    QAEnsureState();
    @synchronized (gQALock) {
        if (gQAColdDeliveredId != nil && [gQAColdDeliveredId isEqualToString:type]) {
            gQAColdDeliveredId = nil;
            return;
        }
        QAStorePerformed(type, YES);
    }
}

// Closes the dedup window. Called on first activation (see +load) because from that
// point on every shortcut tap is a NEW tap the user just made, never a redelivery.
static void QAClearColdDelivered(void) {
    QAEnsureState();
    @synchronized (gQALock) { gQAColdDeliveredId = nil; }
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
// Icon identity persisted alongside the marker (the OS can't read icons back;
// without this, the first post-relaunch push would replace every marked item
// ICONLESS). Mirrors the Android extras path.
static NSString *const kQAIconKey = @"com.emindeniz99.quickactions.icon";
// SF Symbol / template-image icon identity and the app-defined payload, persisted
// for the same reconcile reason as kQAIconKey (keys mirror the Android extras).
static NSString *const kQAIconSymbolKey = @"com.emindeniz99.quickactions.symbol";
static NSString *const kQAIconTemplateKey = @"com.emindeniz99.quickactions.template";
static NSString *const kQAPayloadKey = @"com.emindeniz99.quickactions.payload";
// Per-locale titles/subtitles, encoded by the managed layer into ONE opaque string
// (base text + both tables; same key as the Android extra — keep them in sync).
// Same reason as kQAIconKey: a shortcut item stores only the label the user sees —
// already RESOLVED by C# — so without this a cold start would adopt that resolved
// label as the item's base text and every later language change would translate
// from the wrong original. Never parsed here: locale logic lives in C#, and this
// value is written and handed back verbatim.
static NSString *const kQAL10nKey = @"com.emindeniz99.quickactions.l10n";

static BOOL QAIsOurShortcut(UIApplicationShortcutItem *item) {
    if (![item isKindOfClass:[UIApplicationShortcutItem class]]) return NO;
    id marker = item.userInfo[kQAManagedMarkerKey];
    return [marker isKindOfClass:[NSNumber class]] && [marker boolValue];
}

// Builds UIApplicationShortcutItems from
// {"items":[{Id,Title,Subtitle,Icon,IosSystemImage,IosTemplateImage,Payload,L10n}]}.
// Title/Subtitle arrive already resolved for the active locale (see kQAL10nKey).
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
        NSString *symbol = [item[@"IosSystemImage"] isKindOfClass:[NSString class]] ? item[@"IosSystemImage"] : nil;
        NSString *templateImage = [item[@"IosTemplateImage"] isKindOfClass:[NSString class]] ? item[@"IosTemplateImage"] : nil;
        NSString *payload = [item[@"Payload"] isKindOfClass:[NSString class]] ? item[@"Payload"] : nil;
        NSString *l10n = [item[@"L10n"] isKindOfClass:[NSString class]] ? item[@"L10n"] : nil;

        // Icon priority: SF Symbol (iOS 13+) > bundle template image > IconType
        // system glyph. On iOS 12 a symbol-only item falls through to the next
        // source rather than rendering nothing the caller can't explain.
        // IconType enum: 0 = None; 1..N map to UIApplicationShortcutIconType
        // (which starts at 0), so subtract 1. The C# enum is ordered to match.
        UIApplicationShortcutIcon *icon = nil;
        if (symbol.length > 0) {
            if (@available(iOS 13.0, *)) {
                icon = [UIApplicationShortcutIcon iconWithSystemImageName:symbol];
            }
        }
        if (icon == nil && templateImage.length > 0) {
            icon = [UIApplicationShortcutIcon iconWithTemplateImageName:templateImage];
        }
        NSNumber *iconNumber = item[@"Icon"];
        if (icon == nil && [iconNumber isKindOfClass:[NSNumber class]] && iconNumber.integerValue > 0) {
            icon = [UIApplicationShortcutIcon iconWithType:(UIApplicationShortcutIconType)(iconNumber.integerValue - 1)];
        }

        NSInteger iconValue = [iconNumber isKindOfClass:[NSNumber class]] ? iconNumber.integerValue : 0;
        NSMutableDictionary *userInfo = [NSMutableDictionary dictionaryWithDictionary:@{
            kQAManagedMarkerKey: @YES,
            kQAIconKey: @(iconValue),
        }];
        // Persist only non-empty values (userInfo requires NSSecureCoding values;
        // absent key == empty string on read-back, matching the Android extras).
        if (symbol.length > 0) userInfo[kQAIconSymbolKey] = symbol;
        if (templateImage.length > 0) userInfo[kQAIconTemplateKey] = templateImage;
        if (payload.length > 0) userInfo[kQAPayloadKey] = payload;
        // Only when the item is localized, so an unlocalized shortcut's userInfo is
        // unchanged by this feature.
        if (l10n.length > 0) userInfo[kQAL10nKey] = l10n;
        UIApplicationShortcutItem *shortcut =
            [[UIApplicationShortcutItem alloc] initWithType:identifier
                                             localizedTitle:title
                                          localizedSubtitle:subtitle
                                                       icon:icon
                                                   userInfo:userInfo];
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
        QAStorePerformedCold(launchItem.type);
    }

    // _cmd is forwarded VERBATIM here and at every other chain site in this file, on
    // purpose. We install with class_replaceMethod / class_addMethod — a direct
    // override, never method_exchangeImplementations — so under ordinary dispatch _cmd
    // IS the selector the captured IMP was installed for, which is exactly what the
    // callee expects. When an exchange-based swizzler upstream enters us under a
    // renamed selector (OneSignal's oneSignalApplication:didFinishLaunchingWithOptions:,
    // Firebase C++'s randomized selector), neither value is authoritative: the canonical
    // selector no longer resolves to the IMP we hold, so hardcoding it would name an
    // implementation that is not the one being entered, while _cmd at least names the
    // message actually sent. None of the captured originals — Unity's own
    // didFinishLaunching, the scene callbacks — branches on _cmd. Do not "simplify"
    // this to a hardcoded @selector without re-reading that.
    BOOL result = YES;
    if (gQAOrigDidFinishLaunching != NULL) {
        result = gQAOrigDidFinishLaunching(self, _cmd, application, launchOptions);
    }

    // Return NO ONLY for OUR shortcut (we already captured it), so iOS doesn't also
    // call performActionForShortcutItem for the same item. For a HOST shortcut we must
    // NOT intercept — return the delegate's own result so the host's cold-launch
    // routing (its own performActionForShortcutItem path) still runs. This is the
    // UIApplicationDelegate lifecycle Unity's trampoline uses by default; under the
    // UIScene lifecycle launchOptions carries no shortcut at all and the scene hooks
    // below handle the cold tap instead.
    // NOTE: a host UnityAppController subclass that overrides this selector, calls
    // super, then returns YES unconditionally (discarding our NO) makes iOS ALSO call
    // performActionForShortcutItem for the same item. That second delivery is now
    // swallowed by the consume-once cold marker (QAStorePerformedCold above ->
    // QAStorePerformedWarm in the warm hook), so the cold tap still reaches C# exactly
    // once. Such a subclass should still return the value from
    // [super application:didFinishLaunchingWithOptions:] — the marker is a safety net,
    // not a licence to ignore the contract.
    return launchedFromOurShortcut ? NO : result;
}

static void (*gQAOrigPerformAction)(id, SEL, UIApplication *, UIApplicationShortcutItem *, void (^)(BOOL)) = NULL;

static void QAPerformActionForShortcutItem(id self, SEL _cmd, UIApplication *application,
                                           UIApplicationShortcutItem *shortcutItem,
                                           void (^completionHandler)(BOOL)) {
    // Are we still the TERMINAL handler for this selector? gQAOrigPerformAction ==
    // NULL only says nobody was installed before US at +load; a host plugin may
    // have swizzled ON TOP of us afterwards (capturing our IMP as its "original"
    // and chaining down with the same completionHandler). In that wrapped state
    // the host owns routing and completion — treating ourselves as terminal would
    // steal its taps into our queue and double-invoke the completion handler.
    BOOL terminal = NO;
    if (gQAOrigPerformAction == NULL) {
        Method current = class_getInstanceMethod(object_getClass(self),
            @selector(application:performActionForShortcutItem:completionHandler:));
        terminal = current != NULL && method_getImplementation(current) == (IMP)QAPerformActionForShortcutItem;
    }
    if (QAIsOurShortcut(shortcutItem)) {
        // Enqueue for the single C# poll channel. This runs before
        // applicationDidBecomeActive, so the focus poll drains it on resume. Only
        // OUR shortcuts — a host shortcut is left entirely to its own handler below.
        // Warm source: a call that is really the cold tap coming back a second time
        // (host subclass discarded our NO) is dropped by the marker, not queued twice.
        QAStorePerformedWarm(shortcutItem.type);
    } else if (terminal) {
        // Unmarked item and we are the ONLY handler: in a plain Unity app nothing
        // else can receive this tap (e.g. an Info.plist static shortcut the
        // developer added outside this package, or an item written by a pre-marker
        // build). Dropping it would black-hole the tap while completing YES below —
        // deliver it best-effort through our channel, as the only consumer.
        QAStorePerformedWarm(shortcutItem.type);
    }
    // If UnityAppController already had an implementation (a host app or another
    // native plugin), chain to it and let it own the completion handler so the
    // existing warm-tap handler still runs — mirrors the didFinish path. Complete
    // ourselves only when we are the terminal handler (no prior implementation AND
    // nobody wrapped us) — anything else risks a double completionHandler call.
    // The rule this states, deliberately, is "AT MOST once, never twice". In the one
    // state where we are wrapped AND had no original below us, nobody here calls the
    // handler: a wrapper for a handler-bearing selector may pass ours down and let the
    // chain complete, or substitute its own and complete from that (GoogleUtilities'
    // GULAppDelegateSwizzler ships both shapes in one file), and from in here we cannot
    // tell which. An unreported success BOOL is inert — Apple documents no watchdog on
    // this handler, and the tap is already queued above — while a double-invoked UIKit
    // block is undefined behaviour. WHAT A WRAPPER MUST DO: chain to the IMP it
    // captured, then either complete exactly once itself or leave completion to the
    // chain — never both.
    if (gQAOrigPerformAction != NULL) {
        gQAOrigPerformAction(self, _cmd, application, shortcutItem, completionHandler);
    } else if (terminal && completionHandler != nil) {
        completionHandler(YES);
    }
}

#pragma mark - UIScene lifecycle hooks (installed once the host names its scene delegate)

// Under the UIScene lifecycle iOS routes shortcut taps to the SCENE delegate, so the
// app-delegate hooks above never see them. That delegate's class is unknowable at
// +load — it comes from the UISceneConfiguration the host returns per connection — so
// we learn it there and install these hooks on it. The captured originals are typed
// with `id` for the scene objects (they only pass through) so this file-scope state
// needs no availability annotation; the hooks themselves take the real iOS 13 types.
static void (*gQAOrigSceneWillConnect)(id, SEL, id, id, id) = NULL;
static void (*gQAOrigScenePerformAction)(id, SEL, id, id, void (^)(BOOL)) = NULL;
static id (*gQAOrigSceneConfiguration)(id, SEL, id, id, id) = NULL;
// YES when the scene hooks were installed from the UISceneWillConnectNotification
// fallback onto a class we could NOT confirm is Unity's own scene delegate — i.e. no
// UnityScene class exists in this process, so the scene manifest was authored by a host
// rather than by Unity's trampoline. There we are demonstrably not the app's only
// quick-action consumer, so the unmarked best-effort adoption below stays off. Written
// once, on the main thread, before any hook it gates can fire.
static BOOL gQASceneOwnerUnconfirmed = NO;

// COLD tap under the scene lifecycle: the item rides in the connection options
// instead of launchOptions.
API_AVAILABLE(ios(13.0))
static void QASceneWillConnect(id self, SEL _cmd, UIScene *scene, UISceneSession *session,
                               UISceneConnectionOptions *connectionOptions) {
    UIApplicationShortcutItem *item = connectionOptions.shortcutItem;
    if (QAIsOurShortcut(item)) {
        // Record BEFORE chaining: the host's willConnect is what builds the window and
        // starts Unity, so the queue must already hold the tap when C# first drains it.
        // Only OURS — a host's own shortcut stays with the host's implementation.
        // If iOS also reports this tap through the warm hook below, the cold marker
        // collapses the pair into the single Performed event the user actually caused.
        QAStorePerformedCold(item.type);
    }
    // When there was no original we added this selector; UIKit's own scene setup does
    // not depend on the delegate implementing it, so adding it changes nothing.
    if (gQAOrigSceneWillConnect != NULL) {
        gQAOrigSceneWillConnect(self, _cmd, scene, session, connectionOptions);
    }
}

// WARM tap under the scene lifecycle — the scene-delegate twin of
// QAPerformActionForShortcutItem, with the same terminal/chaining rules.
API_AVAILABLE(ios(13.0))
static void QAScenePerformActionForShortcutItem(id self, SEL _cmd, UIWindowScene *windowScene,
                                                UIApplicationShortcutItem *shortcutItem,
                                                void (^completionHandler)(BOOL)) {
    // Same question as the app-delegate hook: are we still the TERMINAL handler?
    // gQAOrigScenePerformAction == NULL only says the class implemented nothing when we
    // installed; a host plugin may have swizzled ON TOP of us since and now owns both
    // the routing and the completion handler.
    BOOL terminal = NO;
    if (gQAOrigScenePerformAction == NULL) {
        Method current = class_getInstanceMethod(object_getClass(self),
            @selector(windowScene:performActionForShortcutItem:completionHandler:));
        terminal = current != NULL &&
                   method_getImplementation(current) == (IMP)QAScenePerformActionForShortcutItem;
    }
    BOOL adopted = NO;
    if (QAIsOurShortcut(shortcutItem)) {
        // Ours, wrapped or terminal: record it — deduped against a cold tap of the same
        // id that the connecting scene already queued this launch.
        QAStorePerformedWarm(shortcutItem.type);
        adopted = YES;
    } else if (terminal && !gQASceneOwnerUnconfirmed) {
        // Unmarked item and we are the ONLY handler — same rule as the app-delegate
        // hook: dropping it while completing YES below would black-hole the tap, so
        // deliver it best-effort through our channel as the only consumer.
        // NOT when gQASceneOwnerUnconfirmed: there we added this selector to a scene
        // delegate class we could not confirm is Unity's own (see
        // QAInstallSceneHooksFromScene), so "the only consumer" is precisely what we are
        // not — that scene belongs to a host and its unmarked items are the host's.
        QAStorePerformedWarm(shortcutItem.type);
        adopted = YES;
    }
    // Chain when the class already handled this selector and let that implementation
    // own the completion handler; complete ourselves only when we are terminal, so the
    // handler is called at most once on every path we own (the app-delegate twin above
    // spells out why "at most", and what a wrapper owes us). Report `adopted` rather
    // than a flat YES: NO for an unmarked item on an unconfirmed host scene is the
    // honest "we did not perform this action", and it is what UIKit would have seen had
    // we never added the selector to that class.
    if (gQAOrigScenePerformAction != NULL) {
        gQAOrigScenePerformAction(self, _cmd, windowScene, shortcutItem, completionHandler);
    } else if (terminal && completionHandler != nil) {
        completionHandler(adopted);
    }
}

// Installs the two scene hooks on the class the host named as its scene delegate.
// Runs at most once: installing twice would capture OUR OWN IMP as the "original" and
// recurse forever on the next tap. A nil class (the host's configuration doesn't name
// one) leaves us inert rather than guessing — better no scene coverage than hooks on
// a class that never receives the taps. Only the FIRST class learned is hooked: the
// captured originals are per-selector, not per-class, so an app with several distinct
// scene-delegate classes gets scene coverage for that one. Main thread only (UIKit
// asks for the configuration there, and UISceneWillConnectNotification is posted
// there), which is what makes the static flag enough.
// Returns YES only for the call that actually installed, so the notification fallback
// can record HOW the class was learned. `via` names that path for the log line.
API_AVAILABLE(ios(13.0))
static BOOL QAInstallSceneHooks(Class delegateClass, const char *via) {
    static BOOL installed = NO;
    if (installed || delegateClass == Nil) return NO;
    installed = YES;

    // Cold: scene:willConnectToSession:options: (the Apple scene template implements
    // it; preserve and chain to it when present, add it when not).
    SEL willConnectSel = @selector(scene:willConnectToSession:options:);
    Method willConnect = class_getInstanceMethod(delegateClass, willConnectSel);
    if (willConnect != NULL) {
        gQAOrigSceneWillConnect =
            (void (*)(id, SEL, id, id, id))method_getImplementation(willConnect);
        class_replaceMethod(delegateClass, willConnectSel, (IMP)QASceneWillConnect,
                            method_getTypeEncoding(willConnect));
    } else {
        class_addMethod(delegateClass, willConnectSel, (IMP)QASceneWillConnect, "v@:@@@");
    }

    // Warm: windowScene:performActionForShortcutItem:completionHandler: (normally
    // absent, so we add it and become terminal; if the host already routes quick
    // actions here we preserve its IMP and it keeps owning the completion handler).
    SEL performSel = @selector(windowScene:performActionForShortcutItem:completionHandler:);
    Method perform = class_getInstanceMethod(delegateClass, performSel);
    if (perform != NULL) {
        gQAOrigScenePerformAction =
            (void (*)(id, SEL, id, id, void (^)(BOOL)))method_getImplementation(perform);
        class_replaceMethod(delegateClass, performSel, (IMP)QAScenePerformActionForShortcutItem,
                            method_getTypeEncoding(perform));
    } else {
        class_addMethod(delegateClass, performSel, (IMP)QAScenePerformActionForShortcutItem,
                        "v@:@@@?");
    }

    // Always on, one line per process launch: which class we bound to, and by which of
    // the two routes. Under the scene lifecycle this is the difference between a working
    // install and a silently inert one, and an adopter integrating the package into an
    // existing native stack has no other way to see it. See +load for the companion line.
    NSLog(@"[QuickActions] iOS scene hooks installed on %@ via %s",
          NSStringFromClass(delegateClass), via);
    return YES;
}

API_AVAILABLE(ios(13.0))
static void QAInstallSceneHooksScoped(Class declared, const char *via);

// Wrapped purely to LEARN the scene-delegate class; the configuration is returned
// untouched, so the host's scene is built exactly as it would have been.
API_AVAILABLE(ios(13.0))
static UISceneConfiguration *QAConfigurationForConnectingSceneSession(
        id self, SEL _cmd, UIApplication *application,
        UISceneSession *connectingSceneSession, UISceneConnectionOptions *options) {
    UISceneConfiguration *configuration = nil;
    if (gQAOrigSceneConfiguration != NULL) {
        configuration =
            gQAOrigSceneConfiguration(self, _cmd, application, connectingSceneSession, options);
    } else {
        // Nobody implemented the selector and we added it (only ever in an app that
        // declares a scene manifest — see +load), so let UIKit resolve that manifest for
        // us instead of hand-building a configuration. The initializer performs the
        // lookup itself; UIKit's own SDK header documents it, with wording unchanged
        // from iPhoneOS13.0.sdk through iPhoneOS26.0.sdk
        // (UIKit.framework/Headers/UISceneConfiguration.h):
        //   "Creates a UISceneConfiguration instance from your Info.plist using the name
        //    and session role provided. If nil is provided for the name, the first
        //    matching instance of the provided session role is used. If no matching name
        //    is found, or no descriptions of the provided session role exist in your
        //    Info.plist, then an instance with a nil sceneSubclass, delegateClass, and
        //    storyboard is returned."
        // The manifest this branch requires is exactly what that lookup reads, so
        // delegateClass comes back populated from the entry for this role — for Unity's
        // own manifest, UnityScene.
        configuration = [[UISceneConfiguration alloc] initWithName:nil
                                                      sessionRole:connectingSceneSession.role];
    }
    // A nil configuration (or one that names no delegate class) messages to Nil here and
    // is refused — we stay inert rather than hook a guess. And the class it does name is
    // held to the same ownership rule as the notification fallback: a manifest may
    // declare more than one role, and the first session UIKit brings up is not
    // necessarily Unity's window (see QAInstallSceneHooksScoped).
    QAInstallSceneHooksScoped(configuration.delegateClass, "configuration");
    return configuration;
}

// Class-descent test done with the runtime rather than by messaging: a scene delegate
// is not required to descend from NSObject, so -isSubclassOfClass: is not guaranteed to
// be there to ask.
static BOOL QAClassDescendsFrom(Class cls, Class ancestor) {
    if (ancestor == Nil) return NO;
    for (Class c = cls; c != Nil; c = class_getSuperclass(c)) {
        if (c == ancestor) return YES;
    }
    return NO;
}

// The ownership rule BOTH discovery routes apply before binding — the configuration
// wrapper and the notification fallback alike, because either can be handed a scene
// that is not Unity's: a manifest may declare more than one role (CarPlay, an
// external display), and whichever session UIKit brings up first is not necessarily
// Unity's window. Binding to it would burn the one-shot on a host class, so Unity's
// real scene never gets hooked, and ADD the warm selector to a class that lacked it,
// making us "terminal" for the host's own quick actions. So: when this process has
// Unity's own scene delegate, bind to it and nothing else — without consuming the
// one-shot on anyone else's class, so Unity's scene is still hooked whenever it
// connects; when it has none (a host-authored manifest on a trampoline that predates
// UnityScene) bind to the first declared class, as before, but record the owner as
// unconfirmed, which turns OFF the unmarked best-effort adoption in
// QAScenePerformActionForShortcutItem.
API_AVAILABLE(ios(13.0))
static void QAInstallSceneHooksScoped(Class declared, const char *via) {
    if (declared == Nil) return;
    Class unityScene = NSClassFromString(@"UnityScene");
    if (unityScene != Nil) {
        if (!QAClassDescendsFrom(declared, unityScene)) {
            NSLog(@"[QuickActions] iOS scene hooks: %@ (via %s) is not Unity's scene delegate "
                  @"— left alone", NSStringFromClass(declared), via);
            return;
        }
        QAInstallSceneHooks(declared, via);
        return;
    }
    if (QAInstallSceneHooks(declared, via)) gQASceneOwnerUnconfirmed = YES;
}

// FALLBACK for the shape the configuration hook above cannot see: a host
// UnityAppController SUBCLASS that implements
// application:configurationForConnectingSceneSession:options: itself.
// class_getInstanceMethod in +load walks UP from UnityAppController, never down, so
// the subclass's implementation is invisible there; the IMP we add to
// UnityAppController is then shadowed by it and QAConfigurationForConnectingSceneSession
// never runs. IMPL_APP_CONTROLLER_SUBCLASS is Unity's only sanctioned app-delegate
// extension point and Apple's own template puts that selector in the app delegate, so
// this is a mainstream shape — and without a fallback it loses every scene tap silently.
//
// Rather than trying to hook the subclass's configuration method (we would have to
// guess the subclass, and the returned configuration is the host's to build), learn the
// delegate class from the SCENE ITSELF once it exists: UIKit sets scene.delegate from
// the configuration before connecting it, so object_getClass on that delegate is the
// same class the configuration named.
//
// HONEST LIMIT — this does not recover the very first COLD tap in that shape. UIKit
// posts UISceneWillConnectNotification around the delegate's own
// scene:willConnectToSession:options: callback, and if our observer runs after that
// callback the hook is installed one step too late to have seen the connection
// options that carried the shortcut. Warm taps (and every later connection, including
// every cold tap in a subsequent launch of the same install) are covered, because the
// hooks are in place by then. The reliable fix for the first cold tap is for such a
// host to call [super application:configurationForConnectingSceneSession:options:],
// which routes through the wrapper above and installs before willConnect runs.
//
// SCOPE — the notification fires for EVERY connecting scene, and most of them are none
// of our business: a Unity-as-a-Library host's own scene, a SwiftUI @main host, a second
// iPad / Stage Manager window with a different delegate class, an external-display or
// CarPlay scene. Binding to the first one to connect would burn the one-shot on a host
// class (so Unity's real scene never gets hooked) and — worse — ADD
// windowScene:performActionForShortcutItem:completionHandler: to a class that did not
// have it, making us "terminal" for the HOST's own quick actions and swallowing them
// into a queue nothing on their side drains. So we bind only to Unity's own scene when
// this process has one, and stay conservative when it does not.

API_AVAILABLE(ios(13.0))
static void QAInstallSceneHooksFromScene(UIScene *scene) {
    // Prefer the DECLARED class from the session's configuration over the live delegate
    // object: it is the class UIKit resolved for this scene (from UISceneDelegateClassName,
    // or from a host implementation of the configuration selector), it is set even if
    // UIKit has not instantiated the delegate yet, and it is never a per-instance
    // isa-swizzled proxy (Firebase's GUL_<Class>-<UUID>, NSKVONotifying_<Class>) — hooking
    // one of those would burn the one-shot on a class only that one delegate will ever have.
    Class declared = scene.session.configuration.delegateClass;
    if (declared == Nil) {
        id delegate = scene.delegate;
        if (delegate == nil) return; // nothing named yet — a later connection may still name one
        declared = object_getClass(delegate);
    }

    // Unity's trampoline ships its own scene delegate (2022.3.72f1+ / 6000.0.68f1+ /
    // 6000.3.8f1+); the shared rule binds only to it when it exists, and to the first
    // declared class with the owner recorded as unconfirmed when it does not.
    QAInstallSceneHooksScoped(declared, "notification");
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

    // Which branch each install took, for the one diagnostic line at the end.
    const char *didFinishBranch = "added";
    const char *performBranch = "added";
    const char *sceneConfigBranch = "absent";

    // Swizzle application:didFinishLaunchingWithOptions: (Unity implements it).
    SEL didFinishSel = @selector(application:didFinishLaunchingWithOptions:);
    Method didFinish = class_getInstanceMethod(cls, didFinishSel);
    if (didFinish != NULL) {
        gQAOrigDidFinishLaunching =
            (BOOL (*)(id, SEL, UIApplication *, NSDictionary *))method_getImplementation(didFinish);
        class_replaceMethod(cls, didFinishSel, (IMP)QADidFinishLaunching, method_getTypeEncoding(didFinish));
        didFinishBranch = "wrapped";
    } else {
        // Defensive fallback (Unity always implements the selector). Build the return
        // encoding from @encode(BOOL) instead of hardcoding a character: on every
        // 64-bit iOS slice objc.h takes the OBJC_BOOL_IS_BOOL branch, so BOOL is C99
        // _Bool and the encoding is "B"; the legacy 'c' (signed char) belongs to the
        // retired armv7/i386 ABI. Static so the buffer outlives +load whatever the
        // runtime chooses to do with the pointer.
        static char didFinishTypes[16];
        snprintf(didFinishTypes, sizeof(didFinishTypes), "%s@:@@", @encode(BOOL));
        class_addMethod(cls, didFinishSel, (IMP)QADidFinishLaunching, didFinishTypes);
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
        performBranch = "wrapped";
    } else {
        class_addMethod(cls, performSel, (IMP)QAPerformActionForShortcutItem, performTypes);
    }

    // Install application:configurationForConnectingSceneSession:options: — the only
    // way to learn the scene-delegate class, since it exists solely in the configuration
    // the host returns per connection. Wrap an existing implementation; ADD one only in
    // an app that actually opted into the UIScene lifecycle, which UIApplicationSceneManifest
    // in Info.plist is the sole way to do. A default Unity project has no manifest, so the
    // selector stays absent there and UIKit's legacy launch path is exactly what it was
    // without this package — nothing of the scene code above can run.
    // The single gate for EVERYTHING scene-related below: an app can only adopt the
    // UIScene lifecycle by declaring this manifest, so without it nothing here may
    // touch the app — the legacy launch path has to stay byte-identical.
    BOOL hasSceneManifest = [NSBundle mainBundle].infoDictionary[@"UIApplicationSceneManifest"] != nil;
    if (@available(iOS 13.0, *)) {
        SEL sceneConfigSel = @selector(application:configurationForConnectingSceneSession:options:);
        Method sceneConfig = class_getInstanceMethod(cls, sceneConfigSel);
        if (sceneConfig != NULL) {
            gQAOrigSceneConfiguration =
                (id (*)(id, SEL, id, id, id))method_getImplementation(sceneConfig);
            class_replaceMethod(cls, sceneConfigSel, (IMP)QAConfigurationForConnectingSceneSession,
                                method_getTypeEncoding(sceneConfig));
            sceneConfigBranch = "wrapped";
        } else if (hasSceneManifest) {
            class_addMethod(cls, sceneConfigSel, (IMP)QAConfigurationForConnectingSceneSession,
                            "@@:@@@");
            sceneConfigBranch = "added";
        }
    }

    // End the cold-dedup window at the first activation, so ONLY a duplicate that
    // arrives during launch can ever be skipped and every later warm tap is delivered.
    // Both notifications are observed because each lifecycle has its own activation
    // signal (the app-level one under the app-delegate lifecycle, the scene one under
    // UIScene) — clearing an already-clear marker is a no-op, so overlap is harmless.
    // queue:nil keeps delivery synchronous on the posting thread rather than deferring
    // the clear behind another runloop turn. Never unregistered: these live as long as
    // the process and the blocks capture nothing.
    [[NSNotificationCenter defaultCenter] addObserverForName:UIApplicationDidBecomeActiveNotification
                                                      object:nil
                                                       queue:nil
                                                  usingBlock:^(NSNotification *note) {
        QAClearColdDelivered();
    }];
    if (@available(iOS 13.0, *)) {
        [[NSNotificationCenter defaultCenter] addObserverForName:UISceneDidActivateNotification
                                                          object:nil
                                                           queue:nil
                                                      usingBlock:^(NSNotification *note) {
            QAClearColdDelivered();
        }];
        // Late-binding safety net for a host app-controller SUBCLASS that owns the
        // scene-configuration selector, where the hook added above is shadowed and
        // never runs — see QAInstallSceneHooksFromScene for the mechanism and for the
        // one case it cannot recover (the FIRST cold tap of such an install).
        // QAInstallSceneHooks is idempotent, so in the normal shape — where the
        // configuration wrapper already installed the hooks — this is a no-op.
        // Manifest-gated like the configuration hook, and for the same reason: in an
        // app that did NOT adopt the scene lifecycle there is no scene delivery to
        // rescue, and installing scene selectors on the app delegate (which is what a
        // legacy app's scene would hand us) could change the launch path this package
        // promises to leave alone.
        if (hasSceneManifest) {
            [[NSNotificationCenter defaultCenter] addObserverForName:UISceneWillConnectNotification
                                                              object:nil
                                                               queue:nil
                                                          usingBlock:^(NSNotification *note) {
                // Re-checked INSIDE the block: clang does not always carry an enclosing
                // @available context into a block body, and this block outlives +load.
                if (@available(iOS 13.0, *)) {
                    if ([note.object isKindOfClass:[UIScene class]])
                        QAInstallSceneHooksFromScene((UIScene *)note.object);
                }
            }];
        }
    }

    // ONE line per process launch, always on. Every failure mode this file has is
    // otherwise silent: an adopter integrating the package into an existing native
    // stack cannot tell a working install from an inert one, and "quick actions do
    // nothing on iOS" has half a dozen indistinguishable causes. One console line at
    // launch is cheap, and it is exactly what a coexistence bug report needs — which is
    // also why CI's coexistence leg asserts on it. The scene-hook companion line is
    // emitted from QAInstallSceneHooks when (and only when) that binding happens.
    NSLog(@"[QuickActions] iOS hooks: didFinishLaunching=%s performAction=%s "
          @"sceneConfig=%s manifest=%s",
          didFinishBranch, performBranch, sceneConfigBranch, hasSceneManifest ? "yes" : "no");
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
        NSArray<UIApplicationShortcutItem *> *ours = QABuildItems(value);
        UIApplication *app = [UIApplication sharedApplication];
        NSMutableArray<UIApplicationShortcutItem *> *merged = [NSMutableArray array];
        for (UIApplicationShortcutItem *item in app.shortcutItems) {
            if (QAIsOurShortcut(item)) continue;            // replaced by the fresh set below
            // Every unmarked item is preserved — a host app's / other plugin's live
            // shortcut, even one whose `type` collides with an id we're writing. On a
            // collision the id then renders twice, the honest result of two publishers
            // claiming one id; we never adopt or drop an item we didn't mark. (This is
            // the first release, so there is no pre-marker build of this package whose
            // unmarked leftovers would need migrating — the static plist path in
            // QuickActionsBuildPostProcessoriOS makes the same call.)
            [merged addObject:item];
        }
        [merged addObjectsFromArray:ours];
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
        // Icon identity comes from our userInfo (see kQAIconKey): reporting 0 here
        // would make the next push rebuild reconciled items iconless. Same for the
        // symbol/template names and the payload.
        // userInfo is NSDictionary<NSString *, id<NSSecureCoding>>, so every
        // subscript yields id<NSSecureCoding> — declaring these as NSNumber*/
        // NSString* is an incompatible-pointer-types warning (on by default, and
        // an error for any consumer building with -Werror). The isKindOfClass
        // checks below are what actually establish the type.
        id iconBack = item.userInfo[kQAIconKey];
        id symbolBack = item.userInfo[kQAIconSymbolKey];
        id templateBack = item.userInfo[kQAIconTemplateKey];
        id payloadBack = item.userInfo[kQAPayloadKey];
        // Title/Subtitle below are what the Home Screen SHOWS (resolved at the last
        // push); the blob is what lets C# recover the base text + per-locale tables
        // and notice the two disagree. Empty for an item that was never localized.
        id l10nBack = item.userInfo[kQAL10nKey];
        [out addObject:@{
            @"Id": item.type ?: @"",
            @"Title": item.localizedTitle ?: @"",
            @"Subtitle": item.localizedSubtitle ?: @"",
            @"L10n": [l10nBack isKindOfClass:[NSString class]] ? l10nBack : @"",
            @"Icon": [iconBack isKindOfClass:[NSNumber class]] ? iconBack : @0,
            @"IosSystemImage": [symbolBack isKindOfClass:[NSString class]] ? symbolBack : @"",
            @"IosTemplateImage": [templateBack isKindOfClass:[NSString class]] ? templateBack : @"",
            @"Payload": [payloadBack isKindOfClass:[NSString class]] ? payloadBack : @"",
            @"AndroidDrawable": @"",
        }];
    }
    // A serializer failure is a FAILED read (NULL — the C# side maps it to the
    // bridge's null), never an empty set: an empty set is authoritative and the
    // facade prunes against it, which would remove the user's real shortcuts.
    NSData *data = [NSJSONSerialization dataWithJSONObject:@{@"items": out} options:0 error:nil];
    if (data == nil) return NULL;
    NSString *json = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding];
    return json != nil ? QACopyCString(json) : NULL;
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
    __block BOOL abandoned = NO;
    NSObject *lock = [NSObject new];
    dispatch_semaphore_t sem = dispatch_semaphore_create(0);
    dispatch_async(dispatch_get_main_queue(), ^{
        char *json = QABuildShortcutsJson();
        @synchronized (lock) {
            // If the waiter already timed out, nobody will ever consume (or free)
            // this buffer — free it here so a reconcile-retry loop against a wedged
            // main thread can't leak a JSON string per attempt.
            if (abandoned) { if (json != NULL) free(json); }
            else result = json;
        }
        dispatch_semaphore_signal(sem);
    });
    if (dispatch_semaphore_wait(sem, dispatch_time(DISPATCH_TIME_NOW, (int64_t)(2 * NSEC_PER_SEC))) != 0) {
        @synchronized (lock) {
            abandoned = YES;
            // The block may have stored the result between the timeout and this
            // lock — reclaim it; we are returning NULL either way.
            if (result != NULL) { free(result); result = NULL; }
        }
        return NULL; // main thread didn't drain in time — failed read, don't hang
    }
    return result;
}

// Frees a string returned by the getters above (paired with malloc in
// QACopyCString). Called from C# so the alloc/free use the same allocator.
void _QuickActions_FreeString(char *ptr) {
    if (ptr != NULL) free(ptr);
}

} // extern "C"

#endif // QUICKACTIONS_ENABLED
