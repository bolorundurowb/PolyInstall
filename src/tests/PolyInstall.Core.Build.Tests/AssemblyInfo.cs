using Xunit;

// PolyInstall.Cli.Build.BuildLog is a process-wide static sink (Console.Out plus the Quiet/Verbose flags).
// BuildLogTests temporarily swaps Console.Out and the static flags to observe logging behaviour; any other
// test in this assembly that logs through BuildLog (for example LogoRasterizerTests, which exercises
// InstallerBuildPipeline.RasterizeSvgLogo) can run on a different thread at the same moment under xUnit's
// default collection parallelism and leak its output into BuildLogTests' captured writer, or vice versa.
// Disabling parallelization keeps every test in this assembly serialised against that shared static state.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
