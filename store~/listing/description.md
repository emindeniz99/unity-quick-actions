# Home-Screen Quick Actions (iOS & Android)

**Add the long-press app-icon shortcuts players already expect — in minutes,
from one C# API.**

When a user presses and holds your app icon on the home screen, iOS and Android
show a little menu of shortcuts ("New Game", "Daily Reward", "Continue"…).
Quick Actions brings that native feature to Unity with a single, clean API — no
Objective-C, no Java, no editing the generated Xcode/Gradle projects.

## Why you'll like it

• **One API, both platforms** — the same C# call drives iOS
  `UIApplicationShortcutItem` and Android `ShortcutManager`.
• **Dynamic and static** — create shortcuts at runtime, or configure them in
  Project Settings and bake them into the build so they exist on first launch.
• **Tap callback that just works** — a `Performed` event fires with your action
  id on every tap, including the cold launch that started the app.
• **Zero native edits** — the iOS app delegate is hooked automatically; Android
  uses a lightweight trampoline activity merged in for you.
• **Future-proof** — works on Unity 2021 LTS through Unity 6, surviving the
  `UnityPlayerActivity` → `UnityPlayerGameActivity` change on Android.
• **Full source included** — readable C#, Objective-C++ and Java. No black-box
  DLLs. Unit-tested and documented.

## One switch to turn it on

The package is **opt-in by design**: add the `QUICKACTIONS_ENABLED` scripting
define (Project Settings ▸ Player ▸ Scripting Define Symbols) and everything
activates. Without the define the package stays **completely out of your build** —
that's a feature: keep it in your dev build profile only, and your production
builds are guaranteed to contain zero of its code. The included README covers
the one-minute setup.

## Dead-simple usage

```csharp
using EminDeniz99.QuickActions;

void Awake()
{
    // Fires on every tap, including the cold launch that opened the app.
    QuickActions.Performed += id => Route(id);
}

void Start()
{
    QuickActions.Add(new QuickActionItem(
        id: "new_game", title: "New Game",
        subtitle: "Start fresh", icon: IconType.Add));
}
```

Need shortcuts before the first launch? Add them in **Project Settings ▸ Quick
Actions** and they're baked into the build automatically.

## What's included

• Runtime API (add / remove / list / query shortcuts + tap events)
• Static shortcut configuration (Project Settings) with iOS & Android build
  post-processors
• Native plugins with full source (iOS `.mm`, Android `.java`)
• A ready-to-run demo scene + an in-Editor tap Simulator (test without a device)
• A complete README (the full unit-test suite lives in the open-source repo)

## Compatibility

• Unity **2021.3 LTS or newer**, including Unity 6
• **iOS 9+** and **Android 7.1+ (API 25)**
• Mono and IL2CPP; Built-in, URP and HDRP (no rendering involved)
• iOS shortcuts require building on macOS with Xcode (standard for any iOS Unity
  project)

## Support

Questions or feature requests are welcome — see the included README and ROADMAP.
The package ships full source, so it's easy to extend.
