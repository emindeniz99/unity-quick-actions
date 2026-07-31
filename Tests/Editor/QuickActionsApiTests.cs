using System.Collections.Generic;
using NUnit.Framework;
using EminDeniz99.QuickActions;
using EminDeniz99.QuickActions.Internal;

namespace EminDeniz99.QuickActions.Tests
{
    /// <summary>
    /// White-box tests for the <see cref="QuickActions"/> facade's in-memory list
    /// management and event dispatch. These exercise pure C# logic (the Editor
    /// build uses the no-op bridge), so they run both in the Unity Test Runner and
    /// via `dotnet test` against the stub harness.
    /// </summary>
    [TestFixture]
    public class QuickActionsApiTests
    {
        [SetUp]
        public void Reset()
        {
            // Guarantee a clean default bridge and no leaked event subscribers even if
            // a prior test threw before its own cleanup ran (test isolation, Rule 9).
            QuickActions.OverrideBridgeForTesting(null);
            ClearPerformedSubscribers();
            // Locale is process-wide static and defaults to the DEVICE language, so
            // pin it: without this a machine running in French would resolve the
            // localization tests differently than CI, and a test that switches
            // locale would leak that switch into the next one.
            QuickActions.Locale = "en";
            QuickActions.RemoveAll();
        }

        [TearDown]
        public void Cleanup()
        {
            QuickActions.OverrideBridgeForTesting(null);
            ClearPerformedSubscribers();
        }

        // The Performed event is process-wide static; a handler that leaks from one
        // test would corrupt the next. Reset its backing field between tests.
        private static void ClearPerformedSubscribers()
        {
            var field = typeof(QuickActions).GetField("Performed",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            // Fail loud if the event is ever renamed — a silent no-op here would let
            // leaked subscribers corrupt later tests without any failure (Rule 9/12).
            Assert.NotNull(field, "QuickActions.Performed backing field not found — test isolation would silently break.");
            field.SetValue(null, null);
        }

        private static QuickActionItem Item(string id, string title = "Title") =>
            new QuickActionItem(id, title);

        [Test]
        public void Add_NewItem_ReturnsTrueAndIsQueryable()
        {
            Assert.IsTrue(QuickActions.Add(Item("a", "Alpha")));
            Assert.IsTrue(QuickActions.IsAdded("a"));
            Assert.AreEqual("Alpha", QuickActions.GetById("a").Title);
        }

        [Test]
        public void Add_DuplicateId_ReturnsFalseAndKeepsOne()
        {
            Assert.IsTrue(QuickActions.Add(Item("a")));
            Assert.IsFalse(QuickActions.Add(Item("a")));
            Assert.AreEqual(1, QuickActions.GetAll().Count);
        }

        [Test]
        public void Add_InvalidItem_ReturnsFalse()
        {
            Assert.IsFalse(QuickActions.Add(new QuickActionItem("", "NoId")));
            Assert.IsFalse(QuickActions.Add(new QuickActionItem("id", "")));
            Assert.AreEqual(0, QuickActions.GetAll().Count);
        }

        [Test]
        public void Add_Null_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => QuickActions.Add(null));
        }

        [Test]
        public void AddList_SkipsInvalidAndDuplicates()
        {
            QuickActions.Add(Item("a"));
            QuickActions.AddList(new List<QuickActionItem>
            {
                Item("a"),                          // duplicate -> skipped
                Item("b"),                          // added
                new QuickActionItem("", "bad"),     // invalid -> skipped
                Item("c"),                          // added
            });
            CollectionAssert.AreEquivalent(
                new[] { "a", "b", "c" },
                QuickActions.GetAll().ConvertAll(i => i.Id));
        }

        [Test]
        public void RemoveById_RemovesWhenPresent_ElseFalse()
        {
            QuickActions.Add(Item("a"));
            Assert.IsTrue(QuickActions.RemoveById("a"));
            Assert.IsFalse(QuickActions.IsAdded("a"));
            Assert.IsFalse(QuickActions.RemoveById("a"));
        }

        [Test]
        public void Remove_ByItem_UsesId()
        {
            QuickActions.Add(Item("a"));
            Assert.IsTrue(QuickActions.Remove(new QuickActionItem("a", "different title")));
            Assert.AreEqual(0, QuickActions.GetAll().Count);
        }

        [Test]
        public void RemoveAll_ClearsEverything()
        {
            QuickActions.AddList(new List<QuickActionItem> { Item("a"), Item("b") });
            QuickActions.RemoveAll();
            Assert.AreEqual(0, QuickActions.GetAll().Count);
        }

        [Test]
        public void RemoveAll_WhenBridgeReportsFailure_KeepsInMemoryState()
        {
            // A native remove that fails without throwing (e.g. locked profile → the
            // bridge returns false) must NOT clear the managed list, or GetAll() would
            // claim empty while the OS still shows shortcuts.
            QuickActions.OverrideBridgeForTesting(new FailingRemoveAllBridge());
            try
            {
                Assert.IsTrue(QuickActions.Add(Item("a")));
                QuickActions.RemoveAll();
                CollectionAssert.AreEquivalent(
                    new[] { "a" }, QuickActions.GetAll().ConvertAll(i => i.Id));
            }
            finally { QuickActions.OverrideBridgeForTesting(null); }
        }

        [Test]
        public void Add_WhenCurrentShortcutsCannotBeRead_DoesNotModifyTheOs()
        {
            // C1: a failed first read (GetShortcuts returns null) must NOT be cached as
            // an authoritative-empty set and let Add wipe the OS's existing shortcuts.
            var bridge = new ReadErrorBridge("A", "B"); // OS holds A,B; every read fails
            QuickActions.OverrideBridgeForTesting(bridge);
            try
            {
                Assert.IsFalse(QuickActions.Add(Item("C")), "Add must defer when the current set can't be read");
                Assert.AreEqual(0, bridge.SetCount, "must not push a partial set (which would delete A,B)");
                CollectionAssert.AreEquivalent(new[] { "A", "B" }, bridge.Os.ConvertAll(i => i.Id));
            }
            finally { QuickActions.OverrideBridgeForTesting(null); }
        }

        [Test]
        public void FailedRead_RetriesOnNextAccess_InsteadOfCachingErroredEmpty()
        {
            // Once the transient read clears, a later call must reconcile with the real OS.
            var bridge = new ReadErrorBridge("A") { FailReads = true };
            QuickActions.OverrideBridgeForTesting(bridge);
            try
            {
                Assert.IsFalse(QuickActions.Add(Item("C"))); // read fails → deferred
                bridge.FailReads = false;                    // transient condition clears
                CollectionAssert.AreEquivalent(new[] { "A" }, QuickActions.GetAll().ConvertAll(i => i.Id));
            }
            finally { QuickActions.OverrideBridgeForTesting(null); }
        }

        [Test]
        public void FailedWrite_AddReturnsFalse_AndRollsBack()
        {
            // When the OS write fails (the bridge returns null), Add must report the
            // failure (false) and roll back its optimistic mutation — the caller said
            // "install this" and it was NOT installed, so a true here would make the
            // shortcut silently vanish later with no signal to retry on. Reads are cut
            // AFTER the failed write so the assertions can only be satisfied by the
            // rollback itself, never masked by a reconcile (a failed write also
            // forces one — the push may have partially landed on Android).
            var bridge = new FailingSetShortcutsBridge("os1");
            QuickActions.OverrideBridgeForTesting(bridge);
            try
            {
                Assert.IsTrue(QuickActions.IsAdded("os1")); // load before cutting reads
                bridge.FailReads = true;
                Assert.IsFalse(QuickActions.Add(Item("new")), "a failed OS write must not report success");
                CollectionAssert.AreEquivalent(new[] { "os1" }, QuickActions.GetAll().ConvertAll(i => i.Id));
                Assert.IsFalse(QuickActions.IsAdded("new"));
            }
            finally { QuickActions.OverrideBridgeForTesting(null); }
        }

        [Test]
        public void FailedWrite_RemoveByIdReturnsFalse_AndKeepsItemAtItsPosition()
        {
            // A failed write means the device may still show the shortcut — RemoveById
            // must report false and keep the item (at its original position, since
            // order feeds Android ranks) rather than claiming a removal that will
            // silently resurrect on the next reconcile. Reads are cut after the failed
            // write so the order assertion observes the rollback's Insert, not a
            // reconcile (RemoveById forces one — a failed remove pushes a subset and
            // the device may have partially applied it).
            var bridge = new FailingSetShortcutsBridge("a", "b", "c");
            QuickActions.OverrideBridgeForTesting(bridge);
            try
            {
                Assert.IsTrue(QuickActions.IsAdded("a")); // load before cutting reads
                bridge.FailReads = true;
                Assert.IsFalse(QuickActions.RemoveById("b"));
                CollectionAssert.AreEqual(new[] { "a", "b", "c" }, QuickActions.GetAll().ConvertAll(i => i.Id));
            }
            finally { QuickActions.OverrideBridgeForTesting(null); }
        }

