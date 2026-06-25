using System;
using System.Collections.Generic;

namespace Playground.QuickActions.Internal
{
    /// <summary>
    /// JsonUtility cannot (de)serialize a top-level array, so the shortcut set
    /// crosses the C#↔native boundary wrapped as <c>{"items":[...]}</c>. The
    /// native side parses it with NSJSONSerialization (iOS) / org.json (Android).
    /// Icon is serialized as its integer enum value.
    /// </summary>
    [Serializable]
    internal class QuickActionList
    {
        public List<QuickActionItem> items = new List<QuickActionItem>();

        internal QuickActionList() { }

        internal QuickActionList(IEnumerable<QuickActionItem> source)
        {
            if (source != null)
                items.AddRange(source);
        }
    }
}
