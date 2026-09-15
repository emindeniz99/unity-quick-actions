# iOS toolchain and UI-test stack — research record, September 2026

Maintainer notes; this file does not ship (`docs~/` is outside the `files` list
in `package.json`). It answers four questions the owner asked on 2026-09-15 —
**must CI move to Xcode 26/27, is Xcode 16.4 dead, how should the iOS
home-screen tap be automated, what would a real iPhone cost** — with the
sources behind each answer, so the decision can be re-taken in October 2026
without redoing the reading.

How to read the labels: **[verified]** — the sentence quoted was read on the
linked page on 2026-09-15; **[plausible]** — inferred from a secondary source
or from two primary sentences combined; **[unverified]** — nobody found a
source either way. Two multi-agent sweeps produced the raw material (7 sourced
sweeps, 31 agents, ~1,150 page fetches); every decision-bearing claim was then
handed to two independent refuters (one checking the quote against the page,
one hunting for a newer source). What survived is below; what was corrected
by the refuters is stated in its corrected form.

## 1. Decisions taken on 2026-09-15

| decision | rationale in one line | revisit |
|---|---|---|
| **Keep the existing iOS legs exactly as they are** (2022.3 on `macos-15` / Xcode 16.4 default; Unity 6 on `macos-latest` / Xcode 26) | the owner wants a month of data before touching a green matrix | **October 2026** — with the canaries' results in hand |
| **Add Xcode 27 / iOS 27 canary legs** on GitHub's public-preview `xcode-27` image (Xcode 27.0 beta 6 as of 2026-09-15), `continue-on-error` — the run stays green, the job shows its own red X, never a required check — for `ios-simulator` (2022.3, unity6), `ios-simulator-coex` (unity6-coex) and `ios-springboard` (unity6) | the iOS 27 SDK is what App Store uploads must use from April 2027, and it makes the scene lifecycle mandatory — the path this package's UIScene hooks exist for | each run |
| **Automate the home-screen tap with a plain XCUITest bundle on the Simulator, no Apple account** (`tools~/ios-ui`) | every alternative wraps XCUITest anyway; Flutter's official plugin proves the exact scenario in CI; signing is not needed on the Simulator | after the first run |
| **No real-iPhone leg yet** | it needs the owner's signing identity in CI secrets and a device farm; the cheapest credible path is costed in §8 | when the Simulator leg is stable |

## 2. Apple's mandate: what it says, what it covers

- **[verified]** "Since April 28, 2026 — Apps uploaded to App Store Connect
  must be built with Xcode 26 or later using an SDK for iOS 26, iPadOS 26,
  tvOS 26, visionOS 26, or watchOS 26."
  <https://developer.apple.com/news/upcoming-requirements/> (announced
  2026-02-03: <https://developer.apple.com/news/?id=ueeok6yw>).
- **[verified]** The trigger is the *upload to App Store Connect*. TestFlight
  is in scope because a TestFlight build starts with "Upload your build to App
  Store Connect."
  <https://developer.apple.com/help/app-store-connect/test-a-beta-version/testflight-overview>.
  App Store Connect's own "Upload builds" table says an iOS app must be "Built
  using Xcode: Xcode 26 or later"
  <https://developer.apple.com/help/app-store-connect/manage-builds/upload-builds>.
