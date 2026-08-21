using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace EminDeniz99.QuickActions.Editor
{
    /// <summary>
    /// The static-shortcut set a build is about to bake, handed to
    /// <see cref="QuickActionsStaticBuild.Customize"/> subscribers. Mutate
    /// <see cref="Shortcuts"/> freely — add, remove, reorder or edit items — the
    /// platform post-processor bakes exactly what the list holds afterwards. The
    /// items are copies seeded from the settings asset; the asset itself is never
    /// modified, so a build-only change can't dirty the project.
    /// </summary>
    public sealed class QuickActionsStaticBuildContext
    {
        /// <summary>The platform being built (<c>iOS</c> or <c>Android</c>).</summary>
        public BuildTarget Platform { get; }

        /// <summary>
        /// True when this is a Development build. The classic use is shipping a
        /// build-info shortcut to testers only:
        /// <c>if (ctx.DevelopmentBuild) ctx.Shortcuts.Add(…)</c>.
        /// </summary>
        public bool DevelopmentBuild { get; }

        /// <summary>
        /// The shortcuts about to be baked, seeded from
        /// <c>Project Settings ▸ Quick Actions</c> (empty when the project has no
        /// settings asset). Placeholders in these items are interpolated <b>after</b>
        /// every subscriber ran, so added items may use <c>{version}</c> etc. too.
        /// </summary>
        public List<QuickActionItem> Shortcuts { get; }

        internal QuickActionsStaticBuildContext(
            BuildTarget platform, bool developmentBuild, List<QuickActionItem> shortcuts)
        {
            Platform = platform;
            DevelopmentBuild = developmentBuild;
            Shortcuts = shortcuts;
        }
    }

    /// <summary>
    /// Build-time pipeline for <b>static</b> shortcuts: what the platform
    /// post-processors bake is not the raw settings asset but the result of
    /// <see cref="Prepare"/>, which (1) copies the configured items, (2) lets
    /// <see cref="Customize"/> subscribers rewrite the set in code, and
    /// (3) interpolates <c>{placeholder}</c> tokens in every title/subtitle —
    /// base and localized — so a label like <c>v{version} ({build})</c> ships as
    /// <c>v1.4.0 (37)</c>, visible on long-press before the app ever ran.
    ///
    /// Built-in placeholders: <c>{version}</c> (bundleVersion), <c>{build}</c>
    /// (iOS build number / Android versionCode), <c>{bundleId}</c> (on Android
    /// the Gradle-resolved applicationId, so the label can't disagree with the
    /// shipping id), <c>{productName}</c>, <c>{unityVersion}</c>,
    /// <c>{platform}</c>. Matching is case-insensitive; <c>{{</c> / <c>}}</c>
    /// escape a literal brace; an unknown token is left verbatim and warned
    /// about. Add your own values (build date, git hash, CI run number, …) with
    /// <see cref="RegisterPlaceholder"/> from any editor script — registrations
    /// and <see cref="Customize"/> subscriptions made in a
    /// <c>[InitializeOnLoad]</c> static constructor are in place for every
    /// build, batch mode included.
    ///
    /// Everything here is Editor-only and applies to static shortcuts alone.
    /// Dynamic shortcuts don't need it: their strings are built at runtime,
    /// where C# interpolation over <c>Application.version</c> etc. already works.
    /// </summary>
    public static class QuickActionsStaticBuild
    {
        // The six built-in names. BuiltinValues spells its own keys (it has to —
        // each carries a value), so this array MIRRORS that table rather than
        // feeding it; KnownPlaceholders is what reads it, to probe names without
        // computing values in a GUI repaint. Nothing structural keeps the two in
        // step, so a test asserts they hold the same key set per platform — add a
        // built-in in one place only and it goes red.
        internal static readonly string[] BuiltinNames =
            { "version", "build", "bundleId", "productName", "unityVersion", "platform" };

        // Case-insensitive to match the lookup: {BuildDate} and {builddate} are
        // one token, so they must be one registration.
        private static readonly Dictionary<string, Func<string>> CustomPlaceholders =
            new Dictionary<string, Func<string>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Raised once per platform build, before placeholder interpolation.
        /// Subscribers may mutate <see cref="QuickActionsStaticBuildContext.Shortcuts"/>
        /// at will — e.g. append a build-info item only when
        /// <see cref="QuickActionsStaticBuildContext.DevelopmentBuild"/> is true.
        /// A subscriber that throws fails the build loudly, on purpose: baking a
        /// half-customized shortcut set into a release would be worse.
        /// </summary>
        public static event Action<QuickActionsStaticBuildContext> Customize;

        /// <summary>
        /// Registers (or replaces) a custom <c>{name}</c> placeholder for static
        /// shortcut labels. The resolver runs once per build, at bake time; a
        /// null result becomes an empty string, and a thrown exception is
        /// contained — the build continues and the token falls back (with a
        /// warning): it stays verbatim for a new name, or keeps the built-in /
        /// override value when the resolver shadowed one. A broken decoration
        /// must not break the build the way a broken <see cref="Customize"/>
        /// subscriber deliberately does. Custom names win over built-ins, so
        /// <c>{version}</c> can be redefined.
        /// </summary>
        /// <param name="name">Token name without braces — letters, digits,
        /// '_', '-' or '.' (e.g. <c>"buildDate"</c>).</param>
        /// <param name="resolve">Returns the replacement text at build time.</param>
        public static void RegisterPlaceholder(string name, Func<string> resolve)
        {
            if (!IsValidPlaceholderName(name))
                throw new ArgumentException(
                    "Placeholder names use letters, digits, '_', '-' or '.' only — " +
                    "pass \"buildDate\", not \"{buildDate}\".", nameof(name));
            if (resolve == null)
                throw new ArgumentNullException(nameof(resolve));
            CustomPlaceholders[name] = resolve;
        }

        /// <summary>Removes a custom placeholder. True if it was registered.</summary>
        public static bool UnregisterPlaceholder(string name) =>
            !string.IsNullOrEmpty(name) && CustomPlaceholders.Remove(name);

        /// <summary>
        /// Produces the static shortcuts a build should bake: settings-asset
        /// copies → <see cref="Customize"/> → placeholder interpolation. Called
        /// by the platform post-processors; call it yourself only to preview the
        /// exact outcome (e.g. from a build script).
        /// </summary>
        /// <param name="platform">Platform whose built-in values to use.</param>
        /// <param name="developmentBuild">Forwarded to <see cref="Customize"/>.</param>
        /// <param name="valueOverrides">Optional replacements for built-in
        /// values when the caller knows better — the Android post-processor
        /// passes the Gradle-resolved <c>{bundleId}</c>. Registered custom
        /// placeholders still win over these.</param>
        public static List<QuickActionItem> Prepare(
            BuildTarget platform, bool developmentBuild,
            IDictionary<string, string> valueOverrides = null)
        {
            var shortcuts = new List<QuickActionItem>();
            var settings = QuickActionsSettings.GetOrNull();
            if (settings != null)
                foreach (var item in settings.StaticShortcuts)
                    if (item != null)
                        shortcuts.Add(item.Copy());

            // Copies are made BEFORE the hook so no subscriber can reach the
            // serialized asset through the list. Runs even with no settings
            // asset: a project may define its whole static set in code.
            Customize?.Invoke(
                new QuickActionsStaticBuildContext(platform, developmentBuild, shortcuts));

            var values = BuiltinValues(platform);
            if (valueOverrides != null)
                foreach (var pair in valueOverrides)
                    values[pair.Key] = pair.Value;
            foreach (var pair in CustomPlaceholders)
            {
                // Resolve into the table only on success: a throwing resolver
                // that shadowed a built-in must not take the built-in value
                // down with it.
                try
                {
                    values[pair.Key] = pair.Value() ?? string.Empty;
                }
                catch (Exception e)
                {
                    Debug.LogWarning(
                        $"[QuickActions] Custom placeholder '{{{pair.Key}}}' resolver threw " +
                        $"({e.Message}); the token keeps its previous value or stays as-is.");
                }
            }

            var unknown = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < shortcuts.Count; i++)
            {
                if (shortcuts[i] == null)
                    continue; // a subscriber may have added one; the bakers skip it too
                // Interpolate a COPY, never the list entry itself: a subscriber may
                // cache one QuickActionItem and re-add it on every build (the
                // [InitializeOnLoad] pattern the class doc recommends invites it),
                // and writing into that instance would bake THIS build's resolved
                // values into the NEXT build's input — stale, even cross-platform.
                var item = shortcuts[i] = shortcuts[i].Copy();
                var authoredTitle = item.Title;
                item.Title = Interpolate(authoredTitle, values, unknown);
                item.Subtitle = Interpolate(item.Subtitle, values, unknown);
                InterpolateEntries(item.LocalizedTitles, values, unknown);
                InterpolateEntries(item.LocalizedSubtitles, values, unknown);
                // The one skip state the settings page structurally can't warn
                // about: the authored title is non-empty and every token known,
                // yet a resolver returned "" and the bakers will drop the item.
                if (!string.IsNullOrEmpty(authoredTitle) && string.IsNullOrEmpty(item.Title))
                    Debug.LogWarning(
                        $"[QuickActions] Static shortcut '{item.Id}': title '{authoredTitle}' " +
                        "interpolated to an empty string, so the build will skip this shortcut. " +
                        "Make the placeholder resolve to non-empty text (or add literal text).");
            }
            if (unknown.Count > 0)
                Debug.LogWarning(
                    "[QuickActions] Unknown placeholder(s) in static shortcuts: {" +
                    string.Join("}, {", unknown) + "} — left as-is. Register them with " +
                    "QuickActionsStaticBuild.RegisterPlaceholder, or double the brace ({{) " +
                    "for a literal one.");
            return shortcuts;
        }

        // The concrete values for one platform. {build} exists only where a
        // platform defines one (iOS buildNumber / Android versionCode) — on any
        // other target the token stays unresolved, which the Simulator preview
        // shows honestly instead of inventing a number.
        internal static Dictionary<string, string> BuiltinValues(BuildTarget platform)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["version"] = PlayerSettings.bundleVersion,
                ["bundleId"] = PlayerSettings.applicationIdentifier,
                ["productName"] = PlayerSettings.productName,
                ["unityVersion"] = Application.unityVersion,
                ["platform"] = platform.ToString(),
            };
            switch (platform)
            {
                case BuildTarget.iOS:
                    values["build"] = PlayerSettings.iOS.buildNumber;
                    break;
                case BuildTarget.Android:
                    values["build"] = PlayerSettings.Android.bundleVersionCode
                        .ToString(CultureInfo.InvariantCulture);
                    break;
            }
            return values;
        }

        // Name probe for validation: every resolvable name (built-in + custom)
        // mapped to "", so Interpolate can be reused to find unknown tokens
        // without computing real values in a GUI repaint. Kept in step with
        // BuiltinValues by test, not by construction — see BuiltinNames.
        internal static Dictionary<string, string> KnownPlaceholders()
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in BuiltinNames)
                values[name] = string.Empty;
            foreach (var pair in CustomPlaceholders)
                values[pair.Key] = string.Empty;
            return values;
        }

        /// <summary>
        /// Replaces <c>{name}</c> tokens in <paramref name="text"/> from
        /// <paramref name="values"/> (case-insensitive when the dictionary is —
        /// every caller passes one built that way). <c>{{</c> and <c>}}</c>
        /// produce literal braces. A token-shaped name missing from the table is
        /// kept verbatim, braces included, and added to
        /// <paramref name="unknown"/> (when given); anything not token-shaped —
        /// <c>{}</c>, <c>{a b}</c>, an unclosed <c>{</c> — passes through
        /// untouched, so brace text that was never meant as a token keeps
        /// rendering as typed. (Text that DOES look like a token, or doubled
        /// braces, is rewritten — an acknowledged behavior change for labels
        /// authored before placeholders existed.)
        /// </summary>
        internal static string Interpolate(
            string text, IReadOnlyDictionary<string, string> values, ISet<string> unknown)
        {
            if (string.IsNullOrEmpty(text) || (text.IndexOf('{') < 0 && text.IndexOf('}') < 0))
                return text;
            var sb = new StringBuilder(text.Length);
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '{')
                {
                    if (i + 1 < text.Length && text[i + 1] == '{')
                    {
                        sb.Append('{');
                        i++;
                        continue;
                    }
                    var close = text.IndexOf('}', i + 1);
                    var name = close > i + 1 ? text.Substring(i + 1, close - i - 1) : null;
                    if (name != null && IsValidPlaceholderName(name))
                    {
                        if (values.TryGetValue(name, out var value))
                        {
                            sb.Append(value);
                        }
                        else
                        {
                            unknown?.Add(name);
                            // The whole token verbatim, consuming its '}' — so the
                            // '}' can't pair with a following one into an escape.
                            sb.Append(text, i, close - i + 1);
                        }
                        i = close;
                        continue;
                    }
                    sb.Append('{');
                }
                else if (c == '}')
                {
                    if (i + 1 < text.Length && text[i + 1] == '}')
                        i++;
                    sb.Append('}');
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        internal static bool IsValidPlaceholderName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            foreach (var c in name)
                if ((c < 'a' || c > 'z') && (c < 'A' || c > 'Z') && (c < '0' || c > '9') &&
                    c != '_' && c != '-' && c != '.')
                    return false;
            return true;
        }

        private static void InterpolateEntries(
            List<LocalizedText> entries, IReadOnlyDictionary<string, string> values,
            ISet<string> unknown)
        {
            if (entries == null)
                return;
            foreach (var entry in entries)
                if (entry != null)
                    entry.Text = Interpolate(entry.Text, values, unknown);
        }

        // Test-only: the registry and the hook are process-global statics and the
        // NUnit harness runs every test in one process — each must start clean.
        internal static void ResetForTests()
        {
            CustomPlaceholders.Clear();
            Customize = null;
        }
    }
}
