using System;
using System.Collections.Generic;

namespace WolfCurses.Tests.TestDoubles
{
    /// <summary>Destroys itself from inside OnFirstTick, the earliest simulation callback, to prove that a
    ///     mid-tick teardown cannot crash the remainder of the tick.</summary>
    public sealed class SelfDestructingSimulationApp : SimulationApp
    {
        public override IEnumerable<Type> AllowedWindows => new[] { typeof(TestWindow) };

        protected override void OnFirstTick()
        {
            Destroy();
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
