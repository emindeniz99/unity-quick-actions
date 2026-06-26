using System.Collections.Generic;
using NUnit.Framework;
using Playground.QuickActions;

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
    }
}
