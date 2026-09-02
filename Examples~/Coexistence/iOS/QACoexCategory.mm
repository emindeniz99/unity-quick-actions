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
        QACoexCheck(perform != NULL, @"category-load-after-class-load",
                    @"UnityAppController has no performActionForShortcutItem at category "
                    @"+load time — the package's class +load did not run first");

        Method didFinish = class_getInstanceMethod(cls, didFinishSel);
        if (didFinish != NULL) {
            gQACoexOrigDidFinish = (BOOL (*)(id, SEL, UIApplication *, NSDictionary *))
                method_setImplementation(didFinish, (IMP)QACoexSwizzledDidFinish);
        }
        if (perform != NULL) {
            gQACoexOrigPerform =
                (void (*)(id, SEL, UIApplication *, UIApplicationShortcutItem *, void (^)(BOOL)))
                    method_setImplementation(perform, (IMP)QACoexSwizzledPerform);
        }
    });
}

@end

#endif // QUICKACTIONS_ENABLED
