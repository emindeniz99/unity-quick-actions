using System.Collections.Generic;
using NUnit.Framework;
using Playground.QuickActions;
using Playground.QuickActions.Internal;

namespace Playground.QuickActions.Tests
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
            // The runtime's _ready gate + idempotent drain exist to deliver each tap
            // once; assert the count, not just the last id (which can't see a double-fire).
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
            public void SetShortcuts(IList<QuickActionItem> items)
            {
                SetCount++;
                Shortcuts.Clear();
                Shortcuts.AddRange(items);
            }
            public void RemoveAll() => Shortcuts.Clear();
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
            public void SetShortcuts(IList<QuickActionItem> items) { }
            public void RemoveAll() { }
            public string GetLastPerformed() => null;
            public void ResetLastPerformed() { }
            public string ConsumePendingPerformed() => _pending.Count > 0 ? _pending.Dequeue() : null;
            public IList<QuickActionItem> GetShortcuts() => new List<QuickActionItem>();
        }

        // A bridge whose RemoveAll throws, to verify RemoveAll's OS-first ordering.
        private sealed class ThrowingRemoveAllBridge : IQuickActionsBridge
        {
            private readonly List<QuickActionItem> _shortcuts = new List<QuickActionItem>();
            public bool IsPlatformSupported => true;
            public void SetShortcuts(IList<QuickActionItem> items)
            {
                _shortcuts.Clear();
                _shortcuts.AddRange(items);
            }
            public void RemoveAll() => throw new System.InvalidOperationException("native failure");
            public string GetLastPerformed() => null;
            public void ResetLastPerformed() { }
            public string ConsumePendingPerformed() => null;
            public IList<QuickActionItem> GetShortcuts() => new List<QuickActionItem>(_shortcuts);
        }

        private sealed class ReentrantBridge : IQuickActionsBridge
        {
            public int GetShortcutsCalls;
            public bool IsPlatformSupported => true;
            public void SetShortcuts(IList<QuickActionItem> items) { }
            public void RemoveAll() { }
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
    }
}
