// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

// xUnit1051: Suppress warnings about CancellationToken in test methods
// These warnings suggest using TestContext.Current.CancellationToken for better test cancellation support.
// We're suppressing this across the test project as these are unit tests that typically run quickly
// and don't benefit significantly from granular cancellation support.
[assembly: SuppressMessage("xUnit1000", "xUnit1051", Justification = "Unit tests run quickly and don't require granular cancellation support")]
