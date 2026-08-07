# Publishing to OpenUPM

OpenUPM is a free, public registry for open-source UPM packages. End users then
install via a scoped registry (see [README](../README.md) ▸ Install ▸ OpenUPM).
OpenUPM does not host files — it builds packages straight from our public Git
tags.

## Prerequisites

1. **Repo is public** on GitHub (`github.com/emindeniz99/unity-quick-actions`).
   This alone already enables the **Git URL** install for everyone — OpenUPM is
   optional on top of that.
2. **MIT license file** present (`LICENSE.md`) — done.
3. **`package.json` complete** — `name`, `version`, `displayName`, `description`,
   `unity`, `repository`, `license` are required by OpenUPM; we also set
   `author`, `keywords`, `documentationUrl`, `changelogUrl`, `licensesUrl`.
   It sits at the **repo root**, which is exactly the layout OpenUPM's build
   pipeline is happiest with.

## Versioning (single-package repo, package.json at the root)

OpenUPM builds from Git tags. This repo ships exactly one package, so the tags
are plain semver with the conventional `v` prefix:

```bash
git tag v0.4.1
git push origin v0.4.1
```

Each new release = a new semver tag.

Two rules that bite if you get them wrong:

- **The version parsed from the tag must equal `package.json`'s `version`.**
  If the tag says `v0.4.0` but `package.json` still says `0.3.0`, the OpenUPM
  build fails with **E811** (version mismatch). Bump `package.json` in the
  commit you tag, never after.
- **`gitTagPrefix` must be empty** for this layout (see the YAML below). It is a
  literal `startsWith` filter over raw tag names — it is **not** stripped before
  the tag is parsed as semver. It exists for monorepos that namespace tags per
  package, or repos with competing tag families. This repo has one package and
  one family, so leave it `''`. Setting `'v'` would happen to work today and
  silently drop any future tag that doesn't start with a lowercase `v`.
  Likewise leave `gitTagIgnore` empty — the example value `'^v'` you'll see
  around would exclude `v0.4.0`, the only tag there is.

## Submit once

1. Go to <https://openupm.com/packages/add/>, enter the repo URL, and fill the
   form. It does **not** open the pull request for you: "Submit metadata" hands
   you GitHub's *create new file* page under `data/packages/`, prefilled with
   the filename, the generated YAML and a commit message — committing it is what
   opens the PR. First-time contributors are prompted to fork `openupm/openupm`
   first. Keep the default commit message
   (`Create com.emindeniz99.quick-actions.yml`); that exact title is one of the
   forms that lets Mergify auto-merge. Add nothing else to the PR — auto-merge
   requires ≥1 file added and **0 modified, 0 removed**.

   The filename must be **exactly the package name**, with a `.yml` extension:
   `data/packages/com.emindeniz99.quick-actions.yml`

   ```yaml
   name: com.emindeniz99.quick-actions
   aliases: []
   displayName: Home-Screen Quick Actions
   description: >-
     Home-screen quick actions for Unity on iOS and Android: add and remove
     app-icon shortcuts at runtime, with a tap callback. No native edits.
   repoUrl: 'https://github.com/emindeniz99/unity-quick-actions'
   trackingMode: git
   parentRepoUrl: null
   licenseSpdxId: MIT
   licenseName: MIT License
   topics:
     - mobile
     - integration
   hunter: emindeniz99
   gitTagPrefix: ''
   gitTagIgnore: ''
   minVersion: ''
   image: >-
     https://raw.githubusercontent.com/emindeniz99/unity-quick-actions/main/store~/cover.png
   readme: 'main:README.md'
   createdAt: 1786105762845
   ```

   Three of those are easy to get wrong and are worth stating plainly:

   - **`trackingMode: git`**, never `githubRelease`. With `githubRelease`,
     OpenUPM looks for a `.tgz`/`.tar.gz` asset on each Release; ours carries a
     `.unitypackage`, so every build would fail on a missing asset. With `git`
     it clones the tag and runs `npm pack` itself, which is what we want.
   - **`licenseName` is required and must be the canonical SPDX name** for
     `licenseSpdxId` — `MIT` pairs with `MIT License`. A mismatch or an empty
     string fails the Data validation check.
   - **`createdAt` is epoch milliseconds, as a number.** The web form fills it;
     if you hand-write the file, regenerate it with `node -p "Date.now()"`.

   There is **no** `parentDir`/`packageFolder` field in OpenUPM's schema — the
   build pipeline locates `package.json` wherever it lives in the repo. Here
   it's the root, so nothing to configure. Unknown keys are silently stripped,
   so a stray field looks like it worked and does nothing.

2. As a first-time contributor to that repo, the *Data validation* check sits as
   `action_required` until a moderator approves the run; OpenUPM targets 24
   hours. Re-pushing does not speed it up. After it goes green Mergify merges,
   and the build pipeline publishes to `https://package.openupm.com` within
   roughly 15–30 minutes. New tags publish automatically thereafter — no further
   action per release.

3. **Don't submit early.** OpenUPM expects the package to have **at least one
   release within 3 months** of submission. Tag `v0.4.0` first, then submit.

## Notes

- The package name `com.emindeniz99.quick-actions` is author-scoped so it's
  globally unique on OpenUPM. It is independent of the C# namespace and asmdef
  names (`EminDeniz99.QuickActions`), which stay as-is — the UPM name and the code
  namespace don't have to match. (The Android Java package
  `com.emindeniz99.quickactions` is also independent and unchanged.)
- OpenUPM only serves the package's runtime/editor content; `Samples~/`,
  `Tests/`, and dev tooling are handled normally by UPM (`Samples~` imported
  on demand, `Tests` excluded from consumer builds).
- The `.unitypackage` is **not** part of the OpenUPM path at all — it's a build
  output for the Asset Store / manual-import audience, attached to the
  [GitHub Release](https://github.com/emindeniz99/unity-quick-actions/releases)
  (or built locally with `python3 tools/pack_unitypackage.py`).
