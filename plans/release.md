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
| C# compiles | ✅ verified | `dotnet` build, 7 configs, 0 warnings |
| Android plugin compiles | ✅ verified | `javac` vs SDK stubs |
| Unit tests | ✅ full suite passing (`dotnet test` via verify.sh; +2 Unity-only) | `dotnet test` (list logic, validity, equality, icon pin, dispatch, reconcile, ordering, drain, last-performed) |
| Self code-review | ✅ done | 8-angle review; all confirmed bugs fixed |
| Docs | ✅ done | README, CHANGELOG, ROADMAP, plans, per-folder READMEs |
| Store images | ✅ generated | `store~/` at exact required sizes |
| Store checklist | ✅ done | `STORE_CHECKLIST.md` |
| iOS `.mm` compiled | ⚠️ review-only | no Apple SDK on Linux — compiles in Unity on macOS |
| Real-Unity import/compile | ⚠️ blocked | editor installed (`/opt/unity`) but license-gated |
| On-device behaviour | ⛔ not done | needs iOS (macOS/Xcode) + Android hardware |
| Real screenshots | ⛔ mockups | replace `screenshot-1` with device capture |

Legend: ✅ verified here · ⚠️ needs a licensed/real Unity · ⛔ needs hardware.

## Gap analysis — what stands between here and "submit"

1. **Device validation (the only hard blocker).** Build the Demo to an iOS
   device (Xcode) and an Android device (API 25+); confirm long-press shows the
   shortcuts and taps route on cold + warm starts, for both dynamic and static.
   Everything else is verified statically; this is the one thing stubs can't cover.
2. **Real-Unity compile pass.** Open the package in a licensed 2022.3 LTS and a
   Unity 6 editor; confirm zero console errors, and that switching to iOS/Android
   targets resolves the editor-extension DLL references (asmdef
   `precompiledReferences`). High confidence it's correct; needs confirmation.
3. **Real screenshot.** Swap the mockup `screenshot-1` for an on-device shot.
4. **Version decision.** Ship as `0.1.0` (honest pre-device) or cut `1.0.0`
   after device validation. Recommend: validate on device → bump to `1.0.0` →
   submit.

## Phased plan

**Phase A — verify (here, done):** compile + tests + review + images + docs. ✅

**Phase B — real Unity (needs your license, ~1–2 h):**
- Activate Unity Personal (free) — see `tools/install-unity-linux.md` §3.
- Open package in 2022.3 LTS and Unity 6; fix any console issue.
- Switch to iOS and Android targets; confirm compile.
- Run the Test Runner (EditMode) — the same tests + `SerializationTests`.

**Phase C — device (needs hardware):**
- Android: build Demo, install, long-press, verify cold/warm + static/dynamic.
- iOS: build Demo on macOS/Xcode, same checks. Capture a real screenshot.

**Phase D — submit:**
- Bump to `1.0.0` (package.json + CHANGELOG) if device-validated.
- Export clean `.unitypackage` (exclude `.verify/ tools/ plans/ store~/`).
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

The package is **code-complete and statically verified**. The remaining work is
**validation, not construction**: a licensed editor (Phase B) and a device pass
(Phase C). Do those, swap one screenshot, bump to 1.0.0, and submit — the
publishing path itself is free (70/30 split, $4.99 min or free).
