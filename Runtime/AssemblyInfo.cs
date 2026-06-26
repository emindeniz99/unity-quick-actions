using System.Runtime.CompilerServices;

// Lets the test assembly reach internal members (Dispatch, QuickActionItem.IsValid,
// QuickActionList) for white-box unit tests. Harmless if the test assembly is absent.
[assembly: InternalsVisibleTo("Playground.QuickActions.Tests")]
