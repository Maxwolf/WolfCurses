using WolfCurses.Window;

namespace WolfCurses.Tests.TestDoubles
{
    /// <summary>
    ///     A concrete window WITHOUT the single-parameter (SimulationApp) constructor the window factory invokes.
    ///     Window discovery must skip it — registering it would make CreateWindow explode for an app that never
    ///     asked for the type.
    /// </summary>
    public sealed class WrongConstructorWindow : Window<TestCommandsEnum, TestWindowData>
    {
        public WrongConstructorWindow(SimulationApp simUnit, bool unusedExtra) : base(simUnit)
        {
            _ = unusedExtra;
        }
    }
}
