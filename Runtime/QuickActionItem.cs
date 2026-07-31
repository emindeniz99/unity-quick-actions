using System;
using System.Collections.Generic;
using UnityEngine;

namespace EminDeniz99.QuickActions
{
    /// <summary>
    /// One home-screen quick action — an item revealed by long-pressing the app
    /// icon. Created at runtime and registered with <see cref="QuickActions.Add"/>.
    /// Two items are considered equal when their <see cref="Id"/> matches.
    /// </summary>
    [Serializable]
    public class QuickActionItem : IEquatable<QuickActionItem>
    {
        /// <summary>
        /// Stable, unique identifier. Reported back through
        /// <see cref="QuickActions.Performed"/> / <see cref="QuickActions.LastPerformed"/>
        /// when the user taps this action. Required.
        /// </summary>
        public string Id;

        /// <summary>Primary, user-visible label. Required.</summary>
        public string Title;

        /// <summary>
        /// Secondary line under the title. Rendered on iOS; on Android it is used
        /// as the shortcut's long label.
        /// </summary>
        public string Subtitle;

        /// <summary>
        /// Per-locale replacements for <see cref="Title"/>. The entry matching
        /// <see cref="QuickActions.Locale"/> — exactly, else by language prefix
        /// (<c>"pt-BR"</c> matches a <c>"pt"</c> entry), both case-insensitively —
        /// is what the OS shows; with no match the base <see cref="Title"/> is
        /// used. Leave it empty in a single-language app.
        /// </summary>
        public List<LocalizedText> LocalizedTitles = new List<LocalizedText>();

        /// <summary>
        /// Per-locale replacements for <see cref="Subtitle"/>; resolved exactly like
        /// <see cref="LocalizedTitles"/>.
        /// </summary>
        public List<LocalizedText> LocalizedSubtitles = new List<LocalizedText>();

        /// <summary>System icon (see <see cref="IconType"/>). Defaults to none.</summary>
        public IconType Icon = IconType.None;

        /// <summary>
        /// Optional Android drawable resource name to use as the icon, overriding
        /// the <see cref="Icon"/>-derived lookup. Ignored on iOS.
        /// </summary>
        public string AndroidDrawable;

        /// <summary>
        /// Optional SF Symbol name (e.g. <c>"star.fill"</c>) to use as the icon on
        /// iOS 13+, overriding <see cref="IosTemplateImage"/> and <see cref="Icon"/>.
        /// Ignored on Android. On iOS 12 a <b>dynamic</b> item falls through to the
        /// next icon source at runtime; a <b>static</b> (baked) item does not — its
        /// Info.plist entry carries only the symbol key, which iOS 12 ignores, so
        /// it renders iconless there (use <see cref="Icon"/> or
        /// <see cref="IosTemplateImage"/> for static items if you target iOS 12).
        /// </summary>
        public string IosSystemImage;

        /// <summary>
        /// Optional template-image name from the app bundle / asset catalog to use
        /// as the icon on iOS (single-color, ~35×35 pt — see Apple's Human
        /// Interface Guidelines), overriding <see cref="Icon"/>. The image must be
        /// shipped in the Xcode project; a missing name renders no icon. Ignored on
        /// Android.
        /// </summary>
        public string IosTemplateImage;

        /// <summary>
        /// Optional absolute path to a PNG/JPEG file on the device to use as the
        /// icon on Android, overriding <see cref="AndroidDrawable"/> and
        /// <see cref="Icon"/>. Write it yourself, e.g.
        /// <c>File.WriteAllBytes(path, texture.EncodeToPNG())</c> under
        /// <c>Application.persistentDataPath</c> — the file must still exist when
        /// the OS re-renders the shortcut (don't use a temp dir). Ignored on iOS
        /// (<c>UIApplicationShortcutIcon</c> has no runtime-bitmap API).
        /// </summary>
        public string AndroidBitmapFile;

