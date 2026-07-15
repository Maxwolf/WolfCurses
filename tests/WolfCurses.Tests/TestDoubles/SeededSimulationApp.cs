using System;
using System.Collections.Generic;

namespace WolfCurses.Tests.TestDoubles
{
    /// <summary>
    ///     Exposes the seeded <see cref="SimulationApp" /> constructor so tests can prove the shared Randomizer is
    ///     reproducible when a seed is plumbed through the base class.
    /// </summary>
    public sealed class SeededSimulationApp : SimulationApp
    {
        public SeededSimulationApp(int seed) : base(seed)
        {
        }

        public override IEnumerable<Type> AllowedWindows => new[] { typeof(TestWindow) };

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
