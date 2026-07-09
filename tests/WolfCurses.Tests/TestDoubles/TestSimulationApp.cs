using System;
using System.Collections.Generic;

namespace WolfCurses.Tests.TestDoubles
{
    /// <summary>
    ///     Standard app double for simulation tests. AllowedWindows is read by the base constructor (WindowFactory is
    ///     built there), so the list cannot vary per instance — apps needing different window sets are separate types.
    /// </summary>
    public sealed class TestSimulationApp : SimulationApp
    {
        public const string PRERENDER_TEXT = "PRERENDER";

        public int FirstTickCount { get; private set; }

        public int PreDestroyCount { get; private set; }

        public override IEnumerable<Type> AllowedWindows => new[]
        {
            typeof(TestWindow),
            typeof(SecondTestWindow)
        };

        protected override void OnFirstTick()
        {
            FirstTickCount++;
        }

        protected override void OnPreDestroy()
        {
            PreDestroyCount++;
        }

        public override string OnPreRender()
        {
            return PRERENDER_TEXT;
        }
    }
}
