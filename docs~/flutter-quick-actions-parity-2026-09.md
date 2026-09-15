# Parity with Flutter's `quick_actions` — comparison record (September 2026)

*Maintainer document, not shipped with the package. Written 2026-09-15 from
the Flutter plugins' own sources (`flutter/packages` `main`), pub.dev, the
open `p: quick_actions` issues on `flutter/flutter`, and this package's
source. Method: four reading passes over Flutter (front end + platform
interface, `quick_actions_ios`, `quick_actions_android`, ecosystem/issues),
four comparison passes (API, iOS, Android, developer experience), then an
adversarial verification of every claimed gap against our own source — 22
verified verdicts, of which 5 were refuted outright. Labels: **[verified]**
read from the cited source; **[plausible]** inferred; **[unverified]** not
checked.*

Versions read: `quick_actions` 1.1.1, `quick_actions_platform_interface`
1.1.0, `quick_actions_ios` 1.2.5, `quick_actions_android` 1.0.33.

## 1. What Flutter's plugin actually is **[verified]**

The whole app-facing API is three methods and one four-field data class:

```dart
class QuickActions {
  Future<void> initialize(QuickActionHandler handler);   // call once
  Future<void> setShortcutItems(List<ShortcutItem> items); // replace the whole set
  Future<void> clearShortcutItems();
}
class ShortcutItem { final String type, localizedTitle; final String? localizedSubtitle, icon; }
typedef QuickActionHandler = void Function(String type);
```

There is no read-back, no per-item update or remove, no payload, no pinning,
no usage reporting, no count accessor. `icon` is one free-form string naming a
native resource (an xcassets entry on iOS, a `drawable`/`mipmap` name on
Android) — not a Flutter asset.
<https://raw.githubusercontent.com/flutter/packages/main/packages/quick_actions/quick_actions_platform_interface/lib/types/shortcut_item.dart>

- **iOS** (`QuickActionsPlugin.swift`): icons are
  `UIApplicationShortcutIcon(templateImageName:)` **only** — no
  `systemImageName`, no `UIApplicationShortcutIconType`; `userInfo` is
  hardcoded `nil`; `UIApplicationShortcutItems` in `Info.plist` is never
  written or read, so static shortcuts do not exist for the plugin. UIScene
  **is** supported (1.2.4, "Adds support for UIScene lifecycle") — but
  through Flutter's own registrar fan-out (`addApplicationDelegate` +
  `addSceneDelegate` off `FlutterAppDelegate` / `FlutterSceneDelegate`), never
  by swizzling. Minimum iOS 13.
- **Android** (`QuickActions.java`): `ShortcutManagerCompat.setDynamicShortcuts`
  (whole-list replace, never `addDynamicShortcuts`) and
  `removeAllDynamicShortcuts`. No static, no pinned, no
  `getMaxShortcutCountPerActivity`, no adaptive or bitmap icons
  (`Resources.getIdentifier(icon, "drawable", …)` falling back to `mipmap`, an
  unresolved name silently means no icon). No trampoline: the shortcut's
  intent is `getLaunchIntentForPackage` + `ACTION_RUN` + an extra. `minSdk 24`
  but a silent no-op below API 25 — `setShortcutItems` still reports success.
  `reportShortcutUsed` **is** called automatically inside `getLaunchAction()`
  and `onNewIntent`, though never exposed to Dart.
