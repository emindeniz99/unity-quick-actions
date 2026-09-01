#!/usr/bin/env python3
"""Pin the identifier strings that are baked into END-USER DEVICES.

Why this exists
---------------
A handful of literals do not merely cross the C#/native boundary — the OS
persists them. Android stores a dynamic shortcut's intent (action, extras) and
its `PersistableBundle` in the launcher's database; when a user **pins** a
shortcut, that record is frozen there forever and the app cannot repair it.
iOS persists `UIApplicationShortcutItemUserInfo` the same way, and static
shortcuts are baked into every shipped APK's `res/xml` and every IPA's
`Info.plist`.

Change one of these and nothing fails loudly. The app launches, the shortcut
still sits on the home screen, and `Performed` simply never fires — or the
package stops recognising its own shortcuts and refuses to remove them. There
is no migration path, because the record lives on a device we do not control.

Each value is also duplicated across two to four files in three languages
(Java, Objective-C, C#), kept in sync only by comments. `IconType` has a
per-value unit test guarding exactly this class of mistake; these had nothing,
which is what this script fixes. A C# test cannot cover them — the Editor
constants live in assemblies the test asmdef does not reference, and the Java
and Obj-C copies are not C# at all — so the check is a textual one, run by
`tools~/verify.sh`.

If a value below genuinely must change, it is a **breaking change for shortcuts
already on user devices**, not a refactor. Ship it with a major version and a
note in the CHANGELOG.

The table also carries **one build-time cross-language pin** alongside the
device-persisted values — `ic_quickaction_`, the icon-name prefix the Java
lookup concatenates and the C# post-processor's resource-shrinker keep rule
protects. It is here for the same reason: two copies in two languages, no
compiler and no test run that can notice them drifting apart.
"""
from __future__ import annotations

import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent

# value -> every file that must contain it verbatim.
# Adding a file here is fine; changing a VALUE is the thing this guards.
FROZEN: dict[str, list[str]] = {
    # The single ownership test on both platforms. If this changes, every
    # shortcut published by an older build becomes "foreign": never removed,
    # never disabled, and re-publishing the same id is dropped as a collision.
    "com.emindeniz99.quickactions.managed": [
        "Plugins/Android/QuickActionsBridge.java",
        "Plugins/iOS/QuickActions.mm",
        "Editor/iOS/QuickActionsBuildPostProcessoriOS.cs",
        "Editor/NativeGate/iOS/QuickActionsGateCleanupiOS.cs",
    ],
    # The resolved ComponentName inside every pinned Android shortcut.
    "com.emindeniz99.quickactions.QuickActionsTrampolineActivity": [
        "Editor/Android/QuickActionsTrampolineInjectorAndroid.cs",
        "Editor/Android/QuickActionsBuildPostProcessorAndroid.cs",
        "Editor/NativeGate/QuickActionsTrampolineStripperAndroid.cs",
    ],
    # Encodes the id for STATIC shortcuts, baked into res/xml in shipped APKs.
    "com.emindeniz99.quickactions.PERFORM.": [
        "Plugins/Android/QuickActionsBridge.java",
        "Editor/Android/QuickActionsBuildPostProcessorAndroid.cs",
    ],
    # Carries the tapped id on dynamic shortcuts; frozen in pinned intents.
    "com.emindeniz99.quickactions.ACTION_ID": [
        "Plugins/Android/QuickActionsBridge.java",
    ],
    # The JNI class the C# bridge looks up by name.
    "com.emindeniz99.quickactions.QuickActionsBridge": [
        "Runtime/Internal/AndroidQuickActionsBridge.cs",
    ],
    # Extras/userInfo keys. Icon identity is read back from these on cold
    # start, so a change re-publishes every existing shortcut iconless.
    "com.emindeniz99.quickactions.icon": [
        "Plugins/Android/QuickActionsBridge.java",
        "Plugins/iOS/QuickActions.mm",
    ],
    "com.emindeniz99.quickactions.payload": [
        "Plugins/Android/QuickActionsBridge.java",
        "Plugins/iOS/QuickActions.mm",
    ],
    "com.emindeniz99.quickactions.l10n": [
        "Plugins/Android/QuickActionsBridge.java",
        "Plugins/iOS/QuickActions.mm",
    ],
    "com.emindeniz99.quickactions.drawable": ["Plugins/Android/QuickActionsBridge.java"],
    "com.emindeniz99.quickactions.bitmap": ["Plugins/Android/QuickActionsBridge.java"],
    "com.emindeniz99.quickactions.bitmap_adaptive": ["Plugins/Android/QuickActionsBridge.java"],
    "com.emindeniz99.quickactions.symbol": ["Plugins/iOS/QuickActions.mm"],
    "com.emindeniz99.quickactions.template": ["Plugins/iOS/QuickActions.mm"],
    # Unlike every other row here this one is NOT device-persisted: it is a
    # two-language BUILD-TIME contract. The Java lookup builds an icon name by
    # concatenation ("ic_quickaction_" + catalog name) and the C# post-processor
    # writes res/raw/quickactions_keep.xml with tools:keep="@drawable/ic_quickaction_*".
    # If the two spellings drift, the keep rule stops covering the drawables the
    # lookup asks for and minified release builds silently ship blank icons —
    # nothing fails at build time, on either side.
    "ic_quickaction_": [
        "Plugins/Android/QuickActionsBridge.java",
        "Editor/Android/QuickActionsBuildPostProcessorAndroid.cs",
    ],
    # The package's OWN four drawables: the name the Java lookup falls back to,
    # the post-processor that writes them, and the define-off stripper that
    # sweeps them back out of unityLibrary — a drifted prefix there leaves
    # built-in icons in a production build that was meant to carry nothing of
    # this package. Same keep glob as above (ic_quickaction_* covers both).
    "ic_quickaction_builtin_": [
        "Plugins/Android/QuickActionsBridge.java",
        "Editor/Android/QuickActionsBuildPostProcessorAndroid.cs",
        "Editor/NativeGate/QuickActionsTrampolineStripperAndroid.cs",
    ],
}

