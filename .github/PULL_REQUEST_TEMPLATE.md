<!--
Conventional Commits with a MANDATORY area scope:
  <type>(runtime|editor|ios|android|tools|docs|ci|tests|repo): <subject>
See CONTRIBUTING.md.
-->

## What and why

<!-- What changes, and why the diff alone doesn't say it (OS quirk, constraint,
     rejected alternative). Link the issue if there is one. -->

## Verification

- [ ] `tools~/verify.sh` prints **`VERIFY: PASS`**
- [ ] `.meta` files committed for every added/renamed/moved asset
      (`python3 tools~/gen_meta.py`, then `git add` them)
- [ ] No `.unitypackage` in this PR (it is a build output; `dist~/` is ignored)
- [ ] `CHANGELOG.md` updated for user-visible changes
- [ ] `README.md` updated if the public API surface changed

**Device testing** (the static harness cannot reach native behaviour — say what
you actually ran, or "not tested on device"):

<!-- e.g. Pixel 7 / Android 14: long-press shows both shortcuts, tap delivers. -->

## Notes for the reviewer

<!-- Anything risky, deliberately out of scope, or worth a second opinion. -->

---

<!--
Merging: this repo always uses a REAL MERGE COMMIT. Never squash, never
rebase-merge — the per-commit history is the record of how the package was built.

If this PR was drafted with AI assistance, end the description with a
Co-Authored-By: trailer naming the model that actually wrote it. No emoji
"Generated with" footer.
-->
