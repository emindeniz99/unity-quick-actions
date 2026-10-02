using System.Runtime.CompilerServices;

// The ungated editor assemblies that share these rules. Each one compiles with
// the define off, which is why the rules cannot live in a gated assembly.
[assembly: InternalsVisibleTo("EminDeniz99.QuickActions.Editor.Bootstrap")]
[assembly: InternalsVisibleTo("EminDeniz99.QuickActions.Editor.NativeGate")]
[assembly: InternalsVisibleTo("EminDeniz99.QuickActions.Editor.NativeGate.iOS")]
