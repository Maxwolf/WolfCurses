// Created by Maxwolf (bigmaxwolf.com)

using System.Collections.Generic;
using System.Reflection;

namespace WolfCurses.Utility
{
    /// <summary>
    ///     Computes the assembly set WolfCurses walks whenever it discovers types by reflection: <c>[ParentWindow]</c>
    ///     forms, and — unless the app overrides <see cref="SimulationApp.AllowedWindows" /> — window types
    ///     themselves. The form factory and the window discovery both resolve through here so they can never disagree
    ///     about what was scanned; the set is published to consumers as
    ///     <see cref="SimulationApp.DiscoveryAssemblies" />.
    /// </summary>
    internal static class DiscoveryAssemblyResolver
    {
        /// <summary>
        ///     Builds the deduped, null-free scan set: the WolfCurses library itself (so built-in control windows and
        ///     forms are found for every consuming app without it opting them in), the assembly defining the concrete
        ///     <see cref="SimulationApp" /> subclass (so hosted scenarios — unit test runners, plugins, native
        ///     embedding — work even when the app is not the process entry point), the process entry assembly
        ///     (preserving the original behavior for apps that ARE their own entry point), and anything the app opts
        ///     into via <see cref="SimulationApp.AdditionalFormAssemblies" />.
        /// </summary>
        /// <param name="simUnit">The owning simulation, or null to fall back to library plus entry assembly only.</param>
        internal static IReadOnlyCollection<Assembly> Resolve(SimulationApp simUnit)
        {
            var assembliesToScan = new List<Assembly>
            {
                typeof(DiscoveryAssemblyResolver).Assembly,
                simUnit?.GetType().Assembly,
                Assembly.GetEntryAssembly()
            };
            var additionalAssemblies = simUnit?.AdditionalFormAssemblies;
            if (additionalAssemblies != null)
                assembliesToScan.AddRange(additionalAssemblies);

            // Collapse nulls and duplicates once, here, so the published DiscoveryAssemblies is exactly the set the
            // scans walk — an app doing its own attribute discovery over the same set (the reason the property
            // exists) must not scan an assembly the factories skipped, or twice.
            var scanned = new List<Assembly>();
            var seenAssemblies = new HashSet<Assembly>();
            foreach (var assembly in assembliesToScan)
            {
                if (assembly != null && seenAssemblies.Add(assembly))
                    scanned.Add(assembly);
            }

            return scanned;
        }
    }
}
