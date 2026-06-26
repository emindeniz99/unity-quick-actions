# Quick Actions for iOS & Android (Unity)

Home-screen **quick actions** for Unity games — the shortcuts revealed when a
user long-presses your app icon. A clean-room, MIT-licensed equivalent of the
"Quick Actions for iOS and Android" Asset Store package, targeting **Unity 2022
LTS and newer** (including Unity 6).

| Platform | Mechanism | Min OS |
|----------|-----------|--------|
| iOS | `UIApplicationShortcutItem` (dynamic) | iOS 9 |
| Android | `ShortcutManager` dynamic shortcuts | API 25 (Android 7.1) |

- **Runtime (dynamic) API** — add/remove shortcuts from C#; the OS keeps them across launches.
- **Static shortcuts** — configure shortcuts in **Project Settings ▸ Quick
  Actions**; build post-processors bake them into `Info.plist` (iOS) and
  `shortcuts.xml` (Android) so they exist on first launch.
- **Tap callback** — `Performed` event + `LastPerformed` for cold launches —
  identical for static and dynamic shortcuts.
- **Zero native edits** — the iOS app delegate is hooked at load via the ObjC
  runtime; the Android trampoline activity is merged in from a plugin manifest.
- **Version-proof Android** — a trampoline activity instead of subclassing
  Unity's activity, so it works on both `UnityPlayerActivity` (2022) and
  `UnityPlayerGameActivity` (6+).

## Install

**Drag-and-drop (classic).** Drag `dist/QuickActions.unitypackage` into an open
Unity Editor (or *Assets ▸ Import Package ▸ Custom Package…*). It installs under
`Assets/QuickActions/`. Rebuild it any time with `python3 tools/pack_unitypackage.py`
(works without Unity).

**UPM (modern).** Package Manager ▸ *Add package from disk…* ▸ select this
folder's `package.json`. Or add to `Packages/manifest.json`:

```json
"com.playground.quick-actions": "file:../path/to/projects/quick-actions-unity"
```

More on packaging/export: [`tools/export-unitypackage.md`](./tools/export-unitypackage.md).

Then import the **Demo** sample from the package page to try it on a device.

## Usage

```csharp
using Playground.QuickActions;

void Start()
{
    QuickActions.LoggingEnable = true;            // optional Debug.Log tracing
    QuickActions.Performed += OnShortcut;         // fires on tap (incl. cold launch)

    QuickActions.Add(new QuickActionItem(
        id: "new_game", title: "New Game",
        subtitle: "Start fresh", icon: IconType.Add));

    QuickActions.AddList(new List<QuickActionItem>
    {
        new QuickActionItem("continue", "Continue", "Resume last save", IconType.Play),
        new QuickActionItem("daily",    "Daily Reward", "Claim today",   IconType.Favorite),
    });
}

// Fires on every tap, including the cold launch that started the app.
void OnShortcut(string id) => Route(id);
```

### API

| Member | Purpose |
|--------|---------|
| `bool IsPlatformSupported` | True on a supported device; false in-Editor. |
| `bool LoggingEnable` | Toggle `Debug.Log` tracing. |
| `event Action<string> Performed` | Tapped action id (main thread; includes cold launch). |
| `string LastPerformed` | Id the app was last launched/resumed from, or null. |
| `void ResetLastPerformed()` | Clear `LastPerformed`. |
| `bool Add(QuickActionItem)` | Add one; false if invalid or id already added. |
| `void AddList(IList<QuickActionItem>)` | Add several in one OS update. |
| `List<QuickActionItem> GetAll()` | Snapshot of this session's actions. |
| `QuickActionItem GetById(string)` | Lookup by id. |
| `bool Remove(QuickActionItem)` / `RemoveById(string)` | Remove one. |
| `void RemoveAll()` | Remove every action. |
| `bool IsAdded(QuickActionItem)` / `IsAdded(string)` | Membership test. |

`QuickActionItem`: `Id` (required, unique), `Title` (required), `Subtitle`,
`Icon` (`IconType`, iOS system glyph), `AndroidDrawable` (optional drawable name).

### Static shortcuts (baked into the build)

For shortcuts that must exist on the **first** launch (before any runtime
`Add`), open **Project Settings ▸ Quick Actions**, click *Create settings
asset*, and add entries. At build time:

- **iOS** — written into the Xcode `Info.plist` as `UIApplicationShortcutItems`
  (`UIApplicationShortcutItemType` = your `Id`, plus title/subtitle/system icon).
- **Android** — written to `res/xml/quickactions_shortcuts.xml` (with generated
  string resources), and the `android.app.shortcuts` meta-data is injected into
  the launcher activity. Each static intent targets the trampoline and encodes
  its `Id` in the intent action (XML shortcuts can't carry extras).

Taps are delivered through the same `Performed` / `LastPerformed` path as dynamic
shortcuts. Static and dynamic shortcuts coexist; iOS shows up to four total.

## How it works

- **iOS** — `Plugins/iOS/QuickActions.mm` swizzles `UnityAppController` at
  `+load`: both cold launches (`didFinishLaunchingWithOptions:`) and warm taps
  (an injected `application:performActionForShortcutItem:completionHandler:`)
  enqueue the id. Dynamic shortcut items are set on `UIApplication.shortcutItems`.
- **Android** — `Plugins/Android/QuickActionsBridge.java` builds `ShortcutInfo`s
  whose intents target `QuickActionsTrampolineActivity`. The trampoline records
  the tapped id and brings the Unity activity forward.

Delivery is a **single pull channel**: C# drains the native queue one frame after
startup (cold) and on regained focus (warm) — no `UnitySendMessage`. The C# layer
owns the authoritative list and pushes the full set to the OS on every change;
on first access it **reconciles** that list with the shortcuts the OS already has
(from a previous session), so `GetAll()`/`IsAdded()` are accurate across launches
(icons excepted).

## Limitations / roadmap

See [`ROADMAP.md`](./ROADMAP.md). Notable: per-item rasterized icons from
`Texture2D`, pinned shortcuts, OS-backed `GetAll()`, and automated device CI are
not implemented.

## Verification

The package is type-checked and compiled without Unity via a stub-based harness:

```bash
tools/setup.sh     # install dotnet + JDK (once; pre-baked in the devcontainer)
tools/verify.sh    # .meta gen + C# compile (4 configs) + 15 unit tests + Android plugin
```

`verify.sh` runs the unit tests via `dotnet test`; the same tests (plus
JsonUtility serialization tests) run in Unity's **Test Runner** from
`Tests/Editor/`. See [`.verify/README.md`](./.verify/README.md). A green run
proves everything compiles and the logic tests pass; on-device behaviour is
validated with the procedure in [`plans/mvp.md`](./plans/mvp.md) (iOS/Android
quick actions can't run in the Editor or on Linux).

## Notes / learnings

- Min Unity is 2022.3 LTS. The dynamic native hooks avoid editing generated
  `UnityAppController` / `UnityPlayerActivity`; only **static** shortcuts need
  build post-processors (Info.plist / shortcuts.xml).
- The iOS `.mm` is compiled by Unity against the real SDK; here it's reviewed
  and brace/structure-checked only (no Apple SDK on Linux). The C# and Android
  Java are fully compiled against stubs by `tools/verify.sh`.
