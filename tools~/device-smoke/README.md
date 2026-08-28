# Device smoke

`tools~/verify.sh` proves the package *compiles and behaves* headlessly (C# unit
tests, Java against SDK stubs). It cannot prove the two things that only an OS
can answer: whether the shortcuts really land in `ShortcutManager`, and whether
a tap on one really comes back into the game. That is what this directory is
for.

Android has `adb`, so that half is automated: `android_device_smoke.sh`.
iOS has no equivalent — see [iOS](#ios--no-automation-shipped) below, which
documents the manual run instead of pretending it is covered.

## `android_device_smoke.sh`

```
tools~/device-smoke/android_device_smoke.sh <apk> <application-id> [adb-serial]
```

### What it needs

* **A development APK of the Demo sample**, built by Unity with
  **`QUICKACTIONS_ENABLED`** in Player Settings ▸ Scripting Define Symbols, with
  `Samples~/Demo/QuickActionsDemo.unity` as the startup scene. Without the
  define the package is gated off: no `EminDeniz99.QuickActions.dll`, and the
  trampoline `<activity>` is never injected into the manifest (step 6 then fails
  because there is nothing to start). Without the demo scene nothing publishes
  shortcuts (step 5 fails).
* **A device or emulator on API 25+** (`ShortcutManager` does not exist below
  that) with `adb` on `PATH`. Pass an `adb-serial` when more than one is attached.

### What it does

1. Waits (bounded) for a booted device and checks its API level.
2. `adb install -r` the APK.
3. `pm clear` + `am force-stop` the app — the autotest hook runs in `Start`, so
   it needs a fresh process, and clearing data drops shortcuts a previous run
   left behind. A refused `pm clear` prints a warning and the run continues.
4. Launches the app's resolved launcher activity with the string extra
   `com.emindeniz99.quickactions.AUTOTEST=add3`. The Demo sample reads that
   extra on Android and presses its own "Add 3 shortcuts" button — an adb harness
   cannot tap IMGUI. **A launch without the extra behaves exactly as before**,
   so this hook is inert for every real user.
5. Polls `adb shell dumpsys shortcut` until `new_game`, `continue` and `daily`
   appear **in this application id's section** (another app's shortcut with the
   same id does not count).
6. Clears logcat and starts the exported trampoline directly:
   `am start -n <app-id>/com.emindeniz99.quickactions.QuickActionsTrampolineActivity
   -a android.intent.action.VIEW --es com.emindeniz99.quickactions.ACTION_ID new_game`.
   That is the same intent the launcher sends for a tap. The app is still
   running at this point, so this is the **warm** path.
7. Polls logcat for the package's own line
   `[QuickActions] Performed quick action 'new_game'.` (the demo turns
   `LoggingEnable` on in `Awake`).
8. `am force-stop`s the app, **proves the process is gone** (polls `pidof`
   empty — force-stop's exit status says nothing, and a still-alive app would
   silently turn this into a second warm tap), clears logcat and sends the same
   intent shape **for a second registered id** (`continue`) — the assertion
   deliberately matches text that cannot pre-exist in the buffer, because
   `logcat -c` can under-clear on emulators while exiting 0. Nothing of ours is
   running at this point, so the trampoline has to start the process and the id
   has to survive that start. Polls for that id's `Performed` line, on a larger
   budget (`COLD_LOG_ATTEMPTS`) because a cold start carries a whole Unity
   boot. No `pm clear` here: the shortcuts from step 5 must stay registered, or
   the trampoline's ownership gate would reject the tap and the step would
   blame the wrong thing.

Every failure names the step that failed, prints the evidence it collected
(dumpsys section, logcat tail) and exits non-zero. A clean run ends in a single
`PASS:` line — nothing else prints success.

Waits are bounded and overridable by environment variable:
`POLL_INTERVAL` (seconds, default 1), `BOOT_ATTEMPTS` (120),
`SHORTCUT_ATTEMPTS` (45), `LOG_ATTEMPTS` (30), `COLD_LOG_ATTEMPTS` (60), and
`COLD_SETTLE` (seconds slept between the proven force-stop and the cold tap,
default 5 — an emulator's GPU process tears the dead app's Vulkan objects
down asynchronously, and launching the new process into that teardown left a
restarted player engine-silent in CI).

### What it asserts — and what it does not

