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
        /// Push these items to the OS and return the subset it actually accepted —
        /// the same input <see cref="QuickActionItem"/> objects (icons intact), in
        /// input order. Platforms with no cap return the input list <b>unchanged
        /// (same reference = accept-all)</b>; Android returns only the items whose
        /// ids survived the OS dynamic-shortcut cap, so the managed layer can prune
        /// the surplus and keep <see cref="QuickActions.GetAll"/>/<see cref="QuickActions.IsAdded"/> honest.
        /// Returns <c>null</c> when the OS write did not land (rejected / rate-limited /
        /// errored), so the facade reconciles with the real device state on next
        /// access instead of trusting its optimistic mutation.
        /// </summary>
        IList<QuickActionItem> SetShortcuts(IList<QuickActionItem> items);

        /// <summary>
        /// Remove all shortcuts from the OS. Returns true when the OS state is now
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
        /// The <b>dynamic</b> shortcuts the OS currently has (set in a previous
        /// session too); static/build-time shortcuts are not included on either
        /// platform. Lets the managed layer reconcile its list after a cold start.
        /// Icons are not recoverable, so they come back as <see cref="IconType.None"/>.
        /// Returns an empty list when the OS genuinely has none, but <c>null</c> when
        /// the read itself <b>failed</b> (e.g. a locked device) — the facade must not
        /// treat a failed read as an authoritative-empty set.
        /// </summary>
        IList<QuickActionItem> GetShortcuts();
    }
}
