using NUnit.Framework;
using EminDeniz99.QuickActions;

namespace EminDeniz99.QuickActions.Tests
{
    [TestFixture]
    public class QuickActionItemTests
    {
        [Test]
        public void Constructor_SetsFields()
        {
            var item = new QuickActionItem("id", "Title", "Sub", IconType.Play);
            Assert.AreEqual("id", item.Id);
            Assert.AreEqual("Title", item.Title);
            Assert.AreEqual("Sub", item.Subtitle);
            Assert.AreEqual(IconType.Play, item.Icon);
        }

        [Test]
        public void IsValid_RequiresIdAndTitle()
        {
            Assert.IsTrue(new QuickActionItem("id", "Title").IsValid);
            Assert.IsFalse(new QuickActionItem("", "Title").IsValid);
            Assert.IsFalse(new QuickActionItem("id", "").IsValid);
            Assert.IsFalse(new QuickActionItem(null, null).IsValid);
        }

        [Test]
        public void Equality_IsByIdOnly()
        {
            var a1 = new QuickActionItem("a", "One");
            var a2 = new QuickActionItem("a", "Two");
            var b = new QuickActionItem("b", "One");

            Assert.AreEqual(a1, a2);
            Assert.AreEqual(a1.GetHashCode(), a2.GetHashCode());
            Assert.AreNotEqual(a1, b);
        }

        [Test]
        public void Copy_PreservesEveryField()
        {
            // WHY: the facade stores/returns defensive copies everywhere; a field
            // missed by Copy() silently loses that icon/payload the moment an item
            // crosses Add/GetAll/GetById — the OS then re-renders it wrong after a
            // reconcile push. Every instance field gets a distinct NON-default value
            // BY REFLECTION (not a hand-written arrange block), so a future field
            // whose author forgot to extend Copy() arrives here non-default, comes
            // back default from the copy, and fails — a hand-written arrange would
            // leave it default on both sides and pass vacuously.
            // NonPublic is load-bearing: the wire-only `internal L10n` (and any
            // future internal [SerializeField]) is exactly the kind of field an
            // author forgets, and the default GetFields() binding would skip it —
            // the guard would then pass while Copy() dropped it.
            var item = new QuickActionItem();
            foreach (var field in CopyableFields())
            {
                if (field.FieldType == typeof(string))
                    field.SetValue(item, field.Name + "_value");
                else if (field.FieldType == typeof(bool))
                    field.SetValue(item, true);
                else if (field.FieldType.IsEnum)
                    field.SetValue(item, System.Enum.GetValues(field.FieldType).GetValue(2));
                else if (field.FieldType == typeof(System.Collections.Generic.List<LocalizedText>))
                    field.SetValue(item, new System.Collections.Generic.List<LocalizedText>
                    {
                        new LocalizedText(field.Name + "_locale", field.Name + "_text"),
                    });
                else
                    Assert.Fail($"Field '{field.Name}' has type {field.FieldType} this test can't seed — extend the type switch.");
            }

            var copy = item.Copy();

            Assert.AreNotSame(item, copy);
            foreach (var field in CopyableFields())
            {
                var original = field.GetValue(item);
                var copied = field.GetValue(copy);
                // The per-locale tables need element-wise comparison: LocalizedText
                // has no value equality, so AreEqual on the lists would compare
                // references and fail a correct DEEP copy. The AreNotSame pair is
                // the point of the check — sharing the list (or an entry) would let
                // a caller retitle a stored item after Add/GetAll, the exact
                // divergence Copy() exists to prevent.
                if (original is System.Collections.Generic.List<LocalizedText> entries)
                {
                    var copiedEntries = (System.Collections.Generic.List<LocalizedText>)copied;
                    Assert.AreNotSame(entries, copiedEntries, $"Copy() must not share the list in '{field.Name}'");
                    Assert.AreEqual(entries.Count, copiedEntries.Count, $"Copy() must preserve every entry of '{field.Name}'");
                    for (var i = 0; i < entries.Count; i++)
                    {
                        Assert.AreNotSame(entries[i], copiedEntries[i], $"Copy() must not share entries of '{field.Name}'");
                        Assert.AreEqual(entries[i].Locale, copiedEntries[i].Locale, $"Copy() must preserve '{field.Name}' locales");
                        Assert.AreEqual(entries[i].Text, copiedEntries[i].Text, $"Copy() must preserve '{field.Name}' texts");
                    }
                    continue;
                }
                Assert.AreEqual(original, copied, $"Copy() must preserve field '{field.Name}'");
            }
        }

        // Every field Copy() is responsible for: public AND non-public instance
        // fields. Statics are excluded (they are not per-item state, and the default
        // GetFields() binding would include them).
        private static System.Reflection.FieldInfo[] CopyableFields() =>
            typeof(QuickActionItem).GetFields(System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance);

        [Test]
        public void IconType_NoneIsZero_AndOrderMatchesAppleEnum()
        {
            // The native iOS layer maps IconType value N (>0) to
            // UIApplicationShortcutIconType (N-1), so None must be 0 and Compose 1.
            Assert.AreEqual(0, (int)IconType.None);
            Assert.AreEqual(1, (int)IconType.Compose);
            Assert.AreEqual(4, (int)IconType.Add);
        }

        [Test]
        public void IconType_EveryValueIsPinned()
        {
            // Load-bearing contract: each member's integer is index-aligned with the
            // Android ICON_NAMES array and (value-1) with Apple's UIApplicationShortcut
            // IconType. A reorder (even one that keeps the count and endpoints) would
            // silently break the native icon mapping, so pin EVERY value, not just the
            // ends. If this fails after editing IconType, update ICON_NAMES in
            // QuickActionsBridge.java and the (value-1) cast in QuickActions.mm to match.
            var expected = new (IconType icon, int value)[]
            {
                (IconType.None, 0),
                (IconType.Compose, 1),
                (IconType.Play, 2),
                (IconType.Pause, 3),
                (IconType.Add, 4),
                (IconType.Location, 5),
                (IconType.Search, 6),
                (IconType.Share, 7),
                (IconType.Prohibit, 8),
                (IconType.Contact, 9),
                (IconType.Home, 10),
                (IconType.MarkLocation, 11),
                (IconType.Favorite, 12),
                (IconType.Love, 13),
                (IconType.Cloud, 14),
                (IconType.Invitation, 15),
                (IconType.Confirmation, 16),
                (IconType.Mail, 17),
                (IconType.Message, 18),
                (IconType.Date, 19),
                (IconType.Time, 20),
                (IconType.CapturePhoto, 21),
                (IconType.CaptureVideo, 22),
                (IconType.Task, 23),
                (IconType.TaskCompleted, 24),
                (IconType.Alarm, 25),
                (IconType.Bookmark, 26),
                (IconType.Shuffle, 27),
                (IconType.Audio, 28),
                (IconType.Update, 29),
            };

            foreach (var (icon, value) in expected)
                Assert.AreEqual(value, (int)icon, $"IconType.{icon} must equal {value}");

            // No member is left out of the table above (catches an ADDED value too).
            Assert.AreEqual(expected.Length, System.Enum.GetValues(typeof(IconType)).Length);
        }
    }
}
