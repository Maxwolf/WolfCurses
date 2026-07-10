using WolfCurses.Window;

namespace WolfCurses.Tests.TestDoubles
{
    /// <summary>
    ///     Minimal user-data object shared between test windows and forms; the library only requires a parameterless
    ///     constructor via the new() constraint.
    /// </summary>
    public sealed class TestWindowData : WindowData
    {
        public string Payload { get; set; }

        /// <summary>Backdoor to the owning app for forms that need to drive simulation lifecycle mid-tick.</summary>
        public SimulationApp App { get; set; }
    }
}