        [Test]
        public void FailedWrite_AddListAddsNothing()
        {
            var bridge = new FailingSetShortcutsBridge("os1");
            QuickActions.OverrideBridgeForTesting(bridge);
            try
            {
                Assert.IsTrue(QuickActions.IsAdded("os1")); // load before cutting reads
                bridge.FailReads = true;
                QuickActions.AddList(new List<QuickActionItem> { Item("x"), Item("y") });
                CollectionAssert.AreEquivalent(new[] { "os1" }, QuickActions.GetAll().ConvertAll(i => i.Id));
            }
            finally { QuickActions.OverrideBridgeForTesting(null); }
        }

        [Test]
        public void GetById_ReturnsIndependentCopy_MutationDoesNotAffectState()
        {
            QuickActions.Add(new QuickActionItem("a", "Alpha"));
            var got = QuickActions.GetById("a");
            got.Title = "Mutated";
            got.Icon = IconType.Search;
            Assert.AreEqual("Alpha", QuickActions.GetById("a").Title, "returned items must be independent copies");
            Assert.AreEqual(IconType.None, QuickActions.GetById("a").Icon);
        }

        [Test]
        public void Add_ThenMutatingCallerItem_DoesNotAffectState()
        {
            var item = new QuickActionItem("a", "Alpha");
            QuickActions.Add(item);
            item.Title = "Mutated"; // caller mutates its own object after adding
            Assert.AreEqual("Alpha", QuickActions.GetById("a").Title, "stored items must be copies of the caller's");
        }

        [Test]
        public void GetById_ReturnsMatch_NullForMissingOrNullOrEmpty()
        {
            QuickActions.Add(Item("a", "Alpha"));
            Assert.AreEqual("Alpha", QuickActions.GetById("a").Title);
            Assert.IsNull(QuickActions.GetById("missing"));
            Assert.IsNull(QuickActions.GetById(null));
            Assert.IsNull(QuickActions.GetById(""));
        }

        [Test]
        public void LastPerformed_ReflectsBridgeValue()
        {
            var fake = new LastPerformedBridge { Last = "boot_id" };
            QuickActions.OverrideBridgeForTesting(fake);
            try { Assert.AreEqual("boot_id", QuickActions.LastPerformed); }
            finally { QuickActions.OverrideBridgeForTesting(null); }
        }

        [Test]
        public void ResetLastPerformed_ClearsViaBridge()
        {
            var fake = new LastPerformedBridge { Last = "x" };
            QuickActions.OverrideBridgeForTesting(fake);
            try
            {
                QuickActions.ResetLastPerformed();
                Assert.AreEqual(1, fake.ResetCount);
                Assert.IsNull(QuickActions.LastPerformed);
            }
            finally { QuickActions.OverrideBridgeForTesting(null); }
        }

        [Test]
        public void IsPlatformSupported_ReflectsBridge_FalseOnNoOpBridge()
        {
            QuickActions.OverrideBridgeForTesting(new LastPerformedBridge());
            try { Assert.IsTrue(QuickActions.IsPlatformSupported); }
            finally { QuickActions.OverrideBridgeForTesting(null); }
            // The Editor/unsupported no-op bridge reports false (deterministic).
            Assert.IsFalse(new NullQuickActionsBridge().IsPlatformSupported);
        }

        [Test]
        public void EditorSimulateTap_RaisesPerformed_AndSetsLastPerformed_UntilReset()
        {
            string received = null;
            void Handler(string id) => received = id;
            QuickActions.Performed += Handler;
            try
            {
                QuickActions.EditorSimulateTap("sim_id");
                Assert.AreEqual("sim_id", received);
                Assert.AreEqual("sim_id", QuickActions.LastPerformed);
                QuickActions.ResetLastPerformed();
                Assert.IsNull(QuickActions.LastPerformed);
            }
            finally { QuickActions.Performed -= Handler; }
        }

        [Test]
        public void OverrideBridgeForTesting_ClearsSimulatedTapState()
        {
            // A Simulator click before a test run must not shadow the test bridge's
            // LastPerformed (the editor seam takes priority in the getter) — the test
            // seam has to wipe simulated state or edit-mode tests turn flaky.
            QuickActions.EditorSimulateTap("stale_sim");
            var fake = new LastPerformedBridge { Last = "boot_id" };
            QuickActions.OverrideBridgeForTesting(fake);
            try { Assert.AreEqual("boot_id", QuickActions.LastPerformed); }
            finally { QuickActions.OverrideBridgeForTesting(null); }
        }

        [Test]
        public void GetAll_PreservesInsertionOrder()
        {
            // Shortcut order is user-visible (it's the order pushed to the OS), so pin
            // the sequence with AreEqual — most other tests use AreEquivalent and would
            // not catch a reorder regression.
            QuickActions.Add(Item("z"));
            QuickActions.Add(Item("a"));
            QuickActions.AddList(new List<QuickActionItem> { Item("m"), Item("b") });
            CollectionAssert.AreEqual(
                new[] { "z", "a", "m", "b" },
                QuickActions.GetAll().ConvertAll(i => i.Id));
        }

        [Test]
        public void AddList_BeyondOsCap_PrunesSurplusAndPreservesIconsOfAccepted()
        {
            // WHY: this is Codex review #5 — when the OS trims dynamic shortcuts to its
            // cap, GetAll()/IsAdded() must reflect what the device kept (not over-report
            // the surplus), immediately, without waiting for a relaunch reconcile. The
            // icon assertion is load-bearing: it proves the kept items are the caller's
            // own objects (icons intact), not an icon-less device read-back.
            QuickActions.OverrideBridgeForTesting(new CapBridge(2));
            try
            {
                QuickActions.AddList(new List<QuickActionItem>
                {
                    new QuickActionItem("a", "Alpha", null, IconType.Add),
                    Item("b"),
                    Item("c"),
                });
                CollectionAssert.AreEqual(new[] { "a", "b" }, QuickActions.GetAll().ConvertAll(i => i.Id));
                Assert.IsFalse(QuickActions.IsAdded("c"), "surplus beyond the cap must not be over-reported");
                Assert.AreEqual(IconType.Add, QuickActions.GetById("a").Icon, "kept item must keep its supplied icon");
            }
            finally { QuickActions.OverrideBridgeForTesting(null); }
        }

        [Test]
        public void Add_BeyondOsCap_ReflectsWhatOsKept()
        {
            // The single-Add path (each Add calls Push and prunes), complementing the batch.
            QuickActions.OverrideBridgeForTesting(new CapBridge(2));
            try
            {
                Assert.IsTrue(QuickActions.Add(Item("a")));
                Assert.IsTrue(QuickActions.Add(Item("b")));
                // "c" is past the cap, so the OS drops it — Add must report failure, not
                // a success the caller can't observe (GetAll()/IsAdded() show it absent).
                Assert.IsFalse(QuickActions.Add(Item("c")));
                CollectionAssert.AreEqual(new[] { "a", "b" }, QuickActions.GetAll().ConvertAll(i => i.Id));
                Assert.IsFalse(QuickActions.IsAdded("c"));
            }
            finally { QuickActions.OverrideBridgeForTesting(null); }
        }

        [Test]
        public void RequestPin_DispatchesOnlyForAddedIds()
        {
            // WHY: pinning writes a live launcher icon; dispatching a pin for an id
            // this package doesn't manage would either fail confusingly in native
            // or (worse) pin someone else's shortcut. The facade must gate on its
            // own managed set before touching the bridge.
            var fake = new FakeBridge();
            QuickActions.OverrideBridgeForTesting(fake);
            try
            {
                Assert.IsFalse(QuickActions.RequestPin("ghost"), "unknown id must not reach the launcher");
                CollectionAssert.IsEmpty(fake.PinRequests);

                Assert.IsTrue(QuickActions.Add(Item("a")));
                Assert.IsTrue(QuickActions.RequestPin("a"));
                CollectionAssert.AreEqual(new[] { "a" }, fake.PinRequests);

                Assert.IsFalse(QuickActions.RequestPin(null));
                Assert.IsFalse(QuickActions.RequestPin(""));
            }
            finally { QuickActions.OverrideBridgeForTesting(null); }
        }

