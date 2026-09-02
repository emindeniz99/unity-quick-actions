using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using EminDeniz99.QuickActions;
using EminDeniz99.QuickActions.Editor;

namespace EminDeniz99.QuickActions.Tests
{
    /// <summary>
    /// The names-only companion of the built-in icons (always-present Editor
    /// assembly) and the IconType property drawer that reads it. Both exist so
    /// the settings page can say, next to the field, which catalog entries render
    /// blank on Android — these tests hold that copy to the Android source of truth.
    /// </summary>
    [TestFixture]
    public class BuiltInIconSetTests
    {
        [Test]
        public void Set_IsExactlyTheGeneratedEntries()
        {
            // WHY: two generated files from one ICONS list — the generator's --check
            // pins each to the generator, this pins them to each other at the C# level,
            // so a hand edit of either would fail here even if --check were skipped.
            // Entries has four files per icon since the adaptive variant; the set
            // names icons, so compare against the legacy (API 25) row of each.
            CollectionAssert.AreEquivalent(
                QuickActionsBuiltInIcons.Entries
                    .Where(e => e.Layer == QuickActionsBuiltInIcons.IconLayer.Legacy)
                    .Select(e => e.Icon).ToArray(),
                QuickActionsBuiltInIconSet.Icons);
            Assert.AreEqual(QuickActionsBuiltInIconSet.Icons.Length,
                QuickActionsBuiltInIconSet.Icons.Distinct().Count(), "duplicate member in the set");
        }

        [Test]
        public void HasAndroidArt_IsTrueForTheFour_AndFalseForEveryOtherMember()
        {
            foreach (IconType icon in Enum.GetValues(typeof(IconType)))
            {
                var expected = QuickActionsBuiltInIcons.Entries.Any(e => e.Icon == icon);
                Assert.AreEqual(expected, QuickActionsBuiltInIconSet.HasAndroidArt(icon), icon.ToString());
            }
            Assert.IsFalse(QuickActionsBuiltInIconSet.HasAndroidArt(IconType.None));
        }

        [Test]
        public void Note_IsSilentForNone_NamesTheBuiltIn_AndNamesTheMissingDrawableOtherwise()
        {
            Assert.IsNull(QuickActionsIconTypeDrawer.NoteFor(IconType.None, true, false));
            Assert.IsNull(QuickActionsIconTypeDrawer.NoteFor(IconType.None, false, false));

            var builtIn = QuickActionsIconTypeDrawer.NoteFor(IconType.Add, true, false);
            StringAssert.Contains("built-in", builtIn);
            StringAssert.DoesNotContain("blank", builtIn);

            var missing = QuickActionsIconTypeDrawer.NoteFor(IconType.Search, true, false);
            StringAssert.Contains("blank", missing);
            StringAssert.Contains("ic_quickaction_search", missing);
            // The compound name is the one that would betray a naive lower-casing.
            StringAssert.Contains("ic_quickaction_mark_location",
                QuickActionsIconTypeDrawer.NoteFor(IconType.MarkLocation, true, false));
        }

        [Test]
        public void Note_WithTheBuiltInsSwitchedOff_StopsPromisingADrawable()
        {
            // WHY: "Write built-in Android icons" off means the post-processor writes
            // none of the four; a note still saying "ships with the package" would
            // steer a user into an iconless shortcut. The toggle changes the note for
            // the four only — the other 25 never had a built-in to lose.
            var off = QuickActionsIconTypeDrawer.NoteFor(IconType.Add, false, false);
            StringAssert.Contains("off", off);
            StringAssert.Contains("blank", off);
            StringAssert.Contains("ic_quickaction_add", off);
            Assert.AreEqual(QuickActionsIconTypeDrawer.NoteFor(IconType.Search, true, false),
                QuickActionsIconTypeDrawer.NoteFor(IconType.Search, false, false));
        }

        [Test]
        public void Note_ForAStaticShortcut_SaysTheBakerNeedsAndroidDrawable()
        {
            // WHY: the static baker cannot reference a drawable that may not exist,
            // so for a non-built-in Icon it bakes NO icon unless AndroidDrawable names
            // one (QuickActionsBuildPostProcessorAndroid.WriteResources warns and
            // moves on). "Ship ic_quickaction_search" — the right advice for a
            // runtime Add — would leave a static shortcut blank; the note must say so
            // where the static list is edited.
            var staticNote = QuickActionsIconTypeDrawer.NoteFor(IconType.Search, true, true);
            StringAssert.Contains("AndroidDrawable", staticNote);
            StringAssert.Contains("ic_quickaction_search", staticNote);
            StringAssert.DoesNotContain("AndroidDrawable",
                QuickActionsIconTypeDrawer.NoteFor(IconType.Search, true, false));

            // A built-in IS baked for a static item — no AndroidDrawable needed...
            StringAssert.DoesNotContain("AndroidDrawable",
                QuickActionsIconTypeDrawer.NoteFor(IconType.Add, true, true));
            // ...unless the built-ins are switched off, when the static item is in
            // the same position as any other: only AndroidDrawable bakes anything.
            StringAssert.Contains("AndroidDrawable",
                QuickActionsIconTypeDrawer.NoteFor(IconType.Add, false, true));
            Assert.IsNull(QuickActionsIconTypeDrawer.NoteFor(IconType.None, true, true));
        }

        [Test]
        public void SnakeCase_MatchesTheJavaIconNamesTable_ForEveryMember()
        {
            // WHY: the note tells the user which file name to ship. That name is
            // whatever QuickActionsBridge.java looks up — ICON_NAMES[value] — so the
            // drawer's derivation is pinned to the table, member by member, rather
            // than trusting the doc comment's "lower-case with underscores" rule.
            var java = File.ReadAllText(Path.Combine(RepoRoot(), "Plugins", "Android", "QuickActionsBridge.java"));
            var table = Regex.Match(java, @"ICON_NAMES\s*=\s*\{([^}]*)\}", RegexOptions.Singleline);
            Assert.IsTrue(table.Success, "could not find ICON_NAMES in QuickActionsBridge.java");
            var names = Regex.Matches(table.Groups[1].Value, "\"([^\"]*)\"").Cast<Match>()
                .Select(m => m.Groups[1].Value).ToArray();

            foreach (IconType icon in Enum.GetValues(typeof(IconType)))
            {
                if (icon == IconType.None)
                    continue;
                Assert.AreEqual(names[(int)icon], QuickActionsIconTypeDrawer.SnakeCase(icon),
                    $"ICON_NAMES[{(int)icon}] disagrees with the drawer's name for {icon}");
            }
        }

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "package.json")) &&
                    Directory.Exists(Path.Combine(dir.FullName, "Plugins")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            throw new InvalidOperationException("package root not found above " + TestContext.CurrentContext.TestDirectory);
        }
    }
}
