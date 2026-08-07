# Contributing

Thanks for helping out. This repo is the standalone home of the Unity package
**`com.emindeniz99.quick-actions`** (Home-Screen Quick Actions). `package.json`
is at the repo root, so the repo *is* the UPM package.

Contributions are accepted under the repo's [MIT license](./LICENSE.md).

---

## 1. Set up

```bash
git clone https://github.com/emindeniz99/unity-quick-actions.git
cd unity-quick-actions

tools/setup.sh     # one-time: installs the .NET SDK + a JDK if missing
tools/verify.sh    # must print: VERIFY: PASS
```

`tools/verify.sh` is the gate for every change and **needs no Unity install**.
It runs four checks (see [`.verify/README.md`](./.verify/README.md) for the
rationale):

1. every asset has a committed, stable `.meta`;
2. the C# type-checks against UnityEngine/UnityEditor stubs in **nine** build
   configurations (editor / iOS / Android / native gates / sample), so every
   `#if` branch is compiled;
3. the NUnit suite runs via `dotnet test`;
4. the Android Java plugin compiles against Android SDK stubs and passes its
   stateful smoke test.

Run it before you push. CI runs the same script, so a red local run is a red PR.

---

## 2. The local dev loop (with Unity)

The package is developed by consuming it from a **throwaway Unity project** over
a local `file:` path — never by copying sources into a project.

1. Create a scratch project in Unity Hub (any template, 2021.3 LTS or newer).
2. Point it at your clone. Either **Package Manager ▸ + ▸ Add package from
   disk…** and pick this repo's `package.json`, or edit the scratch project's
   `Packages/manifest.json` by hand:

   ```jsonc
   {
     "dependencies": {
       "com.emindeniz99.quick-actions": "file:../../unity-quick-actions"
     },
     "testables": [
       "com.emindeniz99.quick-actions"
     ]
   }
   ```

   **The `testables` entry is not optional.** Unity only surfaces a package's
   tests in the Test Runner when the consuming project lists that package id in
   `testables`. Without it, `Tests/Editor/` silently never appears and you will
   think the suite is empty.

3. **Add the `QUICKACTIONS_ENABLED` scripting define** (Project Settings ▸
   Player ▸ Other Settings ▸ Scripting Define Symbols), per platform tab you
   build. The package is opt-in: with no define it compiles to nothing. The test
   assembly is `defineConstraint`ed on `UNITY_INCLUDE_TESTS` **and**
   `QUICKACTIONS_ENABLED`, so without the define the tests stay invisible even
   with `testables` set.

4. Import the **Demo** sample from the package page to exercise the API.

Quick actions do not exist in the Editor or on a plain simulator — real
behaviour needs a device long-press. See [`GETTING_STARTED.md`](./GETTING_STARTED.md)
for the full device walkthrough, and `tools/device-smoke/` for the adb-driven
Android smoke test.

---

## 3. Unity rules newcomers get wrong

**`.meta` files are required, and are committed alongside every asset.**
Unity identifies assets by the GUID inside the `.meta`; if the package ships
without them, every consumer's Unity generates fresh GUIDs and scene/prefab and
plugin-platform references break. After adding, renaming, or moving any file:

```bash
python3 tools/gen_meta.py    # idempotent; derives stable GUIDs from the path
git add <asset> <asset>.meta
```

`verify.sh` **fails** if `gen_meta.py` had to create anything — that means a
`.meta` was missing from the commit.

