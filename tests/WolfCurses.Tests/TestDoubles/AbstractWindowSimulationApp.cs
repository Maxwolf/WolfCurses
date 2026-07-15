using System;
using System.Collections.Generic;

namespace WolfCurses.Tests.TestDoubles
{
    /// <summary>App whose only allowed window is abstract, to verify how WindowManager.Add surfaces the mistake.</summary>
    public sealed class AbstractWindowSimulationApp : SimulationApp
    {
        public override IEnumerable<Type> AllowedWindows => new[] { typeof(AbstractTestWindow) };

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
