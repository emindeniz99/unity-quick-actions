namespace EminDeniz99.QuickActions
{
    /// <summary>
    /// Built-in icon for a quick action.
    ///
    /// On <b>iOS</b> each value maps to a <c>UIApplicationShortcutIconType</c>
    /// system glyph. On <b>Android</b> there is no system shortcut-icon catalog, so
    /// the icon is taken from a bundled drawable named
    /// <c>ic_quickaction_&lt;snake_case&gt;</c> — the enum member lower-cased with
    /// underscores between words, e.g. <see cref="MarkLocation"/> →
    /// <c>ic_quickaction_mark_location</c>, <see cref="Play"/> →
    /// <c>ic_quickaction_play</c> (the exact names in the Java <c>ICON_NAMES</c>
    /// table). Four members ship a built-in drawable — <see cref="Add"/>,
    /// <see cref="Compose"/>, <see cref="Favorite"/> and <see cref="Play"/>, written
    /// into every Android build as <c>ic_quickaction_builtin_&lt;name&gt;</c> in two
    /// variants of that one name: a plain vector for API 25 and an
    /// <c>&lt;adaptive-icon&gt;</c> under <c>drawable-anydpi-v26</c> for API 26+, so
    /// the launcher masks a full-bleed icon instead of plating the legacy one (not
    /// yet seen on a launcher by anyone). A project's own
    /// <c>ic_quickaction_&lt;name&gt;</c> takes precedence. For the
    /// others, with no drawable in the app the shortcut renders <b>without an
    /// icon</b> — a blank tile on every launcher observed so far. See
    /// <see cref="QuickActionItem.AndroidDrawable"/> to name a drawable explicitly.
    /// </summary>
    // The integer values are a load-bearing contract: the iOS native layer casts
    // (value - 1) to UIApplicationShortcutIconType, and the Android side indexes
    // ICON_NAMES by value. They are pinned explicitly so inserting/reordering a
    // member can never silently shift the native mapping. None must stay 0,
    // Compose 1, ... Update 29. (A unit test pins every value individually.)
    public enum IconType
    {
        None = 0,
        Compose = 1,
        Play = 2,
        Pause = 3,
        Add = 4,
        Location = 5,
        Search = 6,
        Share = 7,
        Prohibit = 8,
        Contact = 9,
        Home = 10,
        MarkLocation = 11,
        Favorite = 12,
        Love = 13,
        Cloud = 14,
        Invitation = 15,
        Confirmation = 16,
        Mail = 17,
        Message = 18,
        Date = 19,
        Time = 20,
        CapturePhoto = 21,
        CaptureVideo = 22,
        Task = 23,
        TaskCompleted = 24,
        Alarm = 25,
        Bookmark = 26,
        Shuffle = 27,
        Audio = 28,
        Update = 29
    }
}
