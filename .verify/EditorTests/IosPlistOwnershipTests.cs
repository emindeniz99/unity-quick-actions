// Harness-only tests for the iOS Info.plist ownership rule
// (Editor/NativeGate/iOS/QuickActionsIosOwnership.cs). The define-off cleanup
// strips static shortcuts through ClearOurEntries, and the gated post-processor
// calls the same method when it has nothing to bake, so this is the one removal
// path on iOS. It has to leave every entry a host app or another plugin wrote.
//
// Driven on an in-memory PlistDocument: the stub never reads or writes a file,
// so the cleanup's file wrapper (exists / read / write when changed) is not
// reachable here, only the decision it writes back.
using NUnit.Framework;
using EminDeniz99.QuickActions.Editor.NativeGate;
using UnityEditor.iOS.Xcode;

namespace EminDeniz99.QuickActions.Tests
{
    [TestFixture]
    public class IosPlistOwnershipTests
    {
        // Spelled out rather than read back from the rule: the first two are Apple's
        // keys, and the marker is persisted on devices, so a typo in the source must
        // not be agreed with here.
        private const string ItemsKey = "UIApplicationShortcutItems";
        private const string UserInfoKey = "UIApplicationShortcutItemUserInfo";
        private const string MarkerKey = "com.emindeniz99.quickactions.managed";

        private static PlistDocument PlistWith(params PlistElement[] entries)
        {
            var plist = new PlistDocument();
            var items = new PlistElementArray();
            items.values.AddRange(entries);
            plist.root.values[ItemsKey] = items;
            return plist;
        }

        // A shortcut entry whose user info holds `marker` under our key; null
        // leaves the key out while keeping the user-info dictionary.
        private static PlistElementDict EntryWithMarker(PlistElement marker)
        {
            var userInfo = new PlistElementDict();
            if (marker != null)
                userInfo.values[MarkerKey] = marker;
            var entry = new PlistElementDict();
            entry.values[UserInfoKey] = userInfo;
            return entry;
        }

        private static PlistElementDict Ours() => EntryWithMarker(new PlistElementBoolean(true));

        private static PlistElementArray Items(PlistDocument plist) =>
            (PlistElementArray)plist.root.values[ItemsKey];

        [Test]
        public void IsOurs_OnlyForADictionaryWhoseUserInfoMarksItTrue()
        {
            Assert.IsTrue(QuickActionsPlistShortcuts.IsOurs(Ours()));

            Assert.IsFalse(QuickActionsPlistShortcuts.IsOurs(EntryWithMarker(new PlistElementBoolean(false))),
                "a marker set to false is not ours");
            Assert.IsFalse(QuickActionsPlistShortcuts.IsOurs(EntryWithMarker(null)),
                "user info without our key belongs to someone else");
            Assert.IsFalse(QuickActionsPlistShortcuts.IsOurs(new PlistElementDict()),
                "an entry with no user info at all is a host's");
            Assert.IsFalse(QuickActionsPlistShortcuts.IsOurs(new PlistElementArray()),
                "a non-dictionary element is never ours");
        }

        [Test]
        public void ClearOurEntries_RemovesOnlyOurs_AndKeepsEveryOtherEntryInOrder()
        {
            var hostNoUserInfo = new PlistElementDict();
            var hostOtherUserInfo = EntryWithMarker(null);
            var hostMarkerFalse = EntryWithMarker(new PlistElementBoolean(false));
            var plist = PlistWith(Ours(), hostNoUserInfo, Ours(), hostOtherUserInfo, hostMarkerFalse);

            Assert.IsTrue(QuickActionsPlistShortcuts.ClearOurEntries(plist), "the plist changed");

            CollectionAssert.AreEqual(
                new PlistElement[] { hostNoUserInfo, hostOtherUserInfo, hostMarkerFalse },
                Items(plist).values);
        }

        [Test]
        public void ClearOurEntries_WhenOnlyOursWereThere_DropsTheKey()
        {
            var plist = PlistWith(Ours(), Ours());

            Assert.IsTrue(QuickActionsPlistShortcuts.ClearOurEntries(plist));

            Assert.IsFalse(plist.root.values.ContainsKey(ItemsKey),
                "an empty UIApplicationShortcutItems array is not left behind");
        }

        [Test]
        public void ClearOurEntries_WithNothingOfOurs_ReportsNoChange()
        {
            // The callers write the file back only on true, so false must mean the
            // host's entries are exactly as they were.
            var host = new PlistElementDict();
            var plist = PlistWith(host);

            Assert.IsFalse(QuickActionsPlistShortcuts.ClearOurEntries(plist));

            CollectionAssert.AreEqual(new PlistElement[] { host }, Items(plist).values);
        }

        [Test]
        public void ClearOurEntries_WithoutTheKeyOrWithAnotherType_ReportsNoChange()
        {
            Assert.IsFalse(QuickActionsPlistShortcuts.ClearOurEntries(new PlistDocument()));

            var plist = new PlistDocument();
            var notAnArray = new PlistElementDict();
            plist.root.values[ItemsKey] = notAnArray;
            Assert.IsFalse(QuickActionsPlistShortcuts.ClearOurEntries(plist));
            Assert.AreSame(notAnArray, plist.root.values[ItemsKey], "a value of another type is not ours to touch");
        }
    }
}
