using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace EminDeniz99.QuickActions.Internal
{
    /// <summary>
    /// Per-locale title/subtitle resolution, plus the encoding that carries an
    /// item's localization state through an OS that stores none of it.
    ///
    /// A shortcut holds exactly ONE label per platform field — the string the user
    /// sees — so resolution happens here, at push time: <see cref="Resolved"/>
    /// hands the bridge a copy whose Title/Subtitle are already final, and the
    /// natives stay pure pass-through (no locale logic in Java/ObjC). Everything
    /// needed to resolve again later (the base text plus both tables) rides along
    /// in <see cref="QuickActionItem.L10n"/>, which the natives persist verbatim and
    /// return on a cold-start read; <see cref="Restore"/> turns that back into an
    /// item and reports whether the device is still showing a stale language.
    /// </summary>
    internal static class QuickActionLocalization
    {
        // Blob format: "qa1" followed by length-prefixed fields ("<len>:<value>") —
        // base title, base subtitle, the title table (count, then locale/text pairs)
        // and the subtitle table.
        // WHY hand-rolled instead of a JsonUtility wrapper: the blob is opaque to
        // both natives (they store and hand back the string untouched), so it only
        // has to be unambiguous and round-trip-safe — and staying independent of
        // Unity's serializer keeps the whole round trip exercised by the stub-based
        // test harness, where JsonUtility is inert, instead of only inside a real
        // Editor. Length prefixes mean no escaping and no delimiter a title could
        // collide with (titles are arbitrary user text).
        private const string BlobHeader = "qa1";

        /// <summary>
        /// The copy a push hands to the bridge: Title/Subtitle resolved for
        /// <paramref name="locale"/>, the tables folded into the blob (so they don't
        /// travel twice), and the caller's stored item left untouched — the managed
        /// list must keep the base text, or a later locale switch would translate a
        /// translation.
        /// </summary>
        internal static QuickActionItem Resolved(QuickActionItem item, string locale)
        {
            var copy = item.Copy();
            copy.Title = Resolve(item.LocalizedTitles, item.Title, locale);
            copy.Subtitle = Resolve(item.LocalizedSubtitles, item.Subtitle, locale);
            copy.L10n = Encode(item);
            copy.LocalizedTitles.Clear();
            copy.LocalizedSubtitles.Clear();
            return copy;
        }

        /// <summary>
        /// Exact locale match (case-insensitive) beats a language-prefix match
        /// (<c>"pt-BR"</c> resolves a <c>"pt"</c> entry) beats the base text.
        /// </summary>
        internal static string Resolve(List<LocalizedText> entries, string fallback, string locale)
        {
            if (entries == null || entries.Count == 0 || string.IsNullOrEmpty(locale))
                return fallback;
            var exact = Find(entries, locale);
            if (exact != null)
                return exact;
            var separator = locale.IndexOf('-');
            if (separator > 0)
            {
                var language = Find(entries, locale.Substring(0, separator));
                if (language != null)
                    return language;
            }
            return fallback;
        }

        /// <summary>Title this item renders with under <paramref name="locale"/>.</summary>
        internal static string ResolveTitle(QuickActionItem item, string locale) =>
            Resolve(item.LocalizedTitles, item.Title, locale);

        /// <summary>Subtitle this item renders with under <paramref name="locale"/>.</summary>
        internal static string ResolveSubtitle(QuickActionItem item, string locale) =>
            Resolve(item.LocalizedSubtitles, item.Subtitle, locale);

        /// <summary>
        /// Undoes <see cref="Resolved"/> on an item that came back from the OS: its
        /// read-back Title/Subtitle are what the device SHOWS (resolved at the last
        /// push, possibly in a language the user has since left), so the base text
        /// and both tables are restored from the blob.
        /// </summary>
        /// <returns>
        /// True when the shown text no longer matches what <paramref name="locale"/>
        /// resolves to — the shortcut is rendered in a stale language and one push
        /// re-renders it. False for an item without localization (or with an
        /// unreadable blob), so an unlocalized set never triggers a write.
        /// </returns>
        internal static bool Restore(QuickActionItem item, string locale)
        {
            var blob = item.L10n;
            item.L10n = null; // wire-only: it must never linger in the managed set
            if (string.IsNullOrEmpty(blob) || !blob.StartsWith(BlobHeader, StringComparison.Ordinal))
                return false;

            var index = BlobHeader.Length;
            if (!TryRead(blob, ref index, out var baseTitle) ||
                !TryRead(blob, ref index, out var baseSubtitle))
                return false; // truncated/corrupted payload: keep what the OS shows
            var titles = ReadEntries(blob, ref index);
            var subtitles = ReadEntries(blob, ref index);
            if (titles == null || subtitles == null)
                return false;

            var shownTitle = item.Title;
            var shownSubtitle = item.Subtitle;
            // An empty base title would make the item invalid and drop it from the
            // managed set — keep the shown text in that (never-written) case.
            if (!string.IsNullOrEmpty(baseTitle))
                item.Title = baseTitle;
            item.Subtitle = baseSubtitle;
            item.LocalizedTitles = titles;
            item.LocalizedSubtitles = subtitles;

            return !SameText(ResolveTitle(item, locale), shownTitle)
                || !SameText(ResolveSubtitle(item, locale), shownSubtitle);
        }

        /// <summary>
        /// The item's localization state as one string, or "" when it declares no
        /// usable entry — an unlocalized item then travels and persists exactly as
        /// it did before this feature existed.
        /// </summary>
        internal static string Encode(QuickActionItem item)
        {
            var titles = Usable(item.LocalizedTitles);
            var subtitles = Usable(item.LocalizedSubtitles);
            if (titles.Count == 0 && subtitles.Count == 0)
                return string.Empty;
            var blob = new StringBuilder(BlobHeader);
            Write(blob, item.Title);
            Write(blob, item.Subtitle);
            WriteEntries(blob, titles);
            WriteEntries(blob, subtitles);
            return blob.ToString();
        }

        /// <summary>
        /// ISO code for the device language — the default for
        /// <see cref="QuickActions.Locale"/>. Unity's <see cref="SystemLanguage"/>
        /// carries no region, so these are bare language codes (except Chinese,
        /// whose script tags are what distinguish the two written forms). Anything
        /// not listed — including <see cref="SystemLanguage.Unknown"/> — answers
        /// "en"; an app that wants a different default sets
        /// <see cref="QuickActions.Locale"/> itself.
        /// </summary>
        internal static string FromSystemLanguage(SystemLanguage language)
        {
            switch (language)
            {
                case SystemLanguage.Afrikaans: return "af";
                case SystemLanguage.Arabic: return "ar";
                case SystemLanguage.Basque: return "eu";
                case SystemLanguage.Belarusian: return "be";
                case SystemLanguage.Bulgarian: return "bg";
                case SystemLanguage.Catalan: return "ca";
                case SystemLanguage.Chinese: return "zh";
                case SystemLanguage.ChineseSimplified: return "zh-Hans";
                case SystemLanguage.ChineseTraditional: return "zh-Hant";
                case SystemLanguage.Czech: return "cs";
                case SystemLanguage.Danish: return "da";
                case SystemLanguage.Dutch: return "nl";
                case SystemLanguage.Estonian: return "et";
                case SystemLanguage.Faroese: return "fo";
                case SystemLanguage.Finnish: return "fi";
                case SystemLanguage.French: return "fr";
                case SystemLanguage.German: return "de";
                case SystemLanguage.Greek: return "el";
                case SystemLanguage.Hebrew: return "he";
                case SystemLanguage.Hindi: return "hi";
                case SystemLanguage.Icelandic: return "is";
                case SystemLanguage.Indonesian: return "id";
                case SystemLanguage.Italian: return "it";
                case SystemLanguage.Japanese: return "ja";
                case SystemLanguage.Korean: return "ko";
                case SystemLanguage.Latvian: return "lv";
                case SystemLanguage.Lithuanian: return "lt";
                case SystemLanguage.Norwegian: return "no";
                case SystemLanguage.Polish: return "pl";
                case SystemLanguage.Portuguese: return "pt";
                case SystemLanguage.Romanian: return "ro";
                case SystemLanguage.Russian: return "ru";
                case SystemLanguage.SerboCroatian: return "sr";
                case SystemLanguage.Slovak: return "sk";
                case SystemLanguage.Slovenian: return "sl";
                case SystemLanguage.Spanish: return "es";
                case SystemLanguage.Swedish: return "sv";
                case SystemLanguage.Thai: return "th";
                case SystemLanguage.Turkish: return "tr";
                case SystemLanguage.Ukrainian: return "uk";
                case SystemLanguage.Vietnamese: return "vi";
                default: return "en";
            }
        }

        private static string Find(List<LocalizedText> entries, string locale)
        {
            foreach (var entry in entries)
            {
                // Skip junk rather than resolving to it: an empty text installs a
                // blank label the OS refuses, so the base text is the honest answer.
                if (entry == null || string.IsNullOrEmpty(entry.Locale) || string.IsNullOrEmpty(entry.Text))
                    continue;
                if (string.Equals(entry.Locale, locale, StringComparison.OrdinalIgnoreCase))
                    return entry.Text;
            }
            return null;
        }

        // Only entries that can actually render — the same filter Find applies, so
        // the blob never carries rows resolution would ignore anyway.
        private static List<LocalizedText> Usable(List<LocalizedText> entries)
        {
            var usable = new List<LocalizedText>();
            if (entries == null)
                return usable;
            foreach (var entry in entries)
                if (entry != null && !string.IsNullOrEmpty(entry.Locale) && !string.IsNullOrEmpty(entry.Text))
                    usable.Add(entry);
            return usable;
        }

        // The natives report an absent subtitle as "" while C# may hold null; a
        // localization refresh must not be triggered by that difference alone.
        private static bool SameText(string first, string second) =>
            (first ?? string.Empty) == (second ?? string.Empty);

        private static void Write(StringBuilder blob, string value)
        {
            value = value ?? string.Empty;
            blob.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value);
        }

        private static void WriteEntries(StringBuilder blob, List<LocalizedText> entries)
        {
            Write(blob, entries.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var entry in entries)
            {
                Write(blob, entry.Locale);
                Write(blob, entry.Text);
            }
        }

        private static bool TryRead(string blob, ref int index, out string value)
        {
            value = null;
            var colon = blob.IndexOf(':', index);
            if (colon < 0)
                return false;
            // NumberStyles.None: no sign, no whitespace, no separators — a length is
            // plain digits or the payload is not ours to trust.
            if (!int.TryParse(blob.Substring(index, colon - index), NumberStyles.None,
                    CultureInfo.InvariantCulture, out var length))
                return false;
            var start = colon + 1;
            if (start + length > blob.Length)
                return false;
            value = blob.Substring(start, length);
            index = start + length;
            return true;
        }

        // Null (not an empty list) on a malformed payload, so the caller can tell
        // "no entries" from "don't trust any of this".
        private static List<LocalizedText> ReadEntries(string blob, ref int index)
        {
            if (!TryRead(blob, ref index, out var countText) ||
                !int.TryParse(countText, NumberStyles.None, CultureInfo.InvariantCulture, out var count))
                return null;
            // Every entry costs at least four characters ("0:0:"), so a count past
            // the remaining length can only come from a corrupted/forged payload —
            // reject it instead of pre-allocating for it.
            if (count > blob.Length - index)
                return null;
            var entries = new List<LocalizedText>();
            for (var i = 0; i < count; i++)
            {
                if (!TryRead(blob, ref index, out var locale) || !TryRead(blob, ref index, out var text))
                    return null;
                entries.Add(new LocalizedText(locale, text));
            }
            return entries;
        }
    }
}