        /// <summary>
        /// When true, <see cref="AndroidBitmapFile"/> is installed as an adaptive
        /// icon (<c>Icon.createWithAdaptiveBitmap</c>, API 26+): the launcher masks
        /// it to its shape instead of rendering the bitmap as-is. Supply the usual
        /// adaptive safe-zone padding in the image. No effect without
        /// <see cref="AndroidBitmapFile"/>.
        /// </summary>
        public bool AndroidBitmapAdaptive;

        /// <summary>
        /// Optional app-defined string carried with the shortcut (iOS
        /// <c>userInfo</c>, Android intent extras) and restored by the cold-start
        /// reconcile. Not delivered with the tap event — read it via
        /// <c>QuickActions.GetById(id)?.Payload</c> from the id
        /// <see cref="QuickActions.Performed"/> reports. Note
        /// <see cref="QuickActions.GetById"/> returns null for a
        /// <b>static</b>-shortcut tap (baked items never join the runtime list and
        /// carry no payload) and for an id removed since the tap — null-check it.
        /// </summary>
        public string Payload;

        // This item's localization state (base text + both tables) encoded as one
        // opaque string by QuickActionLocalization. It is set on the transient copy
        // a push serializes, persisted verbatim by the natives (Android extras /
        // iOS userInfo) and handed straight back on a cold-start read — the OS
        // itself only stores the RESOLVED label, so without this a reconcile would
        // adopt that label as the base text and every later language switch would
        // translate from the wrong original.
        // WHY hidden rather than public API: callers author LocalizedTitles /
        // LocalizedSubtitles; this is only how those survive the round trip.
        // [SerializeField] keeps JsonUtility (de)serializing it — Unity serializes
        // non-public fields that carry the attribute — while it stays out of the
        // public surface, and [HideInInspector] keeps it out of the static-shortcut
        // list in Project Settings.
        [SerializeField, HideInInspector] internal string L10n;

        public QuickActionItem() { }

        public QuickActionItem(string id, string title, string subtitle = null, IconType icon = IconType.None)
        {
            Id = id;
            Title = title;
            Subtitle = subtitle;
            Icon = icon;
        }

        internal bool IsValid => !string.IsNullOrEmpty(Id) && !string.IsNullOrEmpty(Title);

        /// <summary>
        /// A field-by-field copy, with the per-locale tables DEEP-copied. The facade
        /// stores and returns copies so a caller mutating an added item (or an item
        /// from <see cref="QuickActions.GetAll"/> / <see cref="QuickActions.GetById"/>)
        /// can't silently change the internal set out from under the OS state — a
        /// shared <see cref="LocalizedTitles"/> list would reopen exactly that hole
        /// for every label.
        /// </summary>
        internal QuickActionItem Copy() => new QuickActionItem
        {
            Id = Id,
            Title = Title,
            Subtitle = Subtitle,
            LocalizedTitles = CopyEntries(LocalizedTitles),
            LocalizedSubtitles = CopyEntries(LocalizedSubtitles),
            Icon = Icon,
            AndroidDrawable = AndroidDrawable,
            IosSystemImage = IosSystemImage,
            IosTemplateImage = IosTemplateImage,
            AndroidBitmapFile = AndroidBitmapFile,
            AndroidBitmapAdaptive = AndroidBitmapAdaptive,
            Payload = Payload,
            L10n = L10n,
        };

        // New list AND new entries: copying the list alone would still hand out the
        // same LocalizedText objects, so a caller editing one's Text would change
        // what the next push installs. Null entries are preserved rather than
        // dropped, so Copy() stays a faithful copy (the resolver skips them).
        private static List<LocalizedText> CopyEntries(List<LocalizedText> entries)
        {
            var copy = new List<LocalizedText>();
            if (entries == null)
                return copy;
            foreach (var entry in entries)
                copy.Add(entry == null ? null : new LocalizedText(entry.Locale, entry.Text));
            return copy;
        }

        public bool Equals(QuickActionItem other) => other != null && Id == other.Id;

        public override bool Equals(object obj) => Equals(obj as QuickActionItem);

        public override int GetHashCode() => Id != null ? Id.GetHashCode() : 0;

        public override string ToString() => $"QuickActionItem(Id={Id}, Title={Title}, Icon={Icon})";
    }
}