        [Test]
        public void Update_ReplacesInPlace_PreservingPosition()
        {
            // WHY: launchers order dynamic shortcuts by rank = list position; an
            // update that re-appended instead of replacing in place would silently
            // demote the shortcut to the end of the launcher menu.
            var fake = new FakeBridge();
            QuickActions.OverrideBridgeForTesting(fake);
            try
            {
                QuickActions.AddList(new List<QuickActionItem> { Item("a"), Item("b"), Item("c") });
                var updated = new QuickActionItem("b", "Better B", "now with subtitle", IconType.Play);
                Assert.IsTrue(QuickActions.Update(updated));

                CollectionAssert.AreEqual(new[] { "a", "b", "c" },
                    QuickActions.GetAll().ConvertAll(i => i.Id), "position must be preserved");
                Assert.AreEqual("Better B", QuickActions.GetById("b").Title);
                Assert.AreEqual(IconType.Play, QuickActions.GetById("b").Icon);
                CollectionAssert.AreEqual(new[] { "a", "b", "c" },
                    fake.Shortcuts.ConvertAll(i => i.Id), "the OS got the same order");

                updated.Title = "mutated-after-update"; // defensive copy, as with Add
                Assert.AreEqual("Better B", QuickActions.GetById("b").Title);
            }
            finally { QuickActions.OverrideBridgeForTesting(null); }
        }

        [Test]
        public void Update_UnknownOrInvalid_ReturnsFalseWithoutPushing()
        {
            var fake = new FakeBridge();
            QuickActions.OverrideBridgeForTesting(fake);
            try
            {
                Assert.IsTrue(QuickActions.Add(Item("a")));
                var pushes = fake.SetCount;
                Assert.IsFalse(QuickActions.Update(Item("ghost")), "not-added id must refuse (use Add)");
                Assert.IsFalse(QuickActions.Update(new QuickActionItem("a", "")), "invalid item must refuse");
                Assert.Throws<System.ArgumentNullException>(() => QuickActions.Update(null));
                Assert.AreEqual(pushes, fake.SetCount, "refused updates must not touch the OS");
            }
            finally { QuickActions.OverrideBridgeForTesting(null); }
        }

        [Test]
        public void Update_FailedWrite_RestoresThePreviousItem()
        {
            // WHY: same partial-landing contract as Add/RemoveById — a failed OS
            // write must leave queries reporting the item the device still shows
            // (the previous one), not the update that never landed.
            var bridge = new TogglingWriteBridge();
            QuickActions.OverrideBridgeForTesting(bridge);
            try
            {
                Assert.IsTrue(QuickActions.Add(new QuickActionItem("a", "Original")));
                bridge.FailWrites = true;
                Assert.IsFalse(QuickActions.Update(new QuickActionItem("a", "Doomed")));

                // Cut reads too: the ONLY way GetById can now answer "Original" is
                // the in-place restore itself (a reconcile is impossible), so a
                // missing `_items[index] = previous` can't hide behind a re-read.
                bridge.FailReads = true;
                Assert.AreEqual("Original", QuickActions.GetById("a").Title,
                    "the previous item must be restored in place, not recovered by a reconcile");

                // And separately pin the forced reconcile (_loaded = false): hand
                // the OS a different truth, allow reads, and the facade must adopt
                // it — impossible if the failed Update left its cache authoritative.
                bridge.Os.Clear();
                bridge.Os.Add(new QuickActionItem("z", "Z"));
                bridge.FailReads = false;
                CollectionAssert.AreEqual(new[] { "z" }, QuickActions.GetAll().ConvertAll(i => i.Id),
                    "a failed Update must force a reconcile with the device state");
            }
            finally { QuickActions.OverrideBridgeForTesting(null); }
        }

        [Test]
        public void Update_OsDropsTheUpdatedItem_ReturnsFalseAndReportsItGone()
        {
            // WHY: this is Update's own "honesty contract" (same as Add's dropped
            // branch): when the shared budget shrank between pushes — a host app
            // published more shortcuts — the push replaces the previous item and
            // the OS keeps neither. Update must return false and queries must show
            // the id gone; deleting the dropped-id branch must fail THIS test.
            var bridge = new CapBridge(2);
            QuickActions.OverrideBridgeForTesting(bridge);
            try
            {
                Assert.IsTrue(QuickActions.Add(Item("a")));
                Assert.IsTrue(QuickActions.Add(Item("b")));
                bridge.Cap = 1; // the host ate a slot since our last push
                Assert.IsFalse(QuickActions.Update(new QuickActionItem("b", "Bigger B")),
                    "an update the OS dropped must not report success");
                Assert.IsFalse(QuickActions.IsAdded("b"), "the dropped id must read as gone");
                CollectionAssert.AreEqual(new[] { "a" }, QuickActions.GetAll().ConvertAll(i => i.Id));
            }
            finally { QuickActions.OverrideBridgeForTesting(null); }
        }

        [Test]
        public void Update_Succeeds_WhileTheOsPrunesAnotherItem()
        {
            // WHY: Update's success is per-ITEM, not per-push — the same shrunken
            // budget can keep the updated item yet drop a later one. Update must
            // return true (its item landed) while queries honestly drop the other.
            var bridge = new CapBridge(3);
            QuickActions.OverrideBridgeForTesting(bridge);
            try
            {
                QuickActions.AddList(new List<QuickActionItem> { Item("a"), Item("b"), Item("c") });
                bridge.Cap = 2;
                Assert.IsTrue(QuickActions.Update(new QuickActionItem("b", "Better B")),
                    "the updated item survived the push — that is a success");
                CollectionAssert.AreEqual(new[] { "a", "b" }, QuickActions.GetAll().ConvertAll(i => i.Id));
                Assert.IsFalse(QuickActions.IsAdded("c"), "the item beyond the shrunken cap is gone");
                Assert.AreEqual("Better B", QuickActions.GetById("b").Title);
            }
            finally { QuickActions.OverrideBridgeForTesting(null); }
        }

        [Test]
        public void ReportUsed_SendsOnlyForAddedIds()
        {
            // WHY: same ownership gate as RequestPin — reporting usage of an id this
            // package doesn't manage would skew the launcher's ranking for someone
            // else's shortcut (or a ghost).
            var fake = new FakeBridge();
            QuickActions.OverrideBridgeForTesting(fake);
            try
            {
                Assert.IsFalse(QuickActions.ReportUsed("ghost"));
                CollectionAssert.IsEmpty(fake.UsageReports);

                Assert.IsTrue(QuickActions.Add(Item("a")));
                Assert.IsTrue(QuickActions.ReportUsed("a"));
                CollectionAssert.AreEqual(new[] { "a" }, fake.UsageReports);
                Assert.IsFalse(QuickActions.ReportUsed(null));
            }
            finally { QuickActions.OverrideBridgeForTesting(null); }
        }

        [Test]
        public void MaxShortcutCount_SurfacesTheBridgeValue()
        {
            // WHY: callers size their shortcut sets to this number (the README tells
            // them to); it must come from the platform bridge, not a C# constant.
            // FakeBridge reports 7 — a value no real bridge or default uses — so a
            // facade that hardcodes the iOS 4 (or anything else) fails here.
            QuickActions.OverrideBridgeForTesting(new FakeBridge());
            try { Assert.AreEqual(7, QuickActions.MaxShortcutCount); }
            finally { QuickActions.OverrideBridgeForTesting(null); }
        }

        [Test]
        public void Payload_SurvivesAddAndIsReadableById()
        {
            // WHY: the documented payload pattern is "Performed gives the id, read
            // the payload via GetById" — that only works if the facade's defensive
            // copies carry Payload through Add and back out of GetById.
            QuickActions.OverrideBridgeForTesting(new FakeBridge());
            try
            {
                var item = Item("daily");
                item.Payload = "reward=daily&streak=3";
                Assert.IsTrue(QuickActions.Add(item));

                item.Payload = "mutated-after-add"; // must not leak into the stored copy
                Assert.AreEqual("reward=daily&streak=3", QuickActions.GetById("daily").Payload);
            }
            finally { QuickActions.OverrideBridgeForTesting(null); }
        }

        [Test]
        public void AcceptAllBridge_DoesNotPrune()
        {
            // Guards against an over-eager prune: a bridge that accepts everything (returns
            // the input reference) must never drop anything, even past the typical cap.
            var fake = new FakeBridge();
            QuickActions.OverrideBridgeForTesting(fake);
            try
            {
                QuickActions.AddList(new List<QuickActionItem>
                {
                    Item("a"), Item("b"), Item("c"), Item("d"), Item("e"), Item("f"),
                });
                CollectionAssert.AreEqual(
                    new[] { "a", "b", "c", "d", "e", "f" },
                    QuickActions.GetAll().ConvertAll(i => i.Id));
            }
            finally { QuickActions.OverrideBridgeForTesting(null); }
        }

