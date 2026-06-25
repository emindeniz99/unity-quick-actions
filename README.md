# Quick Actions for iOS & Android (Unity)

Home-screen **quick actions** for Unity games — the shortcuts revealed when a
user long-presses your app icon. A clean-room, MIT-licensed equivalent of the
"Quick Actions for iOS and Android" Asset Store package, targeting **Unity 2022
LTS and newer** (including Unity 6).

| Platform | Mechanism | Min OS |
|----------|-----------|--------|
| iOS | `UIApplicationShortcutItem` (dynamic) | iOS 9 |
| Android | `ShortcutManager` dynamic shortcuts | API 25 (Android 7.1) |

- **Runtime API** — add/remove shortcuts from C#; the OS keeps them across launches.
- **Tap callback** — `Performed` event + `LastPerformed` for cold launches.
- **Zero native edits** — the iOS app delegate is hooked at load via the ObjC
  runtime; the Android trampoline activity is merged in from a plugin manifest.
- **Version-proof Android** — a trampoline activity instead of subclassing
  Unity's activity, so it works on both `UnityPlayerActivity` (2022) and
  `UnityPlayerGameActivity` (6+).

## Install

**UPM (recommended).** Package Manager ▸ *Add package from disk…* ▸ select this
folder's `package.json`. Or add to `Packages/manifest.json`:

```json
"com.playground.quick-actions": "file:../path/to/projects/quick-actions-unity"
```

**.unitypackage / classic `Assets/`** — see [`tools/export-unitypackage.md`](./tools/export-unitypackage.md).

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

    // Pull-based cold-launch routing (alternative to the event):
    if (QuickActions.LastPerformed is string id)
    {
        Route(id);
        QuickActions.ResetLastPerformed();
    }
}

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

## How it works

- **iOS** — `Plugins/iOS/QuickActions.mm` swizzles `UnityAppController` at
  `+load`: cold launches are read from `didFinishLaunchingWithOptions:` and
  queued; warm taps arrive via an injected
  `application:performActionForShortcutItem:completionHandler:` that calls
  `UnitySendMessage`. Dynamic shortcut items are set on
  `UIApplication.shortcutItems`.
- **Android** — `Plugins/Android/QuickActionsBridge.java` builds
  `ShortcutInfo`s whose intents target `QuickActionsTrampolineActivity`. The
  trampoline records the tapped id, brings the Unity activity forward, and
  finishes; C# polls the id on startup and on regained focus.

The C# layer owns the authoritative list and pushes the full set to the OS on
every change. On a fresh process the in-memory list starts empty (the OS still
shows the previously-set shortcuts) — re-register on launch if you need
`GetAll()`/`IsAdded()` to be accurate.

## Limitations / roadmap

See [`ROADMAP.md`](./ROADMAP.md). Notable: per-item rasterized icons from
`Texture2D`, pinned shortcuts, and automated device CI are not implemented.

## Notes / learnings

- Min Unity is 2022.3 LTS. The native hooks avoid editing generated
  `UnityAppController` / `UnityPlayerActivity`, which is why no build
  post-processor is needed.
- This container has no Unity Editor, so the package was authored and
  statically checked but not compiled here — see [`plans/mvp.md`](./plans/mvp.md)
  for the device test procedure.
