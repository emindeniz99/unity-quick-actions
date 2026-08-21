// Harness-only tests (same mechanism as AndroidStaticLocalizationTests): they
// drive the build-time placeholder pipeline that bakes static shortcut labels,
// which the Unity Test Runner structurally cannot reach — QuickActionsStaticBuild
// lives in the Editor assembly the runtime-referencing test asmdef can't see.
// `dotnet test` compiles the real source file into this harness assembly instead.
//
// What they pin is what ships INSIDE Info.plist / shortcuts.xml: an interpolation
// bug here isn't a red console line, it's "v{version} ({build})" rendered on a
// user's home screen — or worse, a customizer-driven release baking the wrong set.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using EminDeniz99.QuickActions;
using EminDeniz99.QuickActions.Editor;
using UnityEditor;

namespace EminDeniz99.QuickActions.Tests
{
    [TestFixture]
    public class StaticBuildPlaceholdersTests
    {
        // The registry and the Customize hook are process-global statics and this
        // harness runs every fixture in one process; a leaked registration would
        // make test outcomes depend on execution order.
        [SetUp]
        public void ResetPipeline() => QuickActionsStaticBuild.ResetForTests();

        [TearDown]
        public void ResetPipelineAgain() => QuickActionsStaticBuild.ResetForTests();

