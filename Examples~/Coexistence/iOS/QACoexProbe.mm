// The assertion driver. Runs once, on the main thread, after the app is up.
//
// Everything it does is a direct message send, so it proves what the PACKAGE does
// with a payload — never that UIKit would have routed one to us. The two things only
// UIKit can answer (a real SpringBoard long-press, and the launchOptions /
// connectionOptions it fills in) stay out of reach here; ../README.md says so.
#import <UIKit/UIKit.h>
#import <objc/runtime.h>

#import "QACoex.h"

#if QUICKACTIONS_ENABLED

static void QACoexProbeAttempt(int attempt);

// Stand-in for UISceneConnectionOptions, which UIKit gives no way to build. The
// package's cold scene hook reads only -shortcutItem from it.
@interface QACoexConnectionOptions : NSObject
@property (nonatomic, strong) UIApplicationShortcutItem *shortcutItem;
@end

@implementation QACoexConnectionOptions
@end

// Unity's own scene:willConnectToSession:options: must run once per real connection
// and is never handed the stand-in, so a stub sits between it and the package: real
// connections pass through, a stand-in stops here.
static void (*gQACoexUnityWillConnect)(id, SEL, id, id, id) = NULL;
static BOOL gQACoexSceneStubLoaded = NO;

static void QACoexSceneWillConnect(id self, SEL _cmd, id scene, id session, id options) {
    if ([options isKindOfClass:[QACoexConnectionOptions class]]) return;
    if (gQACoexUnityWillConnect != NULL) {
        gQACoexUnityWillConnect(self, _cmd, scene, session, options);
    }
}

// Never called: only its IMP matters, standing in for a host that wraps the warm selector.
static void QACoexForeignWarm(id self, SEL _cmd, id windowScene, id item, void (^handler)(BOOL)) {
    if (handler != nil) handler(NO);
}

// From +load, so the package (which learns UnityScene at scene-configuration time, after
// every +load) captures the stub and chains to it — the Singular shape the package
// README describes.
@interface QACoexSceneStub : NSObject
@end

@implementation QACoexSceneStub
+ (void)load {
    Class unityScene = NSClassFromString(@"UnityScene");
    if (unityScene == Nil) return;
    Method willConnect =
        class_getInstanceMethod(unityScene, @selector(scene:willConnectToSession:options:));
    if (willConnect != NULL) {
        gQACoexUnityWillConnect = (void (*)(id, SEL, id, id, id))
            method_setImplementation(willConnect, (IMP)QACoexSceneWillConnect);
    }
    gQACoexSceneStubLoaded = YES;
}
@end

// Sends a synthetic COLD connection carrying `item` to the scene's delegate. Only to an
// exact UnityScene, whose cold selector bottoms out in the stub above; NO otherwise.
static BOOL QACoexSendSceneCold(id scene, UIApplicationShortcutItem *item) {
    if (@available(iOS 13.0, *)) {
        UIScene *connected = (UIScene *)scene;
        id<UISceneDelegate> delegate = connected.delegate;
        SEL sel = @selector(scene:willConnectToSession:options:);
        if (!gQACoexSceneStubLoaded || delegate == nil ||
            object_getClass(delegate) != NSClassFromString(@"UnityScene") ||
            ![delegate respondsToSelector:sel]) {
            return NO;
        }
        QACoexConnectionOptions *options = [[QACoexConnectionOptions alloc] init];
        options.shortcutItem = item;
        [delegate scene:connected
            willConnectToSession:connected.session
                         options:(UISceneConnectionOptions *)options];
        return YES;
    }
    return NO;
}

// The connected window scene to drive, or nil. Prefers a foreground-active one.
static id QACoexActiveWindowScene(void) {
    if (@available(iOS 13.0, *)) {
        id fallback = nil;
        for (UIScene *scene in [UIApplication sharedApplication].connectedScenes) {
            if (![scene isKindOfClass:[UIWindowScene class]]) continue;
            if (scene.activationState == UISceneActivationStateForegroundActive) return scene;
            if (fallback == nil) fallback = scene;
        }
        return fallback;
    }
    return nil;
}

