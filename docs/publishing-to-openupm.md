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
git tag v0.4.0
git push origin v0.4.0
```

Each new release = a new semver tag.

Two rules that bite if you get them wrong:

- **The version parsed from the tag must equal `package.json`'s `version`.**
  If the tag says `v0.4.0` but `package.json` still says `0.3.0`, the OpenUPM
  build fails with **E811** (version mismatch). Bump `package.json` in the
  commit you tag, never after.
- **`gitTagPrefix` must be empty** for this layout (see the YAML below).
  OpenUPM strips the prefix from the tag and parses the rest as semver; with a
  `v0.4.0` tag and an empty prefix it parses `v0.4.0` → `0.4.0` correctly. A
  non-empty prefix is only for monorepos that namespace tags per package.

## Submit once

1. Go to <https://openupm.com/packages/add/>, enter the repo URL, and fill the
   form — **it opens the pull request against
   <https://github.com/openupm/openupm> for you**. What it produces (and what
   you'd hand-write if you opened the PR yourself) is a single YAML file whose
   filename must be **exactly the package name**:
   `data/packages/com.emindeniz99.quick-actions.yml`

   ```yaml
   name: com.emindeniz99.quick-actions
   displayName: Home-Screen Quick Actions
   repoUrl: https://github.com/emindeniz99/unity-quick-actions
   gitTagPrefix: ''            # empty — tags are plain "v0.4.0"
   minVersion: 0.4.0
   licenseSpdxId: MIT
   readme: main:README.md      # <branch>:<path>, rendered on the package page
   ```

   There is **no** `parentDir`/`packageFolder` field in OpenUPM's schema — the
   build pipeline locates `package.json` wherever it lives in the repo. Here
   it's the root, so nothing to configure.

2. After the PR merges, OpenUPM's CI picks up each matching tag and publishes it
   to `https://package.openupm.com` within minutes. New tags publish
   automatically thereafter — no further action per release.

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
  [GitHub Release](https://github.com/emindeniz99/unity-quick-actions/releases).
