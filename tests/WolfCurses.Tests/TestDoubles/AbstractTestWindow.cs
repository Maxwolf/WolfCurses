using WolfCurses.Window;

namespace WolfCurses.Tests.TestDoubles
{
    /// <summary>Abstract window type: WindowFactory refuses to instantiate these with an ArgumentException.</summary>
    public abstract class AbstractTestWindow : Window<TestCommandsEnum, TestWindowData>
    {
        protected AbstractTestWindow(SimulationApp simUnit) : base(simUnit)
        {
        }
    }
}
