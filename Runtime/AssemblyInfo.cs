using System.Runtime.CompilerServices;

// Lets the test assembly reach internal members (Dispatch, QuickActionItem.IsValid,
// QuickActionList) for white-box unit tests. Harmless if the test assembly is absent.
[assembly: InternalsVisibleTo("EminDeniz99.QuickActions.Tests")]

// Lets the Editor assembly reach internal Dispatch so the Simulator window can fire
// a tap (raise Performed) exactly as the native bridges do. Editor-only; never ships.
[assembly: InternalsVisibleTo("EminDeniz99.QuickActions.Editor")]