        [Test]
        public void EmptyAcceptedSet_PrunesEverything()
        {
            // A bridge that accepts nothing (empty, non-null result) must leave the public
            // API reporting no shortcuts. This is the contract the Android API<25 path
            // relies on: SetShortcuts returns an empty accepted set there (not accept-all),
            // so GetAll()/IsAdded() don't claim shortcuts the OS never installed.
            QuickActions.OverrideBridgeForTesting(new CapBridge(0));
            try
            {
                Assert.IsFalse(QuickActions.Add(Item("a"))); // the OS kept none, so the add did not take
                Assert.IsEmpty(QuickActions.GetAll());        // ...nothing installed
                Assert.IsFalse(QuickActions.IsAdded("a"));
            }
            finally { QuickActions.OverrideBridgeForTesting(null); }
        }

        [Test]
        public void GetAll_ReturnsCopy_NotInternalList()
        {
            QuickActions.Add(Item("a"));
            QuickActions.GetAll().Clear();              // mutating the copy must not affect state
            Assert.AreEqual(1, QuickActions.GetAll().Count);
        }

        [Test]
        public void Dispatch_RaisesPerformedWithId()
        {
            string received = null;
            void Handler(string id) => received = id;
            QuickActions.Performed += Handler;
            try
            {
                QuickActions.Dispatch("launch_id");
                Assert.AreEqual("launch_id", received);
            }
            finally
            {
                QuickActions.Performed -= Handler;
            }
        }

        [Test]
        public void Dispatch_NullOrEmpty_DoesNotRaise()
        {
            var raised = false;
            void Handler(string id) => raised = true;
            QuickActions.Performed += Handler;
            try
            {
                QuickActions.Dispatch(null);
                QuickActions.Dispatch("");
                Assert.IsFalse(raised);
            }
            finally
            {
                QuickActions.Performed -= Handler;
            }
        }

        [Test]
        public void FirstAccess_ReconcilesFromOsPersistedShortcuts()
        {
            var fake = new FakeBridge();
            fake.Shortcuts.Add(new QuickActionItem("os1", "One"));
            fake.Shortcuts.Add(new QuickActionItem("os2", "Two"));
            QuickActions.OverrideBridgeForTesting(fake);
            try
            {
                // GetAll/IsAdded reflect what the OS already had (W1).
                Assert.IsTrue(QuickActions.IsAdded("os1"));
                CollectionAssert.AreEquivalent(
                    new[] { "os1", "os2" },
                    QuickActions.GetAll().ConvertAll(i => i.Id));

                // Adding writes the full MERGED set back to the OS (not just the new one).
                Assert.IsTrue(QuickActions.Add(new QuickActionItem("os3", "Three")));
                CollectionAssert.AreEquivalent(
                    new[] { "os1", "os2", "os3" },
                    fake.Shortcuts.ConvertAll(i => i.Id));
            }
            finally
            {
                QuickActions.OverrideBridgeForTesting(null);
            }
        }

        [Test]
        public void Reconcile_DropsInvalidAndDuplicateOsItems()
        {
            var fake = new FakeBridge();
            fake.Shortcuts.Add(new QuickActionItem("dup", "A"));
            fake.Shortcuts.Add(new QuickActionItem("dup", "B"));      // duplicate id
            fake.Shortcuts.Add(new QuickActionItem("", "no id"));      // invalid
            QuickActions.OverrideBridgeForTesting(fake);
            try
            {
                CollectionAssert.AreEquivalent(new[] { "dup" }, QuickActions.GetAll().ConvertAll(i => i.Id));
            }
            finally { QuickActions.OverrideBridgeForTesting(null); }
        }

        [Test]
        public void RemoveAll_ThenAdd_DoesNotResurrectOsShortcuts()
        {
            var fake = new FakeBridge();
            fake.Shortcuts.Add(new QuickActionItem("os1", "One"));
            fake.Shortcuts.Add(new QuickActionItem("os2", "Two"));
            QuickActions.OverrideBridgeForTesting(fake);
            try
            {
                QuickActions.RemoveAll();                 // clean slate despite OS having os1,os2
                Assert.AreEqual(0, QuickActions.GetAll().Count);
                Assert.AreEqual(0, fake.Shortcuts.Count);

                Assert.IsTrue(QuickActions.Add(new QuickActionItem("x", "X")));
                CollectionAssert.AreEquivalent(new[] { "x" }, QuickActions.GetAll().ConvertAll(i => i.Id));
            }
            finally { QuickActions.OverrideBridgeForTesting(null); }
        }

        [Test]
        public void AddList_AllInvalidOrDuplicate_DoesNotPush()
        {
            var fake = new FakeBridge();
            QuickActions.OverrideBridgeForTesting(fake);
            try
            {
                Assert.IsTrue(QuickActions.Add(Item("a")));
                var before = fake.SetCount;
                QuickActions.AddList(new List<QuickActionItem> { Item("a"), null, new QuickActionItem("", "bad") });
                Assert.AreEqual(before, fake.SetCount); // nothing valid/new → no OS push
            }
            finally { QuickActions.OverrideBridgeForTesting(null); }
        }

        [Test]
        public void AddList_WithNullElement_SkipsItNoThrow()
        {
            Assert.DoesNotThrow(() =>
                QuickActions.AddList(new List<QuickActionItem> { null, Item("b") }));
            CollectionAssert.AreEquivalent(new[] { "b" }, QuickActions.GetAll().ConvertAll(i => i.Id));
        }

        [Test]
        public void EnsureLoaded_ReentrantBridge_LoadsOnceWithoutRecursing()
        {
            var fake = new ReentrantBridge();
            QuickActions.OverrideBridgeForTesting(fake);
            try
            {
                // GetShortcuts re-enters the facade (IsAdded) mid-load; the _loading
                // guard must prevent recursion and only load once.
                Assert.DoesNotThrow(() => QuickActions.IsAdded("anything"));
                Assert.AreEqual(1, fake.GetShortcutsCalls);
            }
            finally { QuickActions.OverrideBridgeForTesting(null); }
        }

        [Test]
        public void Dispatch_RaisesPerformedExactlyOnce()
        {
            // Scope: one Dispatch call = exactly one Performed invocation (count, not
            // just last-id, so a double-fire regression is visible). The queue-drain
            // exactly-once guarantee has its own test:
            // Drain_DeliversBufferedIdsInOrderExactlyOnce.
            var count = 0;
            void Handler(string id) => count++;
            QuickActions.Performed += Handler;
            try
            {
                QuickActions.Dispatch("launch_id");
                Assert.AreEqual(1, count);
            }
            finally
            {
                QuickActions.Performed -= Handler;
            }
        }

        [Test]
        public void AddList_ValidItems_PushesToOsExactlyOnce()
        {
            // N valid items must be one OS update, not N (the whole point of AddList).
            var fake = new FakeBridge();
            QuickActions.OverrideBridgeForTesting(fake);
            try
            {
                var before = fake.SetCount;
                QuickActions.AddList(new List<QuickActionItem> { Item("a"), Item("b"), Item("c") });
                Assert.AreEqual(before + 1, fake.SetCount);
                CollectionAssert.AreEquivalent(
                    new[] { "a", "b", "c" }, fake.Shortcuts.ConvertAll(i => i.Id));
            }
            finally { QuickActions.OverrideBridgeForTesting(null); }
        }

        [Test]
        public void RemoveAll_WhenBridgeThrows_LeavesInMemoryStateIntact()
        {
            // RemoveAll clears the OS FIRST; if that throws, the in-memory list must
            // survive so a later access reconciles. This locks the documented ordering:
            // reordering RemoveAll to clear _items first would make this test fail.
            var fake = new ThrowingRemoveAllBridge();
            QuickActions.OverrideBridgeForTesting(fake);
            try
            {
                Assert.IsTrue(QuickActions.Add(Item("a")));
                Assert.Throws<System.InvalidOperationException>(() => QuickActions.RemoveAll());
                CollectionAssert.AreEquivalent(
                    new[] { "a" }, QuickActions.GetAll().ConvertAll(i => i.Id));
            }
            finally { QuickActions.OverrideBridgeForTesting(null); }
        }