- **Open on their tracker** (12 issues labelled `p: quick_actions`): bitmap /
  custom icons (#60506, open since 2020, `customer: google`), SF Symbols
  (#170520, 2025), a `getMaxShortcutCount()` accessor (#134351, 2023), update
  / disable / manage existing shortcuts (#149413, 2024, which also notes
  `clearShortcutItems()` does not remove a shortcut the user pinned), static
  manifest shortcuts not forwarded to Dart without an undocumented extra
  (#180468, 2026), plus live bugs (double navigation on relaunch #131121,
  iOS callback only while foregrounded #130243, `initialize()` throwing with
  no attached Activity #190348).

**Four of their five top feature requests are things this package already
ships.** That is the headline of this comparison.

## 2. Where we are ahead **[verified against both sources]**

| capability | Flutter | us |
|---|---|---|
| Coexistence with other shortcut publishers | `setDynamicShortcuts` / `removeAllDynamicShortcuts` wipe **everyone's** dynamic shortcuts; iOS `shortcutItems` is overwritten wholesale | ownership marker + merge on both platforms (`isOurShortcut` / `QAIsOurShortcut`), host and other-plugin items survive; Java smoke tests pin the branches |
| Read-back and per-item edits | none — replace-all is the only mutator | `GetAll`, `GetById`, `IsAdded`, `Add`, `AddList`, `Update`, `Remove`, `RemoveById`, `RemoveAll`, OS-reconciled |
| Payload | no `userInfo` at any layer | `QuickActionItem.Payload` (iOS `userInfo`, Android extras), survives OS read-back |
| Icons, iOS | `templateImageName` only | 29-value `IconType` → `UIApplicationShortcutIconType`, plus `IosSystemImage` (SF Symbols) and `IosTemplateImage` |
| Icons, Android | drawable name only, silent miss | `IconType` with four built-in generated vector sets (incl. adaptive `-v26`), `AndroidDrawable`, `AndroidBitmapFile` + `AndroidBitmapAdaptive` |
| Static shortcuts | none; manifest shortcuts are not even forwarded (#180468) | baked into `Info.plist` and `shortcuts.xml` from a settings asset at build time, and delivered like any other |
| Pinned shortcuts | none | `IsPinSupported`, `RequestPin(id)` |
| Count cap | not exposed (#134351) | `MaxShortcutCount`, shared-cap aware |
| Localization | one pre-localized string per field | `LocalizedTitles` / `LocalizedSubtitles` lists + a `Locale` setter, resolved per platform |
| Below the OS minimum | reports success and does nothing (API < 25) | `IsPlatformSupported` false, adds refused and reported |
| Editor tooling | none (it is a Dart package) | settings page with validation, simulator preview, editor tap simulation, built-in icon generation, define-off stripper |
| Evidence | unit + integration tests | 122 C# tests, 11-config compile, emulator smoke on three Unity lines incl. a real launcher tap, SpringBoard XCUITest on two iOS versions, coexistence mock-host probe |

Where they are ahead: field maturity (207k downloads/month, years of bug
reports), a federated architecture that gets UIScene support for free from the
engine, a hosted API reference on pub.dev, and a one-command install with no
scripting define.

## 3. Verified gaps, in order

### 3.1 `reportShortcutUsed` is never reported on a tap — **implement** **[verified]**

Flutter calls `ShortcutManagerCompat.reportShortcutUsed` automatically in
`getLaunchAction()` and in `onNewIntent`. We expose `QuickActions.ReportUsed(id)`
as public API but never call it ourselves, so a game that does not call it by
hand generates **zero** ranking signal for launcher and assistant shortcut
prediction — with no error, no log, nothing to notice. Fix is Java-side, in
`QuickActionsTrampolineActivity.handleIntent`, right after `isKnownShortcut`
passes and next to `recordPerformed`: report the id inside a try/catch
(`reportShortcutUsed` throws `IllegalStateException` while the user is
locked). That placement reuses the ownership check, covers **static**
shortcuts (which the C# `ReportUsed` gate structurally cannot reach), and adds
no public API.

### 3.2 No one-call "make the set exactly this" — **implement (small)** **[verified]**

`AddList` merges and can never remove an id; `RemoveAll()` returns `void`.
"Replace the installed set" therefore costs `RemoveAll()` + `AddList(…)` with
the package's set empty in between — and if the second call is refused, the
game is left with nothing instead of the old set. Flutter's *only* mutator is
exactly this operation. A `bool SetList(IList<QuickActionItem>)` that diffs
against the current set and pushes once closes it. (Two arguments made for
this in the first pass were wrong and are dropped: removes are **not**
rate-limited on Android, and `RemoveAll` failure *is* observable — it keeps
`_items` and logs.)

### 3.3 `AddList` / `RemoveAll` return `void` — **consider** **[verified]**

`Add` returns `bool`, the batch forms do not, so a rate-limited or
cap-refused batch is silent unless `LoggingEnable` is on (default off). Not a
Flutter gap — Flutter swallows the same refusal — but an internal asymmetry
our own AGENTS.md already concedes. If taken, decide the contract
deliberately (what does `true` mean for a batch whose items were all skipped
as duplicates?) and move `RemoveAll` in the same commit.

### 3.4 R8 keep rule is silent when forgotten — **consider** **[verified]**

Our JNI-only bridge needs a hand-written keep rule; Flutter's plugin classes
need none because they are referenced from Java. Documented in two places and
under CI, but a team that turns on Minify for a store build gets shortcuts
that silently stop appearing in release only. The editor post-processor
already implements `IPreprocessBuildWithReport`: on an Android build with the
define on, if minify is enabled and no matching rule is found, log a build
warning naming the rule and the README anchor.

### 3.5 iOS scene hooks could install earlier — **consider** **[verified]**

Both routes we have (configuration wrapper, notification fallback) are
exercised by CI, and the one uncovered case needs a host that overrides
`application:configurationForConnectingSceneSession:options:` *and* swallows
super — no shipping vendor SDK found doing that. A third, earlier route would
remove the hole entirely: read `UIApplicationSceneManifest` →
`UISceneDelegateClassName` from `Info.plist` in `+load` and, if the named
class descends from `UnityScene`, install the hooks then — before any scene
connects. Static plist read, no ordering window, same ownership rule.

### 3.6 The iOS marshalling layer is unasserted — **consider** **[verified]**

The lifecycle paths are well covered by the coex probe; what nothing asserts
is `QABuildShortcutsJson` / `QAIsOurShortcut` — the write and merge path every
`Add`/`Update`/`GetAll` funnels through, and the one whose regression would
wipe a host app's live `shortcutItems`. Cheap fix: three or four more
`QACoexCheck`s in `Examples~/Coexistence/iOS/QACoexProbe.mm` rather than a new
XCTest target (which would need macOS, so `verify.sh` on Linux would gain
nothing).

### 3.7 Documentation and first-run — **do now / consider** **[verified]**

- Add the full 29-row `IconType` table to the README (enum name → iOS glyph →
  `ic_quickaction_<snake_case>` drawable, flagging the four that ship built
  in). It is the only concrete thing a reader cannot get without opening
  source. **Do now.**
- A hosted API reference is *not* worth the drift risk for a solo package;
  the README table plus IntelliSense is the Unity norm. **Reject.**
- The `QUICKACTIONS_ENABLED` define remains the #1 first-run gotcha: with it
  off, the shipped Demo presents as broken, not off. Documented in four
  places and still the #1 gotcha, so more prose will not move it. Consider one
  `InitializeOnLoad` console line from the ungated bootstrap assembly, once
  per project (`EditorPrefs`-keyed, silent in batch mode). **Consider.**

## 4. Claims that did not survive verification

- "Our cold-launch id is lost if the listener subscribes late" — refuted:
  `LastPerformed` is sticky and the queue is drained after bootstrap.
- "Flutter covers the app-delegate/UIScene split better" — refuted: different
  architecture (engine registrar vs. runtime discovery), not more coverage;
  Unity has no registrar to fan out from.
- "Their example apps beat our samples" — refuted: two importable samples,
  three testbeds, and an editor simulator preview.
- "0.x means no stability promise while Flutter is post-1.0" — mostly
  refuted: their platform packages break too; our CHANGELOG is stricter.
- "Native code has no unit tests" — refuted for Android (a Java smoke suite
  exists) and overstated for iOS (see 3.6 for the sliver that is real).

## 5. Caveats

- Flutter's sources were read at `main` on 2026-09-15; their published
  versions may lag what was read.
- The comparison is capability-for-capability. It does not weigh field
  maturity, which is where they are unambiguously ahead.
- Nothing in §3 is implemented. Each item is sized, not built.
