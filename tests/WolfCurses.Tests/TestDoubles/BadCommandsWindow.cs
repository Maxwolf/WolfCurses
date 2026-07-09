using WolfCurses.Window;

namespace WolfCurses.Tests.TestDoubles
{
    /// <summary>
    ///     TCommands is constrained to struct/IComparable/IFormattable/IConvertible but not to enum; int satisfies the
    ///     compiler yet the Window constructor rejects it at runtime. Exists to pin that guard.
    /// </summary>
    public class BadCommandsWindow : Window<int, TestWindowData>
    {
        public BadCommandsWindow(SimulationApp simUnit) : base(simUnit)
        {
        }
    }
}