It asserts that a `QUICKACTIONS_ENABLED` build publishes dynamic shortcuts the
OS accepts, and that a tap intent for one of them is turned into a `Performed`
event inside the game — **twice**: once into a running process
(`OnApplicationFocus`/`OnApplicationPause`, the warm resume) and once into an
app that was force-stopped first, where the tap starts the process and the id
has to reach the game through the launch intent. Because the id it taps is a
live registered shortcut, it also exercises the path the trampoline's spoof gate
deliberately **allows** (`isKnownShortcut`).

The cold step has run green on the CI emulator legs for all three Unity lines
(2021.3 and 2022.3 on the API 30 image, Unity 6 on API 35) — the `android-smoke`
job in [`unity-ci.yml`](../../.github/workflows/unity-ci.yml). It has never run
against physical hardware, so a cold launcher tap on a real device remains
unobserved.

It does **not** prove:

* that a **launcher** renders those shortcuts, their icons, or their order —
  that needs human eyes on a home screen;
* that a real **launcher tap** on a quit app behaves like the `am start` the
  script sends — the intent is the same one the launcher builds, but only
  SpringBoard-style UI automation could tap the icon itself;
* that an **unregistered** id is *rejected* by the trampoline (the negative half
  of the spoof gate — covered headlessly by the Java smoke test in
  `.verify/JavaSmoke`);
* pinning, static/manifest shortcuts, or host-app coexistence on a real device.

### Local run

```bash
# 1. Build the Demo sample to an APK with QUICKACTIONS_ENABLED (Unity, dev build).
# 2. Start an emulator or plug in a device, then:
adb devices
tools~/device-smoke/android_device_smoke.sh ~/builds/demo.apk com.example.game
# ...or against a specific device:
tools~/device-smoke/android_device_smoke.sh ~/builds/demo.apk com.example.game emulator-5554
```

### CI

[`.github/workflows/unity-ci.yml`](../../.github/workflows/unity-ci.yml) is
where this script actually runs: its `android-build` job builds a development
APK of the Demo sample per Unity line on a licensed Unity (GameCI), and
`android-smoke` feeds each one to this script on an emulator — API 30 for
2021.3 and 2022.3, API 35 for unity6, whose development player dies at engine
init under the older image's ARM translation. Those legs are heavy, so they run
on `workflow_dispatch` and the weekly cron rather than per push.

[`.github/workflows/device-ci.yml`](../../.github/workflows/device-ci.yml) is
the older, standalone lane: it takes a URL to an already-built APK and runs
this script against an API 30 emulator, `workflow_dispatch`-only. It predates
the licence secrets and is kept for driving an APK this repo did not build.

## iOS — no automation shipped

There is no `adb` analog for iOS, and the gap is not one that a script can paper
over:

* A quick-action tap is delivered by **SpringBoard** to the app delegate
  (`application:performActionForShortcutItem:` / the launch-options path). No
  public `simctl` command triggers one, and the package uses no URL scheme, so
  `simctl openurl` cannot stand in for a tap.
* The home-screen long-press menu is SpringBoard UI. Reaching it programmatically
  means an XCUITest bundle driving SpringBoard. Two of its three prerequisites
  now exist: `.github/workflows/unity-ci.yml` builds the Xcode project on a
  licensed Unity and compiles it on a macOS runner, where it also boots a
  simulator and cold-launches the app. What is still missing is the XCUITest
  target itself — and `simctl` still cannot read `UIApplicationShortcutItems`
  or trigger a tap, so the assertion above remains out of reach.

So the iOS half is run **by hand**:

1. Build the Demo sample for iOS with `QUICKACTIONS_ENABLED` and open the
   generated Xcode project.
2. Run it on a simulator or device. Confirm the Xcode console shows
   `[QuickActions]` lines (the demo enables `LoggingEnable`).
3. Tap **Add 3 shortcuts** in the demo.
4. Go to the home screen (Simulator: ⇧⌘H) and long-press/click-and-hold the app
   icon. **Expected:** New Game, Continue, Daily with their icons.
5. Tap one. **Expected (warm path):** the app returns to the foreground and the
   console logs `[QuickActions] Performed quick action '<id>'.` with the demo's
   on-screen log showing the same id.
6. For the **cold** path, quit the app first (stop it in Xcode, or swipe it away
   in the app switcher), then repeat step 5. To keep the debugger attached, set
   Product ▸ Scheme ▸ Edit Scheme ▸ Run ▸ Launch to *Wait for the executable to
   be launched*; otherwise read the log with Console.app / `xcrun simctl spawn
   booted log stream --predicate 'processImagePath CONTAINS "<app>"'`.

Record the outcome in `PRODUCTION_READINESS.md` (the iOS rows marked
`device` — never run here) rather than in this file.
