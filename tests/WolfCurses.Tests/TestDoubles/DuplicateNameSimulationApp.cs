using System;
using System.Collections.Generic;

namespace WolfCurses.Tests.TestDoubles
{
    /// <summary>
    ///     WindowFactory keys its lookup by Type.Name, so two windows with the same short name collide even in
    ///     different namespaces. Constructing this app pins where that ArgumentException surfaces.
    /// </summary>
    public sealed class DuplicateNameSimulationApp : SimulationApp
    {
        public override IEnumerable<Type> AllowedWindows => new[]
        {
            typeof(CloneNamespaceA.CloneWindow),
            typeof(CloneNamespaceB.CloneWindow)
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
