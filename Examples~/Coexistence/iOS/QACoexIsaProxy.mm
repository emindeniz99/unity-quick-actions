// Mock GoogleUtilities-style isa swizzle of the LIVE app delegate.
//
// GULAppDelegateSwizzler allocates a dynamic subclass of the delegate's class, adds
// its donor implementations to it, registers it and object_setClass's the delegate
// instance — "isa swizzling". What this asserts:
//   * the dynamic subclass INHERITS our implementations, so a warm tap through the
//     proxied object still reaches the package — the property the whole Firebase row
//     of the coexistence matrix rests on;
//   * GoogleUtilities' own gate (it abandons the proxy unless the subclass's instance
//     size equals the original's) is reproduced, so this leg fails the same way GUL
//     would rather than proceeding past a condition Firebase itself refuses. Note what
//     it does NOT prove: adding methods cannot change instance size, and the runtime
//     forbids adding an ivar to an already-realized class at all, so this is a gate on
//     the proxy construction, not a detector of package-side layout changes.
//
// Applied from didFinishLaunching (GUL's real timing: component init, after every
// +load), not from a +load of our own.
#import <UIKit/UIKit.h>
#import <objc/runtime.h>
#import <stdio.h>

#import "QACoex.h"

#if QUICKACTIONS_ENABLED

// A donor with the same selector GoogleUtilities donates. Nothing in the testbed
// opens a URL, so shadowing Unity's own implementation for this process is inert.
static BOOL QACoexDonorOpenURL(id self, SEL _cmd, UIApplication *application, NSURL *url,
                               NSDictionary *options) {
    return NO;
}

void QACoexInstallIsaProxy(id delegate) {
    static BOOL installed = NO;
    if (installed || delegate == nil) return;
    installed = YES;

    Class realClass = object_getClass(delegate);
    NSString *name = [NSString stringWithFormat:@"QACOEX_%@-%@", NSStringFromClass(realClass),
                                                [[NSUUID UUID] UUIDString]];
    Class proxy = objc_allocateClassPair(realClass, name.UTF8String, 0);
    if (proxy == Nil) {
        QACoexFail(@"isa-proxy-allocated", @"objc_allocateClassPair returned Nil");
        return;
    }

    // Same @encode(BOOL)-derived encoding rule the package uses: 'B' on every 64-bit
    // iOS slice, never the armv7-era 'c'.
    static char donorTypes[16];
    snprintf(donorTypes, sizeof(donorTypes), "%s@:@@@", @encode(BOOL));
    class_addMethod(proxy, @selector(application:openURL:options:), (IMP)QACoexDonorOpenURL,
                    donorTypes);
    objc_registerClassPair(proxy);

    QACoexCheck(class_getInstanceSize(realClass) == class_getInstanceSize(proxy),
                @"isa-proxy-size-equal",
                [NSString stringWithFormat:@"%lu vs %lu — GoogleUtilities would abandon "
                                           @"this proxy",
                                           (unsigned long)class_getInstanceSize(realClass),
                                           (unsigned long)class_getInstanceSize(proxy)]);

    object_setClass(delegate, proxy);

    // The inherited hook must still resolve through the dynamic subclass.
    Method warm = class_getInstanceMethod(
        object_getClass(delegate),
        @selector(application:performActionForShortcutItem:completionHandler:));
    QACoexCheck(warm != NULL, @"isa-proxy-warm-hook-resolves",
                @"the warm selector no longer resolves through the proxy subclass");
}

#endif // QUICKACTIONS_ENABLED
