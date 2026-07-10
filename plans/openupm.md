# Publishing to OpenUPM

OpenUPM is a free, public registry for open-source UPM packages. End users then
install via a scoped registry (see README ▸ Install ▸ OpenUPM). OpenUPM does not
host files — it builds packages straight from our public Git tags. Steps:

## Prerequisites

1. **Repo is public** on GitHub (`github.com/emindeniz99/playground`). This alone
   already enables the **Git URL** install for everyone — OpenUPM is optional on
   top of that.
2. **MIT license file** present (`LICENSE.md`) — done.
3. **`package.json` complete** — `name`, `version`, `displayName`, `description`,
   `unity`, `repository`, `license` are required by OpenUPM; we also set
   `author`, `keywords`, `documentationUrl`, `changelogUrl`, `licensesUrl`.

## Versioning (monorepo subfolder)

OpenUPM builds from Git tags. Because the package lives in a subfolder, use a
**tag prefix** so its tags don't collide with other projects in the monorepo:

```bash
git tag quick-actions/v0.1.0   # tag prefix = "quick-actions/v"
git push origin quick-actions/v0.1.0
```

Each new release = a new prefixed semver tag. (For a single-package repo you'd
just use `v0.1.0`.)

## Submit once

1. Go to <https://openupm.com/packages/add/> and enter the repo URL, **or** open a
   PR against <https://github.com/openupm/openupm> adding
   `data/packages/com.emindeniz99.quick-actions.yml`:

   ```yaml
   name: com.emindeniz99.quick-actions
   displayName: Home-Screen Quick Actions
   repoUrl: https://github.com/emindeniz99/playground
   gitTagPrefix: quick-actions/v              # matches the tag scheme above
   minVersion: 0.1.0
   licenseSpdxId: MIT
   ```

   OpenUPM **auto-detects** the package's subfolder from where `package.json`
   lives — there is no `parentDir`/`packageFolder` field; the `gitTagPrefix` is
   what distinguishes this package's tags from other projects in the monorepo.

2. After the PR merges, OpenUPM's CI picks up each matching tag and publishes it
   to `https://package.openupm.com` within minutes. New tags publish
   automatically thereafter — no further action per release.

## Notes

- The package name `com.emindeniz99.quick-actions` is author-scoped so it's
  globally unique on OpenUPM. It is independent of the C# namespace and asmdef
  names (`EminDeniz99.QuickActions`), which stay as-is — the UPM name and the code
  namespace don't have to match. (The Android Java package
  `com.emindeniz99.quickactions` is also independent and unchanged.)
- OpenUPM only serves the package's runtime/editor content; `Samples~/`,
  `Tests/`, and dev tooling are handled normally by UPM (`Samples~` imported
  on demand, `Tests` excluded from consumer builds).
