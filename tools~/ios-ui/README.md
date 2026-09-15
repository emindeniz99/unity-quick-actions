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
| `gen_project.rb` | regenerates the project with the `xcodeproj` gem; only needed when the target's shape changes, never on CI |

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
4. Taps the row whose label **begins with** the configured title — SpringBoard
   may render "Title, Subtitle" as one label — and waits for the app to reach
   the foreground.
5. Reads the **marker file** the testbed writes on `QuickActions.Performed`
   (`Assets/Scripts/QuickActionsPerformedMarker.cs` in `Examples~/Testbed2022`
   and `Examples~/Testbed6` — the two exports CI installs — appends every
   delivered id to `persistentDataPath/quickactions-performed.log`)
   from the host side of the simulator's data container, and requires a line
   written *after* the tap that equals the expected id.

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
| `PASS <id> via '<row label>'` | the row was tapped, the app came up, the id reached `Performed` | green |
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
export TEST_RUNNER_QA_OUT="$PWD/out"                 # required
export TEST_RUNNER_QA_APP_ID=com.quickactions.testbed
export TEST_RUNNER_QA_APP_NAME=QuickActionsDemo      # the icon's label
export TEST_RUNNER_QA_ROW_TITLE="Daily Reward"
export TEST_RUNNER_QA_ACTION_ID=daily_reward
export TEST_RUNNER_QA_MARKER="$(xcrun simctl get_app_container "$UDID" com.quickactions.testbed data)/Documents/quickactions-performed.log"
xcodebuild test -project SpringBoardTap.xcodeproj -scheme SpringBoardTap \
  -destination "id=$UDID" CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO CODE_SIGN_IDENTITY=""
```

`xcodebuild` hands every `TEST_RUNNER_*` variable to the test runner with the
prefix stripped. Without `QA_MARKER` the test cannot check delivery and reports
`SKIPPED` after the launch (the launch is still in the evidence); CI always
sets it and fails the job if the container cannot be resolved.

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