        [Test]
        public void Drain_DeliversBufferedIdsInOrderExactlyOnce()
        {
            // Exercises the real delivery invariant that QuickActionsRuntime.PollPending
            // relies on: ConsumeNextPending drains the native queue, each id dispatched
            // once, in order, and a second drain yields nothing. (The MonoBehaviour pump
            // is Unity-only; this drives the same loop over its building blocks.)
            var fake = new QueueBridge(new[] { "a", "b", "c" });
            QuickActions.OverrideBridgeForTesting(fake);
            var received = new List<string>();
            void Handler(string id) => received.Add(id);
            QuickActions.Performed += Handler;
            try
            {
                string id;
                while (!string.IsNullOrEmpty(id = QuickActions.ConsumeNextPending()))
                    QuickActions.Dispatch(id);

                CollectionAssert.AreEqual(new[] { "a", "b", "c" }, received); // order pinned, not just set
                Assert.IsNull(QuickActions.ConsumeNextPending());             // consumed exactly once
            }
            finally
            {
                QuickActions.Performed -= Handler;
                QuickActions.OverrideBridgeForTesting(null);
            }
        }

        [Test]
        public void Resolve_PrefersExactLocale_ThenLanguagePrefix_ThenBaseText()
        {
            // WHY this precedence: a pt-BR device must get the Brazilian string when
            // one exists and the generic pt string when it doesn't, and a locale
            // nobody translated must fall back to the author's base text — an empty
            // label is refused by the OS, so "translate or blank" is not an option.
            var item = new QuickActionItem("play", "Play", "Continue");
            item.LocalizedTitles.Add(new LocalizedText("pt", "Jogar"));
            item.LocalizedTitles.Add(new LocalizedText("PT-br", "Jogar agora"));
            item.LocalizedTitles.Add(new LocalizedText("de", "")); // no text to render

            Assert.AreEqual("Jogar agora", QuickActionLocalization.ResolveTitle(item, "pt-BR"), "an exact match wins");
            Assert.AreEqual("Jogar agora", QuickActionLocalization.ResolveTitle(item, "PT-BR"), "matching ignores case");
            Assert.AreEqual("Jogar", QuickActionLocalization.ResolveTitle(item, "pt-PT"), "the language prefix is the next choice");
            Assert.AreEqual("Jogar", QuickActionLocalization.ResolveTitle(item, "pt"));
            Assert.AreEqual("Play", QuickActionLocalization.ResolveTitle(item, "fr"), "an untranslated locale keeps the base text");
            Assert.AreEqual("Play", QuickActionLocalization.ResolveTitle(item, "de"), "an empty translation is not a translation");
            Assert.AreEqual("Continue", QuickActionLocalization.ResolveSubtitle(item, "pt-BR"),
                "a title-only translation must not blank the subtitle");
        }

        [Test]
        public void Push_SendsResolvedText_WhileTheManagedSetKeepsTheBase()
        {
            // WHY: a shortcut holds ONE label, so the natives must receive final text
            // — but the managed item has to keep the base text and its tables, or the
            // next locale switch would translate an already-translated label.
            var fake = new FakeBridge();
            QuickActions.OverrideBridgeForTesting(fake);
            QuickActions.Locale = "fr";
            try
            {
                var item = new QuickActionItem("play", "Play", "Continue");
                item.LocalizedTitles.Add(new LocalizedText("fr", "Jouer"));
                item.LocalizedSubtitles.Add(new LocalizedText("fr", "Continuer"));
                Assert.IsTrue(QuickActions.Add(item));

                Assert.AreEqual("Jouer", fake.Shortcuts[0].Title, "the OS must receive the RESOLVED title");
                Assert.AreEqual("Continuer", fake.Shortcuts[0].Subtitle);
                // The exact blob, not just "non-empty": this string is the wire
                // format the natives persist verbatim, and the Java smoke test
                // (.verify/JavaSmoke) feeds the SAME literal through
                // setShortcuts/getShortcutsJson — pinning it here is what keeps the
                // two sides from drifting apart unnoticed.
                Assert.AreEqual("qa14:Play8:Continue1:12:fr5:Jouer1:12:fr9:Continuer", fake.Shortcuts[0].L10n,
                    "the tables must ride along so a cold start can restore them");

                var stored = QuickActions.GetById("play");
                Assert.AreEqual("Play", stored.Title, "the managed set keeps the base text");
                Assert.AreEqual("Continue", stored.Subtitle);
                CollectionAssert.AreEqual(new[] { "Jouer" }, stored.LocalizedTitles.ConvertAll(t => t.Text));
            }
            finally { QuickActions.OverrideBridgeForTesting(null); }
        }

        [Test]
        public void Locale_SetToADifferentValue_RePushesOnce_AndAnUnchangedValueDoesNot()
        {
            // WHY: this is the in-app language-picker path — the shortcuts must
            // re-render along with the rest of the UI. And an assignment that changes
            // nothing observable must not spend an OS write (Android rate-limits them
            // in the background, so a wasted one can cost a real update later).
            var fake = new FakeBridge();
            QuickActions.OverrideBridgeForTesting(fake);
            try
            {
                var item = new QuickActionItem("play", "Play");
                item.LocalizedTitles.Add(new LocalizedText("fr", "Jouer"));
                Assert.IsTrue(QuickActions.Add(item));
                var pushes = fake.SetCount;

                QuickActions.Locale = "fr";
                Assert.AreEqual(pushes + 1, fake.SetCount, "a language switch must re-render the installed shortcuts");
                Assert.AreEqual("Jouer", fake.Shortcuts[0].Title);

                QuickActions.Locale = "fr";
                QuickActions.Locale = "FR"; // resolution ignores case → nothing to re-render
                Assert.AreEqual(pushes + 1, fake.SetCount, "an unchanged locale must not touch the OS");

                QuickActions.Locale = "en";
                Assert.AreEqual(pushes + 2, fake.SetCount);
                Assert.AreEqual("Play", fake.Shortcuts[0].Title,
                    "switching back resolves from the base text, not from the previous translation");
            }
            finally { QuickActions.OverrideBridgeForTesting(null); }
        }

        [Test]
        public void Reconcile_RestoresBaseTextAndTables_AndRefreshesAStaleLanguageExactlyOnce()
        {
            // WHY (the feature's core promise): the app was killed, the user changed
            // the device language, and the launcher still shows last session's French
            // labels. The reconcile must restore each item's base text + tables from
            // the payload the OS handed back and re-render ONCE — never adopt "Jouer"
            // as the base title (later switches would then translate a translation),
            // and never push again on the reads that follow.
            var authored = new QuickActionItem("play", "Play", "Continue");
            authored.LocalizedTitles.Add(new LocalizedText("fr", "Jouer"));
            authored.LocalizedSubtitles.Add(new LocalizedText("fr", "Continuer"));

            var fake = new FakeBridge();
            // Exactly what a French session left on the device: resolved labels plus
            // the blob the natives persist verbatim and hand back.
            fake.Shortcuts.Add(QuickActionLocalization.Resolved(authored, "fr"));
            QuickActions.OverrideBridgeForTesting(fake);
            QuickActions.Locale = "en"; // the device language changed while we were dead
            try
            {
                var restored = QuickActions.GetById("play");
                Assert.AreEqual("Play", restored.Title, "the base text must come back, not the French label");
                Assert.AreEqual("Continue", restored.Subtitle);
                CollectionAssert.AreEqual(new[] { "Jouer" }, restored.LocalizedTitles.ConvertAll(t => t.Text),
                    "the per-locale table must survive the round trip");
                CollectionAssert.AreEqual(new[] { "Continuer" }, restored.LocalizedSubtitles.ConvertAll(t => t.Text));
                Assert.AreEqual(1, fake.SetCount, "exactly one localization-refresh push");
                Assert.AreEqual("Play", fake.Shortcuts[0].Title, "the launcher now shows the current locale");

                QuickActions.GetAll();
                QuickActions.IsAdded("play");
                Assert.AreEqual(1, fake.SetCount, "the refresh must not repeat on every later read");
            }
            finally { QuickActions.OverrideBridgeForTesting(null); }
        }

