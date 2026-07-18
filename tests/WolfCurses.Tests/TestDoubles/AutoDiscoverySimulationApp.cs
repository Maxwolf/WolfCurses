namespace WolfCurses.Tests.TestDoubles
{
    /// <summary>
    ///     App double that deliberately does NOT override AllowedWindows, exercising the default reflection scan.
    ///     Under xunit.v3 the test assembly is both the subclass assembly and the entry assembly, so discovery sees
    ///     every concrete test-double window here plus the library's built-in control windows.
    /// </summary>
    public sealed class AutoDiscoverySimulationApp : SimulationApp
    {
        protected override void OnFirstTick()
        {
        }

        protected override void OnPreDestroy()
        {
        }

        public override string OnPreRender()
        {
            return string.Empty;
        }
    }
}
