// Drives SpringBoard on the iOS Simulator the way a finger would: long-press the
// app icon, tap one quick-action row, then read back the marker the testbed
// writes when `QuickActions.Performed` fires. There is no host application —
// SpringBoard and the app under test are both reached through
// XCUIApplication(bundleIdentifier:), which is why the bundle builds unsigned
// and needs no Apple account.
//
// Configuration arrives through the runner's environment: `TEST_RUNNER_QA_*`
// on the xcodebuild command line reaches this process as `QA_*`.
//
//   QA_OUT              directory the evidence is written to (required)
//   QA_APP_ID           bundle id of the app under test   (com.quickactions.testbed)
//   QA_APP_NAME         the icon's label = display name   (QuickActionsDemo)
//   QA_ROW_TITLE        quick-action title to tap         (Daily Reward)
//   QA_ACTION_ID        the id that must reach Performed  (daily_reward)
//   QA_MARKER           host path of the testbed's marker file (unset: SKIPPED
//                       after the launch — delivery cannot be checked)
//   QA_WAIT_SECONDS     seconds the app gets to reach the foreground after
//                       the tap                                        (30)
//   QA_DELIVERY_SECONDS seconds the id gets to reach Performed after the
//                       tap — a cold Unity launch on the Simulator spends
//                       most of its first minute before the first frame
//                       (run 77, 2022.3 / iOS 18.6: 35–43 s)          (120)
//
// The verdict lands in QA_OUT/launcher-tap.txt, one line, with the vocabulary
// of the Android smoke's launcher tap:
//
//   PASS <id> via '<row label>' (<n> s after the tap)
//                                the row was tapped, the app came up, the id arrived
//   SKIPPED <why>                the automation never reached a tap that counts
//   FAIL <why>                   SpringBoard took the tap and nothing arrived, or
//                                its menu opened without the app's quick actions
//
// PASS returns normally, SKIPPED throws XCTSkip (xcodebuild exits 0), FAIL fails
// the test. The automation's own misses are always SKIPPED, never FAIL; the one
// way a run goes red without a FAIL is a test that stops before writing any
// verdict (an XCTest failure such as a lost hit point), which CI reports as
// "no verdict" — pressing and tapping by coordinate, on elements checked for a
// real frame first, exists to make that rare. Every
// step also leaves QA_OUT/NN-<step>.txt (SpringBoard's
// accessibility tree) and NN-<step>.png, so a miss is diagnosable from the
// artifact alone — the first runs exist to learn what SpringBoard exposes.
import XCTest

final class SpringBoardTapUITests: XCTestCase {
    private let env = ProcessInfo.processInfo.environment
    private var out: URL!
    private var step = 0

    override func setUpWithError() throws {
        continueAfterFailure = false
        guard let dir = env["QA_OUT"], !dir.isEmpty else {
            throw XCTSkip("QA_OUT is not set — pass TEST_RUNNER_QA_OUT=<dir> to xcodebuild")
        }
        out = URL(fileURLWithPath: dir, isDirectory: true)
        try FileManager.default.createDirectory(at: out, withIntermediateDirectories: true)
    }

