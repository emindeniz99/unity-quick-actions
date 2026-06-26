# Exporting a classic `.unitypackage`

The package ships in **UPM** layout. To also distribute it the classic way
(a `.unitypackage` that drops into `Assets/`), export it from a Unity project
that has the package installed.

## Option 0 — prebuilt, no Unity needed (recommended)

`tools/pack_unitypackage.py` builds a valid `.unitypackage` directly from the
source files and their `.meta` GUIDs — no Unity install required:

```bash
python3 tools/pack_unitypackage.py   # -> dist/QuickActions.unitypackage
```

It already applies the include/exclude rules below and remaps content under
`Assets/QuickActions/`. Drag the result into the Editor to install. (Import it
once in a real Editor to confirm before submitting to the store.)

## What to include / exclude

Ship only the package itself. **Include:** `Runtime/`, `Editor/`, `Plugins/`,
`Samples~/`, `package.json`, `README.md`, `CHANGELOG.md`, `LICENSE.md`,
`ROADMAP.md`. **Exclude** the dev/publishing collateral: `.verify/`, `tools/`,
`plans/`, `store/`, `STORE_CHECKLIST.md` (`.verify/` is already hidden from Unity;
the others must be left out of the exported selection).

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

`$UNITY` is the path to a 2022.3 LTS (or newer) Unity executable. The exported
file preserves the `.meta` GUIDs shipped here, so scene/script references stay
intact.

> Note: `.unitypackage` is a binary archive and cannot be generated without a
> Unity install (none is present in this repo's CI container). This is a
> documented manual/CI step, tracked in `ROADMAP.md`.
