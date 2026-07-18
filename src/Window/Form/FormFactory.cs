// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 12/31/2015@4:49 AM

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using WolfCurses.Utility;

namespace WolfCurses.Window.Form
{
    /// <summary>
    ///     Keeps track of all the possible states a given game Windows can have by using attributes and reflection to keep
    ///     track of which user data object gets mapped to which particular state.
    /// </summary>
    public sealed class FormFactory
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="T:WolfCurses.Window.Form.FormFactory" /> class, discovering
        ///     forms only from the process entry assembly. Kept for backward compatibility; prefer the overload that
        ///     takes the owning <see cref="SimulationApp" /> so hosted scenarios keep working.
        /// </summary>
        public FormFactory() : this(null)
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="T:WolfCurses.Window.Form.FormFactory" /> class, discovering
        ///     forms from the assembly that defines the concrete <paramref name="simUnit" /> subclass in addition to the
        ///     process entry assembly (and any assemblies the app opts into via
        ///     <see cref="SimulationApp.AdditionalFormAssemblies" />).
        /// </summary>
        /// <param name="simUnit">The owning simulation, or null to fall back to entry-assembly-only discovery.</param>
        public FormFactory(SimulationApp simUnit)
        {
            // Create dictionaries for reference tracking for what states belong to what game modes.
            LoadedForms = new Dictionary<Type, Type>();

            // Discover [ParentWindow] forms from the assembly that defines the concrete SimulationApp subclass (so
            // hosted scenarios — unit test runners, plugins, native embedding — work even when the app is not the
            // process entry point) plus the process entry assembly (preserving the original behavior for apps that
            // ARE their own entry point). Duplicate assemblies are collapsed, so entry == app assembly registers each
            // form exactly once.
            var assembliesToScan = new List<Assembly>
            {
                // The WolfCurses library itself, so built-in control forms (e.g. the file dialog's form) are
                // discovered for every consuming app without it having to opt them in.
                typeof(FormFactory).Assembly,
                simUnit?.GetType().Assembly,
                Assembly.GetEntryAssembly()
            };
            var additionalAssemblies = simUnit?.AdditionalFormAssemblies;
            if (additionalAssemblies != null)
                assembliesToScan.AddRange(additionalAssemblies);

            // Collapse nulls and duplicates once, here, so the published DiscoveryAssemblies is exactly the set the
            // scan below walks — an app doing its own attribute discovery over the same set (the reason the property
            // exists) must not scan an assembly this factory skipped, or twice.
            var scanned = new List<Assembly>();
            var seenAssemblies = new HashSet<Assembly>();
            foreach (var assembly in assembliesToScan)
            {
                if (assembly != null && seenAssemblies.Add(assembly))
                    scanned.Add(assembly);
            }

            DiscoveryAssemblies = scanned;

            // Collect all of the states with the custom attribute decorated on them.
            var foundStates = AttributeExtensions.GetTypesWith<ParentWindowAttribute>(scanned, false);
            foreach (var stateType in foundStates)
            {
                // GetModule the attribute itself from the state we are working on, which gives us the game Windows enum.
                var stateAttribute = stateType.GetTypeInfo().GetAttributes<ParentWindowAttribute>(false).First();
                var stateParentMode = stateAttribute.ParentWindow;

                // Add the state reference list for lookup and instancing later during runtime.
                LoadedForms.Add(stateType, stateParentMode);
            }
        }

        /// <summary>
        ///     Reference dictionary for all the reflected state types.
        /// </summary>
        private Dictionary<Type, Type> LoadedForms { get; set; }

        /// <summary>
        ///     The deduped, null-free set of assemblies this factory scanned for <c>[ParentWindow]</c> forms: the
        ///     library itself, the concrete <see cref="SimulationApp" /> subclass's assembly, the process entry
        ///     assembly, and anything from <see cref="SimulationApp.AdditionalFormAssemblies" />. Published (via
        ///     <see cref="SimulationApp.DiscoveryAssemblies" />) so an application running its own attribute
        ///     discovery — a plugin system, an event registry — can walk the same set instead of reaching for
        ///     <c>Assembly.GetEntryAssembly()</c> and faking it when hosted.
        /// </summary>
        public IReadOnlyCollection<Assembly> DiscoveryAssemblies { get; }

        /// <summary>Creates and adds the specified type of state to currently active game Windows.</summary>
        /// <param name="stateType">Role object that is the actual type of state that needs created.</param>
        /// <param name="activeMode">Current active game Windows passed to factory so no need to call game simulation singleton.</param>
        /// <returns>Created state instance from reference types build on startup.</returns>
        public IForm CreateStateFromType(Type stateType, IWindow activeMode)
        {
            // Check if the state exists in our reference list.
            if (!LoadedForms.ContainsKey(stateType))
                throw new ArgumentException(
                    "State factory cannot create state from type that does not exist in reference states! " +
                    "Perhaps developer forgot [ParentWindow] attribute on form?!");

            // Abstract classes cannot be instantiated; surface the mistake instead of handing back null.
            if (stateType.GetTypeInfo().IsAbstract)
                throw new ArgumentException(
                    $"State factory cannot create an instance of abstract form type {stateType.FullName}!",
                    nameof(stateType));

            // Create the state, it will have constructor with one parameter.
            var stateInstance = Activator.CreateInstance(stateType, activeMode);
            if (stateInstance is not IForm createdForm)
                throw new ArgumentException(
                    $"State factory created {stateType.FullName} but it does not implement IForm!", nameof(stateType));

            // Pass the created state back to caller.
            return createdForm;
        }

        /// <summary>
        ///     Called when primary simulation is closing down.
        /// </summary>
        public void Destroy()
        {
            LoadedForms.Clear();
            LoadedForms = null;
        }
    }
}