        [Test]
        public void LocalizationBlob_RoundTripsAdversarialText_AndSurvivesCorruption()
        {
            // WHY: the blob carries arbitrary user text through two native layers
            // (Android extras, iOS userInfo) and back. Length prefixes are what make
            // it escaping-free, so a label containing the delimiter, a quote, a
            // newline or an astral-plane character must come back byte-for-byte — and
            // a truncated payload must degrade to "no localization" rather than throw
            // out of the cold-start reconcile.
            var item = new QuickActionItem("weird", "3:not a length", "line\nbreak\"quote\\");
            item.LocalizedTitles.Add(new LocalizedText("fr-CA", "Émoji 🎮 : ok"));

            var wire = QuickActionLocalization.Resolved(item, "fr-CA");
            Assert.AreEqual("Émoji 🎮 : ok", wire.Title, "the OS gets the resolved title");

            Assert.IsFalse(QuickActionLocalization.Restore(wire, "fr-CA"),
                "the shown text already matches the locale — nothing to refresh");
            Assert.AreEqual("3:not a length", wire.Title, "delimiter-shaped base text survives the round trip");
            Assert.AreEqual("line\nbreak\"quote\\", wire.Subtitle);
            Assert.AreEqual("Émoji 🎮 : ok", wire.LocalizedTitles[0].Text);

            var truncated = new QuickActionItem("weird", "Shown")
            {
                L10n = QuickActionLocalization.Encode(item).Substring(0, 12),
            };
            Assert.IsFalse(QuickActionLocalization.Restore(truncated, "fr"), "a corrupted blob must not claim a refresh");
            Assert.AreEqual("Shown", truncated.Title, "...and must leave the item exactly as the OS reported it");

            // A length near int.MaxValue is the corruption that BREAKS a naive bounds
            // check: "start + length" wraps negative and slips past it, and the
            // Substring then throws straight out of GetAll/Add/IsAdded — precisely the
            // "reject it, don't throw" contract above. 15 characters is enough.
            var forged = new QuickActionItem("weird", "Shown") { L10n = "qa1" + int.MaxValue + ":x" };
            Assert.IsFalse(QuickActionLocalization.Restore(forged, "fr"),
                "a forged length must be rejected, not overflow the bounds check");
            Assert.AreEqual("Shown", forged.Title);
        }

        [Test]
        public void Reconcile_PushesNothing_WhenTheOsTextAlreadyMatchesTheLocale()
        {
            // The other half of the loop guard: the refresh is triggered by a real
            // mismatch only, so neither an app that never localizes nor one whose
            // labels are already current gains an OS write per cold start.
            var localized = new QuickActionItem("os2", "Play");
            localized.LocalizedTitles.Add(new LocalizedText("fr", "Jouer"));

            var fake = new FakeBridge();
            fake.Shortcuts.Add(new QuickActionItem("os1", "One"));
            fake.Shortcuts.Add(QuickActionLocalization.Resolved(localized, "en")); // already in the current locale
            QuickActions.OverrideBridgeForTesting(fake);
            try
            {
                Assert.IsTrue(QuickActions.IsAdded("os1"));
                Assert.AreEqual("Play", QuickActions.GetById("os2").Title);
                Assert.AreEqual(0, fake.SetCount, "a reconcile that found nothing stale must not write");
            }
            finally { QuickActions.OverrideBridgeForTesting(null); }
        }

        // Models a device whose reads succeed and whose WRITES the OS refuses (the
        // Android background rate limit). Reads hand back FRESH objects rebuilt from
        // the stored set, exactly like both real bridges do (they re-parse JSON per
        // read) — the accept-all FakeBridge hands out its own references instead, and
        // Restore nulling L10n in place on those permanently de-localizes the fake's
        // items, which would mask every repeat of a stale-language refresh.
        private sealed class RefusedWriteBridge : IQuickActionsBridge
        {
            // What the device currently holds/renders — assertable, and the source
            // reads are rebuilt from.
            public readonly List<QuickActionItem> Os = new List<QuickActionItem>();
            // Writes the OS still refuses; every SetShortcuts consumes one.
            // int.MaxValue = "refuses for the whole test"; a FINITE count models the
            // refuse→accept transition inside a single call (a one-off JNI /
            // system_server failure), which is the window the AddList loss needs.
            public int WritesToRefuse = int.MaxValue;
            public int SetCount;
            public bool IsPlatformSupported => true;
            public int MaxShortcutCount => 4;
            public bool IsPinSupported => false;
            public bool RequestPin(string id) => false;
            public bool ReportUsed(string id) => false;
            public IList<QuickActionItem> SetShortcuts(IList<QuickActionItem> items)
            {
                SetCount++;
                if (WritesToRefuse > 0)
                {
                    WritesToRefuse--;
                    return null;
                }
                Os.Clear();
                foreach (var item in items) Os.Add(item.Copy());
                return items;
            }
            public bool RemoveAll() { Os.Clear(); return true; }
            public string GetLastPerformed() => null;
            public void ResetLastPerformed() { }
            public string ConsumePendingPerformed() => null;
            public IList<QuickActionItem> GetShortcuts() => Os.ConvertAll(i => i.Copy());
        }

        [Test]
        public void RefusedRefresh_KeepsTheManagedSetAuthoritative_SoAddListDropsNothing()
        {
            // WHY: EnsureLoaded()==true has to mean "_items IS the set" — AddList
            // appends optimistic copies and then calls IsAdded per item, which
            // re-enters EnsureLoaded. If a refused refresh push left the facade
            // un-loaded, that re-entry would reload and CLEAR the copies appended so
            // far: they never reach the push, AddList reports no failure, and the
            // final loop blames the OS for a loss the package caused itself.
            var authored = new QuickActionItem("old", "Play");
            authored.LocalizedTitles.Add(new LocalizedText("fr", "Jouer"));
            // A French session left 'old' on the device; Locale is "en" (SetUp), so the
            // load finds it stale. The OS refuses the next TWO writes and then accepts:
            // the refresh push and its retry fail, AddList's own push lands — the exact
            // refuse→accept interleaving in which the loss was reproduced.
            var bridge = new RefusedWriteBridge { WritesToRefuse = 2 };
            bridge.Os.Add(QuickActionLocalization.Resolved(authored, "fr"));
            QuickActions.OverrideBridgeForTesting(bridge);
            try
            {
                QuickActions.AddList(new List<QuickActionItem> { Item("a"), Item("b") });

                CollectionAssert.AreEquivalent(new[] { "old", "a", "b" },
                    bridge.Os.ConvertAll(i => i.Id),
                    "every item of the batch must reach the OS — a reload mid-AddList would drop the earlier ones");
                CollectionAssert.AreEquivalent(new[] { "old", "a", "b" },
                    QuickActions.GetAll().ConvertAll(i => i.Id));
            }
            finally { QuickActions.OverrideBridgeForTesting(null); }
        }

        [Test]
        public void RefusedRefresh_RetriesExactlyOnce_ThenReadsIssueNoMoreOsWrites()
        {
            // WHY: the refresh is a WRITE performed from the load path, and GetAll/
            // GetById/IsAdded all load. Without a spend-once latch each read re-detects
            // the same staleness and issues another setShortcuts — an API documented as
            // a "Membership test" becoming one OS write per frame for a polling game,
            // burning the very rate-limit budget that refused the push. One retry, then
            // silence: a later successful mutation re-renders the labels anyway.
            var authored = new QuickActionItem("play", "Play");
            authored.LocalizedTitles.Add(new LocalizedText("fr", "Jouer"));
            var bridge = new RefusedWriteBridge(); // refuses every write
            bridge.Os.Add(QuickActionLocalization.Resolved(authored, "fr"));
            QuickActions.OverrideBridgeForTesting(bridge);
            try
            {
                Assert.IsTrue(QuickActions.IsAdded("play"));
                Assert.AreEqual(1, bridge.SetCount, "the load's own refresh push");

                Assert.IsTrue(QuickActions.IsAdded("play"));
                Assert.AreEqual(2, bridge.SetCount, "the armed retry — exactly one more");

                QuickActions.IsAdded("play");
                QuickActions.GetAll();
                QuickActions.GetById("play");
                Assert.AreEqual(2, bridge.SetCount,
                    "read-only calls must not keep writing once the single retry is spent");
            }
            finally { QuickActions.OverrideBridgeForTesting(null); }
        }

        [Test]
        public void RefusedRefresh_IsHealedByTheNextSuccessfulMutationPush()
        {
            // The other half of "one retry is enough": the app keeps using the API, and
            // the first write the OS accepts renders the current locale — so the labels
            // heal without any read ever forcing an OS write.
            var authored = new QuickActionItem("play", "Play");
            authored.LocalizedTitles.Add(new LocalizedText("fr", "Jouer"));
            // Refuse the refresh push AND its retry, then accept: only the mutation's
            // own push is left to heal the label.
            var bridge = new RefusedWriteBridge { WritesToRefuse = 2 };
            bridge.Os.Add(QuickActionLocalization.Resolved(authored, "fr"));
            QuickActions.OverrideBridgeForTesting(bridge);
            try
            {
                Assert.IsTrue(QuickActions.IsAdded("play")); // the refresh push is refused
                Assert.IsTrue(QuickActions.Add(Item("extra")));
                Assert.AreEqual("Play", bridge.Os[0].Title,
                    "the accepted push re-rendered the stale label for the current locale");
            }
            finally { QuickActions.OverrideBridgeForTesting(null); }
        }

