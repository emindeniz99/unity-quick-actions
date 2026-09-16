# iOS SpringBoard tap — XCUITest on the Simulator

The iOS half of what [`tools~/device-smoke`](../device-smoke/README.md) does
with `adb` on Android: reach the home screen, long-press the app icon, tap one
quick action, and prove the id arrived in the game. There is no `simctl`
command for any of that, so this directory is an **XCUITest bundle that drives
SpringBoard** — the same mechanism Flutter's official `quick_actions_ios`
plugin uses for its own CI test, and the one every higher-level tool (Appium,
Maestro) wraps underneath. It runs on the iOS Simulator, unsigned, with **no
Apple account** anywhere.

## What is here

| file | role |
|---|---|
| `SpringBoardTap.xcodeproj` | a project whose only target is the UI-testing bundle `SpringBoardTapUITests`, with **no host application** ("Target to be Tested: None") — generated, committed |
| `SpringBoardTapUITests/SpringBoardTapUITests.swift` | the test |
| `run_springboard_tap.sh` | one pass: runs the test against a booted simulator and turns its verdict into an exit status and a job-summary block |
| `gen_project.rb` | regenerates the project with the `xcodeproj` gem; only needed when the target's shape changes, never on CI |

The test taps **one** row per run, named by `QA_ACTION_ID` / `QA_ROW_TITLE`, so
CI runs it twice per leg through `run_springboard_tap.sh`: once for
`daily_reward`, which the settings asset baked into `Info.plist`, and once for
`runtime_add`, which nothing baked anywhere — see [Tapping a runtime-added
row](#tapping-a-runtime-added-row).

## What the test does

1. Terminates the app if it is running, presses Home, waits for SpringBoard's
   icons, and dumps the tree.
2. Finds the icon by label (the app's display name; prefix match as fallback).
   Every page's icons are in SpringBoard's tree, but an icon on a page other
   than the current one reports a **zero frame** — the first run found the app
   on page 2 of 2 on both iOS 18.6 and 26.5 — so the test swipes left until
   the icon has a real frame inside the screen (up to four pages).
3. Long-presses it — **by coordinate** at the icon's centre, because
   SpringBoard's icons answer `isHittable` with `false` even when laid out —
   with the **adaptive duration** flutter/packages settled on after two years
   of flaky runs: 1.5 s first; a press that lands in edit ("jiggle") mode was
   too long, one that opens nothing was too short; ±0.2 s, four attempts.
4. Taps the row whose **identifier is the action id** — SpringBoard exposes
   each quick action as a `Button` whose identifier is the shortcut's type and
   whose label is "Title, Subtitle" (run 77, iOS 18.6: identifier
   `daily_reward`, label `Daily Reward, Claim today's gift`); a label that
   **begins with** the configured title is the fallback — and waits for the
   app to reach the foreground (`QA_WAIT_SECONDS`, 30).
5. Reads the **marker file** the testbed writes on `QuickActions.Performed`
   (`Assets/Scripts/QuickActionsPerformedMarker.cs` in `Examples~/Testbed2022`
   and `Examples~/Testbed6` — the two exports CI installs — appends every
   delivered id to `persistentDataPath/quickactions-performed.log`)
   from the host side of the simulator's data container, and requires a line
   written *after* the tap that equals the expected id, within
   `QA_DELIVERY_SECONDS` (120) of the tap. A cold Unity launch on the
   Simulator spends most of its first minute before the first frame, and the
   runtime dispatches the launch id one frame after its bootstrap: run 77's
   2022.3 / iOS 18.6 leg delivered 35–43 s after the tap, past the 30 s
   window the test had then, so its `FAIL` quoted a marker that already held
   the id. The verdict now quotes the read that decided it.

Two minutes of every run are XCUITest's own: it waits for SpringBoard to go
idle before it synthesizes a touch and again after, up to 60 s each, and
SpringBoard is not idle while the context menu's blur animates — the
long-press and the row tap each cost the full 60 s on iOS 18.6. The test
times the launch and the delivery from the moment the tap call returns, which
is within a few seconds of the touch itself.

Every step leaves `NN-<step>.txt` (SpringBoard's accessibility tree) and
`NN-<step>.png` in the output directory and as `.xcresult` attachments — the
first runs exist to learn what each SpringBoard exposes.

## The verdict

`launcher-tap.txt`, one line, with the Android capture's vocabulary and the
same rule: **the automation's own misses never fail the run** — `FAIL` is
reserved for a tap SpringBoard accepted that delivered nothing, and for a
context menu that opened without the app's quick actions in it.

| verdict | meaning | run |
|---|---|---|
| `PASS <id> via '<row label>' (<n> s after the tap)` | the row was tapped, the app came up, the id reached `Performed` | green |
| `SKIPPED <why>` | the automation never reached a tap that counts — no icon, an icon four swipes could not bring on screen, a row with no frame, the menu never opened, the tap did not register (row still on screen), or no `QA_MARKER` to check delivery against | green, with a warning |
| `FAIL <why>` | SpringBoard took the tap and nothing arrived: the app never came up, or came up without the id; or the context menu opened **without** the app's quick actions in it | red |
| *(none)* | the test stopped before writing a verdict — the bundle did not build, or XCUITest recorded a failure first (a lost hit point, a snapshot timeout); read `xcodebuild-test.log` and the `NN-*.txt` trees | red, deliberately — unlike the Android capture, whose crashed capture stays green, because this is also what a broken test bundle looks like |

`PASS` returns normally, `SKIPPED` throws `XCTSkip` (xcodebuild exits 0),
`FAIL` fails the test.

## Running it

CI does this in the `ios-springboard` job of
[`unity-ci.yml`](https://github.com/emindeniz99/unity-quick-actions/blob/main/.github/workflows/unity-ci.yml):
compile the Unity-exported Simulator project, boot a simulator, `simctl
install` the app (never launch it — the tap must cold-start it), then:

```sh
CONTAINER="$(xcrun simctl get_app_container "$UDID" com.quickactions.testbed data)"
QA_PROJECT="$PWD/SpringBoardTap.xcodeproj" \
QA_MARKER="$CONTAINER/Documents/quickactions-performed.log" \
  ./run_springboard_tap.sh "$UDID" "$PWD/out" daily_reward "Daily Reward"
```

Point `QA_PROJECT` at a copy **outside** this repo when you run it from a
checkout: `tools~` ends in `~`, and derived data must never be written back
under one. The script exits 0 on `PASS` and on `SKIPPED` and non-zero on
`FAIL`; `QA_APP_ID`, `QA_APP_NAME`, `QA_DERIVED`, `QA_PASS` and `QA_LEG` are
optional.

Underneath it is one `xcodebuild test` with the test's own configuration in the
environment:

```sh
export TEST_RUNNER_QA_OUT="$PWD/out"                 # required
export TEST_RUNNER_QA_APP_ID=com.quickactions.testbed
export TEST_RUNNER_QA_APP_NAME=QuickActionsDemo      # the icon's label
export TEST_RUNNER_QA_ROW_TITLE="Daily Reward"
export TEST_RUNNER_QA_ACTION_ID=daily_reward
export TEST_RUNNER_QA_MARKER="$CONTAINER/Documents/quickactions-performed.log"
# optional: TEST_RUNNER_QA_WAIT_SECONDS (foreground, 30) and
#           TEST_RUNNER_QA_DELIVERY_SECONDS (Performed, 120), both from the tap
xcodebuild test -project SpringBoardTap.xcodeproj -scheme SpringBoardTap \
  -destination "id=$UDID" CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO CODE_SIGN_IDENTITY=""
```

`xcodebuild` hands every `TEST_RUNNER_*` variable to the test runner with the
prefix stripped. Without `QA_MARKER` the test cannot check delivery and reports
`SKIPPED` after the launch (the launch is still in the evidence); CI always
sets it and fails the job if the container cannot be resolved.

## Tapping a runtime-added row

A freshly installed app shows only the quick actions its `Info.plist` carries,
so every tap this harness took until now was on a **static** shortcut. A row
that `QuickActions.Add` published can only exist after the app has run and
published one, and the demo's own "Add" button cannot be reached: it is drawn
with IMGUI, which puts no accessibility element on screen for XCUITest to find.

So the testbed publishes one itself, on request —
`Examples~/Testbed2022` and `Examples~/Testbed6`,
`Assets/Scripts/QuickActionsRuntimeSeeder.cs`, one item
(`runtime_add` / "Runtime Add"), because iOS shows at most four quick actions
and the three statics take the rest. CI asks for it between the two passes:

```sh
SIMCTL_CHILD_QA_SEED_RUNTIME=1 \
  xcrun simctl launch --terminate-running-process "$UDID" com.quickactions.testbed -qa-seed-runtime
```

Both channels at once — an environment variable through simctl's
`SIMCTL_CHILD_` prefix and a launch argument — because which of the two an
IL2CPP player on iOS actually receives is not documented anywhere we could
check. The seeder writes `Documents/quickactions-seeded.log` naming the one it
saw, and run 94 answered it on all three legs: **`added runtime_add (asked via
env)`** — `Environment.GetEnvironmentVariable` sees the `SIMCTL_CHILD_`
variable, and `Environment.GetCommandLineArgs()` never reported the launch
argument (the seeder would have said `env+argv`). Both are still sent: the
argument costs nothing and the day it starts arriving, the file says so. The
file is also CI's signal that the shortcut is published; the app is then
terminated so the tap still cold-starts it.

Run 94 (2026-09-16) tapped the seeded row on all three legs: `PASS` 5 s after
the tap on 2022.3 / iOS 26.2 and on 6000.3.21f1 / iOS 26.5, and SpringBoard's
own menu held both kinds at once —

```
"New Game, Start a fresh run", "Continue, Resume your save",
"Daily Reward, Claim today's gift", "Runtime Add, Added by QuickActions.Add"
```

— three from `Info.plist`, one from `QuickActions.Add`. The `unity6-xcode27`
canary opened the same menu with the same four rows and its tap never
delivered; the app was in the foreground 0 s after that tap (5 s on the green
legs), so it did not cold-start there and the cause is not established. No seed file means the harness
missed, not that the package failed, so that case is a `SKIPPED` with a
warning — but a menu that opens **without** the row after a confirmed seed is a
`FAIL`, like any other missing quick action.

## What it cannot do

* **A physical iPhone.** XCUITest on a device needs a development-signed
  build and either your own phone or a device farm; the research record in
  [`docs~/ios-toolchain-and-ui-test-research-2026-09.md`](../../docs~/ios-toolchain-and-ui-test-research-2026-09.md)
  costs that out. Nothing here changes the "no iPhone has ever run this"
  status in the shipped docs.
* **Tell you which delivery path fired.** UIScene against app delegate is
  decided by the Unity version's export, not by this test; the `ios-simulator-coex`
  legs assert that.
* **Survive SpringBoard changes silently.** The label formats it matches were
  read from other projects' tests and from Apple's own home screen, not from a
  contract. When a run reports `SKIPPED`, open the artifact's trees first.

## Regenerating the project

```sh
gem install --user-install xcodeproj
ruby tools~/ios-ui/gen_project.rb
```

Runs on Linux too — nothing in the generator needs Xcode.
