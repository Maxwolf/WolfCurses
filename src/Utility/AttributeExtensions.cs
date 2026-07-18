// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 12/31/2015@4:49 AM

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace WolfCurses.Utility
{
    /// <summary>
    ///     Meant for dealing with attributes and grabbing all the available classes of a given type with specified attribute
    ///     using generics.
    /// </summary>
    public static class AttributeExtensions
    {
        /// <summary>
        ///     Find all the classes which have a custom attribute I've defined on them, and I want to be able to find them
        ///     on-the-fly when an application is using my library. Scans only the process entry assembly.
        /// </summary>
        /// <param name="inherit">The inherit.</param>
        /// <remarks>
        ///     Kept for backward compatibility. This works only when the types live in the process entry assembly, so
        ///     hosted scenarios (unit test runners, plugins, or any app whose SimulationApp subclass is not the entry
        ///     point) should prefer <see cref="GetTypesWith{TAttribute}(Assembly,bool)" /> or
        ///     <see cref="GetTypesWith{TAttribute}(IEnumerable{Assembly},bool)" />. Some hosts (native embedding,
        ///     unmanaged entry points) have no managed entry assembly at all, in which case this yields nothing.
        ///     http://stackoverflow.com/a/720171
        /// </remarks>
        public static IEnumerable<Type> GetTypesWith<TAttribute>(bool inherit)
            where TAttribute : Attribute
        {
            return GetTypesWith<TAttribute>(Assembly.GetEntryAssembly(), inherit);
        }

        /// <summary>
        ///     Find all the classes in a single assembly decorated with <typeparamref name="TAttribute" />.
        /// </summary>
        /// <param name="assembly">Assembly to scan; a null assembly yields an empty sequence so callers can pass
        ///     <see cref="Assembly.GetEntryAssembly" /> without a null guard.</param>
        /// <param name="inherit">Whether to also match attributes inherited from base types.</param>
        public static IEnumerable<Type> GetTypesWith<TAttribute>(Assembly assembly, bool inherit)
            where TAttribute : Attribute
        {
            // Collection of types we have found.
            var foundTypes = new List<Type>();

            // Loop through every defined type in the assembly, adding each matching type exactly once no matter
            // how many attribute instances are stacked on it.
            foreach (var typeInfo in GetLoadableDefinedTypes(assembly))
            {
                if (typeInfo.IsDefined(typeof(TAttribute), inherit))
                    foundTypes.Add(typeInfo.UnderlyingSystemType);
            }

            return foundTypes;
        }

        /// <summary>
        ///     Enumerates an assembly's defined types, tolerating the assembly being only partially loadable.
        ///     A partially-loadable assembly throws ReflectionTypeLoadException when enumerated — common once opt-in
        ///     plugin/module assemblies (see <see cref="SimulationApp.AdditionalFormAssemblies" />) are scanned, since
        ///     one may reference a dependency that cannot be resolved at runtime. Rather than aborting all discovery,
        ///     keep the types that DID load and skip the ones that failed. A null assembly yields an empty sequence so
        ///     callers can pass <see cref="Assembly.GetEntryAssembly" /> without a null guard.
        /// </summary>
        internal static IEnumerable<TypeInfo> GetLoadableDefinedTypes(Assembly assembly)
        {
            if (assembly == null)
                return Enumerable.Empty<TypeInfo>();

            try
            {
                return assembly.DefinedTypes.ToList();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null).Select(t => t.GetTypeInfo()).ToList();
            }
        }

        /// <summary>
        ///     Find all the classes across several assemblies decorated with <typeparamref name="TAttribute" />,
        ///     yielding each distinct type exactly once. This is the overload that lets a SimulationApp be discovered
        ///     whether it is the process entry point or merely a referenced assembly, and lets consumers fold in extra
        ///     (plugin/module) assemblies.
        /// </summary>
        /// <param name="assemblies">Assemblies to scan; null entries and repeated assemblies are skipped.</param>
        /// <param name="inherit">Whether to also match attributes inherited from base types.</param>
        public static IEnumerable<Type> GetTypesWith<TAttribute>(IEnumerable<Assembly> assemblies, bool inherit)
            where TAttribute : Attribute
        {
            var foundTypes = new List<Type>();
            if (assemblies == null)
                return foundTypes;

            // The common case is the entry assembly and the SimulationApp assembly being one and the same, so guard
            // against scanning an assembly (or emitting a type) twice — a duplicated form type would otherwise crash
            // FormFactory's dictionary Add.
            var scannedAssemblies = new HashSet<Assembly>();
            var seenTypes = new HashSet<Type>();
            foreach (var assembly in assemblies)
            {
                if (assembly == null || !scannedAssemblies.Add(assembly))
                    continue;

                foreach (var type in GetTypesWith<TAttribute>(assembly, inherit))
                {
                    if (seenTypes.Add(type))
                        foundTypes.Add(type);
                }
            }

            return foundTypes;
        }

        /// <summary>Find the fields in an enum that have a specific attribute with a specific value.</summary>
        /// <param name="source">The source.</param>
        /// <param name="inherit">The inherit.</param>
        /// <returns>The <see cref="IEnumerable" />.</returns>
        public static IEnumerable<T> GetAttributes<T>(this ICustomAttributeProvider source, bool inherit)
            where T : Attribute
        {
            var attrs = source.GetCustomAttributes(typeof (T), inherit);
            return (attrs != null) ? (T[]) attrs : Enumerable.Empty<T>();
        }

        /// <summary>Grabs first attribute from a given object and returns the first one in the enumeration.</summary>
        /// <typeparam name="T">Role of attribute that we should be looking for.</typeparam>
        /// <param name="value">Object that will have attribute tag specified in generic parameter..</param>
        /// <returns>Attribute of the specified type from inputted object.</returns>
        private static T GetAttribute<T>(this object value) where T : Attribute
        {
            var type = value.GetType();
            var memberInfo = type.GetMember(value.ToString());
            var attributes = memberInfo.FirstOrDefault()?.GetCustomAttributes(typeof (T), false);
            return (T) attributes?.FirstOrDefault();
        }

        /// <summary>Attempts to grab description attribute from any object.</summary>
        /// <param name="value">Object that should have description attribute.</param>
        /// <returns>Description attribute text, if null then type name without name space.</returns>
        public static string ToDescriptionAttribute(this object value)
        {
            var attribute = value.GetAttribute<DescriptionAttribute>();
            return attribute == null ? value.ToString() : attribute.Description;
        }
    }
}