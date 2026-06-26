# Quick Actions — Demo

Open `QuickActionsDemo.unity` and build to an **iOS or Android device** (quick
actions do not exist in the Editor or on simulators without home-screen access).

- On-screen buttons add/remove dynamic shortcuts at runtime.
- Long-press the app icon on the home screen to see them.
- Tapping a shortcut launches/resumes the app; the action id appears in the
  on-screen log (both cold and warm taps arrive via `Performed`; `LastPerformed` is shown
  as the pull-based alternative).

The scene uses a single `QuickActionsDemo` MonoBehaviour with IMGUI, so no
Canvas or EventSystem is required.