    func testLongPressIconAndTapQuickAction() throws {
        let appId = env["QA_APP_ID"] ?? "com.quickactions.testbed"
        let appName = env["QA_APP_NAME"] ?? "QuickActionsDemo"
        let actionId = env["QA_ACTION_ID"] ?? "daily_reward"
        let rowTitle = env["QA_ROW_TITLE"] ?? "Daily Reward"
        let markerPath = env["QA_MARKER"].flatMap { $0.isEmpty ? nil : $0 }
        let waitSeconds = TimeInterval(env["QA_WAIT_SECONDS"] ?? "") ?? 30
        let deliverySeconds = TimeInterval(env["QA_DELIVERY_SECONDS"] ?? "") ?? 120

        let app = XCUIApplication(bundleIdentifier: appId)
        let springboard = XCUIApplication(bundleIdentifier: "com.apple.springboard")

        // 1. Cold state: the app must not be running, and the home screen must be up.
        if app.state != .notRunning {
            app.terminate()
            _ = app.wait(for: .notRunning, timeout: 15)
        }
        XCUIDevice.shared.press(.home)
        let homeUp = springboard.icons.firstMatch.waitForExistence(timeout: 30)
        dump("home", springboard)
        guard homeUp else {
            try verdict("SKIPPED", "SpringBoard exposed no icons within 30 s")
            return
        }
        note("icons on the home screen: \(iconLabels(springboard))")

        // 2. The icon, by label — the label is the app's display name.
        guard let icon = findIcon(springboard, named: appName) else {
            dump("no-icon", springboard)
            try verdict("SKIPPED",
                        "no home-screen icon labelled '\(appName)' (icons: \(iconLabels(springboard)))")
            return
        }

        // 3. Long-press with the adaptive duration flutter/packages' quick_actions_ios
        // UI test settled on after two years of flakes: start at 1.5 s; a press that
        // lands in the jiggle/edit state was too long, one that opens nothing was too
        // short; ±0.2 s, four attempts.
        // A quick-action row is a Button whose identifier is the shortcut's type
        // — the id itself — and whose label is "Title, Subtitle" (run 77, iOS
        // 18.6: identifier 'daily_reward', label 'Daily Reward, Claim today's
        // gift'); the label prefix is the fallback for a SpringBoard that does
        // not expose the identifier.
        let rows = springboard.buttons.matching(
            NSPredicate(format: "identifier == %@ OR label BEGINSWITH[c] %@", actionId, rowTitle))
        let menuMarkers = ["Remove App", "Edit Home Screen", "Share App", "Delete App"]
        var duration: TimeInterval = 1.5
        var row: XCUIElement?
        var attempts: [String] = []
        for attempt in 1...4 {
            // The Home press that ends an attempt puts SpringBoard back on page 1;
            // the icon's page has to be brought back before the next press.
            guard bringOnScreen(icon, in: springboard) else {
                try verdict("SKIPPED", "the icon left the screen between attempts and four swipes did not bring it back")
                return
            }
            // By coordinate, not by element: a coordinate press carries no
            // hittability check, and SpringBoard's icons answer isHittable with
            // false even when laid out on the current page (run 76, both legs).
            center(of: icon).press(forDuration: duration)
            let opened = rows.firstMatch.waitForExistence(timeout: 5)
            dump("press-\(attempt)", springboard)
            if opened {
                row = rows.firstMatch
                attempts.append("\(attempt): \(duration) s opened the menu")
                break
            }
            if springboard.buttons["Done"].exists {
                attempts.append("\(attempt): \(duration) s entered edit mode (too long)")
                let done = springboard.buttons["Done"]
                if hasFrame(done) { center(of: done).tap() } // else the Home press below leaves edit mode
                duration = max(0.6, duration - 0.2)
            } else if menuMarkers.contains(where: { springboard.buttons[$0].exists }) {
                // The context menu is open, but with no row starting with our title:
                // SpringBoard showed the app's menu without its quick actions. That is
                // a finding about the app, not about the automation.
                let labels = buttonLabels(springboard)
                XCUIDevice.shared.press(.home)
                try verdict("FAIL",
                            "the context menu opened without a row titled '\(rowTitle)' (buttons: \(labels))")
                return
            } else {
                attempts.append("\(attempt): \(duration) s opened nothing (too short)")
                duration += 0.2
            }
            XCUIDevice.shared.press(.home)
            Thread.sleep(forTimeInterval: 1)
        }
        guard let target = row else {
            try verdict("SKIPPED",
                        "the long press never opened the quick-action menu (\(attempts.joined(separator: "; ")))")
            return
        }
        note("press attempts: \(attempts.joined(separator: "; "))")

        // 4. The tap. Everything above is automation; from here on a miss is
        // SpringBoard's or the app's.
        let label = target.label
        guard hasFrame(target) else {
            try verdict("SKIPPED", "'\(label)' exists but has no frame to tap")
            return
        }
        let before = markerLines(markerPath)
        let tapCalled = Date()
        center(of: target).tap()
        // XCUITest waits for SpringBoard to go idle before it synthesizes the
        // touch and again after it, up to 60 s each, and SpringBoard is not
        // idle while the context menu's blur animates (run 77: the call returned
        // 63 s after it was made, the touch itself landed 3 s before the return).
        // Everything below is timed from the return, i.e. from the touch.
        let tapped = Date()
        note("the tap call returned \(Int(tapped.timeIntervalSince(tapCalled))) s after it was made")
        let launched = app.wait(for: .runningForeground, timeout: waitSeconds)
        note("foreground \(launched ? "reached" : "not reached") \(Int(Date().timeIntervalSince(tapped))) s after the tap")
        dump("after-tap", springboard)
        if !launched {
            if rows.firstMatch.exists {
                try verdict("SKIPPED", "the tap did not register — '\(label)' is still on screen")
                return
            }
            try verdict("FAIL",
                        "'\(label)' was tapped and the menu closed, but \(appId) never reached the foreground within \(Int(waitSeconds)) s")
            return
        }

        // 5. Delivery: the testbed appends every id that reaches Performed to its
        // marker file, and only a line written AFTER the tap counts.
        guard let marker = markerPath else {
            try verdict("SKIPPED",
                        "\(appId) launched via '\(label)', but QA_MARKER is not set — delivery not checked")
            return
        }
        // A cold Unity launch spends most of its first minute before the first
        // frame, and the runtime dispatches the launch id one frame after its
        // bootstrap: run 77 (2022.3 / iOS 18.6) delivered 35–43 s after the
        // touch, past the 30 s window this loop had then. The last read is the
        // one the verdict quotes, so a FAIL can never report a marker that
        // already holds the id, which is exactly what that run's FAIL did.
        let deadline = tapped.addingTimeInterval(deliverySeconds)
        var seen = markerLines(marker)
        while !(seen.count > before.count && seen[before.count...].contains(actionId)) {
            guard Date() < deadline else {
                dump("not-delivered", app)
                try verdict("FAIL",
                            "\(appId) came to the foreground but '\(actionId)' never reached Performed within \(Int(deliverySeconds)) s of the tap (marker \(marker): \(seen))")
                return
            }
            Thread.sleep(forTimeInterval: 1)
            seen = markerLines(marker)
        }
        let deliveredAfter = Int(Date().timeIntervalSince(tapped))
        dump("delivered", app)
        try verdict("PASS", "\(actionId) via '\(label)' (\(deliveredAfter) s after the tap)")
    }

