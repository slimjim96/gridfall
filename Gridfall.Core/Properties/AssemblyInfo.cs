using System.Runtime.CompilerServices;

// First-party tooling needs to poke simulation state directly: the test suite
// mutates individual fields to prove the hash covers them, and the perf harness
// grants itself gold to fill a board with towers.
//
// The Godot project is deliberately NOT on this list. The boundary that actually
// matters is Core <-> View, and the renderer having no write path at all is the
// whole point of SimStateView (ADR-0001).
[assembly: InternalsVisibleTo("Gridfall.Tests")]
[assembly: InternalsVisibleTo("Gridfall.Verify")]
