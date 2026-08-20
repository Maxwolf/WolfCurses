using Xunit;

namespace WolfCurses.Demo.Tests.Support
{
    /// <summary>
    ///     Every test that starts the demo application belongs here. <c>ConsoleSimulationApp</c> is a singleton that
    ///     throws rather than be created twice, and the library carries process-wide state besides — the cached
    ///     colour mode, the once-per-process renderer probe.
    /// </summary>
    [CollectionDefinition("DemoApp", DisableParallelization = true)]
    public class DemoAppCollection
    {
    }
}
