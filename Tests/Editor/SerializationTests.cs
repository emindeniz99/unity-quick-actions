using NUnit.Framework;
using EminDeniz99.QuickActions;
using EminDeniz99.QuickActions.Internal;
using UnityEngine;

namespace EminDeniz99.QuickActions.Tests
{
    /// <summary>
    /// Verifies the JSON contract the native layers parse. Uses the real
    /// <see cref="JsonUtility"/>, so this fixture is Unity-only (it is excluded
    /// from the stub-based `dotnet test`, whose JsonUtility stub is inert).
    /// </summary>
    [TestFixture]
    public class SerializationTests
    {
        [Test]
        public void ToJson_WrapsItemsArray_WithIconAsInt()
        {
            var list = new QuickActionList(new[]
            {
                new QuickActionItem("new_game", "New Game", "Start fresh", IconType.Add),
            });

            var json = JsonUtility.ToJson(list);

            StringAssert.Contains("\"items\"", json);
            StringAssert.Contains("\"Id\":\"new_game\"", json);
            StringAssert.Contains("\"Title\":\"New Game\"", json);
            StringAssert.Contains("\"Subtitle\":\"Start fresh\"", json);
            // IconType.Add == 4 must serialize as the integer 4 (native reads a number).
            StringAssert.Contains("\"Icon\":4", json);
        }

        [Test]
        public void RoundTrip_PreservesFields()
        {
            var original = new QuickActionList(new[]
            {
                new QuickActionItem("a", "Alpha", "sub", IconType.Play),
            });

            var restored = JsonUtility.FromJson<QuickActionList>(JsonUtility.ToJson(original));

            Assert.AreEqual(1, restored.items.Count);
            Assert.AreEqual("a", restored.items[0].Id);
            Assert.AreEqual(IconType.Play, restored.items[0].Icon);
        }
    }
}
