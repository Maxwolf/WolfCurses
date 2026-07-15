using System;
using System.Collections.Generic;
using System.Reflection;

namespace WolfCurses.Tests.TestDoubles
{
    /// <summary>
    ///     Overrides <see cref="SimulationApp.AdditionalFormAssemblies" /> so FormFactory folds an extra assembly into
    ///     form discovery, and counts how many times the hook is read so a test can prove the base constructor consumes
    ///     it. The extra assembly returned is this test assembly (same one auto-scanned), which also exercises the
    ///     de-duplication path — a genuinely separate third assembly is not available in-process.
    /// </summary>
    public sealed class AdditionalAssembliesSimulationApp : SimulationApp
    {
        public int AdditionalFormAssembliesReadCount { get; private set; }

        public override IEnumerable<Type> AllowedWindows => new[] { typeof(TestWindow) };

        public override IEnumerable<Assembly> AdditionalFormAssemblies
        {
            get
            {
                AdditionalFormAssembliesReadCount++;
                return new[] { GetType().Assembly };
            }
        }

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
