using System;
using System.Collections.Generic;
using WolfCurses.Controls;

namespace WolfCurses.Tests.TestDoubles
{
    /// <summary>
    ///     Minimal simulation that allows the built-in list picker, message box, and text-input windows, used to drive
    ///     those controls through the real tick/input pipeline in integration tests.
    /// </summary>
    public sealed class ControlsHostApp : SimulationApp
    {
        public override IEnumerable<Type> AllowedWindows => new[]
        {
            typeof(SelectListWindow), typeof(MessageBoxWindow), typeof(TextInputWindow)
        };

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
