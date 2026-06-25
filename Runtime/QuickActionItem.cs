using System;

namespace Playground.QuickActions
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

        public QuickActionItem() { }

        public QuickActionItem(string id, string title, string subtitle = null, IconType icon = IconType.None)
        {
            Id = id;
            Title = title;
            Subtitle = subtitle;
            Icon = icon;
        }

        internal bool IsValid => !string.IsNullOrEmpty(Id) && !string.IsNullOrEmpty(Title);

        public bool Equals(QuickActionItem other) => other != null && Id == other.Id;

        public override bool Equals(object obj) => Equals(obj as QuickActionItem);

        public override int GetHashCode() => Id != null ? Id.GetHashCode() : 0;

        public override string ToString() => $"QuickActionItem(Id={Id}, Title={Title}, Icon={Icon})";
    }
}
