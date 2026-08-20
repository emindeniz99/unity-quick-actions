#!/usr/bin/env python3
"""Release notes for the version this tree declares — straight from CHANGELOG.md.

The release pipeline does NOT generate notes from commit subjects. This
package's changelog is written by hand as a narrative development log (bold
thesis sentence, then the why), and a machine-generated "* editor: bake
placeholders (#1)" list would be strictly worse than what is already there.
So the release quotes the curated section verbatim instead.

It also enforces the rule MAINTAINING.md states in prose: the version in
`package.json` and the version in the top `CHANGELOG.md` heading must agree,
because OpenUPM rejects a tag/package.json mismatch with error E811.

Usage:
  release_notes.py            print the notes for package.json's version
  release_notes.py --check    validate only; allow a top section still called
                              [Unreleased] (mid-development), which the plain
                              form refuses because it cannot be released
  release_notes.py --version  print the version and exit

Exit codes: 0 ok, 1 inconsistent/missing (message on stderr).
"""
import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
# "## [0.4.6] - 2026-08-20" — the shape every section in this file uses.
HEADING = re.compile(r"^##\s+\[([^\]]+)\](?:\s+-\s+(\S+))?\s*$")


def sections(text):
    """Every '## [x]' heading in file order, as (version, date, body)."""
    found = []
    lines = text.splitlines()
    starts = [i for i, line in enumerate(lines) if HEADING.match(line)]
    for n, start in enumerate(starts):
        end = starts[n + 1] if n + 1 < len(starts) else len(lines)
        version, date = HEADING.match(lines[start]).groups()
        found.append((version, date, "\n".join(lines[start + 1:end]).strip("\n")))
    return found


def main():
    check = "--check" in sys.argv
    version = json.load(open(os.path.join(ROOT, "package.json")))["version"]
    if "--version" in sys.argv:
        print(version)
        return 0

    changelog = os.path.join(ROOT, "CHANGELOG.md")
    found = sections(open(changelog, encoding="utf-8").read())
    if not found:
        print("release_notes: CHANGELOG.md has no '## [version]' section", file=sys.stderr)
        return 1

    top_version, _, _ = found[0]
    # Mid-development the top section may still be [Unreleased]; that is a
    # legal working state to commit, just not one that can be released.
    if check and top_version.lower() == "unreleased":
        print(f"release notes OK (top section is [Unreleased]; package.json is {version})")
        return 0

    if top_version != version:
        print(
            f"release_notes: package.json says {version} but the top CHANGELOG "
            f"section is [{top_version}]. They must agree — OpenUPM rejects the "
            f"mismatch with E811, and the release notes would describe the wrong "
            f"version. Fix whichever is wrong, in the commit that gets tagged.",
            file=sys.stderr,
        )
        return 1

    body = next(b for v, _, b in found if v == version)
    if not body.strip():
        print(f"release_notes: the [{version}] section is empty", file=sys.stderr)
        return 1
    if check:
        print(f"release notes OK ({version}, {len(body.splitlines())} lines)")
        return 0
    print(body)
    return 0


if __name__ == "__main__":
    sys.exit(main())
