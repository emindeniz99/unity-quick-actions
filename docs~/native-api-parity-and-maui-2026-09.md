# Parity with the native APIs, and with .NET MAUI `AppActions` — comparison record (September 2026)

*Maintainer document, not shipped with the package. Written 2026-09-16.
Method: three independent reading passes — Android `ShortcutManager` (28
capabilities), Apple `UIApplicationShortcutItem` (17), .NET MAUI `AppActions`
(16) — each fetched from the live upstream source or reference page and
cross-checked line by line against this repo, then one adversarial judging
pass that re-ran the citations, re-fetched the quoted OS text, discounted
three overstated findings and added one the reading passes missed. Labels:
**[verified]** read from the cited source; **[plausible]** inferred but not
confirmed; **[unverified]** not checked.*

This is the companion to
[`flutter-quick-actions-parity-2026-09.md`](./flutter-quick-actions-parity-2026-09.md).
That one compares us to another *wrapper*; this one compares us to the
*platforms themselves*, which is the harder bar.

## 1. Headline

Native coverage is good, and most of what is missing is missing on purpose.
Six things are genuinely absent and cost a game developer something. Only the
first two matter much, and the biggest one is not a missing call at all — it is
a threading contract we do not honour.

| # | Gap | Verdict |
|---|-----|---------|
| 1 | Every `ShortcutManager` call is synchronous on Unity's main thread | implement |
| 2 | No runtime read-back of our own static shortcuts → free-slot budget is uncomputable | implement |
| 3 | `RequestPin` has no result callback | implement |
| 4 | Runtime bitmap icons are never sized against `getIconMaxWidth/Height` | **done 2026-09-18** |
| 5 | No machine-readable reason for a refused write (rate limit vs cap vs collision) | **partly done 2026-09-18** |
| 6 | iOS `MaxShortcutCount => 4` is called "documented"; Apple documents no integer | document-only |
| 7 | `Update()` cannot refresh a pinned-only copy | document-only (near-unreachable) |

## 2. Correctly missing — do not "fix" these **[verified]**

Enumerated by the Android pass and deliberately *not* listed as gaps: sharing
shortcuts and conversation shortcuts, `setLongLived` /
`removeLongLivedShortcuts`, `setPerson` / `setPersons`, `setCategories` and
static `<categories>`, `setCapabilityBinding` (App Actions / Assistant),
`setActivity`, `setIntents` + `TaskStackBuilder` back stacks, and
`ACTION_CREATE_SHORTCUT` / `createShortcutResultIntent` as a creation surface.
All belong to messaging-app, multi-window and voice-assistant feature areas a
Unity game has no use for.

Two more are absent for reasons the code already documents:

