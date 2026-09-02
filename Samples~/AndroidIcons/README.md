# Quick Actions — Android icons

A **working** copy of the custom-icon recipe from the package README's
[Android icons](https://github.com/emindeniz99/unity-quick-actions/blob/main/README.md#android-icons)
section, so you can start from something that builds instead of assembling one
from five written steps. It is the whole thing:

```
QuickActionIcons.androidlib/
  AndroidManifest.xml                        <manifest package="com.yourcompany.quickactionicons"/>
  res/drawable/ic_quickaction_search.xml     white magnifier on an indigo disc
  res/drawable/ic_quickaction_home.xml       white house on an indigo disc
```

`IconType.Search` and `IconType.Home` are two of the 25 catalog entries the
package ships no built-in drawable for. Without art of your own, a shortcut
using either renders as a blank square; with this plug-in in the project, it
renders these.

## Import it

**Package Manager ▸ Samples ▸ Import**, on the package's page. Unity copies the
folder to

```
Assets/Samples/Home-Screen Quick Actions/<version>/Android icons/QuickActionIcons.androidlib/
```

That path is under `Assets/`, which is the only thing that matters: Unity picks
up every `.androidlib` anywhere under `Assets/` when it generates the Gradle
project, so the icons are in your **next Android build** with no further step —
no settings to tick, no menu item, no code. Move or rename the `.androidlib`
folder wherever you like (keep the `.androidlib` suffix); the resource names,
not the path, are what the package looks up.

## Use it

Two ways in, and they differ:

- **Runtime `Add`** — `icon: IconType.Search` is enough. The Android bridge
  resolves it by name at runtime, asking for `ic_quickaction_search` first and
  the package's own `ic_quickaction_builtin_search` only if yours is missing.
  Your file wins because it is asked for first, not because of any merge order.
- **Static shortcuts** (*Project Settings ▸ Quick Actions*) — a baked item
  cannot be resolved at runtime, so `Icon` alone bakes the built-in (or
  nothing). Set **`AndroidDrawable = "ic_quickaction_search"`** on the item to
  bake a reference to *this* drawable. That also makes it a real `@drawable`
  reference, which the resource shrinker follows through a minified release
  build.

## Verify it reached the APK

Build an Android player, then read the resource table back out of it —
this needs no device:

```bash
aapt2 dump resources app.apk | grep ic_quickaction_search
```

A line naming the drawable means the plug-in was merged. Nothing means the
`.androidlib` was not picked up — the near-universal cause is the layout trap
below, which produces a perfectly green build and no warning.

## Two traps this sample already avoids

- **Manifest and `res/` at the ROOT.** This `.androidlib` has no `build.gradle`
  of its own, and Unity generates a *bare* one as a module with that layout.
  The Gradle-module layout, `src/main/res/`, is **silently ignored** here. The
  package's CI measures exactly this on Unity 2022.3 every push: a drawable
  under `src/main/res/` never reaches the APK, one under `res/` does. Add a
  `build.gradle` and the rule inverts — then, and only then, `src/main/` is
  correct.
- **Not `Assets/Plugins/Android/res/`.** Unity removed that path in 2021.2,
  below this package's floor; it fails the build outright now.

## Before you use this on Unity 6

The manifest names its module with the `package` attribute, which AGP 7.x
(exported by Unity 2021.3 and 2022.3) accepts and **AGP 8 — what Unity 6
exports — removed**. On a Unity 6 project, give the module a `build.gradle`
with `namespace "com.yourcompany.quickactionicons"` instead of the attribute —
and remember that a `.androidlib` *with* a `build.gradle` takes the
`src/main/` layout, so `AndroidManifest.xml` and `res/` move under
`src/main/` in the same change. Unity's own `com.unity.mobile.notifications`
ships that second shape.

## Caveats

- **The `.unitypackage` install cannot carry this.** `.androidlib` folders do
  not survive `.unitypackage` export/import, so this sample is a Git/OpenUPM
  (UPM) install feature. If you installed the drag-and-drop
  `QuickActions.unitypackage`, create the folder by hand from the listing above
  — the file contents are the point, and they are plain XML.
- **Colour and geometry are placeholders.** Indigo `#3F51B5` is the built-ins'
  colour, chosen here so a replacement icon sits next to them consistently.
  Swap in your own art; keep the disc, or some other opaque background, because
  API 26+ launchers draw a legacy shortcut drawable onto a white plate where a
  white-on-transparent glyph disappears.
- **VectorDrawable, not PNG.** Density-independent, ~1 KB, crisp on every
  launcher density. A PNG under `res/drawable-xhdpi/` works too and is what you
  will have if you export from a raster tool.
