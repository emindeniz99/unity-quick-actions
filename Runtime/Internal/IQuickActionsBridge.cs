using System.Collections.Generic;

namespace EminDeniz99.QuickActions.Internal
{
    /// <summary>
    /// Platform abstraction behind the public <see cref="QuickActions"/> facade.
    /// The managed layer owns the authoritative shortcut list; the bridge only
    /// pushes the full set to the OS and surfaces "which action was tapped".
    /// One implementation per platform, selected by
    /// <see cref="QuickActionsBridgeFactory"/>.
    /// </summary>
    internal interface IQuickActionsBridge
    {
        /// <summary>True when the OS supports quick actions (false in-Editor).</summary>
        bool IsPlatformSupported { get; }

        /// <summary>
        /// How many shortcuts the OS accepts/shows for this app. Android:
        /// <c>getMaxShortcutCountPerActivity</c>. iOS: 4, the Home Screen display
        /// limit (there is no OS query; extra items are accepted but not shown).
        /// The budget is shared with static shortcuts on both platforms (and with
        /// host-published dynamic ones on Android) — see the facade docs. 0 where
        /// quick actions don't exist (Editor / unsupported).
        /// </summary>
        int MaxShortcutCount { get; }

        /// <summary>
        /// True when the launcher supports pinning shortcuts to the home screen
        /// (Android 8.0+ with a compatible launcher). Always false on iOS (no
        /// pinned-shortcut concept) and in the Editor.
        /// </summary>
        bool IsPinSupported { get; }

        /// <summary>
        /// Ask the launcher to pin the (already added, package-managed) shortcut
        /// with this id. Returns true when the request was DISPATCHED — the user
        /// still confirms/denies in launcher UI and the OS reports no outcome.
        /// False when unsupported, the id isn't currently one of ours on the OS,
        /// or the native call failed.
        /// </summary>
        bool RequestPin(string id);

        /// <summary>
        /// Push these items to the OS as <b>this package's subset</b> of the dynamic
        /// shortcuts (both platforms mark their items; a host app's own shortcuts
        /// are never modified) and return the subset it actually accepted — the
        /// same input <see cref="QuickActionItem"/> objects (icons intact), in
        /// input order. Platforms with no cap return the input list <b>unchanged
        /// (same reference = accept-all)</b>; Android returns only the items whose
        /// ids survived the OS dynamic-shortcut cap (shared with manifest shortcuts
        /// AND the host's own dynamic shortcuts), so the managed layer can prune
        /// the surplus and keep <see cref="QuickActions.GetAll"/>/<see cref="QuickActions.IsAdded"/> honest.
        /// Returns <c>null</c> when the OS write did not <b>fully</b> land (rejected /
        /// rate-limited / errored — on Android the removal of stale items may already
        /// have applied), so the facade reconciles with the real device state on next
        /// access instead of trusting its optimistic mutation.
        /// </summary>
        IList<QuickActionItem> SetShortcuts(IList<QuickActionItem> items);

        /// <summary>
        /// Remove the shortcuts <b>this package manages</b> from the OS (a host
        /// app's own shortcuts are untouched). Returns true when our subset is now
        /// clear (including when there was nothing to remove); false when the native
        /// removal failed, so the facade can keep its list rather than falsely
        /// marking itself empty.
        /// </summary>
        bool RemoveAll();

        /// <summary>
        /// The id of the action the app was most recently launched or resumed
        /// from, persisted by the OS layer until <see cref="ResetLastPerformed"/>.
        /// Null if none.
        /// </summary>
        string GetLastPerformed();

        /// <summary>Clear the persisted "last performed" id.</summary>
        void ResetLastPerformed();

        /// <summary>
        /// Pull-and-clear the next queued "performed" id used to raise the
        /// <see cref="QuickActions.Performed"/> event. Null if the queue is empty.
        /// Covers cold launch and warm resume on both platforms (single channel).
        /// </summary>
        string ConsumePendingPerformed();

        /// <summary>
        /// The <b>dynamic</b> shortcuts <b>this package manages</b> that the OS
        /// currently has (set in a previous session too); static/build-time
        /// shortcuts are not included, and a host app's / other publisher's items
        /// are never surfaced (absorbing them would make the next push republish
        /// them with our intents). Lets the managed layer reconcile its list after
        /// a cold start. Both bridges recover icon identity from their
        /// ownership-marker payload (Android extras, iOS userInfo) — the OS
        /// itself can't read icons back. Returns an empty list when our subset is
        /// genuinely empty, but <c>null</c> when the read itself <b>failed</b>
        /// (e.g. a locked device) — the facade must not treat a failed read as an
        /// authoritative-empty set.
        /// </summary>
        IList<QuickActionItem> GetShortcuts();
    }
}
