using System;
using System.Collections.Generic;

namespace WolfCurses.Tests.TestDoubles
{
    /// <summary>
    ///     Lists the same window type twice. WindowFactory collapses the repeat instead of refusing it (mirroring how
    ///     stacked [ParentWindow] attributes register once), so a hand-curated AllowedWindows override that
    ///     accidentally repeats an entry still constructs.
    /// </summary>
    public sealed class RepeatedListingSimulationApp : SimulationApp
    {
        public override IEnumerable<Type> AllowedWindows => new[]
        {
            typeof(TestWindow),
            typeof(TestWindow)
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