- **`setDynamicShortcuts`** (full replace) — would wipe a host app's own
  dynamic shortcuts. `addDynamicShortcuts` is our only write path, on purpose.
  (This is exactly what Flutter's plugin *does* do, and what MAUI does.)
- **`pushDynamicShortcut`** — lives in `androidx.core`, which a zero-dependency
  package cannot take. Confirmed: no `androidx.core.content.pm` import anywhere
  in `Plugins/Android`, and no `ShortcutManagerCompat` — we talk to the raw
  framework `android.content.pm.ShortcutManager`.

On iOS the equivalents are `targetContentIdentifier` (multi-scene / iPad
routing) and `UIApplicationShortcutIcon.init(contact:)` (CNContact photo or
monogram) — both real Apple capabilities with zero references in the repo, both
irrelevant to a single-scene game. `UIMutableApplicationShortcutItem` is unused
but is **not** a gap: we achieve the documented outcome ("replace your app
object's `shortcutItems` array") by rebuilding and reassigning the whole array.

Everything else on iOS is 1:1 **[verified]**: `type`, `localizedTitle` /
`localizedSubtitle`, `userInfo`, all 29 `IconType` cases, `templateImageName`,
`systemImageName` (SF Symbols), all 7 `Info.plist` sub-keys, the documented
icon fallback order (symbol > file > type — matched exactly on both the static
and the dynamic path), array-order onscreen ordering, and all four delivery
surfaces (launch-options key, app-delegate `performActionForShortcutItem`,
`UIScene.ConnectionOptions.shortcutItem`, `windowScene(_:performActionFor:)`).

## 3. The six real gaps

### 3.1 Worker-thread contract **[verified]** — *implement*

Android's reference page carries, verbatim, on `addDynamicShortcuts`,
`setDynamicShortcuts`, `updateShortcuts`, `getDynamicShortcuts`,
`getManifestShortcuts`, `getPinnedShortcuts`, `getShortcuts(int)`,
`requestPinShortcut` and `createShortcutResultIntent`:

> This method may take several seconds to complete, so it should only be called
> from a worker thread.

We do none of that. `Runtime/QuickActions.cs:20` documents the facade as
main-thread-only and not internally synchronised, and every path is a blocking
`AndroidJavaClass.CallStatic`. One cold-start `QuickActions.Add()` runs
`getDynamicShortcuts` (`QuickActionsBridge.java:376`), then `setShortcuts` →
`getManifestShortcuts` (:142), `getDynamicShortcuts` (:164),
`getPinnedShortcuts` (:181), `getMaxShortcutCountPerActivity` (:208),
optionally `removeDynamicShortcuts`/`disableShortcuts`/`enableShortcuts`
(:241-256) and finally `addDynamicShortcuts` (:264) — five to six
documented-slow calls back to back on the render thread. Grep finds no
`Task`/`async`/coroutine on the write path, and no "worker thread" / "several
seconds" warning in any shipped doc.

Scope note: the 0.25 s poll in `QuickActionsRuntime.cs` only calls
`consumePendingPerformed`, a cheap in-process static. The **write** path is
what is slow; the tap-delivery poll is fine.

Blast radius is the whole Android user base, and a developer cannot fix it from
outside — the facade is documented main-thread-only and is not internally
synchronised, so wrapping our calls in `Task.Run()` is unsafe. Minimum honest
fix: document the cost prominently. Real fix: move the JNI work to a Java
worker with a main-thread callback, or add an explicitly async surface
alongside the synchronous one.

### 3.2 Static shortcuts are not readable at runtime **[verified]** — *implement*

`getManifestShortcuts()` is called internally only — as a collision guard
(`QuickActionsBridge.java:142`) and to shrink the dynamic budget (:203-208) —
and `getShortcutsJson` iterates only `getDynamicShortcuts` (:376).
`getShortcuts(int matchFlags)` (API 30, `FLAG_MATCH_MANIFEST | DYNAMIC |
PINNED`) is not used at all.

Not reconciling statics is a defensible design decision and is documented as
such (`Runtime/QuickActions.cs:29`, `:57`, `README.md:671`, `:821`). Exposing
them **read-only** is a different and much cheaper thing. Today
`MaxShortcutCount` returns the raw `getMaxShortcutCountPerActivity` value —
`README.md:490` correctly warns that slots are shared with statics, but the
package offers no way to learn how many fewer are free. With three baked
statics on a device reporting 5, the developer sees "5", gets `false` back from
`Add()`, and cannot tell cap-exhaustion from an id collision without reading
logcat. This is hit once per project, during the exact phase where a developer
decides whether the package works.

`getShortcuts(matchFlags)` would answer static, dynamic and pinned in one call
instead of three — which also reduces the main-thread cost in §3.1.

### 3.3 `RequestPin` reports dispatch, not outcome **[verified]** — *implement*

`QuickActionsBridge.java:574` calls `manager.requestPinShortcut(s, null)` — the
`IntentSender` is always null, so the confirmation broadcast never arrives.
`createShortcutResultIntent` is not referenced anywhere. Our own Javadoc is
accurate about the consequence ("the user still confirms in launcher UI; the OS
reports no outcome"), but a game building a "pin this to your home screen"
prompt gets a boolean meaning *the dialog was shown*: it cannot award a reward,
cannot stop re-prompting someone who already accepted, cannot measure
conversion. There is no workaround from C# — the `IntentSender` has to be built
and registered on the Java side. Cost is a `PendingIntent` plus a small
`BroadcastReceiver` and a C# event mirroring the existing `Performed` channel.
If we do not implement it, the README should say outright that the result is
unobservable.

### 3.4 Runtime bitmaps are passed through unsized **[verified]** — *consider*

`getIconMaxWidth()` / `getIconMaxHeight()` are never called.
`resolveIcon` (`QuickActionsBridge.java:486-503`) decodes whatever absolute
path the caller set in `AndroidBitmapFile` and hands it straight to
`Icon.createWithAdaptiveBitmap` / `createWithBitmap` — no dimension check, no
downscale, no inset math. Android's own wording:

> Note that this method returns max width of icon's visible part. […] To
> calculate bitmap image to function as AdaptiveIconDrawable, multiply
> 1 + 2 * AdaptiveIconDrawable.getExtraInsetFraction() to the returned size.

Our guidance says only "supply the usual adaptive safe-zone padding in the
image" (`QuickActionItem.cs:88-91`) and `README.md:504-505` tells the developer
to `EncodeToPNG()` to `persistentDataPath` with no pixel target. A Unity
`Texture2D` is commonly 512 or 1024 px square, well over the typical limit, and
crosses a binder transaction at that size on every publish.
**[plausible]** the OS silently downscales rather than throwing — Android
documents no failure mode for exceeding the limit, and we have not observed
one, so no crash is claimed here. The inset half is concrete: without the
documented multiplier, a correctly-authored square avatar gets its edges eaten
by the launcher mask. If only one half ships, ship the doc half. Note these two
getters are cheap property reads and are **not** among the methods carrying the
worker-thread warning.

### 3.5 No machine-readable refusal reason **[verified]** — *consider*

`isRateLimitingActive()` is never called; we infer rate limiting after the fact
from a `false` return of `addDynamicShortcuts` (`QuickActionsBridge.java:264`),
and `QuickActions.Add` rolls the optimistic add back cleanly
(`Runtime/QuickActions.cs:496-506`). That post-hoc handling is correct and is
the part that matters — `isRateLimitingActive` on its own is inherently racy.
What is missing is the DX half: the three distinct causes (background rate
limiting, cap exhaustion, id owned by another publisher) exist only as
`Debug.Log` text (`:504`, `:515`, and the `AddList` / `Update` twins at
`:565`, `:640`), never in a return value. A game that
writes shortcuts on backgrounding — the natural "update the continue-playing
shortcut when the player quits" — sees an intermittent `false` and cannot tell
"retry after the next foreground" from "retrying will never work". This is a
C#-side API-shape fix, not a native gap.

### 3.6 iOS `MaxShortcutCount` **[verified]** — *document-only*

`Runtime/Internal/iOSQuickActionsBridge.cs:26` is `public int
MaxShortcutCount => 4;` with the comment "4 is the documented Home Screen
display limit", repeated in `IQuickActionsBridge.cs:19`, `QuickActions.cs:237`
and `README.md:490`. Apple documents no integer — only "up to the
system-defined limit" — and the "Add Home Screen quick actions" article says:

> Don't limit the number of quick actions provided to the shortcutItems
> property, because the system displays only the number of items that fit the
> screen.

Nothing in `Runtime/` enforces the 4: `IOSQuickActionsBridge.SetShortcuts`
returns the caller's items unchanged, so no item is ever dropped. It is
advisory only, and 4 is the commonly observed value — so changing the *number*
would be worse than keeping it. One word is wrong: "documented" should be
"observed". Per this repo's own rule about not inventing status claims, that is
the fix.

### 3.7 `Update()` and pinned-only copies **[verified, downgraded]** — *document-only*

`updateShortcuts` is never called; the only write is `addDynamicShortcuts`,
relying on same-id-updates-in-place, which by definition cannot reach a
pinned-only entry. But the judging pass traced whether such a live entry can
exist through our API and found it close to unreachable: `setShortcuts`
computes `stalePinned` from any of our pinned ids absent from the new set and
disables them immediately (:236-242), and a budget-trimmed item is likewise
absent from `newIds` and so also disabled. `getShortcutsJson` reads only
dynamics, so the entry would be invisible to `GetAll`/`GetById`/`IsAdded`
anyway. The reachable route is external interference. Implementing
`updateShortcuts` would add a second rate-limited write path to guard a state
our own code does not produce — poor value against §3.1. Note in the API docs
that `Update` reaches dynamic entries only. Revisit only if pinned shortcuts
ever become first-class in `GetAll`/`GetById`, at which point `updateShortcuts`
and `getShortcuts(FLAG_MATCH_PINNED)` land together.

## 4. .NET MAUI `AppActions` **[verified from source]**

`dotnet/maui`, MIT — same licence as this package. Its predecessor
Xamarin.Essentials has been archived read-only since 2024-05-15. Read from
`src/Essentials/src/AppActions/` on `main`: `AppActions.shared.cs`,
`.android.cs`, `.ios.cs`, `.windows.cs`, plus `LICENSE.txt` and the Microsoft
Learn article.

The whole public API:

```csharp
interface IAppActions {
    bool IsSupported { get; }
    Task<IEnumerable<AppAction>> GetAsync();
    Task SetAsync(IEnumerable<AppAction> actions);   // full replace
    event EventHandler<AppActionEventArgs>? AppActionActivated;
}
class AppAction(string id, string title, string? subtitle = null, string? icon = null);
```

`Icon` is `internal` — settable through the constructor, never readable back.
Everything is replace-all: no Add/Update/Remove/RemoveById/RemoveAll/
GetById/IsAdded, so changing one shortcut means `GetAsync`, mutate the list
yourself, `SetAsync` the whole thing.

Three things of theirs have no counterpart here, one worth stealing:

- A Windows implementation over `Windows.UI.StartScreen.JumpList` — irrelevant,
  we do not target Windows.
- **[plausible]** declarative startup registration,
  `builder.ConfigureEssentials(e => e.AddAppAction(…).OnAppAction(…))`. Sourced
  from Learn only; the implementing file could not be located. Our nearest
  equivalent is the build-time settings asset, which is a better fit for Unity.
- A hard, code-level platform gate:
  `[SupportedOSPlatformGuard("android25.0")] public bool IsSupported =>
  OperatingSystem.IsAndroidVersionAtLeast(25)`, with
  `FeatureNotSupportedException` thrown from `GetAsync`/`SetAsync` rather than
  a silent no-op. We return bools and log — quieter, but easier to ignore.

Everything else runs the other way. They have no static/manifest shortcuts and
no ownership marker (their iOS `SetAsync` unconditionally overwrites
`UIApplication.SharedApplication.ShortcutItems` with no `Info.plist` merge;
their Android side touches only dynamics), no `LocalizedTitles`/
`LocalizedSubtitles`, no `IconType` enum, no per-platform icon fields (one
string: `Resources.GetIdentifier` on Android,
`UIApplicationShortcutIcon.FromTemplateImageName` on iOS — no SF Symbol, no
adaptive, no bitmap file), no `Payload`, no
`LastPerformed`/`ResetLastPerformed`, no `IsPinSupported`/`RequestPin`, no
`ReportUsed`, no `Locale`, no `LoggingEnable`, no `MaxShortcutCount`. Their iOS
delivery handler gates on a fixed constant, `shortcutItem.Type ==
"XE_APP_ACTION_TYPE"`, so a shortcut declared directly in `Info.plist` is
silently ignored — the exact failure our marker-plus-static-baking design
exists to avoid. Their Android cold-launch path requires the consuming app to
hand-edit `MainActivity.cs` with an `IntentFilter` and `OnResume`/`OnNewIntent`
overrides (their docs say iOS, Mac Catalyst and Windows need "No setup"), where
our trampoline and build-time injection handle it.

One caveat against §3.1: MAUI's API is `Task`-returning throughout, so their
callers at least get a shape that can move off the UI thread — even though the
underlying `ShortcutManager` calls are made synchronously inside those Tasks.
Their async-ness is cosmetic, but it is a cosmetic we lack.

## 5. What was discounted

The judging pass refused to promote three reading-pass findings as stated:

1. The `updateShortcuts` entry overstated its impact — see §3.7.
2. The iOS `MaxShortcutCount` entry was self-labelled "plausible"; re-checking
   confirmed nothing enforces the number, so it is a doc-accuracy issue, not a
   behavioural one — see §3.6.
3. The MAUI `ConfigureEssentials`/`AddAppAction` entry is Learn-sourced only
   and stays labelled plausible — see §4.

And one capability neither reading pass surfaced was added by the judge from
the live reference page: the worker-thread sentence in §3.1, which reframes the
highest-cost gap. `getShortcuts(int matchFlags)` (API 30) was also missed —
it is the modern one-call read-back covering §3.2.

## 6. What this document produced — resolution log (2026-09-18)

Added after the fact, so the table above keeps reading as it was written.

- **§3.4 — done.** `resolveIcon` now measures a decoded `AndroidBitmapFile`
  against `getIconMaxWidth/Height` and downscales past them with the aspect
  ratio preserved, giving an adaptive bitmap the
  `1 + 2 * AdaptiveIconDrawable.getExtraInsetFraction()` allowance the platform
  documents. Both halves the section asked for shipped — the behaviour and the
  doc — and the README's `AndroidBitmapFile` / `AndroidBitmapAdaptive` rows say
  so. Every failure path (no `ShortcutManager`, a budget reported as 0, any
  throw) leaves the bitmap untouched, because an oversized icon is still a valid
  icon while a throw would cost the whole write. Note the section's own
  **[plausible]** label stands: we still have not observed what an unclamped
  oversized icon does on a device, so this is a cost fix, not a bug fix.

- **§3.5 — partly done.** `QuickActions.IsRateLimitingActive` surfaces the
  Android flag, which is the half that separates "retry once foregrounded" from
  "this will never work". The other half — a machine-readable code on the write
  itself, distinguishing cap exhaustion from an id another publisher owns — is
  still only `Debug.Log` text. That one is an API-shape change to every write's
  return type and is deliberately not bundled here.

- **§3.7 — not doing, and that is the decision.** `updateShortcuts` stays out.
  The section already downgraded it: a pinned-only live copy of one of our
  shortcuts is close to unreachable through this API (`setShortcuts` disables
  our pinned ids the moment they leave the new set, and `getShortcutsJson`
  reads dynamics only, so such an entry would be invisible to
  `GetAll`/`GetById`/`IsAdded` anyway), and the only route to one is external
  interference. Adding it would buy a second rate-limited write path guarding a
  state our own code does not produce. Revisit only together with pinned
  shortcuts becoming first-class in `GetAll`/`GetById`, when `updateShortcuts`
  and `getShortcuts(FLAG_MATCH_PINNED)` would land as one change.

- **§3.6 — done earlier.** No copy of the 4 calls itself documented any more:
  `iOSQuickActionsBridge.cs`, `IQuickActionsBridge.cs` and `QuickActions.cs`
  say *observed*, and `README.md` says "display limit, no OS query".

Gaps 1, 2 and 3 — the main-thread contract, the static-shortcut read-back and
the `RequestPin` result callback — are untouched and stay `implement`.
