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
