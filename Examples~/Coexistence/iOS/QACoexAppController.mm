// Mock HOST app-controller subclass — the shape that breaks most Unity iOS plugins.
//
// IMPL_APP_CONTROLLER_SUBCLASS is Unity's only sanctioned app-delegate extension
// point and is what AppsFlyer, Braze, Singular, Branch and Helpshift all use. This
// subclass reproduces the awkward half of that shape on purpose:
//   * it injects a MARKED shortcut into the launchOptions it forwards to super, on
//     the ONE super call it makes (a second would re-initialise Unity), so the
//     package's cold path runs for real without a SpringBoard tap;
//   * it then DISCARDS the package's NO and returns YES — the Braze/host shape the
//     consume-once cold marker exists to survive;
//   * it overrides the warm selector and chains to super, so the package is entered
//     wrapped rather than terminal.
// It proves our code handles those payloads correctly; it cannot prove UIKit would
// have delivered them (see ../README.md).
#import <UIKit/UIKit.h>
#import <stdlib.h>
#import <string.h>

// Unity's generated project puts Classes/ on the UnityFramework target's header
// search path — the same import every vendor app-controller subclass uses. If that
// ever stops being true this file fails to compile, which is the honest signal.
#import "UnityAppController.h"

#import "QACoex.h"

#if QUICKACTIONS_ENABLED

int gQACoexSubclassSuperCalls = 0;

@interface QACoexAppController : UnityAppController
// Declared (not just defined) so the availability annotation lands on the
// declaration, which is what gives the body its iOS 13 context.
- (UISceneConfiguration *)application:(UIApplication *)application
    configurationForConnectingSceneSession:(UISceneSession *)connectingSceneSession
                                   options:(UISceneConnectionOptions *)options
    API_AVAILABLE(ios(13.0));
@end

@implementation QACoexAppController

- (BOOL)application:(UIApplication *)application
    didFinishLaunchingWithOptions:(NSDictionary *)launchOptions {
    UIApplicationShortcutItem *item = QACoexMakeItem(@"qa_ci_cold", YES);
    NSMutableDictionary *injected = launchOptions != nil ? [launchOptions mutableCopy]
                                                         : [NSMutableDictionary dictionary];
    injected[UIApplicationLaunchOptionsShortcutItemKey] = item;

    gQACoexSubclassSuperCalls++;
    BOOL result = [super application:application didFinishLaunchingWithOptions:injected];

    // Apple: returning NO from didFinishLaunchingWithOptions is what stops the system
    // ALSO calling performActionForShortcutItem for the same item. The package returns
    // it only for its own marked item, so this asserts the whole cold chain ran:
    // subclass -> category swizzle -> package -> Unity.
    QACoexCheck(result == NO, @"cold-returns-no",
                [NSString stringWithFormat:@"super returned %@ for a marked launch item",
                                           result ? @"YES" : @"NO"]);

    // When the app delegate returns YES for a launch item — which this host is about
    // to do on the package's behalf — Apple ALSO delivers that item through the warm
    // selector. UIKit will not do that for an item we injected into launchOptions
    // ourselves, so the redelivery is driven by hand, through this class's own
    // override and down the chain (subclass -> category swizzle -> package). This is
    // the duplicate the package's consume-once cold marker exists to collapse: the
    // queue must hand back the id exactly once, and the handler must run exactly once
    // (the category swizzle owns it). Reading the queue without this send would be
    // "once" for any implementation, dedup or not.
    __block int dupCompletions = 0;
    [self application:application
        performActionForShortcutItem:item
                   completionHandler:^(BOOL handled) {
                       dupCompletions++;
                       QACoexNote([NSString stringWithFormat:
                           @"warm redelivery of the launch item completed handled=%d",
                           (int)handled]);
                   }];

    NSString *first = QACoexConsume();
    NSString *second = QACoexConsume();
    QACoexCheck([first isEqualToString:@"qa_ci_cold"], @"cold-queued-id",
                [NSString stringWithFormat:@"queue handed back %@", first ?: @"(nothing)"]);
    QACoexCheck(second == nil, @"cold-warm-dedup",
                [NSString stringWithFormat:@"the warm redelivery of the launch item was "
                                           @"queued again: a second read returned %@",
                                           second ?: @"(nothing)"]);
    QACoexCheck(dupCompletions == 1, @"cold-warm-dedup-completion-once",
                [NSString stringWithFormat:@"the redelivery's completion handler ran %d "
                                           @"time(s)", dupCompletions]);

    // GoogleUtilities applies its proxy from component init — after every +load, around
    // this point in the launch. Match that timing rather than swizzling at load.
    QACoexInstallIsaProxy(self);
    QACoexProbeSchedule();

    // Deliberately discard the package's NO: the host shape the cold marker defends.
    return YES;
}

- (void)application:(UIApplication *)application
    performActionForShortcutItem:(UIApplicationShortcutItem *)shortcutItem
               completionHandler:(void (^)(BOOL))completionHandler {
    // Chain and pass the handler down: the category swizzle below us owns completion.
    // Completing here as well would be the double invocation the package refuses to
    // risk, and the probe's exactly-once counter would catch it.
    [super application:application
        performActionForShortcutItem:shortcutItem
                   completionHandler:completionHandler];
}

// The subclass-shadows-the-configuration-selector shape from the coexistence matrix.
// One build, two behaviours, chosen at launch through
// SIMCTL_CHILD_QA_COEX_SHADOW_SCENE_CONFIG so CI needs no second export:
//   unset/0 — call [super ...], which routes into the package's wrapper and installs
//             the scene hooks BEFORE willConnect ("via configuration");
//   1       — return the session's own configuration without calling super, so the
//             package's wrapper never runs and only the UISceneWillConnectNotification
//             fallback can install ("via notification").
- (UISceneConfiguration *)application:(UIApplication *)application
    configurationForConnectingSceneSession:(UISceneSession *)connectingSceneSession
                                   options:(UISceneConnectionOptions *)options {
    const char *shadow = getenv("QA_COEX_SHADOW_SCENE_CONFIG");
    BOOL skipSuper = shadow != NULL && strcmp(shadow, "1") == 0;
    SEL sel = @selector(application:configurationForConnectingSceneSession:options:);
    if (!skipSuper && [[self superclass] instancesRespondToSelector:sel]) {
        QACoexNote(@"host subclass forwards configurationForConnectingSceneSession to super");
        return [super application:application
            configurationForConnectingSceneSession:connectingSceneSession
                                           options:options];
    }
    QACoexNote(@"host subclass shadows configurationForConnectingSceneSession (no super)");
    return connectingSceneSession.configuration;
}

@end

IMPL_APP_CONTROLLER_SUBCLASS(QACoexAppController)

#endif // QUICKACTIONS_ENABLED