// Ready = the surface we are about to message actually exists.
static BOOL QACoexProbeReady(BOOL sceneLifecycle) {
    if (sceneLifecycle) {
        if (@available(iOS 13.0, *)) {
            id scene = QACoexActiveWindowScene();
            return scene != nil && ((UIScene *)scene).delegate != nil;
        }
        return NO;
    }
    return [UIApplication sharedApplication].applicationState == UIApplicationStateActive;
}

// Sends a synthetic warm tap down the lifecycle's own delivery selector. `scene` nil
// means the app-delegate path. Returns NO when the receiver does not implement the
// selector at all — i.e. the package never installed its hook.
static BOOL QACoexSendWarm(id scene, UIApplicationShortcutItem *item, void (^handler)(BOOL)) {
    UIApplication *app = [UIApplication sharedApplication];
    if (scene != nil) {
        if (@available(iOS 13.0, *)) {
            UIWindowScene *windowScene = (UIWindowScene *)scene;
            id<UIWindowSceneDelegate> delegate = (id<UIWindowSceneDelegate>)windowScene.delegate;
            SEL sel = @selector(windowScene:performActionForShortcutItem:completionHandler:);
            if (![delegate respondsToSelector:sel]) return NO;
            [delegate windowScene:windowScene
                performActionForShortcutItem:item
                           completionHandler:handler];
            return YES;
        }
        return NO;
    }
    id<UIApplicationDelegate> delegate = app.delegate;
    SEL sel = @selector(application:performActionForShortcutItem:completionHandler:);
    if (![delegate respondsToSelector:sel]) return NO;
    [delegate application:app performActionForShortcutItem:item completionHandler:handler];
    return YES;
}

