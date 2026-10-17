using Xunit;

// PortablePaths resolves a process-wide base directory, so tests that use it (presets, settings)
// must not run in parallel with each other.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
