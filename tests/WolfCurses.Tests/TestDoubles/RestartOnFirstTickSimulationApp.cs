using System;
using System.Collections.Generic;

namespace WolfCurses.Tests.TestDoubles
{
    /// <summary>
    ///     Mirrors the idiomatic consumer pattern (used by WolfCurses.Example and downstream apps): OnFirstTick attaches
    ///     the initial window by delegating to Restart(). Because Restart() used to unconditionally reset the tick
    ///     counter, this pattern re-entered OnFirstTick on every subsequent tick — endlessly clearing and re-adding the
    ///     window so no form could ever survive a tick. Counts first-tick fires and exposes the current window so the
    ///     regression test can assert the loop is gone and the window instance is stable.
    /// </summary>
    public sealed class RestartOnFirstTickSimulationApp : SimulationApp
    {
        public int FirstTickCount { get; private set; }

        public override IEnumerable<Type> AllowedWindows => new[] { typeof(TestWindow) };

        protected override void OnFirstTick()
        {
            FirstTickCount++;
            Restart();
        }

        public override void Restart()
        {
            base.Restart();
            WindowManager.Add(typeof(TestWindow));
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
