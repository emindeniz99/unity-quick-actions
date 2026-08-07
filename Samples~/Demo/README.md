# Quick Actions — Demo

Import this sample through **Package Manager ▸ Samples ▸ Import** rather than
copying the files by hand. Unity resolves the scene's script reference by the
GUID in `QuickActionsDemo.cs.meta`; a manual copy that misses the `.meta`
files silently breaks that reference, and the scene loads with the component
gone ("The referenced script on this Behaviour is missing!") — nothing renders
and nothing runs.

Open `QuickActionsDemo.unity` and build to an **iOS or Android device**. Quick
actions do not exist in the Editor. They DO work on the **iOS Simulator**
(verified on iOS 26.5: long-press the app icon on the simulator's home screen
and the shortcuts appear, and tapping one cold-launches the app) — note that
Unity's own player currently renders black in the simulator, so read results
from the console (`xcrun simctl launch --console-pty <udid> <bundle-id>`)
rather than the on-screen log.

- On-screen buttons add/remove dynamic shortcuts at runtime.
- Long-press the app icon on the home screen to see them.
- Tapping a shortcut launches/resumes the app; the action id appears in the
  on-screen log (both cold and warm taps arrive via `Performed`; `LastPerformed` is shown
  as the pull-based alternative).

The scene uses a single `QuickActionsDemo` MonoBehaviour with IMGUI, so no
Canvas or EventSystem is required.
