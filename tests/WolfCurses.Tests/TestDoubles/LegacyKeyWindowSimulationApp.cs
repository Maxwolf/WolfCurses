using System;
using System.Collections.Generic;

namespace WolfCurses.Tests.TestDoubles
{
    /// <summary>
    ///     App double whose only window is <see cref="LegacyKeyWindow" />. A separate type because AllowedWindows is
    ///     read by the base constructor, so the window set cannot vary per <see cref="TestSimulationApp" /> instance.
    /// </summary>
    public sealed class LegacyKeyWindowSimulationApp : SimulationApp
    {
        public override IEnumerable<Type> AllowedWindows => new[]
        {
            typeof(LegacyKeyWindow)
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
