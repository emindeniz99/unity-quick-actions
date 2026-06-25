namespace Playground.QuickActions
{
    /// <summary>
    /// Built-in icon for a quick action.
    ///
    /// On <b>iOS</b> each value maps to a <c>UIApplicationShortcutIconType</c>
    /// system glyph. On <b>Android</b> there is no system shortcut-icon catalog,
    /// so the icon is taken from a bundled drawable named
    /// <c>ic_quickaction_&lt;value&gt;</c> (lower-case) when present, otherwise the
    /// app icon is used. See <see cref="QuickActionItem.AndroidDrawable"/> to
    /// override the Android drawable explicitly.
    /// </summary>
    public enum IconType
    {
        None = 0,
        Compose,
        Play,
        Pause,
        Add,
        Location,
        Search,
        Share,
        Prohibit,
        Contact,
        Home,
        MarkLocation,
        Favorite,
        Love,
        Cloud,
        Invitation,
        Confirmation,
        Mail,
        Message,
        Date,
        Time,
        CapturePhoto,
        CaptureVideo,
        Task,
        TaskCompleted,
        Alarm,
        Bookmark,
        Shuffle,
        Audio,
        Update
    }
}
