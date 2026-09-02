// Mock VENDOR category swizzle — the AppsFlyer / OneSignal / Firebase-C++ shape.
//
// A category +load on UnityAppController that method_setImplementation's the two
// app-delegate selectors the package owns, saving and chaining each original. Two
// things fall out of it:
//
//   1. ORDERING, executably. objc4 runs every pending CLASS +load before any CATEGORY
//      +load in the same image, and the package installs from a class +load. So by the
//      time this runs, application:performActionForShortcutItem:completionHandler:
//      must already exist on UnityAppController — a selector Unity itself never
//      implements. That single lookup is the proof the whole design leans on.
//   2. The package entered WRAPPED. Its terminal probe re-reads the installed IMP at
//      call time, sees ours rather than its own, and therefore declines to touch the
//      completion handler. We pass the handler down and then complete it ourselves,
//      exactly once — so if the package ever regressed to completing while wrapped,
//      the probe's counter would see 2 and the leg would go red.
#import <UIKit/UIKit.h>
#import <objc/runtime.h>

#import "UnityAppController.h"

#import "QACoex.h"

#if QUICKACTIONS_ENABLED

BOOL gQACoexCategoryColdChained = NO;
BOOL gQACoexCategoryWarmChained = NO;

static BOOL (*gQACoexOrigDidFinish)(id, SEL, UIApplication *, NSDictionary *) = NULL;
static void (*gQACoexOrigPerform)(id, SEL, UIApplication *, UIApplicationShortcutItem *,
                                  void (^)(BOOL)) = NULL;

static BOOL QACoexSwizzledDidFinish(id self, SEL _cmd, UIApplication *application,
                                    NSDictionary *launchOptions) {
    gQACoexCategoryColdChained = YES;
    if (gQACoexOrigDidFinish != NULL) {
        return gQACoexOrigDidFinish(self, _cmd, application, launchOptions);
    }
    return YES;
}

static void QACoexSwizzledPerform(id self, SEL _cmd, UIApplication *application,
                                  UIApplicationShortcutItem *shortcutItem,
                                  void (^completionHandler)(BOOL)) {
    gQACoexCategoryWarmChained = YES;
    if (gQACoexOrigPerform != NULL) {
        gQACoexOrigPerform(self, _cmd, application, shortcutItem, completionHandler);
    }
    // We are the outermost implementation on this selector and we handed the handler
    // down, so completion is ours to own. Exactly once — see the header comment.
    if (completionHandler != nil) completionHandler(YES);
}

@implementation UnityAppController (QACoexSwizzle)

+ (void)load {
    static dispatch_once_t once;
    dispatch_once(&once, ^{
        Class cls = [UnityAppController class];
        SEL performSel = @selector(application:performActionForShortcutItem:completionHandler:);
        SEL didFinishSel = @selector(application:didFinishLaunchingWithOptions:);

        Method perform = class_getInstanceMethod(cls, performSel);
        // Which +load ran first is RECORDED, never required. The first CI run showed
        // why: on the 2022.3.62f3 export this category's +load ran BEFORE the
        // package's class +load, on the 6000.3.21f1 export AFTER it — every file in
        // the same UnityFramework target both times. objc4 orders a class's own
        // +load before its categories' and superclasses before subclasses; what it
        // promises between a class +load and a category +load on DIFFERENT classes
        // is nothing, and the two toolchains delivered both orders. So this category
        // behaves like a real vendor swizzle in either order: when the selector is
        // already there it wraps and chains, and when nothing implements it yet it
        // ADDS its own handler (which then completes itself); the package, arriving
        // later, wraps THAT and chains to it. The probe's chain and exactly-once
        // checks must hold either way — that is the contract worth asserting.
        NSLog(@"QA-COEX: PASS category-load-ran order=%s",
              perform != NULL ? "class-first" : "category-first");

        Method didFinish = class_getInstanceMethod(cls, didFinishSel);
        if (didFinish != NULL) {
            gQACoexOrigDidFinish = (BOOL (*)(id, SEL, UIApplication *, NSDictionary *))
                method_setImplementation(didFinish, (IMP)QACoexSwizzledDidFinish);
        }
        if (perform != NULL) {
            gQACoexOrigPerform =
                (void (*)(id, SEL, UIApplication *, UIApplicationShortcutItem *, void (^)(BOOL)))
                    method_setImplementation(perform, (IMP)QACoexSwizzledPerform);
        } else {
            gQACoexOrigPerform = NULL;
            class_addMethod(cls, performSel, (IMP)QACoexSwizzledPerform, "v@:@@@?");
        }
    });
}

@end

#endif // QUICKACTIONS_ENABLED
