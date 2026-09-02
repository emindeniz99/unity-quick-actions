// Shared helpers for the iOS coexistence mock host (CI only — see ../README.md).
//
// Every check prints exactly one line, `QA-COEX: PASS <name>` or
// `QA-COEX: FAIL <name> <detail>`, via NSLog. The workflow step in
// .github/workflows/unity-ci.yml requires every PASS name it expects, fails on any
// FAIL anywhere in the log, and requires the closing `QA-COEX: DONE` so a probe that
// never ran cannot pass by silence.
#ifndef QACOEX_H
#define QACOEX_H

#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>

// Mirrors kQAManagedMarkerKey in Plugins/iOS/QuickActions.mm. It is a FROZEN
// device-facing string: if these ever disagree the package stops recognising the
// items this mock builds and every marked assertion below turns red, which is the
// intended failure. (This folder is outside tools~/check_frozen_strings.py's scan
// dirs on purpose — it is not shipped code.)
#define QACOEX_MARKER_KEY @"com.emindeniz99.quickactions.managed"

#ifdef __cplusplus
extern "C" {
#endif

// The package's own C entry points, declared locally: this file compiles into the
// same target (UnityFramework) as Plugins/iOS/QuickActions.mm, so the symbols are
// there without a header from the package.
char *_QuickActions_ConsumePendingPerformed(void);
void _QuickActions_FreeString(char *ptr);

// QACoexProbe.mm — starts the deferred assertion pass (idempotent).
void QACoexProbeSchedule(void);
// QACoexIsaProxy.mm — GoogleUtilities-style isa swizzle of the live app delegate.
void QACoexInstallIsaProxy(id delegate);

// State the probe reads back from the other translation units.
extern BOOL gQACoexCategoryColdChained;
extern BOOL gQACoexCategoryWarmChained;
extern int gQACoexSubclassSuperCalls;

#ifdef __cplusplus
}
#endif

static inline void QACoexPass(NSString *name) {
    NSLog(@"QA-COEX: PASS %@", name);
}

static inline void QACoexFail(NSString *name, NSString *detail) {
    NSLog(@"QA-COEX: FAIL %@ %@", name, detail);
}

static inline void QACoexCheck(BOOL ok, NSString *name, NSString *detail) {
    if (ok) {
        QACoexPass(name);
    } else {
        QACoexFail(name, detail);
    }
}

static inline void QACoexNote(NSString *message) {
    NSLog(@"QA-COEX: NOTE %@", message);
}

// One entry off the package's pull channel, or nil when it is empty.
static inline NSString *QACoexConsume(void) {
    char *raw = _QuickActions_ConsumePendingPerformed();
    if (raw == NULL) return nil;
    NSString *value = [NSString stringWithUTF8String:raw];
    _QuickActions_FreeString(raw);
    return value;
}

static inline void QACoexDrain(void) {
    while (QACoexConsume() != nil) {
        // Start every assertion from an empty queue.
    }
}

// A shortcut item shaped like one of ours (marked) or like a host's (unmarked).
static inline UIApplicationShortcutItem *QACoexMakeItem(NSString *type, BOOL marked) {
    NSDictionary<NSString *, id<NSSecureCoding>> *info = nil;
    if (marked) {
        info = (NSDictionary<NSString *, id<NSSecureCoding>> *)@{QACOEX_MARKER_KEY: @YES};
    }
    return [[UIApplicationShortcutItem alloc] initWithType:type
                                           localizedTitle:type
                                        localizedSubtitle:nil
                                                     icon:nil
                                                 userInfo:info];
}

static inline BOOL QACoexHasSceneManifest(void) {
    return [NSBundle mainBundle].infoDictionary[@"UIApplicationSceneManifest"] != nil;
}

#endif // QACOEX_H