        [Test]
        public void Locale_SetBeforeAnythingIsLoaded_ReRendersTheDevicesShortcuts()
        {
            // WHY: this is the documented in-app language-picker path on a COLD start —
            // the app restores its saved language in Awake and touches nothing else.
            // _items is empty there because nothing has loaded yet, not because nothing
            // is installed; conflating the two left the launcher in the old language for
            // the whole session.
            var authored = new QuickActionItem("play", "Play");
            authored.LocalizedTitles.Add(new LocalizedText("fr", "Jouer"));
            var fake = new FakeBridge();
            fake.Shortcuts.Add(QuickActionLocalization.Resolved(authored, "en")); // device shows "Play"
            QuickActions.OverrideBridgeForTesting(fake);
            try
            {
                QuickActions.Locale = "fr";
                Assert.AreEqual(1, fake.SetCount, "the first assignment of a session must reconcile and re-push");
                Assert.AreEqual("Jouer", fake.Shortcuts[0].Title);
            }
            finally { QuickActions.OverrideBridgeForTesting(null); }
        }

        [Test]
        public void Locale_SetWithNothingInstalled_DoesNotSpendAnOsWrite()
        {
            // The empty-set early return still has to hold once the reconcile has
            // PROVEN the OS set is empty: pushing an empty payload anyway is a write
            // that changes nothing, and on Android even an empty addDynamicShortcuts
            // burns a rate-limit token a real update may need later.
            var fake = new FakeBridge();
            QuickActions.OverrideBridgeForTesting(fake);
            try
            {
                Assert.IsFalse(QuickActions.IsAdded("anything")); // reconcile: the OS really is empty
                QuickActions.Locale = "de";
                Assert.AreEqual(0, fake.SetCount, "nothing installed — nothing to re-render");

                // ...and the assignment still took effect, so the FIRST push uses it.
                var item = new QuickActionItem("play", "Play");
                item.LocalizedTitles.Add(new LocalizedText("de", "Spielen"));
                Assert.IsTrue(QuickActions.Add(item));
                Assert.AreEqual("Spielen", fake.Shortcuts[0].Title);
            }
            finally { QuickActions.OverrideBridgeForTesting(null); }
        }

        [Test]
        public void Locale_WhenTheRePushIsRefused_ArmsOneRetry_AndReadsStayReadOnly()
        {
            // The setter's failure branch must follow the reconcile's rule: the managed
            // set still matches the device's IDS (only the labels are stale), so keep it
            // authoritative and spend exactly one retry — never turn reads into writes.
            var authored = new QuickActionItem("play", "Play");
            authored.LocalizedTitles.Add(new LocalizedText("fr", "Jouer"));
            var bridge = new TogglingWriteBridge();
            QuickActions.OverrideBridgeForTesting(bridge);
            try
            {
                Assert.IsTrue(QuickActions.Add(authored));
                var pushes = bridge.SetCount;

                bridge.FailWrites = true;
                QuickActions.Locale = "fr";
                Assert.AreEqual(pushes + 1, bridge.SetCount, "the refused re-push");

                QuickActions.GetAll();
                Assert.AreEqual(pushes + 2, bridge.SetCount, "the armed retry — still refused");
                QuickActions.GetAll();
                QuickActions.IsAdded("play");
                Assert.AreEqual(pushes + 2, bridge.SetCount, "and then reads stop writing");

                // The retry is what a caller relies on when the rate limit lifts:
                // allow writes, re-arm with another switch, and the labels land.
                bridge.FailWrites = false;
                QuickActions.Locale = "en";
                Assert.AreEqual("Play", bridge.Os[0].Title);
            }
            finally { QuickActions.OverrideBridgeForTesting(null); }
        }

        [Test]
        public void Reconcile_RefreshesAnItemWhoseOnlyStaleLabelIsItsSubtitle()
        {
            // WHY: Restore's staleness answer is title-mismatch OR subtitle-mismatch.
            // An item with a locale-invariant title and a translated subtitle exercises
            // ONLY the second half — drop it and this item keeps rendering last
            // session's subtitle under the new locale with nothing to notice it.
            var authored = new QuickActionItem("play", "Play", "Continue");
            authored.LocalizedSubtitles.Add(new LocalizedText("fr", "Continuer"));

            var fake = new FakeBridge();
            fake.Shortcuts.Add(QuickActionLocalization.Resolved(authored, "fr"));
            QuickActions.OverrideBridgeForTesting(fake);
            try
            {
                Assert.AreEqual("Continue", QuickActions.GetById("play").Subtitle,
                    "the base subtitle must come back, not the French one");
                Assert.AreEqual(1, fake.SetCount, "a stale SUBTITLE alone must trigger the refresh push");
                Assert.AreEqual("Continuer", authored.LocalizedSubtitles[0].Text);
                Assert.AreEqual("Continue", fake.Shortcuts[0].Subtitle, "the launcher now shows the current locale");
            }
            finally { QuickActions.OverrideBridgeForTesting(null); }
        }

        [Test]
        public void FromSystemLanguage_MapsTheDeviceLanguage_IncludingUnitysTypoedHungarian()
        {
            // WHY: this switch is what makes "defaults to the device language" true, and
            // nothing else in the suite reaches it (Application.systemLanguage is a stub
            // constant and every other test pins Locale). Hungarian is the case that was
            // missing: Unity's member is the typo `Hugarian` (value 18) with `Hungarian`
            // as its usable alias, so a Hungarian phone silently answered "en" and every
            // "hu" translation was unreachable.
            Assert.AreEqual("hu", QuickActionLocalization.FromSystemLanguage(UnityEngine.SystemLanguage.Hungarian));
            Assert.AreEqual("fr", QuickActionLocalization.FromSystemLanguage(UnityEngine.SystemLanguage.French));
            Assert.AreEqual("zh-Hans", QuickActionLocalization.FromSystemLanguage(UnityEngine.SystemLanguage.ChineseSimplified));
            Assert.AreEqual("zh-Hant", QuickActionLocalization.FromSystemLanguage(UnityEngine.SystemLanguage.ChineseTraditional));
            // Documented fallbacks, not omissions: English IS "en", and an unknown
            // device language deliberately answers "en" too.
            Assert.AreEqual("en", QuickActionLocalization.FromSystemLanguage(UnityEngine.SystemLanguage.English));
            Assert.AreEqual("en", QuickActionLocalization.FromSystemLanguage(UnityEngine.SystemLanguage.Unknown));
        }

        private sealed class FakeBridge : IQuickActionsBridge
        {
            public readonly List<QuickActionItem> Shortcuts = new List<QuickActionItem>();
            public int SetCount;
            public bool IsPlatformSupported => true;
            public int MaxShortcutCount => 7; // deliberately NOT 4 (the iOS/stub default), so a
                                              // hardcoded facade constant can't pass the surfacing test
            // Accept-all pin recorder: it does NOT re-implement the managed-set
            // gate (the Java layer's own gate is smoke-tested separately) — so a
            // RequestPin that reaches the bridge for an unmanaged id shows up in
            // PinRequests and FAILS the facade-gating test instead of being
            // silently absorbed by a duplicate check here.
            public readonly List<string> PinRequests = new List<string>();
            public bool IsPinSupported => true;
            public bool RequestPin(string id) { PinRequests.Add(id); return true; }
            // Accept-all recorder, same rationale as RequestPin: the facade's
            // managed-set gate is the code under test, not a duplicate here.
            public readonly List<string> UsageReports = new List<string>();
            public bool ReportUsed(string id) { UsageReports.Add(id); return true; }
            public IList<QuickActionItem> SetShortcuts(IList<QuickActionItem> items)
            {
                SetCount++;
                Shortcuts.Clear();
                Shortcuts.AddRange(items);
                return items; // accept-all (same reference) — facade prunes nothing
            }
            public bool RemoveAll() { Shortcuts.Clear(); return true; }
            public string GetLastPerformed() => null;
            public void ResetLastPerformed() { }
            public string ConsumePendingPerformed() => null;
            public IList<QuickActionItem> GetShortcuts() => new List<QuickActionItem>(Shortcuts);
        }

        // A bridge that hands back a scripted queue of pending ids (cold-launch buffer).
        private sealed class QueueBridge : IQuickActionsBridge
        {
            private readonly Queue<string> _pending;
            public QueueBridge(IEnumerable<string> ids) => _pending = new Queue<string>(ids);
            public bool IsPlatformSupported => true;
            public int MaxShortcutCount => 4;
            public bool IsPinSupported => false;
            public bool RequestPin(string id) => false;
            public bool ReportUsed(string id) => false;
            public IList<QuickActionItem> SetShortcuts(IList<QuickActionItem> items) => items;
            public bool RemoveAll() => true;
            public string GetLastPerformed() => null;
            public void ResetLastPerformed() { }
            public string ConsumePendingPerformed() => _pending.Count > 0 ? _pending.Dequeue() : null;
            public IList<QuickActionItem> GetShortcuts() => new List<QuickActionItem>();
        }