- **[plausible]** Ad hoc / development exports and Simulator runs never pass
  through App Store Connect, so the rule cannot touch them
  (<https://developer.apple.com/documentation/xcode/distributing-your-app-to-registered-devices>).
  The rejection code developers quote is `ITMS-90725` ("SDK version issue …
  must be built with the iOS 26 SDK or later, included in Xcode 26 or later")
  — a developer-forum quote, not an Apple page
  (<https://developer.apple.com/forums/thread/813123>).
- **[verified]** The next round is already announced. Apple news, 2026-09-09:
  "Starting April 2027, apps and games uploaded to App Store Connect need to
  meet the following minimum requirements: iOS and iPadOS apps must be built
  with the iOS 27 & iPadOS 27 SDK or later."
  <https://developer.apple.com/news/?id=k1mtkt1k>. No day of month yet; the
  Upcoming Requirements page does not list it yet.
- **[verified]** Cadence: Xcode 14.1 from 2023-04-25, Xcode 15 from
  2024-04-29 (<https://developer.apple.com/news/?id=fxu2qp7b>), Xcode 16 from
  2025-04-24 (ITMS text quoted on
  <https://developer.apple.com/forums/thread/775864>), Xcode 26 from
  2026-04-28, iOS 27 SDK from April 2027. Late April, every year.

## 3. Xcode 16.4 today — not dead as a tool, dead for shipping

- **[verified]** Apple still lists it, under "Other Xcode versions": macOS
  "Sequoia 15.3 – Tahoe 26.1.x", SDK iOS 18.5, Simulator "iOS 15–18"
  <https://developer.apple.com/support/xcode/>. Its release notes say it
  "requires a Mac running macOS Sequoia 15.3 through macOS Tahoe 26.1"
  <https://developer.apple.com/documentation/xcode-release-notes/xcode-16_4-release-notes>.
- **[verified]** It is the default Xcode on GitHub's `macos-15` image (macOS
  15.7.9) — see §6.
- **What it cannot do:** upload anything to App Store Connect (§2), or run an
  iOS 26 Simulator runtime (**[plausible]** — Apple's Simulator column tops out
  at iOS 18 for it, and on the runner image every iOS 26 runtime is bound to an
  Xcode 26.x install).
- **So:** the 2022.3 leg on Xcode 16.4 still compiles and launches; whether
  it can drive a SpringBoard tap on iOS 18.6 is what the new `ios-springboard`
  2022.3 leg will measure (**[unverified]** — no run recorded as of
  2026-09-15; §10 item 5). It mirrors a toolchain no one can ship with any
  more. Its one remaining distinct value is the **iOS 18 runtime**, which
  exists only on `macos-15` (`macos-26` ships no iOS 18 runtime).

## 4. Xcode 27, iOS 27 and the scene-lifecycle mandate

- **[verified]** "Xcode 27 (27A266a)" released **2026-09-14**
  (<https://developer.apple.com/news/releases/>). Release notes: "requires a Mac running macOS Tahoe 26.6 or
  later", "will only install and run on Apple silicon Macs", and — the line
  that matters for old Unity exports — "**The ld64 linker has been removed and
  the `-ld_classic` option is no longer supported.**"
  <https://developer.apple.com/documentation/xcode-release-notes/xcode-27-release-notes>.
  App Store Connect accepts Xcode 27 builds since 2026-09-14
  (<https://developer.apple.com/help/app-store-connect/release-notes/>).
- **[verified]** iOS & iPadOS 27 release notes, UIKit → Deprecations: "**Apps
  built with the latest SDK must adopt the scene-based life cycle or they fail
  to launch.**"
  <https://developer.apple.com/documentation/ios-ipados-release-notes/ios-ipados-27-release-notes>.
  TN3187: "In the next major release following iOS 26, UIScene lifecycle will
  be required when building with the latest SDK; otherwise, your app won't
  launch. While supporting multiple scenes is encouraged, only adoption of
  scene life-cycle is required."
  <https://developer.apple.com/documentation/technotes/tn3187-migrating-to-the-uikit-scene-based-life-cycle>.
  Apple DTS on the forums: "Failing to adopt the scene-based life cycle in the
  next major iOS release will prevent the app from launching."
  <https://developer.apple.com/forums/thread/820807>.
- **What that means for this package:** from April 2027 every new upload is
  built with the iOS 27 SDK, so quick actions arrive through the scene
  delegate (`windowScene:performActionForShortcutItem:` / the connection
  options) and never through the app-delegate selector. The UIScene path the
  `ios-simulator-coex` legs assert becomes the *only* shipping path. That is
  why the canaries were added now rather than in April.
- **[verified]** No documented quick-action / SpringBoard behaviour difference
  between iOS 18 and iOS 26/27 Simulators exists in Apple's release notes; the
  only documented delivery difference is app-delegate vs scene-delegate, which
  is SDK/lifecycle-driven.

## 5. Unity lines against those Xcodes

### 2022.3 — the public line ends at 2022.3.62f3 (our testbed)

- **[verified]** Unity's public Release API (the Hub feed) lists 65 2022.3
  entries, newest **2022.3.62f3 (2025-10-28)**; queries for 63f1 / 72f1 / 76f1
  return nothing
  (<https://services.api.unity.com/unity/editor/release/v1/releases?version=2022.3&stream=LTS&order=RELEASE_DATE_DESC&limit=6>).
  Unity staff, 2025-05-14: "Unity 2022.3.62f1 came out on May 7th. We have no
  further public releases in the 2022 LTS line planned."
  <https://discussions.unity.com/t/unity-2022-lts-end-of-life/1642861>.
- **[verified]** 2022.3.63f1–**76f1** exist on unity.com (76f1 "Released on
  May 6, 2026"; 77f1+ are 404) and their pages carry `entitlements: ["XLTS"]`
  and "Get 3-year LTS with Unity Enterprise or Industry" — the Enterprise /
  Industry third-year patches
  (<https://unity.com/releases/editor/whats-new/2022.3.76f1>,
  <https://unity.com/releases/unity-6/support>). Their tarballs are publicly
  reachable; the split is entitlement, not availability **[plausible]**.
- **[verified]** No 2022.3 note from 61f1 to 76f1 declares Xcode 26 (or 27)
  support; the manual says "For App Store submission: refer to Apple's
  submission guidelines for the required Xcode version" and names no Xcode
  <https://docs.unity3d.com/2022.3/Documentation/Manual/system-requirements.html>.
- **[verified]** Two iOS-26-era fixes exist **only in XLTS builds**:
  - UIScene lifecycle events: 2022.3.72f1 (2026-02-11), more in 76f1
    (<https://unity.com/releases/editor/whats-new/2022.3.72f1>; Unity's
    official post lists "Unity 2022 xLTS: 2022.3.72f1+"
    <https://discussions.unity.com/t/info-apple-update-your-editor-to-receive-uiscene-lifecycle-support/1709065>).
  - **UUM-132447** "[iOS] Stripping issue on Xcode 26 when 'Use incremental GC'
    is disabled" — fixed in 2022.3.76f1, 6000.0.74f1, 6000.3.14f1, 6000.4.4f1;
    "Not reproducible on Xcode 16 (by user)"; mitigation: keep incremental GC
    enabled <https://issuetracker.unity.com/api/v1.0/issues/17849>. Both
    testbeds have `gcIncremental: 1`.
- **[verified]** Unity Build Automation's 2026 deprecation notice (Unity
  staff, tagged Official, 2026-06-24, edited 2026-08-19) keeps "2022.3.62f3 and
  2022.3.40f1" enabled while deprecating "2022.3.x, except for 2022.3.62f3 and
  2022.3.40f1 — LTS expired May 2025", keeps "Xcode 26.x (all stable
  releases)", 16.2, 16.3 and 16.4 enabled, and removes Xcode 15.4 / 16.0 / 16.1
  on 2026-10-06 "To align with Apple's requirement that App Store submissions
  must use Xcode 26 or later starting April 28, 2026"
  <https://discussions.unity.com/raw/1724029/1>. It makes no Unity↔Xcode
  pairing; Unity's own Xcode lookup page says "You can use an older Xcode
  version to build, but you won't be able to submit your app to the App Store"
  <https://docs.unity.com/en-us/build-automation/reference/available-xcode-versions>.
- **Inference [plausible], and the one that matters most:** a public-licence
  2022.3 user is on 62f3, ships with Xcode 26.x today, and has **no UIScene
  implementation** — so once the iOS 27 SDK is mandatory (April 2027) that
  user's app "fails to launch" unless Unity ships something public. Worth a
  line in the README's support matrix once Unity's position is known.

### 2021.3

- **[verified]** Newest 2021.3.45f2 (2025-10-03, CVE re-release);
  "Enterprise LTS expired February 2026" per the same Build Automation
  notice. No Unity statement about 2021.3 + Xcode 26 exists **[unverified]**.
  Its IL2CPP output needs `-ld_classic` under Xcode 15+ (this repo's own
  finding), which Xcode 27 removes — so that line's ceiling is Xcode 26.x. CI
  exports it and never compiles it on the Simulator; unchanged.

### Unity 6

- **[verified]** 6000.3 manual: "Xcode version 16 or later", "Unity supports
  iOS 15 and above", "Unity 6.3 LTS is supported until December 2027"
  <https://docs.unity3d.com/6000.3/Documentation/Manual/system-requirements.html>;
  newest patch 6000.3.24f1 (2026-09-10). UIScene from 6000.3.8f1 (testbed:
  6000.3.21f1). 6000.0 LTS "supported through October 2026".
- **[verified]** A grep of every newest release note (6000.7.0a6, 6000.6.0f1,
  6000.5.11f1, 6000.4.12f1, 6000.3.24f1, 6000.0.83f1, 2022.3.62f3,
  2021.3.45f2) finds **no mention of Xcode 27, ld64 or ld_classic** as of
  2026-09-15. Unity 6.5 is *not* documented as an Xcode 27 line (its
  announcement mentions only the experimental Swift project type
  <https://discussions.unity.com/t/unity-6-5-is-now-available/1723176>).
- **[verified]** Known Xcode 26 linker failure, ARKit-specific: UnityFramework
  "Assertion failed: (it != _dylibToOrdinal.end())" when the ARKit plugin adds
  `-ld64`; fixed in ARKit 6.3.1 / 6.4.0 with backports
  (<https://issuetracker.unity.com/api/v1.0/issues/7602>). Not this package's
  code path, but the same family of change. The `*-xcode27` canaries do NOT
  grep for `-ld64` / `-ld_classic` — they run the unchanged xcodebuild/launch
  steps under `continue-on-error`; if Unity emitted such a flag it sits in
  `OTHER_LDFLAGS` of the exported `project.pbxproj` in the
  `ios-simulator-xcodeproj-<export>` artifact (14-day retention), and Xcode
  27's rejection would show only in the canary leg's xcodebuild log.

## 6. GitHub-hosted macOS images (readmes read raw, image versions of 2026-09-07)

| label | macOS | Xcode installed | iOS Simulator runtimes | source |
|---|---|---|---|---|
| `macos-15` (arm64; the Intel `macos-15-intel` is identical) | 15.7.9 | 16.0, 16.1, 16.2, 16.3, **16.4 (default)**, 26.0.1, 26.1.1, 26.2, 26.3 | 18.5, 18.6, 26.0, 26.1, 26.2 | <https://raw.githubusercontent.com/actions/runner-images/main/images/macos/macos-15-arm64-Readme.md> |
| `macos-26` = `macos-latest` (arm64) | 26.6.2 | 26.0.1, 26.1.1, 26.2, 26.3, 26.4.1, 26.5, **26.6 (default)** — no 16.x, no 27 | 26.2, 26.4, 26.5 — no 18.x | <https://raw.githubusercontent.com/actions/runner-images/main/images/macos/macos-26-arm64-Readme.md> |
| `xcode-27` / `xcode-27-xlarge` (public preview, arm64 only) | 27.0 beta | 27.0 beta 6 (27A5252f) only — the GA 27A266a is "awaiting deployment" (#14709) | 27.0 | <https://raw.githubusercontent.com/actions/runner-images/main/images/macos/xcode-27-arm64-Readme.md>, <https://github.blog/changelog/2026-09-10-xcode-27-runner-image-now-runs-on-macos-27/> |
| `macos-14` | 14.8.9 | 15.x (15.4 default), 16.1, 16.2 | — | deprecated: brownouts through October 2026, gone 2026-11-02 <https://github.com/actions/runner-images/issues/13518> |

- **[verified]** Xcode 26.4.1 / 26.5 / 26.6 require macOS Tahoe 26.2+ per
  Apple, so **Xcode 26.3 is `macos-15`'s permanent ceiling**; Xcode 27 needs
  26.6+ (<https://developer.apple.com/support/xcode/>).
- **[verified]** Policies (root README
  <https://raw.githubusercontent.com/actions/runner-images/main/README.md>):
  `-latest` moved to macOS 26 between 2026-06-15 and 2026-07-15 (default
  Xcode 16.4 → 26.4.1, then 26.6 from 2026-07-21); default-Xcode bumps are
  announced "2 weeks prior" (#14172 gave 11 days, #14344 gave 14); "we only
  support the latest 2 versions of an OS"; since 2026-07-16 the newest image is
  keyed by Xcode major, not OS ("we will support one major Xcode version per
  image"). No `macos-15` deprecation is announced as of 2026-09-15; by policy it
  is next once a macOS 27 image is GA (**[plausible]**).
- **[verified]** Since 2025-08-11 GitHub keeps "at most three runtimes
  (simulators) in the image" — iOS 18.4 was already removed from `macos-15`
  on 2026-01-12
  (<https://github.blog/changelog/2025-07-11-upcoming-changes-to-macos-hosted-runners-macos-latest-migration-and-xcode-support-policy-updates/>).
- **Pinning:** the maintainers' own recommendation is `sudo xcode-select -s
  /Applications/Xcode_26.x.app` or `maxim-lobanov/setup-xcode` (a thin wrapper
  over the same call, verified in its source). Minor-version symlinks
  (`Xcode_26.4.app → 26.4.1`) survive the "previous patch is replaced" rule.

### What CI does today, and what the canaries add

| job | leg | runner | Xcode | iOS runtime | status |
|---|---|---|---|---|---|
| `ios-simulator` | 2022.3 | `macos-15` | 16.4 (image default, not pinned) | 18.6 | unchanged |
| `ios-springboard` | 2022.3 | `macos-15` | 16.4 (image default, not pinned) | 18.6 | new |
| `ios-simulator` | unity6 | `macos-latest` | 26.6 (image default) | 26.5 | unchanged |
| `ios-springboard` | unity6 | `macos-26` | 26.6 (image default) | 26.5 | new |
| `ios-simulator-coex` | 2022.3-coex / unity6-coex | `macos-15` / `macos-latest` | 16.4 / 26.6 | 18.6 / 26.5 | unchanged |
| all three | `*-xcode27` | `xcode-27` | 27.0 beta 6 | 27.0 | **canary — run stays green, job shows its own red X; not a required check** |

Options for October, in the order the evidence favours: (a) move the 2022.3
legs to Xcode 26.3 on `macos-15` — meets Apple's rule, keeps the iOS 18
runtime available, no runner change; (b) move everything to `macos-26` +
Xcode 26.6 and drop iOS 18 coverage; (c) keep a 16.4 leg as a non-gating
legacy canary. Whatever is chosen: pin the Xcode explicitly and add a guard
step (`xcodebuild -version`), because defaults move with two weeks' notice.

## 7. Automating the iOS home screen — the options, measured

Everything on the Simulator funnels into XCTest: Appium's XCUITest driver is
WebDriverAgent, an XCTest bundle; Maestro rebuilt its iOS driver on XCUITest in
2023 (<https://maestro.dev/blog/maestro-re-building-the-ios-driver>). Only
idb/AXe take the private-API + HID route.

- **Plain XCUITest** — `XCUIApplication(bundleIdentifier: "com.apple.springboard")`,
  `springboard.icons[name].firstMatch.press(forDuration:)`, then the menu row
  as `springboard.buttons[label]`. Public API (`XCUIApplication(bundleIdentifier:)`,
  `press(forDuration:)`, element type `.icon` — all in Apple's XCUIAutomation
  docs). Recipes: Jesse Squires (app deletion on the Simulator)
  <https://www.jessesquires.com/blog/2021/10/25/delete-app-during-ui-tests/>;
  a UI-test bundle with no host app
  <https://testableapple.com/test-multiple-apps-using-bundle-identifier-in-xctest/>;
  Apple DTS on why `TEST_HOST` must stay unset for such a bundle
  <https://developer.apple.com/forums/thread/765316>. Unsigned on the
  Simulator with `CODE_SIGNING_ALLOWED=NO`.
- **The industry datapoint that settles it — Flutter's official plugin.**
  `packages/quick_actions/quick_actions_ios/example/ios/RunnerUITests/RunnerUITests.swift`
  does exactly this in Flutter's CI: `springboard.icons["Quick Actions
  Example"]`, press 1.5 s, tap `"Action two"` / `"Action one, Action one
  subtitle"`, assert inside the app. Its flake history is the design input:
  lifecycle fix in 2021 (<https://github.com/flutter/plugins/pull/4003>), P1
  flake in 2023 because a too-long press opens the *Remove App* menu instead
  (<https://github.com/flutter/flutter/issues/125509>) → adaptive duration
  ±0.2 s with four retries, 30 s waits, terminate in `tearDown`.
  <https://github.com/flutter/packages/blob/main/packages/quick_actions/quick_actions_ios/example/ios/RunnerUITests/RunnerUITests.swift>
- **Appium** — works (Appium Pro 108 on Reminders: `mobile: touchAndHold`
  2.0 s, rows by accessibility id; 116 on SpringBoard sessions with
  `autoLaunch: false`) <https://appiumpro.com/editions/108-working-with-ios-app-home-screen-actions>,
  <https://appiumpro.com/editions/116-working-with-the-ios-home-screen-springboard>;
  `cordova-plugin-3dtouch` PR #3 drives the same scenario for cordova-ios 8's
  UIScene delivery <https://github.com/herdwatch-apps/cordova-plugin-3dtouch/pull/3>.
  Cost: Node + Appium server + a WDA `xcodebuild` — no gain over raw XCUITest
  on a Simulator.
- **Maestro** — Simulator-only on iOS; can reach the home screen (its
  clipboard recipe uses `pressKey: home` and Spotlight
  <https://docs.maestro.dev/examples/recipes/check-the-clipboard-content.md>),
  its runner reads SpringBoard's hierarchy as a fallback, but `longPress` is
  coordinate-based and no icon-long-press recipe exists. Off its design
  centre, plus a JVM CLI.
- **idb / AXe** — Appium dropped its idb driver because "the upstream fb-idb
  stack is not maintained well" <https://github.com/appium/appium-idb>; idb
  has coordinate `--duration` taps but no element queries on SpringBoard. AXe
  (private AX APIs, Xcode 26/27, `describe-ui`, `tap --label`, no documented
  long press) is built for AI agents driving a Simulator interactively, not
  for CI assertions <https://www.axe-cli.com/>.
- **SBShortcutMenuSimulator** (dylib injected into the Simulator's
  SpringBoard) — last issue 2018, Xcode 10; dead
  <https://github.com/DeskConnect/SBShortcutMenuSimulator>.
- **Hazards to expect** — SpringBoard's tree is not an API: springboard
  queries broke for people on iOS 15.2 and 17.4
  (<https://developer.apple.com/forums/thread/701473>); Xcode 26.1/26.2 on
  iOS 26 had hierarchy regressions (<https://developer.apple.com/forums/thread/812307>,
  root cause in that case the app's own SwiftUI graph); an Apple ID login
  prompt reportedly appears at random on iOS 26.1 Simulators during XCUITest;
  a press of 1.3 s landed in jiggle mode in one write-up, 2.0 s reached the
  menu in two others; the menu row's label is not documented to equal the
  shortcut title (Flutter sees "Title, Subtitle"; Reminders shows "New in
  Reminders"); `xcodebuild test` installs a `-Runner.app` icon next to the app
  under test. The harness therefore matches rows by *prefix*, dumps the tree
  at every step, and records `SKIPPED` rather than `FAIL` for every miss of its
  own (no icon, no hit point, no menu, a tap that did not register); `FAIL` is
  a tap SpringBoard accepted that delivered nothing, or a menu that opened
  without the app's quick actions. A test that dies before any verdict is red
  on purpose — a broken bundle must not read as a skipped tap.

### What comparable SDKs actually do (primary sources: their CI files)

| project | iOS in CI | OS-level tap? |
|---|---|---|
| Firebase Unity SDK | Simulator via `scripts/gha/test_simulator.py`; real devices via Firebase Test Lab **game-loop** (a `firebase-game-loop://` URL open); uploads an **unsigned** IPA (`CODE_SIGNING_ALLOWED=NO`, hand-zipped `Payload/`) | no — synthetic delivery, like this repo's coex leg. <https://raw.githubusercontent.com/firebase/firebase-unity-sdk/main/.github/workflows/integration_tests.yml> |
| Unity `com.unity.mobile.notifications` | Yamato, Simulator only (`IOS_SIMULATOR_SDK: 1`); Android on real hardware | no — in-app `QueryLastRespondedNotification`. <https://raw.githubusercontent.com/Unity-Technologies/com.unity.mobile.notifications/master/.yamato/upm-ci.yml> |
| AppsFlyer Unity plugin | macos-15, Unity Simulator project, `simctl boot`, bespoke shell harness | no. <https://raw.githubusercontent.com/AppsFlyerSDK/appsflyer-unity-plugin/master/.github/workflows/rc-e2e-ios.yml> |
| RevenueCat | StoreKitTest + unit/integration; "UI-dependent behaviour is covered by manual testing" | no. <https://www.revenuecat.com/blog/engineering/test-sdks-revenuecat/> |
| Branch iOS SDK | integration tests `workflow_dispatch` only | no. <https://raw.githubusercontent.com/BranchMetrics/ios-branch-deep-linking-attribution/master/.github/workflows/integration-tests.yml> |
| **OneSignal Unity SDK** | real signed IPA (Apple cert + ASC API key in secrets, manual `xcodebuild archive`/`-exportArchive`, entitlement assertion) → BrowserStack App Automate, WebdriverIO/Appium | **yes, real devices** — the only one. <https://raw.githubusercontent.com/OneSignal/OneSignal-Unity-SDK/main/.github/workflows/e2e.yml> |
| **Flutter `quick_actions_ios`** | XCUITest on SpringBoard, Simulator | **yes, Simulator** — this repo's model. |

Current position (synthetic sends on the Simulator, adb on Android emulators,
no iPhone) is the sector norm; the SpringBoard leg moves this repo past it on
the Simulator only.

## 8. A real iPhone from GitHub Actions — costed, not built

The owner has an Apple Developer Program account; the Simulator leg needs
none of it. Real hardware would:

- **Cheapest credible path: Firebase Test Lab, orchestrated from GitHub
  Actions.** macOS job: development-signed `xcodebuild -sdk iphoneos
  build-for-testing` on the Unity export plus the XCUITest target, zip the
  `Debug-iphoneos` products with the `.xctestrun`
  (<https://firebase.google.com/docs/test-lab/ios/run-xctest>); ubuntu job:
  `google-github-actions/auth` (Workload Identity Federation) + `gcloud
  firebase test ios run --type xctest --test … --device model=…,version=…
  --xcode-version=…` (<https://docs.cloud.google.com/sdk/gcloud/reference/firebase/test/ios/run>).
  **[verified]** "Test Lab re-signs your app with its own provisioning
  profile and certificate" — the farm's UDIDs never enter the owner's profile;
  the profile still needs **at least one registered device** (Apple: "Select
  one or more devices, then click Continue")
  <https://developer.apple.com/help/account/provisioning-profiles/create-a-development-provisioning-profile>,
  so the owner registers their own iPhone once. Signing on a runner: GitHub's
  documented keychain recipe (secrets `BUILD_CERTIFICATE_BASE64`,
  `P12_PASSWORD`, `BUILD_PROVISION_PROFILE_BASE64`, `KEYCHAIN_PASSWORD`)
  <https://docs.github.com/en/actions/use-cases-and-examples/deploying/installing-an-apple-certificate-on-macos-runners-for-xcode-development>,
  or automatic signing with an App Store Connect API key
  (`-allowProvisioningUpdates -authenticationKeyPath …`, documented "for
  headless environments" since Xcode 13). Secrets are not available to
  fork PRs, so the leg is gated to `push`/`workflow_dispatch`.
- **[verified]** Cost: Blaze gives "30 minutes of test time per day on
  physical devices" free, then "$5 per hour for each physical device",
  per-minute, install time not billed
  <https://firebase.google.com/docs/test-lab/usage-quotas-pricing>; Spark has
  5 physical runs/day without a billing account. GitHub macOS minutes are free
  on a public repository. FTL's newest Xcode note is "Xcode 26.2 is now
  available" (2026-06-10) <https://firebase.google.com/support/releases>;
  26.3+/27 support is **[unverified]** — check `gcloud firebase test ios
  versions list` and pin the build Xcode to a listed id.
- **Alternatives:** BrowserStack App Automate re-signs with a wildcard
  profile (entitlements removed — harmless here), $175/month; its open-source
  programme names Live/Automate/Percy, not App Automate
  <https://www.browserstack.com/open-source>. AWS Device Farm: "$0.17 /
  device minute" after 1,000 free, XCTest cannot skip re-signing
  <https://docs.aws.amazon.com/devicefarm/latest/developerguide/skip-app-re-signing-on-private-devices.html>.
  Sauce Labs $199/month. Xcode Cloud: 25 h/month with the programme, but its
  test destinations are Simulators **[plausible]** — it buys signing and
  TestFlight, not hardware.
- **What stays unverifiable even on green:** it is the farm's re-signed
  bundle on the farm's phone; whether the icon lands on the home screen (not
  the App Library) and SpringBoard automation is permitted there; which
  lifecycle fired. Android hardware is a separate story — Firebase Test Lab
  runs UI Automator instrumentation tests on real Pixels at the same free
  tier, with no signing story at all, which would retire the "tap arriving as
  `Performed` on hardware" caveat for Android cheaply.

## 9. Min SDK note (asked alongside)

The package sets no Android `minSdk` on any Unity line; the testbeds carry
`AndroidMinSdkVersion` 22 (2021, 2022) and 25 (Testbed6). `ShortcutManager`
is API 25+: below it the bridge returns early, `IsPlatformSupported` is false
and every call is a safe no-op. iOS: `@available(iOS 13.0, *)` guards,
deployment target iOS 13 in the docs.

## 10. What to re-check in October 2026

1. The `*-xcode27` canaries: do the 2022.3.62f3 and 6000.3.21f1 exports
   compile under Xcode 27 (read `OTHER_LDFLAGS` in `project.pbxproj` from the
   `ios-simulator-xcodeproj-2022.3` / `-unity6` artifacts and the canary legs'
   xcodebuild logs by hand — no CI step surfaces it), launch under iOS 27,
   and does the coex leg still see the scene hooks?
2. Unity: any release note or staff post naming Xcode 27; Unity's answer for
   public 2022.3 users on the UIScene mandate.
3. GitHub: a `macos-15` deprecation announcement; Xcode 27 GA landing on the
   `xcode-27` image; a `macos-27`/Xcode-keyed GA image.
4. Apple: the exact April 2027 date on the Upcoming Requirements page.
5. The `ios-springboard` results across 2022.3 (iOS 18.6), unity6 (iOS 26.5)
   and the canary (iOS 27): row-label format, icon label, press duration that
   worked — then decide whether the 16.4 leg still earns its place.

## 11. Method and caveats

- Research ran as two Workflow runs (Opus agents): `ios-ui-test-stack-research`
  (3 sweeps) and `xcode-26-ci-policy-research` (4 sweeps + 24 refuters; 28
  agents, 918 tool calls). Refuters were told to default to "refuted" when a
  page did not literally say what a claim said; several claims were corrected
  for paraphrase drift (e.g. "the only staff statement", "exactly the
  testbed") and appear here in their corrected form.
- Unity Discussions blocks scripted fetches; forum quotes came through a
  fetch-and-summarise tool and are marked [plausible] unless the raw Discourse
  JSON was read. The Unity Issue Tracker was read through its JSON API.
- The Wayback Machine was unreachable, so the 2025 (Xcode 16) requirement
  wording is from a developer's quote of Apple's ITMS message.
- Comparison articles ("Maestro vs Appium vs Detox in 2026") were
  deliberately not used for any claim.