# The Java package the trampoline and bridge live in — the first half of every
# FQCN above.
JAVA_PACKAGE = "com.emindeniz99.quickactions"
JAVA_PACKAGE_FILES = [
    "Plugins/Android/QuickActionsBridge.java",
    "Plugins/Android/QuickActionsTrampolineActivity.java",
]

# Where a stray/renamed variant would hide.
SCAN_DIRS = ["Runtime", "Editor", "Plugins"]
LITERAL_RE = re.compile(r'"(com\.emindeniz99\.quickactions[^"]*)"')


def main() -> int:
    errors: list[str] = []

    # 1. Every frozen value is present, verbatim, in every file that owns a copy.
    for value, files in FROZEN.items():
        for rel in files:
            p = ROOT / rel
            if not p.is_file():
                errors.append(f"missing file {rel} (expected to contain {value!r})")
                continue
            # Match the QUOTED literal, not a substring: a rename to
            # "…managed2" still contains "…managed", so a bare `in` would pass.
            # Every copy is a string literal in Java/C#/Obj-C, so the closing
            # quote is the boundary that makes this exact.
            if f'"{value}"' not in p.read_text(encoding="utf-8", errors="replace"):
                errors.append(
                    f"{rel}: does NOT contain the frozen string {value!r}.\n"
                    f"      This string is persisted on end-user devices. If a copy was\n"
                    f"      renamed, revert it; the value cannot change without breaking\n"
                    f"      shortcuts that already exist on phones."
                )

    # 2. The Java package declaration behind every FQCN.
    for rel in JAVA_PACKAGE_FILES:
        p = ROOT / rel
        if not p.is_file():
            errors.append(f"missing file {rel}")
            continue
        if f"package {JAVA_PACKAGE};" not in p.read_text(encoding="utf-8", errors="replace"):
            errors.append(f"{rel}: package declaration is not `package {JAVA_PACKAGE};`")

    # 3. Reverse check — no NEW or misspelled variant may appear. This is what
    #    catches a rename that updated some copies and invented a new key.
    known = set(FROZEN)
    for d in SCAN_DIRS:
        for p in sorted((ROOT / d).rglob("*")):
            if p.suffix not in (".cs", ".java", ".mm", ".h") or not p.is_file():
                continue
            for m in LITERAL_RE.finditer(p.read_text(encoding="utf-8", errors="replace")):
                if m.group(1) not in known:
                    rel = p.relative_to(ROOT)
                    errors.append(
                        f"{rel}: unknown device-facing literal {m.group(1)!r}.\n"
                        f"      Either it is a typo of an existing key, or it is a new one —\n"
                        f"      add it to FROZEN in tools~/check_frozen_strings.py so it is\n"
                        f"      pinned from now on."
                    )

    if errors:
        print("FROZEN STRINGS: FAIL", file=sys.stderr)
        for e in errors:
            print(f"  - {e}", file=sys.stderr)
        return 1

    total = sum(len(v) for v in FROZEN.values()) + len(JAVA_PACKAGE_FILES)
    print(f"Frozen device-facing strings OK ({len(FROZEN)} values, {total} copies pinned)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
