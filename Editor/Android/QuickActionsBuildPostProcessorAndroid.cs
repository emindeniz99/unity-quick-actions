// Gated by the Editor.Android asmdef's defineConstraints (UNITY_ANDROID), so it
// only compiles when Android is the active build target and the Android editor
// extension exists.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using EminDeniz99.QuickActions;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace EminDeniz99.QuickActions.Editor
{
    /// <summary>
    /// Bakes the static shortcuts from <see cref="QuickActionsSettings"/> — as
    /// prepared by <see cref="QuickActionsStaticBuild"/> (Customize hook +
    /// <c>{placeholder}</c> interpolation) — into the generated Gradle project:
    /// writes <c>res/xml/quickactions_shortcuts.xml</c> (+ the required string
    /// resources) and injects the <c>android.app.shortcuts</c> meta-data into
    /// the launcher activity.
    ///
    /// Static shortcut intents target <see cref="QuickActionsTrampolineActivity"/>
    /// and encode the action id in the intent action (XML shortcut intents cannot
    /// carry extras); the trampoline decodes it. Guarded by <c>UNITY_ANDROID</c>.
    /// </summary>
    internal sealed class QuickActionsBuildPostProcessorAndroid
        : IPostGenerateGradleAndroidProject, IPreprocessBuildWithReport
    {
        private const string AndroidNs = "http://schemas.android.com/apk/res/android";
        private const string ActionPrefix = "com.emindeniz99.quickactions.PERFORM.";
        private const string TrampolineClass = "com.emindeniz99.quickactions.QuickActionsTrampolineActivity";
        private const string ShortcutsResource = "quickactions_shortcuts";
        private const string StringsResource = "quickactions_strings";
        private const string KeepResource = "quickactions_keep";
        // Must stay in lock-step with QuickActionsBridge.java's lookup — the
        // ic_quickaction_ + <catalog name> concatenation this keep rule shields.
        // Pinned cross-language by tools~/check_frozen_strings.py, whose check
        // matches the QUOTED literal: keep this comment unquoted, or the pin is
        // satisfied by the comment and stops noticing the const drifting.
        internal const string IconPrefix = "ic_quickaction_";

        public int callbackOrder => 100;

        // The Development flag of the build ACTUALLY running. The Gradle callback
        // gets no BuildReport, and EditorUserBuildSettings.development is only the
        // persisted Build Settings checkbox — a scripted BuildPlayer(options:
        // Development) doesn't sync it, so reading the checkbox here would hand
        // Customize subscribers the wrong flag on exactly the CI builds the
        // dev-only recipe targets (the iOS baker reads report.summary.options;
        // the platforms must agree). Captured in OnPreprocessBuild, which Unity
        // runs for the same build earlier in the same domain; the checkbox is
        // only the fallback for a state where no preprocess ever fired.
        private static bool? s_developmentBuild;

        public void OnPreprocessBuild(BuildReport report) =>
            s_developmentBuild = (report.summary.options & BuildOptions.Development) != 0;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            // FIRST, before every early return below and before any launcher
            // discovery: icons are resolved BY NAME at runtime
            // (getIdentifier("ic_quickaction_…")), so with minifyEnabled +
            // shrinkResources nothing statically references them. AGP's DEFAULT
            // "safe" mode does carry a heuristic that retains resources whose
            // names start with a string constant used near a getIdentifier call,
            // so the catalog names are LIKELY kept there — but that is an
            // implementation detail, not a contract, and any single library in
            // the consuming app carrying tools:shrinkMode="strict" flips the
            // WHOLE app to strict mode, where nothing name-resolved survives and
            // a package cannot opt out. This rule makes the catalog prefix immune
            // to both. It is written independently of the static set because a
            // project with ZERO static shortcuts still adds icons at runtime —
            // the dynamic-only case is exactly the one the returns below skip.
            WriteKeepRules(path);

            // The applicationId also feeds the {bundleId} placeholder, so a label
            // and the intent it sits next to can never disagree about the id.
            var appId = ResolveApplicationId(path);
            // The baked set is the PREPARED one — settings copies run through the
            // Customize hook, then {placeholder} interpolation — not the raw asset.
            var shortcuts = QuickActionsStaticBuild.Prepare(BuildTarget.Android,
                s_developmentBuild ?? EditorUserBuildSettings.development,
                new Dictionary<string, string> { ["bundleId"] = appId });
            var hasShortcuts = shortcuts.Count > 0;

            // The launcher activity (MAIN/LAUNCHER) may live in the unityLibrary
            // module (given here) or the sibling launcher module. Resources and the
            // meta-data must go in whichever module declares it.
            var modules = new[] { path, Path.GetFullPath(Path.Combine(path, "..", "launcher")) };
            string manifestPath = null, moduleDir = null;
            foreach (var module in modules)
            {
                var candidate = Path.Combine(module, "src", "main", "AndroidManifest.xml");
                if (File.Exists(candidate) && HasLauncherActivity(candidate))
                {
                    manifestPath = candidate;
                    moduleDir = module;
                    break;
                }
            }
            if (manifestPath == null)
            {
                if (hasShortcuts)
                    Debug.LogWarning("[QuickActions] No launcher activity found; skipping static shortcuts.");
                return;
            }

            if (!hasShortcuts)
            {
                // No static shortcuts to bake. A reused/exported Gradle project may
                // still hold the res files + meta-data a previous build wrote; remove
                // them so stale long-press shortcuts don't ship. (Parity with iOS.)
                RemoveGeneratedShortcuts(moduleDir, manifestPath);
                return;
            }

            // Log what was actually WRITTEN, not the prepared count: the writer
            // still skips empty/duplicate items (an interpolated-to-empty title
            // included), and claiming those were written would be a lie.
            var written = WriteResources(moduleDir, shortcuts, appId);
            if (InjectMetaData(manifestPath))
                Debug.Log($"[QuickActions] Wrote {written} static shortcut(s) to the Android project.");
        }

        // Removes the generated shortcut resources and the launcher meta-data so a
        // reused build directory doesn't keep shipping shortcuts after they're all
        // removed from the settings (mirrors the iOS post-processor's stale-plist clear).
        //
        // Deliberately NOT the keep file: its lifetime is tied to the DEFINE, not to
        // the static set. With zero static shortcuts the package is still live and
        // dynamic icons still resolve by name, so the keep rule must stay. Only the
        // define-off stripper (QuickActionsTrampolineStripperAndroid) removes it.
        internal static void RemoveGeneratedShortcuts(string moduleDir, string manifestPath)
        {
            var removedMeta = RemoveShortcutsMetaData(manifestPath);
            var xml = Path.Combine(moduleDir, "src", "main", "res", "xml", ShortcutsResource + ".xml");
            var strings = Path.Combine(moduleDir, "src", "main", "res", "values", StringsResource + ".xml");
            var removedFiles = SafeDelete(xml) | SafeDelete(strings);
            foreach (var localized in GeneratedLocalizedStringFiles(moduleDir))
                removedFiles |= SafeDelete(localized);
            if (removedMeta || removedFiles)
                Debug.Log("[QuickActions] Cleared stale static-shortcut output (no static shortcuts configured).");
        }

        // Removes the android.app.shortcuts meta-data from the launcher activity.
        // Returns true if anything was removed. Shared shape with the ungated stripper.
        internal static bool RemoveShortcutsMetaData(string manifestPath)
        {
            if (!File.Exists(manifestPath))
                return false;
            var doc = new XmlDocument();
            doc.Load(manifestPath);
            var removed = false;
            foreach (var meta in doc.GetElementsByTagName("meta-data").Cast<XmlElement>().ToList())
            {
                // Only remove OUR meta-data (resource == @xml/quickactions_shortcuts);
                // never touch a host app's own android.app.shortcuts declaration.
                if (meta.GetAttribute("name", AndroidNs) == "android.app.shortcuts" &&
                    meta.GetAttribute("resource", AndroidNs) == "@xml/" + ShortcutsResource)
                {
                    meta.ParentNode.RemoveChild(meta);
                    removed = true;
                }
            }
            if (removed)
                doc.Save(manifestPath);
            return removed;
        }

        // The per-locale string files a previous build of THIS package wrote. Ours
        // by exact file name inside a values-* directory — a host app's own
        // values-fr/strings.xml sits in the same folder and is never touched.
        private static IEnumerable<string> GeneratedLocalizedStringFiles(string moduleDir)
        {
            var resDir = Path.Combine(moduleDir, "src", "main", "res");
            if (!Directory.Exists(resDir))
                yield break;
            foreach (var dir in Directory.GetDirectories(resDir, "values-*"))
            {
                var file = Path.Combine(dir, StringsResource + ".xml");
                if (File.Exists(file))
                    yield return file;
            }
        }

        internal static bool SafeDelete(string filePath)
        {
            if (!File.Exists(filePath))
                return false;
            try { File.Delete(filePath); return true; }
            catch { return false; }
        }

        // The static intent targets the trampoline by explicit package+class, so it
        // must use the REAL shipping applicationId. A Gradle `applicationId` override
        // (common in prod / flavors) isn't reflected by PlayerSettings, so prefer the
        // launcher module's build.gradle value and only fall back to the Player setting.
        private static string ResolveApplicationId(string unityLibraryPath)
        {
            try
            {
                var gradle = Path.GetFullPath(Path.Combine(unityLibraryPath, "..", "launcher", "build.gradle"));
                if (File.Exists(gradle))
                {
                    var m = System.Text.RegularExpressions.Regex.Match(
                        File.ReadAllText(gradle), @"applicationId\s+['""]([^'""]+)['""]");
                    if (m.Success)
                        return m.Groups[1].Value;
                }
            }
            catch
            {
                // fall through to the Player setting
            }
            return PlayerSettings.applicationIdentifier;
        }

        private static bool HasLauncherActivity(string manifestPath)
        {
            var doc = new XmlDocument();
            doc.Load(manifestPath);
            return FindLauncherActivity(doc) != null;
        }

        private static XmlElement FindLauncherActivity(XmlDocument doc)
        {
            // The MAIN/LAUNCHER entry can be a plain <activity> OR an <activity-alias>
            // (aliases can own the launcher filter + meta-data). Search both, so a
            // manifest that exposes its launcher via an alias isn't skipped.
            foreach (var tag in new[] { "activity", "activity-alias" })
            {
                foreach (XmlElement component in doc.GetElementsByTagName(tag))
                {
                    foreach (XmlElement filter in component.GetElementsByTagName("intent-filter"))
                    {
                        var hasMain = false;
                        var hasLauncher = false;
                        foreach (XmlElement action in filter.GetElementsByTagName("action"))
                            hasMain |= action.GetAttribute("name", AndroidNs) == "android.intent.action.MAIN";
                        foreach (XmlElement category in filter.GetElementsByTagName("category"))
                            hasLauncher |= category.GetAttribute("name", AndroidNs) == "android.intent.category.LAUNCHER";
                        if (hasMain && hasLauncher)
                            return component;
                    }
                }
            }
            return null;
        }

        private const string ToolsNs = "http://schemas.android.com/tools";

        // Written into unityLibrary (the module this callback receives) — keep rules
        // are GLOBAL to the app's shrink analysis over the merged resource set, so
        // the file need not sit next to the drawables it protects. The unique
        // (package-scoped) file name is required: keep files merge by name, and a
        // host app's own res/raw/keep.xml must never collide with ours.
        //
        // No tools:shrinkMode is emitted: switching the host app's shrinker between
        // safe and strict is the app's decision, never a package's. No try/catch
        // either — the sibling writers don't have one, and a res/ write that fails
        // means the Gradle build is doomed anyway; failing loudly beats the silent
        // blank-icon state this file exists to prevent.
        internal static void WriteKeepRules(string moduleDir)
        {
            var rawDir = Path.Combine(moduleDir, "src", "main", "res", "raw");
            Directory.CreateDirectory(rawDir);
            File.WriteAllText(Path.Combine(rawDir, KeepResource + ".xml"),
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<resources xmlns:tools=\"" + ToolsNs + "\"\n" +
                "    tools:keep=\"@drawable/" + IconPrefix + "*\" />\n");
        }

        // Returns the number of shortcuts actually written (invalid/duplicate
        // items are skipped).
        private static int WriteResources(string moduleDir, IReadOnlyList<QuickActionItem> items, string appId)
        {
            var resDir = Path.Combine(moduleDir, "src", "main", "res");
            var xmlDir = Path.Combine(resDir, "xml");
            var valuesDir = Path.Combine(resDir, "values");
            Directory.CreateDirectory(xmlDir);
            Directory.CreateDirectory(valuesDir);

            // Clear the per-locale files a previous build wrote before regenerating:
            // a locale dropped from the settings must stop shipping, exactly like a
            // dropped shortcut does (same marker-by-file-name scoping as above).
            foreach (var stale in GeneratedLocalizedStringFiles(moduleDir))
                SafeDelete(stale);

            // qualifier ("fr", "pt-rBR", "b+zh+Hans") -> the <string> lines that
            // locale overrides. Android resolves resources per STRING, so each file
            // carries only what that locale actually translates and everything else
            // falls back to the default values/ file below.
            // Case-insensitive keys because the key becomes a DIRECTORY name and the
            // Editor runs on case-insensitive macOS/Windows filesystems, where two
            // spellings of one qualifier ARE one directory and the second WriteAllText
            // silently destroys the first bucket. ResourceQualifier already
            // canonicalizes casing, so this is the belt to that braces.
            var localized = new SortedDictionary<string, StringBuilder>(
                System.StringComparer.OrdinalIgnoreCase);
            // "<qualifier>/<resource name>" pairs already emitted. aapt2 HARD-FAILS the
            // build on a duplicate <string name> under one config, so a settings list
            // with two rows for the same locale (trivially produced by the inspector's
            // "+" button, which clones the previous element) must not reach it.
            var emitted = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

            var shortcuts = new StringBuilder();
            shortcuts.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            shortcuts.AppendLine("<shortcuts xmlns:android=\"http://schemas.android.com/apk/res/android\">");

            var strings = new StringBuilder();
            strings.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            strings.AppendLine("<resources>");

            var seen = new HashSet<string>();
            var index = 0;
            foreach (var item in items)
            {
                if (item == null || string.IsNullOrEmpty(item.Id) || string.IsNullOrEmpty(item.Title))
                    continue;

                if (!seen.Add(item.Id))
                    continue; // skip genuine duplicate ids

                // Resource names are index-based so they are unique regardless of
                // how the (raw) id is spelled; the shortcutId/action keep the raw id.
                var shortName = "qa_short_" + index;
                var longName = "qa_long_" + index;
                index++;
                var subtitle = string.IsNullOrEmpty(item.Subtitle) ? item.Title : item.Subtitle;
                strings.AppendLine($"  <string name=\"{shortName}\" formatted=\"false\">{EscapeResValue(item.Title)}</string>");
                strings.AppendLine($"  <string name=\"{longName}\" formatted=\"false\">{EscapeResValue(subtitle)}</string>");
                // Same two resource names, per locale: the launcher then renders a
                // baked shortcut in the device language without the app running.
                AppendLocalized(localized, emitted, item.LocalizedTitles, shortName, item.Id);
                // The long label mirrors the rule above — with no base subtitle it IS
                // the title — so an item that only translates its title must translate
                // both resources, or one of the two labels renders in the base language.
                var localizedLong = item.LocalizedSubtitles;
                if ((localizedLong == null || localizedLong.Count == 0) && string.IsNullOrEmpty(item.Subtitle))
                    localizedLong = item.LocalizedTitles;
                AppendLocalized(localized, emitted, localizedLong, longName, item.Id);

                shortcuts.AppendLine("  <shortcut");
                shortcuts.AppendLine($"      android:shortcutId=\"{Escape(item.Id)}\"");
                shortcuts.AppendLine("      android:enabled=\"true\"");
                if (!string.IsNullOrEmpty(item.AndroidDrawable))
                    shortcuts.AppendLine($"      android:icon=\"@drawable/{Escape(item.AndroidDrawable)}\"");
                else if (item.Icon != IconType.None)
                    // Unlike iOS (system-icon enum) and the Android *dynamic* path, a
                    // static res/xml shortcut can only reference a drawable resource, and
                    // emitting an unresolved @drawable would hard-fail aapt2. Warn instead
                    // of silently shipping no icon.
                    Debug.LogWarning($"[QuickActions] Static shortcut '{item.Id}' has Icon={item.Icon} but no " +
                        "AndroidDrawable; Android static shortcuts need a drawable resource name (its icon " +
                        "will be blank). Set AndroidDrawable to a bundled drawable, or add it at runtime with QuickActions.Add(...).");
                shortcuts.AppendLine($"      android:shortcutShortLabel=\"@string/{shortName}\"");
                shortcuts.AppendLine($"      android:shortcutLongLabel=\"@string/{longName}\">");
                shortcuts.AppendLine("    <intent");
                shortcuts.AppendLine($"        android:action=\"{Escape(ActionPrefix + item.Id)}\"");
                shortcuts.AppendLine($"        android:targetPackage=\"{Escape(appId)}\"");
                shortcuts.AppendLine($"        android:targetClass=\"{TrampolineClass}\" />");
                shortcuts.AppendLine("  </shortcut>");
            }

            shortcuts.AppendLine("</shortcuts>");
            strings.AppendLine("</resources>");

            File.WriteAllText(Path.Combine(xmlDir, ShortcutsResource + ".xml"), shortcuts.ToString());
            File.WriteAllText(Path.Combine(valuesDir, StringsResource + ".xml"), strings.ToString());

            foreach (var locale in localized)
            {
                var localeDir = Path.Combine(resDir, "values-" + locale.Key);
                Directory.CreateDirectory(localeDir);
                var file = new StringBuilder();
                file.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
                file.AppendLine("<resources>");
                file.Append(locale.Value);
                file.AppendLine("</resources>");
                File.WriteAllText(Path.Combine(localeDir, StringsResource + ".xml"), file.ToString());
            }
            if (localized.Count > 0)
                Debug.Log($"[QuickActions] Wrote localized static-shortcut labels for {localized.Count} locale(s).");
            return index;
        }

        // Adds one item's per-locale labels to the per-qualifier buckets, skipping
        // rows the runtime resolver would skip too (blank locale/text) so the build
        // output can't disagree with what a dynamic shortcut would render.
        // internal (like ResourceQualifier) so the harness can drive it directly:
        // everything it guards against — duplicate resource names, colliding
        // qualifiers — only shows up as an aapt2 error in a real Gradle build.
        internal static void AppendLocalized(IDictionary<string, StringBuilder> localized,
            HashSet<string> emitted, List<LocalizedText> entries, string resourceName, string id)
        {
            if (entries == null)
                return;
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.Locale) || string.IsNullOrEmpty(entry.Text))
                    continue;
                var qualifier = ResourceQualifier(entry.Locale);
                if (qualifier == null)
                {
                    // Warn instead of emitting a directory aapt2 would reject (which
                    // fails the whole build over one bad row).
                    Debug.LogWarning($"[QuickActions] Static shortcut '{id}': locale '{entry.Locale}' is not a usable " +
                        "Android resource qualifier; that translation was skipped (the base label still ships).");
                    continue;
                }
                if (!emitted.Add(qualifier + "/" + resourceName))
                {
                    // Two rows resolving to ONE locale for one label. Emitting both
                    // would put two <string name="qa_short_N"> under one config and
                    // aapt2 fails the whole build with "duplicate value for resource".
                    // First entry wins, which is also what the runtime resolver does
                    // (Find returns the first usable match), so a static shortcut and a
                    // dynamic one with the same table still render the same label.
                    Debug.LogWarning($"[QuickActions] Static shortcut '{id}': locale '{entry.Locale}' resolves to " +
                        $"'{qualifier}', which already has a translation for this label; kept the first and skipped " +
                        "this one. Remove the duplicate row (locales are matched case-insensitively).");
                    continue;
                }
                if (!localized.TryGetValue(qualifier, out var body))
                    localized[qualifier] = body = new StringBuilder();
                body.AppendLine($"  <string name=\"{resourceName}\" formatted=\"false\">{EscapeResValue(entry.Text)}</string>");
            }
        }

        // Locale tag -> Android resource-directory qualifier (the part after
        // "values-"): "fr" -> "fr", "pt-BR" -> "pt-rBR" (the classic language+region
        // form), anything richer -> the BCP-47 form "b+zh+Hans", which is the ONLY
        // shape aapt2 accepts for script/variant subtags (API 21+; shortcuts
        // themselves need API 25, so that floor is never the binding constraint).
        // Null for a tag that can't be a directory name — the caller warns and skips
        // rather than emitting a resource folder that fails the build.
        //
        // The output is CANONICALLY cased (language lower, script Title, region
        // upper), not the caller's spelling. BCP-47 casing is conventional, not
        // semantic — aapt2 folds "b+zh+Hans" and "b+zh+hans" to the same resource
        // config, and the runtime resolver matches locales case-insensitively — so two
        // spellings of one locale MUST land on one qualifier here. Left verbatim they
        // produced two directories that are one directory on the case-insensitive
        // filesystems Unity Editors run on, and the second write silently destroyed
        // the first locale's labels.
        internal static string ResourceQualifier(string locale)
        {
            if (string.IsNullOrEmpty(locale))
                return null;
            var parts = locale.Split('-');
            foreach (var part in parts)
                if (part.Length == 0 || !IsAsciiAlphanumeric(part))
                    return null;
            var language = parts[0].ToLowerInvariant();
            if (language.Length < 2 || language.Length > 3 || !IsAsciiAlpha(language))
                return null;
            if (parts.Length == 1)
                return language;
            if (parts.Length == 2 && parts[1].Length == 2 && IsAsciiAlpha(parts[1]))
                return language + "-r" + parts[1].ToUpperInvariant();
            var bcp47 = new StringBuilder("b+").Append(language);
            for (var i = 1; i < parts.Length; i++)
                bcp47.Append('+').Append(CanonicalSubtag(parts[i]));
            return bcp47.ToString();
        }

        // BCP-47 subtag conventions, applied so one locale has one spelling here:
        // 4 letters = a script subtag (Titlecase, "Hans"), 2 letters = a region
        // subtag (uppercase, "BR"); everything else — 3-digit UN regions, variants,
        // extensions — lowercases, which is both the convention and a no-op for digits.
        private static string CanonicalSubtag(string part)
        {
            if (part.Length == 4 && IsAsciiAlpha(part))
                return char.ToUpperInvariant(part[0]) + part.Substring(1).ToLowerInvariant();
            if (part.Length == 2 && IsAsciiAlpha(part))
                return part.ToUpperInvariant();
            return part.ToLowerInvariant();
        }

        private static bool IsAsciiAlpha(string value)
        {
            foreach (var c in value)
                if ((c < 'a' || c > 'z') && (c < 'A' || c > 'Z'))
                    return false;
            return true;
        }

        private static bool IsAsciiAlphanumeric(string value)
        {
            foreach (var c in value)
                if ((c < 'a' || c > 'z') && (c < 'A' || c > 'Z') && (c < '0' || c > '9'))
                    return false;
            return true;
        }

        // Returns true when our shortcuts meta-data is present after the call (freshly
        // injected or already ours); false when the launcher already declares a DIFFERENT
        // android.app.shortcuts resource, in which case we warn instead of silently
        // shipping none (Android allows only one shortcuts resource per activity).
        private static bool InjectMetaData(string manifestPath)
        {
            var doc = new XmlDocument();
            doc.Load(manifestPath);

            var activity = FindLauncherActivity(doc);
            if (activity == null)
                return false;

            // Scan the launcher's android.app.shortcuts meta-data: ours (our resource)
            // and/or a host's (a different resource). Collect both before mutating so we
            // can resolve an Append-build state that has BOTH.
            XmlElement ours = null;
            XmlElement host = null;
            foreach (XmlElement meta in activity.GetElementsByTagName("meta-data"))
            {
                if (meta.GetAttribute("name", AndroidNs) != "android.app.shortcuts")
                    continue;
                if (meta.GetAttribute("resource", AndroidNs) == "@xml/" + ShortcutsResource)
                    ours = meta;
                else
                    host = meta;
            }

            if (host != null)
            {
                // A host app / another plugin owns the single shortcuts slot. Drop any
                // stale meta-data WE injected on a prior build first, so the manifest
                // isn't left with two android.app.shortcuts resources (invalid — Android
                // allows one per activity), then warn rather than silently ship none.
                if (ours != null)
                {
                    ours.ParentNode.RemoveChild(ours);
                    doc.Save(manifestPath);
                }
                Debug.LogWarning(
                    $"[QuickActions] The launcher activity already declares android.app.shortcuts " +
                    $"(resource={host.GetAttribute("resource", AndroidNs)}); the configured static shortcuts were NOT injected " +
                    $"(Android allows only one shortcuts resource per activity). Merge them into " +
                    $"that resource manually, or register them at runtime with QuickActions.Add(...).");
                return false;
            }

            if (ours != null)
                return true; // already ours — idempotent (Append re-runs)

            var element = doc.CreateElement("meta-data");
            SetAndroidAttr(doc, element, "name", "android.app.shortcuts");
            SetAndroidAttr(doc, element, "resource", "@xml/" + ShortcutsResource);
            activity.AppendChild(element);
            doc.Save(manifestPath);
            return true;
        }

        private static void SetAndroidAttr(XmlDocument doc, XmlElement element, string name, string value)
        {
            var attr = doc.CreateAttribute("android", name, AndroidNs);
            attr.Value = value;
            element.SetAttributeNode(attr);
        }

        private static string Escape(string value)
        {
            var sb = new StringBuilder(value.Length);
            foreach (var c in value)
            {
                // Drop XML-1.0-illegal control chars (also keeps the action-encoded
                // id single-line so the trampoline's prefix match stays intact).
                if (c < 0x20)
                    continue;
                switch (c)
                {
                    case '&': sb.Append("&amp;"); break;
                    case '<': sb.Append("&lt;"); break;
                    case '>': sb.Append("&gt;"); break;
                    case '"': sb.Append("&quot;"); break;
                    case '\'': sb.Append("&apos;"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        // String-resource VALUES need Android resource escaping on top of XML
        // escaping: after aapt2 parses the XML, a bare apostrophe or double-quote is
        // a span delimiter and a leading '@'/'?' is a resource reference — all break
        // the build. (formatted="false" on the element neutralizes a literal '%'.)
        private static string EscapeResValue(string value)
        {
            var sb = new StringBuilder(value.Length);
            foreach (var c in value)
            {
                if (c < 0x20)
                    continue;
                switch (c)
                {
                    case '&': sb.Append("&amp;"); break;   // XML well-formedness
                    case '<': sb.Append("&lt;"); break;
                    case '>': sb.Append("&gt;"); break;
                    case '\'': sb.Append("\\'"); break;    // Android: escape apostrophe
                    case '"': sb.Append("\\\""); break;    // Android: escape quote
                    case '\\': sb.Append("\\\\"); break;   // Android: escape backslash
                    default: sb.Append(c); break;
                }
            }
            if (sb.Length > 0 && (sb[0] == '@' || sb[0] == '?'))
                sb.Insert(0, '\\');
            // aapt trims unquoted leading/trailing whitespace; wrap in literal double
            // quotes to preserve intentional edge whitespace (raw '"' is legal in XML
            // element text, and the escapes above stay valid inside a quoted value).
            if (sb.Length > 0 && (sb[0] == ' ' || sb[sb.Length - 1] == ' '))
            {
                sb.Insert(0, '"');
                sb.Append('"');
            }
            return sb.ToString();
        }
    }
}
