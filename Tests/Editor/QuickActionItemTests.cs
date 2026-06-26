using NUnit.Framework;
using Playground.QuickActions;

namespace Playground.QuickActions.Tests
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
        public void IconType_NoneIsZero_AndOrderMatchesAppleEnum()
        {
            // The native iOS layer maps IconType value N (>0) to
            // UIApplicationShortcutIconType (N-1), so None must be 0 and Compose 1.
            Assert.AreEqual(0, (int)IconType.None);
            Assert.AreEqual(1, (int)IconType.Compose);
            Assert.AreEqual(4, (int)IconType.Add);
        }

        [Test]
        public void IconType_CountAndRangeArePinned()
        {
            // Load-bearing contract: 30 values (None=0 .. Update=29), index-aligned
            // with the Android ICON_NAMES array and (value-1) with Apple's enum.
            // If this fails after editing IconType, update ICON_NAMES in
            // QuickActionsBridge.java to match.
            Assert.AreEqual(30, System.Enum.GetValues(typeof(IconType)).Length);
            Assert.AreEqual(29, (int)IconType.Update);
        }
    }
}
