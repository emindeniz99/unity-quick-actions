# Maintaining this package

Notes for maintainers of `com.emindeniz99.quick-actions`. Contributor-facing
rules live in [`CONTRIBUTING.md`](./CONTRIBUTING.md); what is and is not proven
lives in [`PRODUCTION_READINESS.md`](./PRODUCTION_READINESS.md).

## Verification gate

Every release starts from a green static verification:

```bash
tools~/verify.sh        # must end with: VERIFY: PASS
```

`tools~/setup.sh` installs the toolchain once (dotnet SDK + a JDK). `verify.sh`
checks `.meta` completeness, compiles the C# in 10 configurations against the
Unity stubs in `.verify/`, runs the NUnit suite, and compiles plus smoke-tests
the Android Java plugin. Anything short of `VERIFY: PASS` blocks the release —
the failing check is fixed first, not worked around.

## Cutting a release

1. `tools~/verify.sh` passes.
2. `version` in `package.json` is bumped to the new semver value.
3. The matching `CHANGELOG.md` section is renamed from `Unreleased` to that
   version and given a real date. The version in `package.json` and the
   version in the top `CHANGELOG.md` heading must agree — OpenUPM rejects a
   mismatch between the tag and `package.json` with error **E811**, so the bump
   belongs in the commit that gets tagged, never in a follow-up commit.
4. Both changes are committed together.
5. The commit is tagged with a plain semver tag: `git tag v<version>`.
6. The tag is pushed: `git push origin v<version>`.
7. CI (`.github/workflows/ci.yml`) re-runs the verification on the tagged
   commit, then packs `dist~/QuickActions.unitypackage` with
   `tools~/pack_unitypackage.py` and attaches it to the GitHub Release. The
   `.unitypackage` is a build output — `dist~/` is gitignored and the artifact
   is never committed. If the release asset is missing, `tools~/release.sh`
   rebuilds it locally for a manual upload.
8. The release is confirmed at
   <https://github.com/emindeniz99/unity-quick-actions/releases> with the
   `.unitypackage` attached, because the install docs point downloaders there.
   (`v0.4.0`, cut 2026-08-07, is the first one.)

## OpenUPM

OpenUPM distribution is a one-time submission, after which every new tag
publishes automatically. The full procedure — the YAML entry, the empty
`gitTagPrefix` requirement, and the "at least one release before submitting"
rule — is in [`docs~/publishing-to-openupm.md`](./docs~/publishing-to-openupm.md).

## Version policy

The package stays in `0.x` until the on-device checklist below has actually been
walked. A `1.0.0` is a claim that the package was validated on real devices
across the supported Unity range; it is not a milestone to be assigned by
sentiment. Bumping later is cheap.

## What must be true before a 1.0

Quick actions cannot be observed in the Editor, so none of the following can be
replaced by the static harness. The Editor pass is closed on 2021.3, 2022.3 and
6.3; what is still open is the physical-hardware work below — see
`PRODUCTION_READINESS.md` for the record of what has been executed. (Quick
actions *do* work on the iOS Simulator, which is where the 6.3 runtime pass was
done; the 2021.3 line cannot be run there at all, because Unity ships an
x86_64-only simulator runtime on 2021 LTS.)

**Editor pass, per claimed Unity line**

- [x] The package imports into a fresh project on Unity **2021.3 LTS** — the
      declared minimum — and on the lines already validated (2022.3, 6.0, 6.3),
      each with **0 console errors and 0
      warnings** after the `QUICKACTIONS_ENABLED` scripting define is added to
      the Android and iOS tabs of Player ▸ Scripting Define Symbols.
- [x] `Window ▸ Quick Actions ▸ Simulator` and `Window ▸ Quick Actions ▸ About`
      both register.
- [x] With the Demo sample imported: a click in the Simulator with Play Mode
      off starts Play Mode and logs the performed id at startup (the cold path);
      a click during Play Mode delivers immediately (the warm path).

**Android device pass**

- [ ] The Demo sample builds and runs on a physical Android 7.1+ device
      (API 25 is the floor for launcher shortcuts).
- [ ] After the demo's "Add 3 shortcuts" button, a **long-press on the launcher
      icon** shows the three dynamic shortcuts.
- [ ] **Warm tap**: with the app running, tapping a shortcut foregrounds it and
      logs the shortcut id.
- [ ] **Cold tap**: with the app force-closed, tapping a shortcut cold-launches
      it and still logs the id.
- [ ] A **static** shortcut configured in Project Settings ▸ Quick Actions
      exists on first launch, before anything is pressed at runtime.

**iOS device pass** (requires a Mac)

- [ ] The same Demo sample builds for iOS, opens in Xcode, and runs on a
      physical device with a signing team set.
- [ ] The warm-tap and cold-tap checks above hold on iOS.

**Gate pass, per platform** — the headline promise

- [ ] With `QUICKACTIONS_ENABLED` removed and the project rebuilt, the
      generated/merged `AndroidManifest.xml` contains no
      `QuickActionsTrampolineActivity` and a long-press shows no shortcuts from
      this package. (The manifest half is already verified on 2021.3 and 6.3 —
      a define-off player build carries no trace of the trampoline; only the
      long-press half still needs a device.)
- [ ] The generated production Xcode project greps clean for
      `QUICKACTIONS_ENABLED`.

A testbed used for this work is a throwaway project per editor version. Upgrading
one project through several Unity versions tests migration, not fresh installs,
and does not count.

## Known failure modes during a device pass

| Symptom | Cause |
|---|---|
| Console errors on import or platform switch | Most likely the asmdef ↔ extension-DLL seam; the exact message is worth capturing before anything is changed |
| `QuickActions` type not found | The `QUICKACTIONS_ENABLED` define is missing on that platform's tab — it is per-platform |
| No shortcuts on long-press | The demo's "Add 3 shortcuts" was not pressed, or the device is below Android 7.1 |
| Tap opens the app but logs nothing | The delivery path, not the registration path, is at fault; `adb logcat -s QuickActions Unity` around the tap is the evidence to keep |