        // A bridge with a settable last-performed id, to test LastPerformed/Reset.
        private sealed class LastPerformedBridge : IQuickActionsBridge
        {
            public string Last;
            public int ResetCount;
            public bool IsPlatformSupported => true;
            public int MaxShortcutCount => 4;
            public bool IsPinSupported => false;
            public bool RequestPin(string id) => false;
            public bool ReportUsed(string id) => false;
            public IList<QuickActionItem> SetShortcuts(IList<QuickActionItem> items) => items;
            public bool RemoveAll() => true;
            public string GetLastPerformed() => Last;
            public void ResetLastPerformed() { Last = null; ResetCount++; }
            public string ConsumePendingPerformed() => null;
            public IList<QuickActionItem> GetShortcuts() => new List<QuickActionItem>();
        }

        // A bridge whose RemoveAll throws, to verify RemoveAll's OS-first ordering.
        private sealed class ThrowingRemoveAllBridge : IQuickActionsBridge
        {
            private readonly List<QuickActionItem> _shortcuts = new List<QuickActionItem>();
            public bool IsPlatformSupported => true;
            public int MaxShortcutCount => 4;
            public bool IsPinSupported => false;
            public bool RequestPin(string id) => false;
            public bool ReportUsed(string id) => false;
            public IList<QuickActionItem> SetShortcuts(IList<QuickActionItem> items)
            {
                _shortcuts.Clear();
                _shortcuts.AddRange(items);
                return items;
            }
            public bool RemoveAll() => throw new System.InvalidOperationException("native failure");
            public string GetLastPerformed() => null;
            public void ResetLastPerformed() { }
            public string ConsumePendingPerformed() => null;
            public IList<QuickActionItem> GetShortcuts() => new List<QuickActionItem>(_shortcuts);
        }

        private sealed class ReentrantBridge : IQuickActionsBridge
        {
            public int GetShortcutsCalls;
            public bool IsPlatformSupported => true;
            public int MaxShortcutCount => 4;
            public bool IsPinSupported => false;
            public bool RequestPin(string id) => false;
            public bool ReportUsed(string id) => false;
            public IList<QuickActionItem> SetShortcuts(IList<QuickActionItem> items) => items;
            public bool RemoveAll() => true;
            public string GetLastPerformed() => null;
            public void ResetLastPerformed() { }
            public string ConsumePendingPerformed() => null;
            public IList<QuickActionItem> GetShortcuts()
            {
                GetShortcutsCalls++;
                QuickActions.IsAdded("reentry"); // re-enter during load
                return new List<QuickActionItem>();
            }
        }

        // Models the Android OS cap: keeps only the first `Cap` items and returns
        // exactly that trimmed subset as a NEW list (not the input reference), so the
        // facade actually prunes. GetShortcuts() is intentionally empty to prove the
        // prune relies solely on the SetShortcuts RETURN, never a device read-back.
        private sealed class CapBridge : IQuickActionsBridge
        {
            // Mutable so a test can shrink the budget BETWEEN pushes — modelling a
            // host app publishing more shortcuts into the shared cap mid-session.
            public int Cap;
            public readonly List<QuickActionItem> Shortcuts = new List<QuickActionItem>();
            public CapBridge(int cap) => Cap = cap;
            public bool IsPlatformSupported => true;
            public int MaxShortcutCount => 4;
            public bool IsPinSupported => false;
            public bool RequestPin(string id) => false;
            public bool ReportUsed(string id) => false;
            public IList<QuickActionItem> SetShortcuts(IList<QuickActionItem> items)
            {
                var accepted = new List<QuickActionItem>();
                for (var i = 0; i < items.Count && i < Cap; i++)
                    accepted.Add(items[i]);
                Shortcuts.Clear();
                Shortcuts.AddRange(accepted);
                return accepted;
            }
            public bool RemoveAll() { Shortcuts.Clear(); return true; }
            public string GetLastPerformed() => null;
            public void ResetLastPerformed() { }
            public string ConsumePendingPerformed() => null;
            public IList<QuickActionItem> GetShortcuts() => new List<QuickActionItem>();
        }

        // A bridge whose GetShortcuts read fails (returns null) — models a locked/
        // direct-boot device. Tracks its "OS" set and how many times SetShortcuts ran,
        // so a test can prove Add doesn't wipe the OS when the read failed.
        private sealed class ReadErrorBridge : IQuickActionsBridge
        {
            public readonly List<QuickActionItem> Os = new List<QuickActionItem>();
            public int SetCount;
            public bool FailReads = true;
            public ReadErrorBridge(params string[] osIds)
            {
                foreach (var id in osIds) Os.Add(new QuickActionItem(id, id));
            }
            public bool IsPlatformSupported => true;
            public int MaxShortcutCount => 4;
            public bool IsPinSupported => false;
            public bool RequestPin(string id) => false;
            public bool ReportUsed(string id) => false;
            public IList<QuickActionItem> SetShortcuts(IList<QuickActionItem> items)
            {
                SetCount++;
                Os.Clear();
                foreach (var i in items) Os.Add(i);
                return items;
            }
            public bool RemoveAll() { Os.Clear(); return true; }
            public string GetLastPerformed() => null;
            public void ResetLastPerformed() { }
            public string ConsumePendingPerformed() => null;
            public IList<QuickActionItem> GetShortcuts() => FailReads ? null : new List<QuickActionItem>(Os);
        }

        // A bridge whose SetShortcuts reports failure (null) — models an OS write that
        // was rejected/rate-limited. GetShortcuts returns a fixed "device" state so the
        // facade's next-access reconcile has something authoritative to sync to; flip
        // FailReads to make reads fail too, so a test can prove an assertion is
        // satisfied by the CALLER'S ROLLBACK rather than masked by a reconcile.
        private sealed class FailingSetShortcutsBridge : IQuickActionsBridge
        {
            private readonly List<QuickActionItem> _os = new List<QuickActionItem>();
            public bool FailReads;
            public FailingSetShortcutsBridge(params string[] osIds)
            {
                foreach (var id in osIds) _os.Add(new QuickActionItem(id, id));
            }
            public bool IsPlatformSupported => true;
            public int MaxShortcutCount => 4;
            public bool IsPinSupported => false;
            public bool RequestPin(string id) => false;
            public bool ReportUsed(string id) => false;
            public IList<QuickActionItem> SetShortcuts(IList<QuickActionItem> items) => null; // write failed
            public bool RemoveAll() => true;
            public string GetLastPerformed() => null;
            public void ResetLastPerformed() { }
            public string ConsumePendingPerformed() => null;
            public IList<QuickActionItem> GetShortcuts() => FailReads ? null : new List<QuickActionItem>(_os);
        }

        // A bridge whose writes can be toggled to fail (null) while reads keep
        // reflecting the last SUCCESSFUL write — models a transient Android
        // rate-limit, where the device still shows the pre-failure set.
        private sealed class TogglingWriteBridge : IQuickActionsBridge
        {
            public readonly List<QuickActionItem> Os = new List<QuickActionItem>();
            public bool FailWrites;
            public bool FailReads;
            public int SetCount;
            public bool IsPlatformSupported => true;
            public int MaxShortcutCount => 4;
            public bool IsPinSupported => false;
            public bool RequestPin(string id) => false;
            public bool ReportUsed(string id) => false;
            public IList<QuickActionItem> SetShortcuts(IList<QuickActionItem> items)
            {
                SetCount++;
                if (FailWrites) return null;
                Os.Clear();
                Os.AddRange(items);
                return items;
            }
            public bool RemoveAll() { Os.Clear(); return true; }
            public string GetLastPerformed() => null;
            public void ResetLastPerformed() { }
            public string ConsumePendingPerformed() => null;
            public IList<QuickActionItem> GetShortcuts() => FailReads ? null : new List<QuickActionItem>(Os);
        }

        // A bridge whose RemoveAll reports failure (false) WITHOUT throwing — models an
        // OS remove that no-ops on a locked profile.
        private sealed class FailingRemoveAllBridge : IQuickActionsBridge
        {
            public bool IsPlatformSupported => true;
            public int MaxShortcutCount => 4;
            public bool IsPinSupported => false;
            public bool RequestPin(string id) => false;
            public bool ReportUsed(string id) => false;
            public IList<QuickActionItem> SetShortcuts(IList<QuickActionItem> items) => items;
            public bool RemoveAll() => false;
            public string GetLastPerformed() => null;
            public void ResetLastPerformed() { }
            public string ConsumePendingPerformed() => null;
            public IList<QuickActionItem> GetShortcuts() => new List<QuickActionItem>();
        }
    }
}
