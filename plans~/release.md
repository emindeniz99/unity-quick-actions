# Release plan & readiness analysis — Home-Screen Quick Actions (iOS & Android)

Deep assessment of where the package stands versus a shippable Asset Store
product, and the path to launch. Companion to
[`STORE_CHECKLIST.md`](../STORE_CHECKLIST.md) (the step-by-step) and
[`ROADMAP.md`](../ROADMAP.md) (feature backlog).

## Readiness scorecard

| Area | State | Evidence |
|------|-------|----------|
| Core feature (dynamic) | ✅ done | `QuickActions` API; iOS swizzle; Android trampoline |
| Static shortcuts | ✅ done | Project Settings asset + iOS/Android post-processors |
| Cross-version Android | ✅ done | trampoline avoids `UnityPlayerActivity`/`GameActivity` divergence |
| C# compiles | ✅ verified | `dotnet` build, 9 configs, 0 warnings |
| Android plugin compiles | ✅ verified | `javac` vs SDK stubs |
| Unit tests | ✅ full suite passing (73 headless via verify.sh; 74 in Unity's Test Runner — it adds 5 JsonUtility SerializationTests, and 4 tests are headless-only) | `dotnet test` (list logic, validity, equality, icon pin, dispatch, reconcile, ordering, drain, last-performed) |
| Self code-review | ✅ done | 8-angle review; all confirmed bugs fixed |
| Docs | ✅ done | README, CHANGELOG, ROADMAP, plans, per-folder READMEs |
| Store images | ✅ generated | `store~/` at exact required sizes |
| Store checklist | ✅ done | `STORE_CHECKLIST.md` |
| Real-Unity import + tests (2021.3, 2022.3, 6.3) | ✅ verified | licensed 2021.3.45f2, 2022.3.62f3, 6000.3.21f1: import 0 errors, Test Runner 74/74 — see [`PRODUCTION_READINESS.md`](../PRODUCTION_READINESS.md). (Earlier runs on 2022.3 and 6.0 reported 35/35 — historical, the suite was that size then.) |
| Real Android build (2021.3, 2022.3, 6.3) | ✅ verified | real player builds: trampoline injection (`UnityPlayerActivity` path on 2021.3/2022.3, `UnityPlayerGameActivity` path on 6.3) + static-shortcuts baker + managed gate; the same build with `QUICKACTIONS_ENABLED` removed contains no trace of the trampoline |
| Real-Unity pass on **2021.3** | ✅ verified | 2021.3.45f2, the declared minimum: clean import, Test Runner 74/74, Android player build with the trampoline injected (the old in-container `bee_backend` hang was a headless-Linux tooling artifact) |
| iOS `.mm` compiled | ✅ verified | the generated Xcode project compiles with `xcodebuild` against the real iOS device SDK on 2021.3 and 6.3 — zero warnings from `QuickActions.mm` |
| On-device behaviour | ⛔ not done (physical hardware) | the iOS **Simulator** runtime run passes on 6.3 / iOS 26.5 — static shortcuts baked into Info.plist plus one added at runtime both show with their SF Symbol icons, and tapping one cold-launches the app with the id arriving on `Performed`; a real iPhone and a real Android device are still untested |
| Real screenshots | ⛔ mockups | replace `screenshot-1` with device capture |

Legend: ✅ verified · ⛔ needs physical hardware.

## Gap analysis — what stands between here and "submit"

1. **Physical-device validation (the only hard blocker left).** Build the Demo
   to an iOS device (Xcode) and an Android device (API 25+); confirm long-press
   shows the shortcuts and taps route on cold + warm starts, for both dynamic
   and static. The iOS Simulator already covers the runtime path end-to-end
   (6.3 / iOS 26.5: static + runtime-added shortcuts appear, a tap cold-launches
   and fires `Performed`); Android has no simulator equivalent for this, and
   neither has real hardware.
2. **iOS toolchain pass — done.** The `.mm` has been compiled by a real Apple
   SDK: the generated Xcode project builds with `xcodebuild` against the iOS
   device SDK on both 2021.3 and 6.3 with zero warnings from `QuickActions.mm`,
   which also confirms the gated Editor asmdefs' `precompiledReferences`
   resolve for iOS.
3. **Unity 2021.3 compile pass — done.** 2021.3.45f2, the claimed minimum,
   imports clean, runs the Test Runner suite 74/74 and produces an Android
   player with the trampoline injected. The in-container attempt that hung had
   hit a headless-Linux `bee_backend` artifact, not a package defect.
4. **Real screenshot.** Swap the mockup `screenshot-1` for an on-device shot.
5. **Version.** `0.4.0` is the first public release — honest pre-device. Cut
   `1.0.0` only after the device pass.

## Phased plan

**Phase A — verify (done):** compile + tests + review + images + docs. ✅

**Phase B — real Unity (done):**
- ✅ 2021.3.45f2, 2022.3.62f3 and 6000.3.21f1: import with 0 console errors,
  Test Runner 74/74, both menus register, managed gate proven in real player
  builds; all three produced real Android players. (The earlier 2022.3 and 6.0
  runs reported 35/35 — historical, the suite was that size then.)
- ✅ 2021.3: clean compile on a desktop editor, the declared minimum confirmed.
- ✅ The **iOS** target builds on macOS — `xcodebuild` against the real iOS
  device SDK on 2021.3 and 6.3, zero warnings from `QuickActions.mm` — and a
  6.3 iOS **Simulator** run exercises the whole runtime path.

**Phase C — device (needs physical hardware):**
- Android: build Demo, install, long-press, verify cold/warm + static/dynamic.
- iOS: build Demo on macOS/Xcode, same checks. Capture a real screenshot.

**Phase D — submit:**
- Bump to `1.0.0` (package.json + CHANGELOG) if device-validated.
- Build the `.unitypackage` (`tools~/release.sh`, or grab it from the
  [GitHub Release](https://github.com/emindeniz99/unity-quick-actions/releases)
  — `v0.4.0` is the first release; a local `python3 tools~/pack_unitypackage.py`
  produces the same file).
- Create publisher account, fill metadata + images, upload, submit for review.

## Risk register

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| Editor asmdef ext-DLL refs don't resolve on a target | low | `#if`-guarded; `precompiledReferences` set; confirm in Phase B; fallback = split asmdef |
| Android trampoline focus/foreground quirk on some OEM | med | conventional launcher-intent flags; validate Phase C; fallback = subclass activity |
| iOS swizzle conflicts with another plugin | low | chains original IMP; idempotent; review-confirmed |
| Store review decline (screenshots/quality) | med | follow checklist; real screenshot; clean import |
| Non-ASCII id handling | resolved | UTF-8 marshaling fixed in review |

## Recommendation

The package is **code-complete**, statically verified, and proven in a real
licensed editor on 2021.3, 2022.3 and 6.3 (including real Android players and
an Apple-SDK Xcode compile, plus a full iOS Simulator runtime run on 6.3). The
remaining work is **validation, not construction**: a physical-hardware pass
(Phase C) is the only gap left. Ship `0.4.0`
publicly now; do those, swap one screenshot, bump to 1.0.0, and submit to the
Asset Store — the publishing path itself is free (70/30 split, $4.99 min or
free).
