# CLAUDE.md — rules for AI agents in this repo

This repo **is** the Unity package `com.emindeniz99.quick-actions`
(`package.json` at the root). Human-facing detail lives in
[`CONTRIBUTING.md`](./CONTRIBUTING.md) — read it when you need the *why*. The
rules below are non-negotiable.

## Before you push — always

```bash
tools~/verify.sh        # must end with: VERIFY: PASS
```

It checks `.meta` completeness, compiles the C# in 10 configs against Unity
stubs, runs the NUnit suite, and compiles + smoke-tests the Android Java plugin.
`tools~/setup.sh` installs the toolchain once. Never report a change as done on a
red or unrun verify; say what failed.

## Unity discipline

- **Every asset needs a committed `.meta`.** After adding/renaming/moving files,
  run `python3 tools~/gen_meta.py` and `git add` the `.meta` next to the asset.
  `verify.sh` fails if it had to generate one.
- **`Samples~/`, `store~/`, `dist~/`** — folders ending in `~` are invisible to
  Unity; the folders themselves get no `.meta`. Files inside `Samples~/Demo/` do
  have `.meta`s (gen_meta handles that folder explicitly) — leave them.
- **Never commit a `.unitypackage`.** It is a build output; `dist~/` is
  gitignored, `tools~/pack_unitypackage.py` produces it, CI attaches it to the
  GitHub Release (first one: `v0.4.0`).
- The package is opt-in behind the `QUICKACTIONS_ENABLED` scripting define, and
  the test assembly is constrained on it plus `UNITY_INCLUDE_TESTS`. A consuming
  project also needs `"testables": ["com.emindeniz99.quick-actions"]` in its
  `Packages/manifest.json` or the tests never show in the Test Runner.
- Quick actions cannot be observed in the Editor, but they **do** work on the
  iOS Simulator — verified on Unity 6.3 / iOS 26.5, where static and
  runtime-added shortcuts appeared on the Simulator home screen and a tap
  cold-launched the app and delivered the id to `Performed`. (Android has no
  simulator equivalent; the 2021.3 line cannot do a Simulator run at all —
  Unity ships an x86_64-only simulator runtime there.) **Physical hardware is
  partly covered**: one Android run (Moto G Play 2024 / Android 14) confirmed
  static shortcuts on a cold install, runtime `Add`, and the static/dynamic
  id-collision rule; a tap arriving as `Performed` on hardware, and any iPhone
  run, are still unverified. Do not claim real-device behaviour you did not run;
  the static harness cannot reach it.

## Commits

Conventional Commits, **scope mandatory and area-based**:

```
<type>(<scope>): <subject>
```

- Scopes: `runtime`, `editor`, `ios`, `android`, `tools`, `docs`, `ci`, `tests`,
  `repo`. The old project-name scope `quick-actions-unity` is retired — do not
  use it.
- Types: `feat`, `fix`, `docs`, `refactor`, `perf`, `test`, `chore`, `build`,
  `ci`, `revert`.
- Imperative mood, lowercase subject, no trailing period, header ≤ 72 chars.
- Body only when the diff does not explain *why*. Don't paste tool output.
- One commit = one reason. Split unrelated work into separate commits.
- Breaking change: `!` after the scope **and** a `BREAKING CHANGE:` footer.

Stage explicitly (`git commit --only -m "…" -- <paths>` or `git add <paths>`).
Never `git add -A`. Never `--no-verify`, never `--amend` a pushed commit, never
force-push, unless the owner asks.

## Attribution

Every commit you write carries a trailer naming **the model that actually wrote
it** — the real model/agent id, never a fixed string copied from this file:

```
Co-Authored-By: <the model that wrote it> <noreply@anthropic.com>
```

The same trailer goes on AI-drafted PR bodies and substantive PR comments. Do
**not** add an emoji "Generated with …" footer. Never post a PR, comment, or
push on the owner's behalf without being asked.

## Merging

Always a **real merge commit**: `merge_method: "merge"` / `git merge --no-ff`.
**Never squash. Never rebase-merge.** The per-commit history is the record of
how this package was built; on `main` collapsing it is irreversible. The only
exception is the owner explicitly asking for a squash on one specific PR. Don't
ask "merge or squash?" — the answer is merge.

## Docs

Prefer relative links between files in this repo (`./GETTING_STARTED.md`) so
they resolve on GitHub *and* in the Unity Package Manager. Install URL is
`https://github.com/emindeniz99/unity-quick-actions.git` — `package.json` is at
the repo root, so the URL carries no subfolder query suffix. Pin a version with
`#v0.4.8`. Tags are plain semver; `v0.4.0` was the first one.

Don't invent status claims. If a doc asserts something you cannot verify, leave
it as it is rather than "improving" it.
