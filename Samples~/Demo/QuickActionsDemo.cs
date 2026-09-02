// Guarded by the package's opt-in define: the QuickActions types only exist when
// QUICKACTIONS_ENABLED is set, so the demo (compiled into Assembly-CSharp) guards
// its call sites the same way your own game code does — see the README. The
// component and its IMGUI shell stay compiled either way: a define-off build of
// this scene then carries a real (inert) component instead of a "missing
// script", and only the package calls come and go with the define.
using System.Collections.Generic;
#if QUICKACTIONS_ENABLED
using EminDeniz99.QuickActions;
#endif
using UnityEngine;

namespace EminDeniz99.QuickActions.DemoSample
{
    /// <summary>
    /// Minimal on-screen demo for the Quick Actions package. Attach to a GameObject
    /// in an otherwise empty scene, build to a device, then long-press the app icon.
    /// Uses IMGUI so it needs no Canvas/EventSystem setup.
    /// </summary>
    public sealed class QuickActionsDemo : MonoBehaviour
    {
        private readonly List<string> _log = new List<string>();
        private Vector2 _scroll;

        // Cache LastPerformed instead of reading it in OnGUI: IMGUI calls OnGUI
        // several times per frame and each LastPerformed read round-trips through the
        // native bridge (a string marshal). It only changes on launch/resume, so
        // refresh it once at startup and whenever a tap arrives.
        private string _lastPerformed;

#if QUICKACTIONS_ENABLED
        private static readonly QuickActionItem[] Catalog =
        {
            new QuickActionItem("new_game", "New Game", "Start fresh", IconType.Add),
            new QuickActionItem("continue", "Continue", "Resume last save", IconType.Play),
            new QuickActionItem("daily", "Daily Reward", "Claim today", IconType.Favorite),
            new QuickActionItem("settings", "Settings", "Tweak options", IconType.Compose),
        };

        private void Awake()
        {
            QuickActions.LoggingEnable = true;
            QuickActions.Performed += OnPerformed;
            _lastPerformed = QuickActions.LastPerformed; // cold-launch value, read once
        }

        private void OnDestroy() => QuickActions.Performed -= OnPerformed;

        // A cold launch is reported through Performed (subscribed in Awake); this
        // demo routes everything through that one channel. LastPerformed is shown
        // in the on-screen label as the pull-based alternative.
        private void OnPerformed(string id)
        {
            _lastPerformed = id;
            Add($"Performed '{id}'");
        }

        private static string Supported => QuickActions.IsPlatformSupported.ToString();

        // The buttons' bodies. "Add 3 shortcuts" is shared with the Android autotest
        // hook below so the automated run can never drift from what a human tap does.
        private void AddThree()
        {
            QuickActions.AddList(new List<QuickActionItem> { Catalog[0], Catalog[1], Catalog[2] });
            Add("Added new_game, continue, daily");
        }

        private void AddSettings() => Add(QuickActions.Add(Catalog[3]) ? "Added settings" : "settings already added");

        private void RemoveDaily() => Add(QuickActions.RemoveById("daily") ? "Removed daily" : "daily not present");

        private void RemoveAll()
        {
            QuickActions.RemoveAll();
            Add("Removed all");
        }

        private void ResetLastPerformed()
        {
            QuickActions.ResetLastPerformed();
            _lastPerformed = null;
            Add("Reset LastPerformed");
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        // Launch-intent string extra that asks this demo to drive itself, set by
        // tools~/device-smoke/android_device_smoke.sh — keep the two in sync.
        private const string AutotestExtra = "com.emindeniz99.quickactions.AUTOTEST";

        // Device-smoke hook: an adb-driven harness cannot tap an IMGUI button, so it
        // launches the app with AUTOTEST=add3 and the demo presses "Add 3 shortcuts"
        // for it. A launch without the extra does nothing here, so normal behaviour
        // is unchanged. It runs in Start rather than Awake to leave the Awake path
        // (subscribe, then read the cold-launch LastPerformed) exactly as a real
        // launch has it — the same path the harness then exercises with a tap.
        // Logging is already on from Awake, which is what the harness asserts on.
        private void Start()
        {
            if (ReadAutotestExtra() == "add3")
                AddThree();
        }

        // A normal launch intent simply has no such extra (null), and the JNI hop can
        // fail outright on an activity that isn't Unity's — the demo has to keep
        // running either way, so every failure degrades to "no autotest".
        private static string ReadAutotestExtra()
        {
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var intent = activity.Call<AndroidJavaObject>("getIntent"))
                    return intent == null ? null : intent.Call<string>("getStringExtra", AutotestExtra);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[QuickActionsDemo] autotest extra not readable: {e.Message}");
                return null;
            }
        }
#endif
#else
        // The define is off: the package's assembly is not compiled, so there is
        // nothing for the buttons to call. The shell below is drawn all the same —
        // that is what keeps a define-off build of this scene free of a "missing
        // script", and what lets CI's gate-off job measure the package's footprint
        // as the package alone: the same IMGUI code on both sides of the diff, so
        // engine-code stripping keeps the same engine modules in both APKs.
        private const string OffLine = "QUICKACTIONS_ENABLED is off — the package is not compiled";

        private void Awake() => Add(OffLine);

        private static string Supported => "n/a (define off)";

        private void AddThree() => Add(OffLine);
        private void AddSettings() => Add(OffLine);
        private void RemoveDaily() => Add(OffLine);
        private void RemoveAll() => Add(OffLine);

        private void ResetLastPerformed()
        {
            _lastPerformed = null;
            Add(OffLine);
        }
#endif

        private void Add(string line)
        {
            _log.Insert(0, line);
            Debug.Log($"[QuickActionsDemo] {line}");
        }

        private void OnGUI()
        {
            const float pad = 16f;
            using (new GUILayout.AreaScope(new Rect(pad, pad, Screen.width - pad * 2, Screen.height - pad * 2)))
            {
                GUILayout.Label($"Quick Actions Demo   supported={Supported}");
                GUILayout.Label($"LastPerformed: {_lastPerformed ?? "(none)"}");

                GUILayout.Space(8);
                if (GUILayout.Button("Add 3 shortcuts", GUILayout.Height(56)))
                    AddThree();
                if (GUILayout.Button("Add 'settings'", GUILayout.Height(56)))
                    AddSettings();
                if (GUILayout.Button("Remove 'daily'", GUILayout.Height(56)))
                    RemoveDaily();
                if (GUILayout.Button("Remove all", GUILayout.Height(56)))
                    RemoveAll();
                if (GUILayout.Button("Reset LastPerformed", GUILayout.Height(56)))
                    ResetLastPerformed();

                GUILayout.Space(8);
                GUILayout.Label("Log:");
                using (var scope = new GUILayout.ScrollViewScope(_scroll))
                {
                    _scroll = scope.scrollPosition;
                    foreach (var line in _log)
                        GUILayout.Label("• " + line);
                }
            }
        }
    }
}
