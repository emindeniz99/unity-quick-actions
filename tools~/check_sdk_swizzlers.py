#!/usr/bin/env python3
"""Sentinel: no popular iOS SDK swizzles the quick-action selectors.

This package composes with app-delegate swizzlers by wrapping and chaining
whatever it finds (see Examples~/Coexistence/), and CI proves that against a
mock host shaped like the real ones. What the mock host cannot tell us is
whether any real SDK actually *competes* for the two selectors we own:

    application:performActionForShortcutItem:completionHandler:
    windowScene:performActionForShortcutItem:completionHandler:

An audit of the iOS SDKs a Unity game is most likely to link said no — none of
them touches either selector; they hook URL opening, universal links and remote
notifications. That answer has a shelf life: it was read once, from one version.
This script re-reads the sources at a PINNED tag on every CI run, so the day an
upstream release starts hooking a quick-action selector, CI says so instead of a
future maintainer rediscovering it from a bug report.

It reads upstream over the network and asserts on what it read; it never
vendors anything. A network failure is the automation missing, not a finding, so
it warns and exits 0 unless --require-network is passed.

Usage:
    python3 tools~/check_sdk_swizzlers.py [--require-network]
"""

import argparse
import re
import sys
import urllib.error
import urllib.request

# (label, url, why this file is the one to watch). Pinned by TAG, never by a
# branch: a moving target would make this check's own result unreproducible.
#
# GoogleUtilities is the whole list on purpose. It is the swizzler under
# Firebase — by far the most-linked iOS SDK in a Unity game, and the only
# widely shipped one that rewrites a live app delegate's isa rather than
# swizzling methods on a class. Every other audited SDK (AppsFlyer, Branch,
# OneSignal, Adjust, Singular, Braze) swizzles or subclasses by hand with no
# shared source we could pin, and none of them was found near these selectors
# either; adding an unpinnable grep for each would trade this check's
# reproducibility for coverage it cannot actually keep.
GOOGLEUTILITIES_TAG = "8.1.0"
RAW = "https://raw.githubusercontent.com/google/GoogleUtilities/{tag}/{path}"
SOURCES = [
    (
        "GoogleUtilities " + GOOGLEUTILITIES_TAG + " GULAppDelegateSwizzler.m",
        RAW.format(tag=GOOGLEUTILITIES_TAG,
                   path="GoogleUtilities/AppDelegateSwizzler/GULAppDelegateSwizzler.m"),
        "the app-delegate isa proxy Firebase installs",
    ),
    (
        "GoogleUtilities " + GOOGLEUTILITIES_TAG + " GULSceneDelegateSwizzler.m",
        RAW.format(tag=GOOGLEUTILITIES_TAG,
                   path="GoogleUtilities/AppDelegateSwizzler/GULSceneDelegateSwizzler.m"),
        "the scene-delegate half of the same proxy",
    ),
]

# Case-insensitive: the finding is "this file is now in the quick-action
# business at all", which a renamed helper or a comment would announce just as
# well as the selector itself.
NEEDLE = re.compile(r"shortcut", re.I)
SELECTOR = re.compile(r"@selector\(([^)]*)\)")


def fetch(url: str) -> str:
    request = urllib.request.Request(url, headers={"User-Agent": "quick-actions-sentinel"})
    with urllib.request.urlopen(request, timeout=30) as response:
        return response.read().decode("utf-8", "replace")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--require-network", action="store_true",
                        help="treat a failed fetch as an error instead of a warning")
    args = parser.parse_args()

    findings = []
    unreachable = []
    for label, url, why in SOURCES:
        try:
            text = fetch(url)
        except (urllib.error.URLError, TimeoutError, OSError) as error:
            unreachable.append((label, url, error))
            continue
        hits = sorted({match.group(0) for match in NEEDLE.finditer(text)})
        # What it DOES hook, so a change in scope is visible in the log even
        # when the answer is still "not ours".
        # The source wraps long selectors across lines, so fold the whitespace out
        # BEFORE deduplicating or the same selector lands in the set twice.
        selectors = sorted({re.sub(r"\s+", "", s) for s in SELECTOR.findall(text)})
        print("%s — %s" % (label, why))
        print("    %d lines, %d @selector() reference(s)" % (len(text.splitlines()), len(selectors)))
        for selector in selectors:
            print("      @selector(%s)" % selector)
        if hits:
            findings.append((label, hits))
            print("    !! mentions %s" % ", ".join(hits))
        else:
            print("    ok: no mention of a shortcut anywhere in the file")

    for label, url, error in unreachable:
        message = "could not read %s (%s): %s" % (label, url, error)
        if args.require_network:
            print("!! " + message)
        else:
            print("::warning::sdk swizzler sentinel — " + message)

    if findings:
        print()
        for label, hits in findings:
            print("::error::%s now mentions %s — an upstream release may have started "
                  "competing for the quick-action selectors. Read it before trusting "
                  "Examples~/Coexistence/README.md's audit." % (label, ", ".join(hits)))
        return 1
    if unreachable and args.require_network:
        return 1
    if unreachable and len(unreachable) == len(SOURCES):
        print("SDK SWIZZLERS: SKIPPED (nothing could be read)")
        return 0
    print("SDK SWIZZLERS: CLEAN (%d source(s) read, none touches a quick action)"
          % (len(SOURCES) - len(unreachable)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
