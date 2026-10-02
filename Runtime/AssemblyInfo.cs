using System.Runtime.CompilerServices;

// Lets the test assembly reach internal members (Dispatch, QuickActionItem.IsValid,
// QuickActionList) for white-box unit tests. Harmless if the test assembly is absent.
[assembly: InternalsVisibleTo("EminDeniz99.QuickActions.Tests")]

// Lets the Editor assembly reach the internal Editor* hooks on QuickActions
// (EditorSimulateTap, EditorResetAfterPlaySession, EditorClearPerformedSubscribers,
// EditorColdLaunchKey) and QuickActionItem.Copy. The Editor assembly itself never ships.
[assembly: InternalsVisibleTo("EminDeniz99.QuickActions.Editor")]
