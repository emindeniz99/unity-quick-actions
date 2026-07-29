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

        // Models the Android OS cap: keeps only the first `_cap` items and returns
        // exactly that trimmed subset as a NEW list (not the input reference), so the
        // facade actually prunes. GetShortcuts() is intentionally empty to prove the
        // prune relies solely on the SetShortcuts RETURN, never a device read-back.
        private sealed class CapBridge : IQuickActionsBridge
        {
            private readonly int _cap;
            public readonly List<QuickActionItem> Shortcuts = new List<QuickActionItem>();
            public CapBridge(int cap) => _cap = cap;
            public bool IsPlatformSupported => true;
            public int MaxShortcutCount => 4;
            public bool IsPinSupported => false;
            public bool RequestPin(string id) => false;
            public IList<QuickActionItem> SetShortcuts(IList<QuickActionItem> items)
            {
                var accepted = new List<QuickActionItem>();
                for (var i = 0; i < items.Count && i < _cap; i++)
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
            public IList<QuickActionItem> SetShortcuts(IList<QuickActionItem> items) => null; // write failed
            public bool RemoveAll() => true;
            public string GetLastPerformed() => null;
            public void ResetLastPerformed() { }
            public string ConsumePendingPerformed() => null;
            public IList<QuickActionItem> GetShortcuts() => FailReads ? null : new List<QuickActionItem>(_os);
        }

        // A bridge whose RemoveAll reports failure (false) WITHOUT throwing — models an
        // OS remove that no-ops on a locked profile.
        private sealed class FailingRemoveAllBridge : IQuickActionsBridge
        {
            public bool IsPlatformSupported => true;
            public int MaxShortcutCount => 4;
            public bool IsPinSupported => false;
            public bool RequestPin(string id) => false;
            public IList<QuickActionItem> SetShortcuts(IList<QuickActionItem> items) => items;
            public bool RemoveAll() => false;
            public string GetLastPerformed() => null;
            public void ResetLastPerformed() { }
            public string ConsumePendingPerformed() => null;
            public IList<QuickActionItem> GetShortcuts() => new List<QuickActionItem>();
        }
    }
}