    // MARK: - SpringBoard helpers

    /// The app icon by exact label first, then by prefix (SpringBoard may append
    /// state to the label), then brought onto the screen: every icon of every
    /// home-screen page is in the tree, but an icon on a page that is not the
    /// current one reports a ZERO frame (run 76: the app landed on page 2 of 2 on
    /// both iOS 18.6 and 26.5), so the test swipes left until the frame is real
    /// and inside the screen. Nil when nothing matches or four swipes did not
    /// reach it.
    private func findIcon(_ springboard: XCUIApplication, named name: String) -> XCUIElement? {
        var icon = springboard.icons[name].firstMatch
        if !icon.waitForExistence(timeout: 10) {
            let byPrefix = springboard.icons.matching(
                NSPredicate(format: "label BEGINSWITH[c] %@", name)).firstMatch
            guard byPrefix.waitForExistence(timeout: 5) else { return nil }
            icon = byPrefix
        }
        return bringOnScreen(icon, in: springboard) ? icon : nil
    }

    /// Swipes left until the element has a real frame inside the screen — an icon
    /// on another home-screen page reports a zero frame — at most four pages.
    private func bringOnScreen(_ element: XCUIElement, in app: XCUIApplication) -> Bool {
        var swipes = 0
        while !isOnScreen(element, in: app), swipes < 4 {
            app.swipeLeft()
            swipes += 1
            Thread.sleep(forTimeInterval: 1)
        }
        if swipes > 0 {
            note("swiped \(swipes) page(s) to reach '\(element.label)'; frame now \(element.frame)")
            dump("page-\(swipes + 1)", app)
        }
        return isOnScreen(element, in: app)
    }

