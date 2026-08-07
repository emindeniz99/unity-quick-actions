# Exporting a classic `.unitypackage`

The package ships in **UPM** layout. To also distribute it the classic way
(a `.unitypackage` that drops into `Assets/`), export it from a Unity project
that has the package installed.

> **Just want the file?** Every release ships one, built exactly this way:
> download `QuickActions.unitypackage` from the
> [Releases page](https://github.com/emindeniz99/unity-quick-actions/releases).
> It is a build output, so it is **not** in the repo tree.

## Option 0 — prebuilt, no Unity needed (recommended)

`tools/pack_unitypackage.py` builds a valid `.unitypackage` directly from the
source files and their `.meta` GUIDs — no Unity install required:

```bash
python3 tools/pack_unitypackage.py   # -> dist~/QuickActions.unitypackage
```

It already applies the include/exclude rules below and remaps content under
`Assets/QuickActions/`. Drag the result into the Editor to install. (Import it
once in a real Editor to confirm before submitting to the store.)

`dist~/` is gitignored — the artifact is never committed. The build is
byte-reproducible (sorted entries, `mtime=0`), so running this on a release tag
reproduces the exact file attached to that release.

## What to include / exclude

Ship only the package itself — the list is **include-only**, so new root files
are excluded by default. **Include exactly:** `Runtime/`, `Editor/`, `Plugins/`,
`Samples~/`, and the four root docs `README.md`, `CHANGELOG.md`, `LICENSE.md`,
`ROADMAP.md`. **Everything else stays out** — `.verify/`, `tools/`, `plans/`,
`store~/`, `dist~/`, `Tests/`, `package.json` (the classic `Assets/` layout
doesn't use a UPM manifest), and all other root docs (`STORE_CHECKLIST.md`,
`GETTING_STARTED.md`, `PRODUCTION_READINESS.md`, `SECURITY.md`,
`RELEASE_RUNBOOK.md`, …). (`tools/pack_unitypackage.py` in Option 0 applies
exactly this split via its INCLUDE_DIRS/INCLUDE_FILES lists.)

## Option A — Unity Editor (GUI)

1. Install the package (Package Manager ▸ *Add package from disk…*).
2. Right-click the package in the Project window ▸ **Export Package…**, or move
   the contents under `Assets/QuickActions/` and use *Assets ▸ Export Package…*.
3. Keep *Include dependencies* on. Save `QuickActions.unitypackage`.

## Option B — batch mode (CI)

With the package copied to `Assets/QuickActions/` inside a throwaway project:

```bash
"$UNITY" -batchmode -quit -projectPath "$PROJECT" \
  -exportPackage Assets/QuickActions QuickActions.unitypackage \
  -logFile -
```

`$UNITY` is the path to a 2021.3 LTS (or newer) Unity executable. The exported
file preserves the `.meta` GUIDs shipped here, so scene/script references stay
intact.

> Note: Options A/B need a Unity install; Option 0 does not (the format is a
> gzip tar of GUID-named entries, which `pack_unitypackage.py` writes directly).
> Option 0 is what CI runs on a release tag, and its output is the
> `.unitypackage` attached to the GitHub Release.