**Folders ending in `~` are invisible to Unity's importer.** `Samples~/`,
`store~/`, `dist~/` are skipped by Unity and by the asset walk in
`gen_meta.py`; the folders themselves get no `.meta`. (The files *inside*
`Samples~/Demo/` do carry `.meta`s — they are copied into `Assets/` on sample
import — and `gen_meta.py` handles that folder explicitly. Don't delete them.)

**The `.unitypackage` is a build output and is never committed.** `dist~/` is
gitignored. Produce one locally with `python3 tools/pack_unitypackage.py` (or
`tools/release.sh` for every release artifact at once); CI attaches it to the
[GitHub Release](https://github.com/emindeniz99/unity-quick-actions/releases).
Never add a `.unitypackage` to a commit or a PR.

**Don't hand-edit generated collateral.** `store~/` PNGs come from
`tools/gen_store_images.py`; regenerate rather than retouch.

---

## 4. Commits

**Conventional Commits, and the scope is mandatory.**

```
<type>(<scope>): <subject>

[optional body — wrap at 72 cols, explain WHY]

[optional footer — BREAKING CHANGE: …, Refs: …]
```

Rules:

- **Never** `feat: …` without a scope.
- Subject in **imperative mood** ("add", not "added"/"adds"), lowercase first
  letter, no trailing period.
- The whole header line (`type(scope): subject`) is **≤ 72 characters**.
- Write a body when the diff does not explain *why* — a constraint, an OS quirk,
  a rejected alternative. Don't restate the diff or paste tool output.
- One commit = one reason. Split unrelated work.

### Scopes — area-based

| Scope     | Covers                                                        |
|-----------|---------------------------------------------------------------|
| `runtime` | `Runtime/` — the public API and managed state                 |
| `editor`  | `Editor/` — settings, simulator window, build post-processors |
| `ios`     | `Plugins/iOS/` and iOS-specific editor/build code             |
| `android` | `Plugins/Android/` and Android-specific editor/build code     |
| `tools`   | `tools/` — gen_meta, packer, release, device smoke            |
| `docs`    | README, GETTING_STARTED, CHANGELOG, plans, this file          |
| `ci`      | `.github/workflows/`                                          |
| `tests`   | `Tests/`, `.verify/` harness and stubs                        |
| `repo`    | repo-wide config: `.gitignore`, license, metadata             |

### Types

`feat`, `fix`, `docs`, `refactor`, `perf`, `test`, `chore`, `build`, `ci`,
`revert`. If none fit, prefer `chore`.

### Breaking changes

Mark with `!` after the scope **and** a footer:

```
feat(runtime)!: drop the legacy string-only Add overload

BREAKING CHANGE: Callers must pass a QuickActionItem. See README.md.
```

### Examples

Good:

```
feat(android): keep foreign shortcuts on a cold-start reconcile
fix(ios): preserve unmarked shortcuts when an id collides
docs(docs): correct the Add-on-cap-trim contract
chore(tools): pin the unitypackage packer to deterministic mtimes
```

Bad:

```
feat: add localization                  # no scope
fix(quick-actions-unity): …             # retired project-name scope
fix(runtime): Fixed the crash.          # capitalized, past tense, period
```

---

## 5. AI-assisted contributions

They are welcome, and they must be **attributed and reviewed by you** before
they are pushed.

- Every AI-assisted commit carries a `Co-Authored-By:` trailer naming **the
  model or agent that actually did the work** — not a fixed placeholder. If a
  different model writes the next commit, the trailer changes.

  ```
  Co-Authored-By: <model that wrote it> <noreply@anthropic.com>
  ```

- The same trailer goes at the end of **AI-drafted PR descriptions** and
  substantive PR comments.
- Do **not** add an emoji "Generated with …" footer.
- Attribution does not transfer responsibility: you still read the diff, and
  `tools/verify.sh` still has to print `VERIFY: PASS`.

---

## 6. Pull requests

- Branch off `main`, keep the PR focused on one topic.
- `tools/verify.sh` must pass locally; state what you tested on a **device** if
  the change touches native code — the static harness cannot reach it.
- Update [`CHANGELOG.md`](./CHANGELOG.md) under the unreleased heading for any
  user-visible change.
- Update `README.md` when you change the public API surface.

### Merging — always a real merge commit

- Merge with the **`merge` method** (`merge_method: "merge"` via the API/CLI,
  `git merge --no-ff` locally). This is the default, every time.
- **Never squash.** Squashing collapses the branch into one commit and destroys
  the per-commit history, which is the record of how this package was built.
  On `main` that loss is irreversible.
- **Never rebase-merge** either — it rewrites SHAs and drops the merge topology.
- The only exception is the repo owner explicitly asking for a squash on one
  specific PR, in that PR.

---

## 7. Releases

Releases are cut by the maintainer — see [`MAINTAINING.md`](./MAINTAINING.md).
Tags are plain semver: `v0.4.0`. Consumers pin a version by appending the tag to
the git URL:

```
https://github.com/emindeniz99/unity-quick-actions.git#v0.4.0
```

---

## 8. Security

Do not open a public issue for a vulnerability — follow
[`SECURITY.md`](./SECURITY.md).
