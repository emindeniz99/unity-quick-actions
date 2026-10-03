using System.Collections.Generic;

namespace EminDeniz99.QuickActions.Editor
{
    /// <summary>
    /// Which static shortcuts a build writes. Both platform bakers skip what this
    /// rejects, and the settings page applies the same rule to warn. The bakers
    /// see the prepared list (after Customize and placeholders) and the page the
    /// raw asset, so the inputs can still differ. Use one instance per pass over a
    /// list: it remembers the ids it accepted, and the first item with an id wins.
    /// </summary>
    internal sealed class QuickActionsBakeFilter
    {
        internal enum Verdict
        {
            Bake,
            MissingIdOrTitle,
            DuplicateId,
        }

        private readonly HashSet<string> _acceptedIds = new HashSet<string>();

        internal Verdict Check(QuickActionItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.Id) || string.IsNullOrEmpty(item.Title))
                return Verdict.MissingIdOrTitle;
            return _acceptedIds.Add(item.Id) ? Verdict.Bake : Verdict.DuplicateId;
        }

        internal bool Accepts(QuickActionItem item) => Check(item) == Verdict.Bake;
    }
}
