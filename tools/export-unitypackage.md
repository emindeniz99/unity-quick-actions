# Exporting a classic `.unitypackage`

The package ships in **UPM** layout. To also distribute it the classic way
(a `.unitypackage` that drops into `Assets/`), export it from a Unity project
that has the package installed.

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
