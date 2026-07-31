// Harness-only tests (same idea as .verify/JavaSmoke): they drive the ANDROID
// build post-processor's per-locale resource generation, which the Unity Test
// Runner structurally cannot reach — Editor/Android lives behind an asmdef whose
// defineConstraints are UNITY_ANDROID, so a test assembly referencing it would
// fail to compile whenever another build target is active. Living under `.verify/`
// keeps them invisible to Unity while `dotnet test` compiles the real source file.
//
// What they pin is a BUILD-BREAKING contract: aapt2 hard-fails on two <string>
// elements with one name under one resource config, and it folds locale
// qualifiers case-insensitively — so two spellings of one locale must produce one
// directory and one entry per label. Neither failure is visible before Gradle runs.
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using EminDeniz99.QuickActions;
using EminDeniz99.QuickActions.Editor;

namespace EminDeniz99.QuickActions.Tests
{
    [TestFixture]
    public class AndroidStaticLocalizationTests
    {
        private static SortedDictionary<string, StringBuilder> NewBuckets() =>
            new SortedDictionary<string, StringBuilder>(System.StringComparer.OrdinalIgnoreCase);

        private static HashSet<string> NewEmitted() =>
            new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        private static int Count(string haystack, string needle)
        {
            var n = 0;
            for (var i = haystack.IndexOf(needle, System.StringComparison.Ordinal); i >= 0;
                 i = haystack.IndexOf(needle, i + needle.Length, System.StringComparison.Ordinal))
                n++;
            return n;
        }

        [Test]
        public void ResourceQualifier_IsCanonicallyCased_SoOneLocaleIsOneDirectory()
        {
            // BCP-47 casing is conventional, not semantic: aapt2 resolves
            // values-b+zh+Hans and values-b+zh+hans to the SAME config, and on the
            // case-insensitive macOS/Windows filesystems the Editor runs on they are
            // literally one directory — so the caller's spelling must not survive.
            Assert.AreEqual("b+zh+Hans",
                QuickActionsBuildPostProcessorAndroid.ResourceQualifier("zh-Hans"));
            Assert.AreEqual("b+zh+Hans",
                QuickActionsBuildPostProcessorAndroid.ResourceQualifier("zh-hans"),
                "a lowercase script subtag must canonicalise to the same qualifier");
            Assert.AreEqual("b+zh+Hans",
                QuickActionsBuildPostProcessorAndroid.ResourceQualifier("ZH-HANS"));

            // The classic language+region form was already normalised; pin it so a
            // refactor of the BCP-47 branch can't regress it.
            Assert.AreEqual("pt-rBR", QuickActionsBuildPostProcessorAndroid.ResourceQualifier("pt-BR"));
            Assert.AreEqual("pt-rBR", QuickActionsBuildPostProcessorAndroid.ResourceQualifier("PT-br"));
            Assert.AreEqual("fr", QuickActionsBuildPostProcessorAndroid.ResourceQualifier("FR"));
            // A 3-digit UN region is not a script: it stays as written.
            Assert.AreEqual("b+es+419", QuickActionsBuildPostProcessorAndroid.ResourceQualifier("es-419"));
            // Unusable tags still answer null so the caller warns and skips rather
            // than emitting a directory that fails the build.
            Assert.IsNull(QuickActionsBuildPostProcessorAndroid.ResourceQualifier("fr_CA"));
            Assert.IsNull(QuickActionsBuildPostProcessorAndroid.ResourceQualifier(""));
        }

        [Test]
        public void AppendLocalized_KeepsTheFirstRow_WhenTwoRowsAreTheSameLocaleAndLabel()
        {
            // Duplicating a list element in the inspector (its "+" button clones the
            // previous one) is all it takes. Emitting both puts two
            // <string name="qa_short_0"> under config 'fr' and aapt2 fails the whole
            // build with "duplicate value for resource" — pointing at a generated file
            // the developer never wrote.
            var buckets = NewBuckets();
            var emitted = NewEmitted();
            QuickActionsBuildPostProcessorAndroid.AppendLocalized(buckets, emitted,
                new List<LocalizedText>
                {
                    new LocalizedText("fr", "Jouer"),
                    new LocalizedText("FR", "Jouer maintenant"), // same locale, different casing
                    new LocalizedText("fr", "Jouer encore"),     // same locale, same casing
                },
                "qa_short_0", "play");

            Assert.AreEqual(1, buckets.Count, "one locale must produce one bucket");
            var body = buckets["fr"].ToString();
            Assert.AreEqual(1, Count(body, "name=\"qa_short_0\""),
                "one resource name may appear at most once per locale");
            StringAssert.Contains(">Jouer<", body, "the first row wins");
        }

        [Test]
        public void AppendLocalized_MergesTwoSpellingsOfOneLocale_KeepingBothLabels()
        {
            // Different LABELS under the same locale spelled two ways must land in the
            // SAME bucket: written to two directories they collapse to one file on a
            // case-insensitive filesystem and one item's labels vanish silently.
            var buckets = NewBuckets();
            var emitted = NewEmitted();
            QuickActionsBuildPostProcessorAndroid.AppendLocalized(buckets, emitted,
                new List<LocalizedText> { new LocalizedText("zh-Hans", "播放") }, "qa_short_0", "play");
            QuickActionsBuildPostProcessorAndroid.AppendLocalized(buckets, emitted,
                new List<LocalizedText> { new LocalizedText("zh-hans", "设置") }, "qa_short_1", "settings");

            Assert.AreEqual(1, buckets.Count, "both spellings are one locale");
            var body = buckets["b+zh+Hans"].ToString();
            StringAssert.Contains("播放", body);
            StringAssert.Contains("设置", body, "the second item's labels must not be lost");
        }

        [Test]
        public void AppendLocalized_SkipsUnusableRows_WithoutBlockingTheRest()
        {
            // Same filter the runtime resolver applies, so a static shortcut and a
            // dynamic one with the same table render the same way — and a bad row must
            // not consume the (qualifier, label) slot the good row needs.
            var buckets = NewBuckets();
            var emitted = NewEmitted();
            QuickActionsBuildPostProcessorAndroid.AppendLocalized(buckets, emitted,
                new List<LocalizedText>
                {
                    null,
                    new LocalizedText("", "no locale"),
                    new LocalizedText("de", ""),        // no text to render
                    new LocalizedText("fr_CA", "bad qualifier"),
                    new LocalizedText("de", "Spielen"), // the only usable row
                },
                "qa_short_0", "play");

            Assert.AreEqual(1, buckets.Count);
            StringAssert.Contains(">Spielen<", buckets["de"].ToString());
        }
    }
}
