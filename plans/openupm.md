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
   `data/packages/com.playground.quick-actions.yml`:

   ```yaml
   name: com.playground.quick-actions
   displayName: Quick Actions for iOS & Android
   repoUrl: https://github.com/emindeniz99/playground
   parentDir: projects/quick-actions-unity   # subfolder with package.json
   gitTagPrefix: quick-actions/v              # matches the tag scheme above
   minVersion: 0.1.0
   licenseSpdxId: MIT
   ```

2. After the PR merges, OpenUPM's CI picks up each matching tag and publishes it
   to `https://package.openupm.com` within minutes. New tags publish
   automatically thereafter — no further action per release.

## Notes

- The package name `com.playground.quick-actions` must be globally unique on
  OpenUPM. If it's taken, rename to e.g. `com.emindeniz99.quick-actions` (this
  cascades into the asmdef names and the C# namespace — do it before first tag).
- OpenUPM only serves the package's runtime/editor content; `Samples~/`,
  `Tests/`, and dev tooling are handled normally by UPM (`Samples~` imported
  on demand, `Tests` excluded from consumer builds).
