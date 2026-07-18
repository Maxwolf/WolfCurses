using WolfCurses.Window;

namespace WolfCurses.Tests.TestDoubles
{
    /// <summary>
    ///     A concrete window whose only constructor demands one specific SimulationApp subclass. Window discovery
    ///     must skip it on purpose: discovery promises every listed window is creatable by whichever app is running,
    ///     and this one is only creatable under <see cref="AutoDiscoverySimulationApp" />. An app wanting such a
    ///     window opts it in by overriding AllowedWindows.
    /// </summary>
    public sealed class ConcreteAppConstructorWindow : Window<TestCommandsEnum, TestWindowData>
    {
        public ConcreteAppConstructorWindow(AutoDiscoverySimulationApp simUnit) : base(simUnit)
        {
        }
    }
}
