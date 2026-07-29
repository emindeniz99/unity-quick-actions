using System;

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
        /// Ignored on Android and on iOS 12 (which falls through to the next icon
        /// source).
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
        /// <see cref="QuickActions.GetById"/> from the id
        /// <see cref="QuickActions.Performed"/> reports.
        /// </summary>
        public string Payload;

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
        /// A field-by-field copy. The facade stores and returns copies so a caller
        /// mutating an added item (or an item from <see cref="QuickActions.GetAll"/> /
        /// <see cref="QuickActions.GetById"/>) can't silently change the internal set
        /// out from under the OS state.
        /// </summary>
        internal QuickActionItem Copy() => new QuickActionItem
        {
            Id = Id,
            Title = Title,
            Subtitle = Subtitle,
            Icon = Icon,
            AndroidDrawable = AndroidDrawable,
            IosSystemImage = IosSystemImage,
            IosTemplateImage = IosTemplateImage,
            AndroidBitmapFile = AndroidBitmapFile,
            AndroidBitmapAdaptive = AndroidBitmapAdaptive,
            Payload = Payload,
        };

        public bool Equals(QuickActionItem other) => other != null && Id == other.Id;

        public override bool Equals(object obj) => Equals(obj as QuickActionItem);

        public override int GetHashCode() => Id != null ? Id.GetHashCode() : 0;

        public override string ToString() => $"QuickActionItem(Id={Id}, Title={Title}, Icon={Icon})";
    }
}
