namespace WolfCurses.Tests.TestDoubles
{
    /// <summary>
    ///     A second window type so tests can exercise focus changes, stacking, and removal; WindowManager keys windows
    ///     by type, so a distinct class is required.
    /// </summary>
    public class SecondTestWindow : TestWindow
    {
        public SecondTestWindow(SimulationApp simUnit) : base(simUnit)
        {
        }
    }
}
