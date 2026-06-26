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
        public void Reset() => QuickActions.RemoveAll();

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

                // Adding writes the full reconciled set back to the OS.
                Assert.IsTrue(QuickActions.Add(new QuickActionItem("os3", "Three")));
                Assert.AreEqual(3, fake.Shortcuts.Count);
            }
            finally
            {
                QuickActions.OverrideBridgeForTesting(null);
            }
        }

        private sealed class FakeBridge : IQuickActionsBridge
        {
            public readonly List<QuickActionItem> Shortcuts = new List<QuickActionItem>();
            public bool IsPlatformSupported => true;
            public void SetShortcuts(IList<QuickActionItem> items)
            {
                Shortcuts.Clear();
                Shortcuts.AddRange(items);
            }
            public void RemoveAll() => Shortcuts.Clear();
            public string GetLastPerformed() => null;
            public void ResetLastPerformed() { }
            public string ConsumePendingPerformed() => null;
            public IList<QuickActionItem> GetShortcuts() => new List<QuickActionItem>(Shortcuts);
        }
    }
}
