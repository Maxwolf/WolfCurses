using System;
using System.Collections.Generic;
using WolfCurses.Controls;

namespace WolfCurses.Tests.TestDoubles
{
    /// <summary>
    ///     Minimal simulation whose only allowed window is the built-in <see cref="FileDialogWindow" />, used to drive
    ///     the file dialog through the real tick/input pipeline in integration tests.
    /// </summary>
    public sealed class FileDialogHostApp : SimulationApp
    {
        public override IEnumerable<Type> AllowedWindows => new[] { typeof(FileDialogWindow) };

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