        private static Dictionary<string, string> Values(params (string name, string value)[] pairs)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (name, value) in pairs)
                values[name] = value;
            return values;
        }

        [Test]
        public void Interpolate_ReplacesTokens_AndMatchesCaseInsensitively()
        {
            // Users will type {Version} as readily as {version}; the OS renders
            // whatever we bake, so both spellings must resolve identically.
            var values = Values(("version", "1.2.3"), ("build", "7"));
            Assert.AreEqual("v1.2.3 (7)",
                QuickActionsStaticBuild.Interpolate("v{version} ({BUILD})", values, null));
            // Adjacent tokens with no separator must not swallow each other.
            Assert.AreEqual("1.2.37",
                QuickActionsStaticBuild.Interpolate("{version}{build}", values, null));
        }

        [Test]
        public void Interpolate_DoubledBraces_EscapeToLiteralBraces()
        {
            // "{{" is the only way to render a literal brace next to a real token,
            // so the escape must win over token recognition.
            var values = Values(("version", "9"));
            Assert.AreEqual("{version}",
                QuickActionsStaticBuild.Interpolate("{{version}}", values, null));
            Assert.AreEqual("a}b", QuickActionsStaticBuild.Interpolate("a}}b", values, null));
            Assert.AreEqual("{9}", QuickActionsStaticBuild.Interpolate("{{{version}}}", values, null));
        }

        [Test]
        public void Interpolate_UnknownToken_IsLeftVerbatimAndReported()
        {
            // Baking an empty hole for a typo'd token would ship a blank label;
            // leaving it verbatim keeps the mistake visible AND reversible, and
            // the report lets the settings page warn before a build ever runs.
            var unknown = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            Assert.AreEqual("v{verison}",
                QuickActionsStaticBuild.Interpolate("v{verison}", Values(("version", "1")), unknown));
            CollectionAssert.Contains(unknown, "verison");

            // The kept token's closing brace must not pair with a following '}'
            // into an escape — the whole thing stays exactly as typed.
            Assert.AreEqual("{nope}}",
                QuickActionsStaticBuild.Interpolate("{nope}}", Values(), unknown));
        }

        [Test]
        public void Interpolate_NonTokenBraces_PassThroughUntouched()
        {
            // Titles written before this feature existed may contain braces that
            // were never meant as tokens; they must keep rendering as typed.
            var values = Values(("version", "1"));
            Assert.AreEqual("{}", QuickActionsStaticBuild.Interpolate("{}", values, null));
            Assert.AreEqual("{a b}", QuickActionsStaticBuild.Interpolate("{a b}", values, null));
            Assert.AreEqual("smile {", QuickActionsStaticBuild.Interpolate("smile {", values, null));
            Assert.AreEqual("{unclosed", QuickActionsStaticBuild.Interpolate("{unclosed", values, null));
            Assert.IsNull(QuickActionsStaticBuild.Interpolate(null, values, null));
            Assert.AreEqual("", QuickActionsStaticBuild.Interpolate("", values, null));
        }

        [Test]
        public void BuiltinValues_PlatformPicksTheBuildNumberSource()
        {
            // {build} means "the number this platform ships": iOS buildNumber and
            // Android versionCode are set independently in Player Settings, and
            // baking the wrong one would mislabel every build. The stub values
            // are distinct ("7" vs 42) precisely so this cross-wiring would fail.
            var ios = QuickActionsStaticBuild.BuiltinValues(BuildTarget.iOS);
            Assert.AreEqual("7", ios["build"]);
            Assert.AreEqual("iOS", ios["platform"]);

            var android = QuickActionsStaticBuild.BuiltinValues(BuildTarget.Android);
            Assert.AreEqual("42", android["build"]);
            Assert.AreEqual("Android", android["platform"]);

            // No platform build number exists elsewhere; inventing one would bake
            // a lie, so the token must simply stay unresolved there.
            Assert.IsFalse(QuickActionsStaticBuild.BuiltinValues(BuildTarget.StandaloneWindows64)
                .ContainsKey("build"));

            // The rest of the core set is platform-independent.
            Assert.AreEqual("1.2.3", ios["version"]);
            Assert.AreEqual("com.example.app", ios["bundleId"]);
            Assert.AreEqual("Example App", ios["productName"]);
            Assert.AreEqual("2021.3.45f1", ios["unityVersion"]);
        }

        [Test]
        public void KnownPlaceholders_CoversBuiltinsAndCustoms_ForValidation()
        {
            // The settings-page validator flags only names NEITHER built-in NOR
            // registered; a drift between this probe and BuiltinValues would turn
            // every valid {build} into a false warning (or hide a real typo).
            // BuiltinValues spells its own keys, so nothing structural keeps it
            // and the name probe in step. Compare the two key sets directly —
            // asserting only that the probe contains BuiltinNames would be a
            // tautology (the probe is BUILT from BuiltinNames) and would not
            // notice a token added to the value table alone. Both platforms
            // define {build}, so each key set is exactly the six names.
            foreach (var platform in new[] { BuildTarget.iOS, BuildTarget.Android })
                CollectionAssert.AreEquivalent(
                    QuickActionsStaticBuild.BuiltinNames,
                    new List<string>(QuickActionsStaticBuild.BuiltinValues(platform).Keys),
                    $"the {platform} value table and the name probe disagree");

            QuickActionsStaticBuild.RegisterPlaceholder("buildDate", () => "today");
            var known = QuickActionsStaticBuild.KnownPlaceholders();
            foreach (var name in QuickActionsStaticBuild.BuiltinNames)
                Assert.IsTrue(known.ContainsKey(name), $"built-in '{name}' missing from the probe");
            Assert.IsTrue(known.ContainsKey("builddate"), "custom names must probe case-insensitively");
        }

        [Test]
        public void RegisterPlaceholder_RejectsBraceAndEmptyNames()
        {
            // "{buildDate}" as a NAME would register a token nothing can ever
            // match (the parser hands Register-style names to the lookup without
            // braces) — fail at the call site, where the mistake is fixable.
            Assert.Throws<ArgumentException>(
                () => QuickActionsStaticBuild.RegisterPlaceholder("{buildDate}", () => ""));
            Assert.Throws<ArgumentException>(
                () => QuickActionsStaticBuild.RegisterPlaceholder("has space", () => ""));
            Assert.Throws<ArgumentException>(
                () => QuickActionsStaticBuild.RegisterPlaceholder("", () => ""));
            Assert.Throws<ArgumentException>(
                () => QuickActionsStaticBuild.RegisterPlaceholder(null, () => ""));
            Assert.Throws<ArgumentNullException>(
                () => QuickActionsStaticBuild.RegisterPlaceholder("ok", null));
        }

        [Test]
        public void Prepare_CustomizerAddedItems_AreInterpolatedToo()
        {
            // The whole point of the Customize hook is adding items in code
            // (e.g. a dev-only build-info shortcut); those items must get the
            // same interpolation as asset-authored ones, or the hook would bake
            // literal "{version}" onto a home screen.
            QuickActionsStaticBuild.Customize += ctx =>
                ctx.Shortcuts.Add(new QuickActionItem("app_info", "App info", "v{version} ({build})"));

            var shortcuts = QuickActionsStaticBuild.Prepare(BuildTarget.Android, false);

            Assert.AreEqual(1, shortcuts.Count);
            Assert.AreEqual("v1.2.3 (42)", shortcuts[0].Subtitle);
        }

        [Test]
        public void Prepare_CustomizerSeesPlatformAndDevelopmentFlag()
        {
            // "if (ctx.DevelopmentBuild) add the info shortcut" is the documented
            // recipe; hand the subscriber the wrong flags and that recipe ships
            // debug shortcuts to production.
            BuildTarget? seenPlatform = null;
            bool? seenDev = null;
            QuickActionsStaticBuild.Customize += ctx =>
            {
                seenPlatform = ctx.Platform;
                seenDev = ctx.DevelopmentBuild;
            };

            QuickActionsStaticBuild.Prepare(BuildTarget.iOS, true);

            Assert.AreEqual(BuildTarget.iOS, seenPlatform);
            Assert.AreEqual(true, seenDev);
        }

        [Test]
        public void Prepare_LocalizedRows_AreInterpolated()
        {
            // Android bakes LocalizedTitles/Subtitles into per-locale resources;
            // interpolating only the base strings would ship resolved English
            // next to literal "{version}" in every other language.
            QuickActionsStaticBuild.Customize += ctx =>
            {
                var item = new QuickActionItem("app_info", "App info v{version}");
                item.LocalizedTitles.Add(new LocalizedText("tr", "Uygulama v{version}"));
                item.LocalizedSubtitles.Add(new LocalizedText("tr", "derleme {build}"));
                ctx.Shortcuts.Add(item);
            };

            var shortcuts = QuickActionsStaticBuild.Prepare(BuildTarget.Android, false);

            Assert.AreEqual("App info v1.2.3", shortcuts[0].Title);
            Assert.AreEqual("Uygulama v1.2.3", shortcuts[0].LocalizedTitles[0].Text);
            Assert.AreEqual("derleme 42", shortcuts[0].LocalizedSubtitles[0].Text);
        }

        [Test]
        public void Prepare_ValueOverridesAndCustoms_WinInThatOrder()
        {
            // Precedence is load-bearing: the Android post-processor overrides
            // {bundleId} with the Gradle-resolved applicationId (more truthful
            // than PlayerSettings), and a user registration must beat even that
            // — it is the "give the developer freedom" end of the design.
            QuickActionsStaticBuild.Customize += ctx =>
                ctx.Shortcuts.Add(new QuickActionItem("x", "{bundleId}|{version}"));

            var overridden = QuickActionsStaticBuild.Prepare(BuildTarget.Android, false,
                new Dictionary<string, string> { ["bundleId"] = "com.real.app" });
            Assert.AreEqual("com.real.app|1.2.3", overridden[0].Title);

            QuickActionsStaticBuild.RegisterPlaceholder("version", () => "9.9-custom");
            var custom = QuickActionsStaticBuild.Prepare(BuildTarget.Android, false,
                new Dictionary<string, string> { ["version"] = "5.5", ["bundleId"] = "com.real.app" });
            Assert.AreEqual("com.real.app|9.9-custom", custom[0].Title);
        }

        [Test]
        public void Prepare_ThrowingResolver_KeepsTheBuildAliveAndTheBuiltinValue()
        {
            // A custom resolver shells out to git, reads env vars… it WILL throw
            // on someone's CI. That must cost one label, never the build — and a
            // throwing shadow of a built-in must not take the built-in down.
            QuickActionsStaticBuild.RegisterPlaceholder("boom",
                () => throw new InvalidOperationException("no git here"));
            QuickActionsStaticBuild.RegisterPlaceholder("version",
                () => throw new InvalidOperationException("also broken"));
            QuickActionsStaticBuild.Customize += ctx =>
                ctx.Shortcuts.Add(new QuickActionItem("x", "x{boom}y v{version}"));

            List<QuickActionItem> shortcuts = null;
            Assert.DoesNotThrow(
                () => shortcuts = QuickActionsStaticBuild.Prepare(BuildTarget.iOS, false));
            Assert.AreEqual("x{boom}y v1.2.3", shortcuts[0].Title);
        }

        [Test]
        public void Prepare_NullResolverResult_BecomesEmptyNotNullTitle()
        {
            // Environment.GetEnvironmentVariable returns null when unset; that is
            // "no value", not a crash and not the four characters "null".
            QuickActionsStaticBuild.RegisterPlaceholder("ci", () => null);
            QuickActionsStaticBuild.Customize += ctx =>
                ctx.Shortcuts.Add(new QuickActionItem("x", "run[{ci}]"));

            var shortcuts = QuickActionsStaticBuild.Prepare(BuildTarget.iOS, false);

            Assert.AreEqual("run[]", shortcuts[0].Title);
        }

        [Test]
        public void Prepare_WithoutSettingsAsset_StartsEmptyAndStaysUsable()
        {
            // In this harness AssetDatabase.FindAssets finds nothing, which is
            // exactly the "project defines its static set purely in code" case:
            // no asset must not mean no hook and no shortcuts.
            Assert.AreEqual(0, QuickActionsStaticBuild.Prepare(BuildTarget.iOS, false).Count);
        }

        [Test]
        public void Prepare_DoesNotMutateASubscribersCachedItem_AcrossBuilds()
        {
            // The natural Customize pattern is an [InitializeOnLoad] static that
            // caches ONE item and re-adds it every build. Interpolating that
            // instance in place would bake build #1's resolved values into build
            // #2's input — including cross-platform: an Android pass would leave
            // its versionCode inside the title the next iOS pass bakes. Prepare
            // must therefore interpolate a copy, never the subscriber's object.
            var cached = new QuickActionItem("app_info", "b{build}");
            QuickActionsStaticBuild.Customize += ctx => ctx.Shortcuts.Add(cached);

            var android = QuickActionsStaticBuild.Prepare(BuildTarget.Android, false);
            Assert.AreEqual("b42", android[0].Title);
            Assert.AreEqual("b{build}", cached.Title, "the subscriber's instance must stay pristine");

            var ios = QuickActionsStaticBuild.Prepare(BuildTarget.iOS, false);
            Assert.AreEqual("b7", ios[0].Title, "the second build must bake ITS platform's value, not the first's");
        }

        [Test]
        public void Prepare_WholeTitleTokenResolvingEmpty_YieldsTheEmptyTitleTheBakersSkip()
        {
            // Title "{flavor}" with a resolver reading an unset env var (null → "")
            // is the one skip state the settings page structurally cannot warn
            // about: the authored title is non-empty and the token is known. Pin
            // the precondition (empty Title reaches the bakers, which drop it) —
            // Prepare's job on this path is the build-log warning, not a rescue.
            QuickActionsStaticBuild.RegisterPlaceholder("flavor", () => null);
            QuickActionsStaticBuild.Customize += ctx =>
                ctx.Shortcuts.Add(new QuickActionItem("x", "{flavor}"));

            var shortcuts = QuickActionsStaticBuild.Prepare(BuildTarget.Android, false);

            Assert.AreEqual("", shortcuts[0].Title);
        }

        [Test]
        public void UnregisterPlaceholder_RemovesCaseInsensitively()
        {
            // Register("Foo") + Unregister("FOO") leaving a live token would leak
            // a resolver across what a caller believes is a clean teardown.
            QuickActionsStaticBuild.RegisterPlaceholder("Foo", () => "1");
            Assert.IsTrue(QuickActionsStaticBuild.UnregisterPlaceholder("FOO"));
            Assert.IsFalse(QuickActionsStaticBuild.UnregisterPlaceholder("foo"));
        }
    }
}