static void QACoexRunChecks(void) {
    UIApplication *app = [UIApplication sharedApplication];
    BOOL sceneLifecycle = QACoexHasSceneManifest();
    id windowScene = sceneLifecycle ? QACoexActiveWindowScene() : nil;

    // Which lifecycle this build actually runs. The workflow requires the one the leg
    // expects, so a testbed that silently stops emitting UIApplicationSceneManifest
    // (or starts) turns the leg red instead of quietly downgrading its coverage.
    QACoexPass(sceneLifecycle ? @"lifecycle-scene" : @"lifecycle-app-delegate");

    QACoexCheck(gQACoexSubclassSuperCalls == 1, @"subclass-super-called-once",
                [NSString stringWithFormat:@"the host subclass called super %d time(s)",
                                           gQACoexSubclassSuperCalls]);
    QACoexCheck(gQACoexCategoryColdChained, @"category-cold-chained",
                @"the category swizzle never saw didFinishLaunching");

    if (sceneLifecycle) {
        if (@available(iOS 13.0, *)) {
            Class unityScene = NSClassFromString(@"UnityScene");
            UIScene *scene = (UIScene *)windowScene;
            id delegate = scene.delegate;
            QACoexCheck(delegate != nil && unityScene != Nil &&
                            [delegate isKindOfClass:unityScene],
                        @"scene-delegate-is-unityscene",
                        [NSString stringWithFormat:@"the connected scene's delegate is %@",
                                                   delegate != nil
                                                       ? NSStringFromClass([delegate class])
                                                       : @"(nil)"]);
            Class declared = scene.session.configuration.delegateClass;
            QACoexCheck(declared != Nil && declared == unityScene,
                        @"scene-config-delegate-class",
                        [NSString stringWithFormat:@"session.configuration.delegateClass is %@",
                                                   declared != Nil ? NSStringFromClass(declared)
                                                                   : @"(Nil)"]);
            SEL warmSel = @selector(windowScene:performActionForShortcutItem:completionHandler:);
            QACoexCheck(unityScene != Nil &&
                            class_getInstanceMethod(unityScene, warmSel) != NULL,
                        @"scene-warm-hook-installed",
                        @"UnityScene carries no windowScene:performActionForShortcutItem: — the "
                        @"package's scene hooks never installed");
        }
    }

    // The app delegate is still the GUL-style proxy applied during launch.
    NSString *liveClass = NSStringFromClass(object_getClass(app.delegate));
    QACoexCheck([liveClass hasPrefix:@"QACOEX_"], @"isa-proxy-installed",
                [NSString stringWithFormat:@"the live delegate's class is %@", liveClass]);

    // A warm tap through the proxied app delegate must still reach the package, and the
    // category swizzle in between must have been the one to chain it there.
    QACoexDrain();
    __block int isaCompletions = 0;
    BOOL isaSent = QACoexSendWarm(nil, QACoexMakeItem(@"qa_ci_isa", YES), ^(BOOL ok) {
        isaCompletions++;
    });
    NSString *isaGot = isaSent ? QACoexConsume() : nil;
    QACoexCheck(isaSent && [isaGot isEqualToString:@"qa_ci_isa"] && isaCompletions == 1,
                @"isa-proxy-warm-reaches-package",
                [NSString stringWithFormat:@"sent=%d queued=%@ completions=%d", (int)isaSent,
                                           isaGot ?: @"(nothing)", isaCompletions]);
    QACoexCheck(gQACoexCategoryWarmChained, @"category-warm-chained",
                @"the category swizzle never saw performActionForShortcutItem");
    QACoexDrain();

    // The lifecycle's OWN warm selector: one queue entry, one completion. Both reads
    // happen on this same runloop turn, so Unity's C# drain cannot interleave.
    __block int completions = 0;
    BOOL sent = QACoexSendWarm(windowScene, QACoexMakeItem(@"qa_ci_warm", YES), ^(BOOL ok) {
        completions++;
    });
    NSString *warmGot = sent ? QACoexConsume() : nil;
    NSString *warmAgain = sent ? QACoexConsume() : nil;
    QACoexCheck(sent && [warmGot isEqualToString:@"qa_ci_warm"], @"warm-queued-id",
                [NSString stringWithFormat:@"sent=%d queue handed back %@", (int)sent,
                                           warmGot ?: @"(nothing)"]);
    QACoexCheck(sent && warmAgain == nil, @"warm-queued-once",
                [NSString stringWithFormat:@"a second read returned %@",
                                           warmAgain ?: @"(nothing)"]);
    QACoexCheck(sent && completions == 1, @"warm-completion-once",
                [NSString stringWithFormat:@"the completion handler ran %d time(s)",
                                           completions]);
    QACoexDrain();

    // Several taps arriving before C# drains: the queue must keep them all, in
    // arrival order, and must not collapse a repeat. Only the COLD source arms a
    // dedup marker, and it is consumed by the time this runs, so a,b,a is the
    // shape that pins both properties at once. Everything happens on this one
    // runloop turn, so the C# drain cannot interleave.
    __block int multiCompletions = 0;
    void (^count)(BOOL) = ^(BOOL ok) { multiCompletions++; };
    BOOL multiSent = QACoexSendWarm(windowScene, QACoexMakeItem(@"qa_ci_multi_a", YES), count);
    multiSent = QACoexSendWarm(windowScene, QACoexMakeItem(@"qa_ci_multi_b", YES), count) && multiSent;
    multiSent = QACoexSendWarm(windowScene, QACoexMakeItem(@"qa_ci_multi_a", YES), count) && multiSent;
    NSMutableArray<NSString *> *drained = [NSMutableArray array];
    for (NSString *one = QACoexConsume(); one != nil; one = QACoexConsume()) {
        [drained addObject:one];
    }
    NSArray<NSString *> *expected = @[ @"qa_ci_multi_a", @"qa_ci_multi_b", @"qa_ci_multi_a" ];
    QACoexCheck(multiSent && [drained isEqualToArray:expected], @"multi-id-queue-order",
                [NSString stringWithFormat:@"sent=%d queue handed back [%@]", (int)multiSent,
                                           [drained componentsJoinedByString:@", "]]);
    QACoexCheck(multiSent && multiCompletions == 3, @"multi-id-completion-each",
                [NSString stringWithFormat:@"the completion handler ran %d time(s), wanted 3",
                                           multiCompletions]);
    QACoexDrain();

    // An UNMARKED item — a host's own quick action. Whether the package adopts it is a
    // documented, path-dependent decision (it does when it is the only handler, it does
    // not when it is wrapped or when the scene owner is unconfirmed), so the portable
    // assertion is the one that must hold everywhere: the handler still runs once.
    __block int unmarkedCompletions = 0;
    BOOL unmarkedSent = QACoexSendWarm(windowScene, QACoexMakeItem(@"qa_ci_unmarked", NO),
                                       ^(BOOL ok) { unmarkedCompletions++; });
    QACoexCheck(unmarkedSent && unmarkedCompletions == 1, @"unmarked-completion-once",
                [NSString stringWithFormat:@"sent=%d completions=%d", (int)unmarkedSent,
                                           unmarkedCompletions]);
    QACoexDrain();

    // A COLD scene launch carrying an UNMARKED item. Both launches bind to UnityScene, so
    // the owner is confirmed and the package is terminal for the warm selector: queued
    // once; a warm redelivery before activation collapses into it; and with the warm
    // selector wrapped by someone else it is not queued at all.
    if (sceneLifecycle) {
        UIApplicationShortcutItem *coldItem = QACoexMakeItem(@"qa_ci_unmarked_cold", NO);
        BOOL coldSent = QACoexSendSceneCold(windowScene, coldItem);
        NSString *coldGot = coldSent ? QACoexConsume() : nil;
        NSString *coldAgain = coldSent ? QACoexConsume() : nil;
        QACoexCheck(coldSent && [coldGot isEqualToString:@"qa_ci_unmarked_cold"] &&
                        coldAgain == nil,
                    @"scene-unmarked-cold-queued-once",
                    [NSString stringWithFormat:@"sent=%d queue handed back %@ then %@",
                                               (int)coldSent, coldGot ?: @"(nothing)",
                                               coldAgain ?: @"(nothing)"]);

        __block int redeliveryCompletions = 0;
        BOOL redeliverySent = coldSent && QACoexSendWarm(windowScene, coldItem, ^(BOOL ok) {
            redeliveryCompletions++;
        });
        NSString *redeliveryGot = redeliverySent ? QACoexConsume() : nil;
        QACoexCheck(redeliverySent && redeliveryGot == nil && redeliveryCompletions == 1,
                    @"scene-unmarked-cold-warm-dedup",
                    [NSString stringWithFormat:@"sent=%d the redelivery queued %@, completions=%d",
                                               (int)redeliverySent, redeliveryGot ?: @"(nothing)",
                                               redeliveryCompletions]);
        QACoexDrain();

        Method warm = class_getInstanceMethod(NSClassFromString(@"UnityScene"),
            @selector(windowScene:performActionForShortcutItem:completionHandler:));
        BOOL wrappedSent = NO;
        if (warm != NULL) {
            IMP installed = method_setImplementation(warm, (IMP)QACoexForeignWarm);
            wrappedSent = QACoexSendSceneCold(windowScene,
                                              QACoexMakeItem(@"qa_ci_unmarked_wrapped", NO));
            method_setImplementation(warm, installed);
        }
        NSString *wrappedGot = wrappedSent ? QACoexConsume() : nil;
        QACoexCheck(wrappedSent && wrappedGot == nil, @"scene-unmarked-cold-wrapped-not-queued",
                    [NSString stringWithFormat:@"sent=%d queue handed back %@", (int)wrappedSent,
                                               wrappedGot ?: @"(nothing)"]);
        QACoexDrain();
    }

    NSLog(@"QA-COEX: DONE");
}

static void QACoexProbeAttempt(int attempt) {
    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(1.0 * NSEC_PER_SEC)),
                   dispatch_get_main_queue(), ^{
        BOOL sceneLifecycle = QACoexHasSceneManifest();
        // Give the engine and (under the scene lifecycle) the scene connection time to
        // come up, but run the checks anyway at the limit: printed FAILs are a far
        // better report than a job that times out with no output at all.
        if (!QACoexProbeReady(sceneLifecycle) && attempt < 30) {
            QACoexProbeAttempt(attempt + 1);
            return;
        }
        if (!QACoexProbeReady(sceneLifecycle)) {
            QACoexNote(@"probe ran without a ready surface — assertions below may cascade");
        }
        QACoexRunChecks();
    });
}

void QACoexProbeSchedule(void) {
    static dispatch_once_t once;
    dispatch_once(&once, ^{
        QACoexProbeAttempt(0);
    });
}

#endif // QUICKACTIONS_ENABLED
