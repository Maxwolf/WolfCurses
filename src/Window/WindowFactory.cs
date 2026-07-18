// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 12/31/2015@4:49 AM

using System;
using System.Collections.Generic;
using System.Reflection;

namespace WolfCurses.Window
{
    /// <summary>
    ///     Factory pattern for creating game modes on the fly during runtime based on enumeration input parameter.
    /// </summary>
    public sealed class WindowFactory
    {
        /// <summary>
        ///     Reference to running game simulation we will need to pass along to created window instances.
        /// </summary>
        private readonly SimulationApp _simUnit;

        /// <summary>
        ///     Initializes a new instance of the <see cref="WindowFactory" /> class.
        ///     Creates a new Windows factory that will look over the application for all known game types and create reference
        ///     list which we can use to get instances of a given Windows by asking for it.
        /// </summary>
        /// <param name="simUnit">Core simulation which is controlling the window factory.</param>
        public WindowFactory(SimulationApp simUnit)
        {
            // Copy over reference for simulation core.
            _simUnit = simUnit;

            // Create dictionaries for holding statistics about times run and for reference loading.
            Windows = new Dictionary<string, Type>();

            // Loop through every game Windows type the simulation allows (discovered by reflection unless the app
            // overrode AllowedWindows with a curated list).
            foreach (var window in simUnit.AllowedWindows)
            {
                // Keyed by full name so identically named windows in different namespaces coexist — a must now that
                // the default AllowedWindows scans whole assemblies the app does not control the contents of.
                if (Windows.TryGetValue(window.FullName, out var existing))
                {
                    // The same type appearing twice is collapsed rather than refused (mirroring how stacked
                    // [ParentWindow] attributes register once); two DISTINCT types sharing a full name across
                    // assemblies cannot both be registered, so surface both spellings and the way out.
                    if (existing == window)
                        continue;

                    throw new ArgumentException(
                        $"Window factory cannot register two distinct window types sharing the full name {window.FullName}: " +
                        $"{existing.AssemblyQualifiedName} and {window.AssemblyQualifiedName}. " +
                        "Override SimulationApp.AllowedWindows to curate which one the simulation should use.");
                }

                // Add the game Windows to reference list for lookup and instancing later during runtime.
                Windows.Add(window.FullName, window);
            }
        }

        /// <summary>
        ///     Reference dictionary for all the found game modes that have the game Windows attribute on top of them which the
        ///     simulation will want to be able to create instances of when running.
        /// </summary>
        private Dictionary<string, Type> Windows { get; }

        /// <summary>
        ///     Change to new view Windows when told that internal logic wants to display view options to player for a specific set
        ///     of data in the simulation.
        /// </summary>
        /// <param name="window">The windows.</param>
        /// <returns>New game Windows instance based on the Windows input parameter.</returns>
        public IWindow CreateWindow(Type window)
        {
            // Grab the game Windows type reference from inputted Windows type enum.
            var modeType = Windows[window.FullName];

            // Abstract classes cannot be instantiated; surface the mistake instead of handing back null.
            if (modeType.GetTypeInfo().IsAbstract)
                throw new ArgumentException(
                    $"Window factory cannot create an instance of abstract window type {modeType.FullName}! " +
                    "Only concrete window classes may be listed in SimulationApp.AllowedWindows.", nameof(window));

            // Create the game Windows, it will have single parameter for user data.
            var gameModeInstance = Activator.CreateInstance(modeType, _simUnit);
            if (gameModeInstance is not IWindow createdWindow)
                throw new ArgumentException(
                    $"Window factory created {modeType.FullName} but it does not implement IWindow!", nameof(window));

            return createdWindow;
        }

        /// <summary>
        ///     Called when the simulation is closing down.
        /// </summary>
        public void Destroy()
        {
            Windows.Clear();
        }
    }
}