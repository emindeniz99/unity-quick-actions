using System;

namespace EminDeniz99.QuickActions
{
    /// <summary>
    /// One locale's text for a <see cref="QuickActionItem"/> title or subtitle:
    /// <see cref="Locale"/> is a BCP-47-ish tag (<c>"fr"</c>, <c>"pt-BR"</c>) and
    /// <see cref="Text"/> the string shown while that locale is active.
    ///
    /// WHY a list of pairs rather than a dictionary: the shortcut set crosses the
    /// C#↔native boundary through <c>JsonUtility</c>, which cannot (de)serialize a
    /// <c>Dictionary</c> — the list of pairs <b>is</b> the wire format, not a
    /// convenience wrapper over one. Matching is case-insensitive and falls back
    /// from a region tag to its language (<c>"pt-BR"</c> → a <c>"pt"</c> entry) and
    /// finally to the item's base text. Entries with an empty locale or empty text
    /// are ignored: an empty label is refused by the OS, so falling back to the
    /// base text is the only outcome the caller can actually observe.
    /// </summary>
    [Serializable]
    public class LocalizedText
    {
        /// <summary>Locale tag this text applies to, e.g. <c>"fr"</c> or <c>"pt-BR"</c>.</summary>
        public string Locale;

        /// <summary>Title/subtitle shown when <see cref="QuickActions.Locale"/> matches.</summary>
        public string Text;

        public LocalizedText() { }

        public LocalizedText(string locale, string text)
        {
            Locale = locale;
            Text = text;
        }

        public override string ToString() => $"LocalizedText({Locale}={Text})";
    }
}
