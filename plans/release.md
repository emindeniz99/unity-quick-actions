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
| Unit tests | ✅ full suite passing (`dotnet test` via verify.sh; +2 Unity-only) | `dotnet test` (list logic, validity, equality, icon pin, dispatch, reconcile, ordering, drain, last-performed) |
| Self code-review | ✅ done | 8-angle review; all confirmed bugs fixed |
| Docs | ✅ done | README, CHANGELOG, ROADMAP, plans, per-folder READMEs |
| Store images | ✅ generated | `store~/` at exact required sizes |
| Store checklist | ✅ done | `STORE_CHECKLIST.md` |
| Real-Unity import + tests (2022.3, 6.0, 6.3) | ✅ verified | licensed 2022.3.9f1, 6000.0.79f1, 6000.3.20f1: import 0 errors, Test Runner 35/35 — see [`PRODUCTION_READINESS.md`](../PRODUCTION_READINESS.md) |
| Real Android build (2022.3) | ✅ verified | real dev/prod APKs: trampoline injection + static-shortcuts baker + managed gate |
| Real-Unity pass on **2021.3** | ⚠️ not done | resolves/loads, but the headless `bee_backend` compile hangs in-container (tooling artifact) — never actually compiled |
| iOS `.mm` compiled | ⚠️ review-only | never built by a real Apple SDK — needs macOS/Xcode |
| On-device behaviour | ⛔ not done | needs iOS (macOS/Xcode) + Android hardware |
| Real screenshots | ⛔ mockups | replace `screenshot-1` with device capture |

Legend: ✅ verified · ⚠️ needs a toolchain we don't have here (2021.3 editor, Apple SDK) · ⛔ needs hardware.

## Gap analysis — what stands between here and "submit"

1. **Device validation (the only hard blocker).** Build the Demo to an iOS
   device (Xcode) and an Android device (API 25+); confirm long-press shows the
   shortcuts and taps route on cold + warm starts, for both dynamic and static.
   Everything else is verified statically or in a real editor; this is the one
   thing stubs and headless builds can't cover.
2. **iOS toolchain pass.** The `.mm` has only ever been reviewed, never
   compiled by a real Apple SDK. Open the package on macOS, switch to the iOS
   target, and build — that also confirms the gated Editor asmdefs'
   `precompiledReferences` resolve for iOS.
3. **Unity 2021.3 compile pass.** 2021.3 is the claimed minimum but has never
   actually compiled; the in-container attempt hit a headless-Linux
   `bee_backend` hang, not a package defect. Confirm on a desktop 2021.3.
4. **Real screenshot.** Swap the mockup `screenshot-1` for an on-device shot.
5. **Version.** `0.4.0` is the first public release — honest pre-device. Cut
   `1.0.0` only after the device pass.

## Phased plan

**Phase A — verify (done):** compile + tests + review + images + docs. ✅

**Phase B — real Unity (partly done):**
- ✅ 2022.3.9f1, 6000.0.79f1 and 6000.3.20f1: import with 0 console errors,
  Test Runner 35/35, both menus register, managed gate proven in real player
  builds; 2022.3 additionally produced real dev/prod Android APKs.
- ⬜ 2021.3: open on a desktop editor and confirm a clean compile.
- ⬜ Switch to the **iOS** target on macOS and build (the one target never
  exercised).

**Phase C — device (needs hardware):**
- Android: build Demo, install, long-press, verify cold/warm + static/dynamic.
- iOS: build Demo on macOS/Xcode, same checks. Capture a real screenshot.

**Phase D — submit:**
- Bump to `1.0.0` (package.json + CHANGELOG) if device-validated.
- Build the `.unitypackage` (`tools/release.sh`, or grab it from the
  [GitHub Release](https://github.com/emindeniz99/unity-quick-actions/releases)).
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
licensed editor on 2022.3, 6.0 and 6.3 (including real Android APKs). The
remaining work is **validation, not construction**: the two untested toolchains
(2021.3 and the iOS/Apple SDK) and a device pass (Phase C). Ship `0.4.0`
publicly now; do those, swap one screenshot, bump to 1.0.0, and submit to the
Asset Store — the publishing path itself is free (70/30 split, $4.99 min or
free).
