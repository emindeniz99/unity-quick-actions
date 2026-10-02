using System.Runtime.CompilerServices;

// The platform bakers apply the shared bake filter (QuickActionsBakeFilter),
// which stays internal rather than becoming package API.
[assembly: InternalsVisibleTo("EminDeniz99.QuickActions.Editor.iOS")]
[assembly: InternalsVisibleTo("EminDeniz99.QuickActions.Editor.Android")]
