using System;
using WolfCurses.Window;

namespace WolfCurses.Tests.TestDoubles
{
    /// <summary>
    ///     Test double mirroring the example app's <c>ExampleWindow</c>: ESC, while a form is attached, backs out of it
    ///     by clearing the form (returning to the bare menu). Proves the routing the example's Escape-to-menu feature
    ///     relies on — ESC reaching a window's <c>OnKeyPressed(ConsoleKey)</c> and <c>ClearForm</c> removing the form —
    ///     travels the real dispatch chain intact. Every other key is passed straight through to the form.
    /// </summary>
    public class EscapeReturnsToMenuWindow : Window<TestCommandsEnum, TestWindowData>
    {
        public EscapeReturnsToMenuWindow(SimulationApp simUnit) : base(simUnit)
        {
        }

        public override void OnKeyPressed(ConsoleKey key)
        {
            if (key == ConsoleKey.Escape && CurrentForm != null)
            {
                ClearForm();
                return;
            }

            base.OnKeyPressed(key);
        }
    }
}
