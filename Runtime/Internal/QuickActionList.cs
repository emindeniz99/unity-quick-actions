using System;
using System.Collections.Generic;
using UnityEngine;

namespace EminDeniz99.QuickActions.Internal
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

        /// <summary>
        /// Parse a <c>{"items":[...]}</c> payload from the native layer. Returns
        /// <c>null</c> when the payload cannot be parsed — the same "read failed"
        /// signal <see cref="IQuickActionsBridge.GetShortcuts"/> documents — never an
        /// empty list: an empty list is authoritative, and the facade would act on it
        /// by pruning, which after a serializer failure would remove the user's real
        /// shortcuts. An empty or null string is the natives' "nothing" and parses to
        /// an empty list.
        /// </summary>
        internal static List<QuickActionItem> Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
                return new List<QuickActionItem>();
            try
            {
                var list = JsonUtility.FromJson<QuickActionList>(json);
                if (list == null)
                    return null;
                return list.items ?? new List<QuickActionItem>();
            }
            catch (Exception e)
            {
                // A malformed native payload must not throw out of a facade method —
                // and must not read as "no shortcuts" either.
                Debug.LogWarning("[QuickActions] Could not parse the native shortcut payload: " + e.Message);
                return null;
            }
        }
    }
}
