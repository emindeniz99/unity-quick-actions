using System;
using System.Collections.Generic;
using UnityEngine;

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

        /// <summary>Parse a <c>{"items":[...]}</c> payload from the native layer.</summary>
        internal static List<QuickActionItem> Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
                return new List<QuickActionItem>();
            try
            {
                var list = JsonUtility.FromJson<QuickActionList>(json);
                return list?.items ?? new List<QuickActionItem>();
            }
            catch (Exception)
            {
                // A malformed native payload must not throw out of a facade method.
                return new List<QuickActionItem>();
            }
        }
    }
}
