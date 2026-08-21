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

**Releases are automatic.** You decide the version and write the changelog;
merging is the whole ceremony. There is no tag to push and no artifact to
upload by hand.

1. In the release PR, bump `version` in `package.json` **and** rename the top
   `CHANGELOG.md` section from `Unreleased` to that version with a real date.
   The two must agree: `tools~/verify.sh` check 6 fails the PR when they do
   not, because OpenUPM rejects a tag/`package.json` mismatch with error
   **E811** and the release notes are quoted from that section — so the bump
   belongs in the commit that gets tagged, never in a follow-up commit.
2. Merge the PR with a real merge commit (never squash — see below).
3. [`.github/workflows/release.yml`](./.github/workflows/release.yml) takes it
   from there: it notices that main declares a version with no tag, runs
   `verify.sh`, packs `dist~/QuickActions.unitypackage`, creates the tag
   `v<version>` at the merge commit and publishes the GitHub Release with your
   changelog section as its notes. A merge that does not bump the version is a
   no-op, so the workflow can watch every push to main harmlessly.
4. Confirm at <https://github.com/emindeniz99/unity-quick-actions/releases>
   that the `.unitypackage` is attached, because the install docs point
   downloaders there. (`v0.4.0`, cut 2026-08-07, is the first one.) The
   `.unitypackage` is a build output — `dist~/` is gitignored and the artifact
   is never committed.

**Manual fallback**, still fully supported: push a tag by hand
(`git tag v<version> && git push origin v<version>`) and the tag-triggered
`release` job in `ci.yml` does the same packaging. `tools~/release.sh`
rebuilds the artifact locally if you ever need to upload one yourself.

**Why not Release Please / semantic-release.** Both own `CHANGELOG.md`, and
this one is written by hand on purpose — a narrative log with a bold thesis
per entry, which is why the release notes can simply quote it. Release Please
offers no opt-out for changelog generation, so adopting it means trading that
for `* editor: bake placeholders (#1)`. It also cannot fire our packaging step:
anything it does with the repo's own `GITHUB_TOKEN` is invisible to other
workflows, so its tag would publish a release with no `.unitypackage` unless
we added a long-lived PAT to a public repo. `release.yml` avoids both by doing
verify → pack → tag → publish inside one run. What remains of their value —
inferring the version from `feat:`/`fix:` prefixes — is a judgement this
package makes deliberately, not one to hand to a parser.

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

- [x] The Demo sample builds and runs on a physical Android 7.1+ device
      (API 25 is the floor for launcher shortcuts). *(Moto G Play 2024 /
      Android 14, 2026-08-07.)*
- [x] After the demo's "Add 3 shortcuts" button, a **long-press on the launcher
      icon** shows the three dynamic shortcuts. *(Same run; the id-collision
      rule was observed too — see `PRODUCTION_READINESS.md`.)*
- [ ] **Warm tap**: with the app running, tapping a shortcut foregrounds it and
      logs the shortcut id.
- [ ] **Cold tap**: with the app force-closed, tapping a shortcut cold-launches
      it and still logs the id.
- [x] A **static** shortcut configured in Project Settings ▸ Quick Actions
      exists on first launch, before anything is pressed at runtime. *(Same
      run, on a cold, never-opened install.)*

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
