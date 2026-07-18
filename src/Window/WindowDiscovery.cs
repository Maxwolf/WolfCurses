// Created by Maxwolf (bigmaxwolf.com)

using System;
using System.Collections.Generic;
using System.Reflection;
using WolfCurses.Utility;

namespace WolfCurses.Window
{
    /// <summary>
    ///     Reflection scan behind the default <see cref="SimulationApp.AllowedWindows" />: finds every window type the
    ///     simulation could create without the app having to list them, the same way <see cref="Form.FormFactory" />
    ///     finds <c>[ParentWindow]</c> forms — so windows and forms are discovered from the same assemblies by the
    ///     same rules.
    /// </summary>
    internal static class WindowDiscovery
    {
        /// <summary>
        ///     Finds every type in <paramref name="assemblies" /> the window factory could actually instantiate: a
        ///     concrete (non-abstract, non-generic) <see cref="IWindow" /> implementation with a public
        ///     single-parameter constructor accepting any <see cref="SimulationApp" /> — the constructor shape
        ///     <see cref="WindowFactory.CreateWindow" /> invokes. Types that implement <see cref="IWindow" /> without
        ///     that shape are skipped rather than surfaced as errors, so an off-convention helper type in a scanned
        ///     assembly cannot poison an app that never wanted it; such a type can still be opted in by overriding
        ///     <see cref="SimulationApp.AllowedWindows" /> explicitly.
        /// </summary>
        /// <param name="assemblies">Deduped assembly set to scan (see <see cref="DiscoveryAssemblyResolver" />).</param>
        internal static IReadOnlyCollection<Type> DiscoverWindows(IEnumerable<Assembly> assemblies)
        {
            var foundWindows = new List<Type>();
            foreach (var assembly in assemblies)
            {
                foreach (var typeInfo in AttributeExtensions.GetLoadableDefinedTypes(assembly))
                {
                    if (typeInfo.IsAbstract || typeInfo.IsInterface || typeInfo.IsGenericTypeDefinition)
                        continue;

                    var windowType = typeInfo.UnderlyingSystemType;
                    if (!typeof(IWindow).IsAssignableFrom(windowType))
                        continue;

                    if (HasFactoryConstructor(windowType))
                        foundWindows.Add(windowType);
                }
            }

            return foundWindows;
        }

        /// <summary>
        ///     True when the type has a public constructor taking exactly one parameter that any
        ///     <see cref="SimulationApp" /> satisfies, guaranteeing <c>Activator.CreateInstance(type, simUnit)</c>
        ///     succeeds no matter which concrete app is running. A constructor demanding a specific app subclass
        ///     fails this test on purpose — it would only be constructible under that one app.
        /// </summary>
        private static bool HasFactoryConstructor(Type windowType)
        {
            foreach (var constructor in windowType.GetConstructors())
            {
                var parameters = constructor.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType.IsAssignableFrom(typeof(SimulationApp)))
                    return true;
            }

            return false;
        }
    }
}