    private func hasFrame(_ element: XCUIElement) -> Bool {
        let frame = element.frame
        return frame.width > 0 && frame.height > 0
    }

    private func isOnScreen(_ element: XCUIElement, in app: XCUIApplication) -> Bool {
        hasFrame(element) && app.frame.contains(element.frame)
    }

    /// The element's centre as a coordinate: XCUICoordinate taps and presses
    /// synthesize the touch at that point with no hittability check.
    private func center(of element: XCUIElement) -> XCUICoordinate {
        element.coordinate(withNormalizedOffset: CGVector(dx: 0.5, dy: 0.5))
    }

    private func iconLabels(_ springboard: XCUIApplication) -> [String] {
        Array(springboard.icons.allElementsBoundByIndex.map(\.label).filter { !$0.isEmpty }.prefix(60))
    }

    private func buttonLabels(_ springboard: XCUIApplication) -> [String] {
        Array(springboard.buttons.allElementsBoundByIndex.map(\.label).filter { !$0.isEmpty }.prefix(60))
    }

    // MARK: - Evidence

    /// The accessibility tree and a screenshot, numbered in the order taken, both
    /// as files under QA_OUT and as attachments in the .xcresult.
    private func dump(_ name: String, _ app: XCUIApplication) {
        step += 1
        let stem = String(format: "%02d-%@", step, name)
        let tree = app.debugDescription
        let shot = XCUIScreen.main.screenshot()
        try? tree.write(to: out.appendingPathComponent("\(stem).txt"), atomically: true, encoding: .utf8)
        try? shot.pngRepresentation.write(to: out.appendingPathComponent("\(stem).png"))
        let treeAttachment = XCTAttachment(string: tree)
        treeAttachment.name = "\(stem).txt"
        treeAttachment.lifetime = .keepAlways
        add(treeAttachment)
        let shotAttachment = XCTAttachment(screenshot: shot)
        shotAttachment.name = "\(stem).png"
        shotAttachment.lifetime = .keepAlways
        add(shotAttachment)
    }

    private func note(_ text: String) {
        print("[springboard-tap] \(text)")
        let url = out.appendingPathComponent("notes.txt")
        let line = text + "\n"
        if let handle = try? FileHandle(forWritingTo: url) {
            handle.seekToEndOfFile()
            handle.write(Data(line.utf8))
            handle.closeFile()
        } else {
            try? line.write(to: url, atomically: true, encoding: .utf8)
        }
    }

    /// Writes the one-line verdict, then ends the test the way the verdict says.
    private func verdict(_ kind: String, _ text: String) throws {
        let line = "\(kind) \(text)"
        note("verdict: \(line)")
        try? (line + "\n").write(to: out.appendingPathComponent("launcher-tap.txt"),
                                 atomically: true, encoding: .utf8)
        switch kind {
        case "SKIPPED": throw XCTSkip(text)
        case "FAIL": XCTFail(text)
        default: break
        }
    }

    private func markerLines(_ path: String?) -> [String] {
        guard let path = path, let text = try? String(contentsOfFile: path, encoding: .utf8) else {
            return []
        }
        return text.split(separator: "\n").map(String.init).filter { !$0.isEmpty }
    }
}
