// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System;
using System.Collections.Generic;
using System.Linq;

namespace WolfCurses.Controls
{
    /// <summary>
    ///     A ready-made list picker for WolfCurses applications. Call <see cref="Choose(SimulationApp,string,IEnumerable{string},Action{int},Action)" />
    ///     to let the user pick one option (chosen by number) or <see cref="ChooseMany(SimulationApp,string,IEnumerable{string},Action{IReadOnlyList{int}},Action,IEnumerable{int})" />
    ///     to let them check several before confirming (optionally pre-checking a starting set). Generic overloads take your own item type plus a label
    ///     selector and hand the chosen item(s) straight back to you. The dialog pushes itself on top of the current
    ///     screen and closes itself once the user chooses or cancels.
    /// </summary>
    /// <remarks>
    ///     The picker is a window shipped in the library, so the default
    ///     <see cref="SimulationApp.AllowedWindows" /> discovers it (and its form) automatically; an app that
    ///     overrides <c>AllowedWindows</c> must include <c>typeof(SelectListWindow)</c> in its list. From inside a
    ///     window the simulation is available as <c>SimUnit</c>.
    /// </remarks>
    public static class SelectList
    {
        /// <summary>Lets the user pick one option by number; the callback receives the chosen option's index.</summary>
        public static void Choose(SimulationApp simulation, string title, IEnumerable<string> options,
            Action<int> onChosen, Action onCancelled = null)
        {
            if (onChosen == null)
                throw new ArgumentNullException(nameof(onChosen));

            var labels = options?.ToList() ?? new List<string>();
            Show(simulation, title, labels, false,
                indices =>
                {
                    if (indices.Count > 0)
                        onChosen(indices[0]);
                },
                onCancelled);
        }

        /// <summary>Lets the user pick one item of your own type; the callback receives the chosen item.</summary>
        public static void Choose<T>(SimulationApp simulation, string title, IEnumerable<T> options,
            Func<T, string> label, Action<T> onChosen, Action onCancelled = null)
        {
            if (onChosen == null)
                throw new ArgumentNullException(nameof(onChosen));

            var items = options?.ToList() ?? new List<T>();
            Show(simulation, title, ToLabels(items, label), false,
                indices =>
                {
                    if (indices.Count > 0)
                        onChosen(items[indices[0]]);
                },
                onCancelled);
        }

        /// <summary>
        ///     Lets the user check several options; the callback receives the chosen option indices (ascending).
        ///     Pass <paramref name="initiallySelected" /> to pre-check option indices (e.g. the current state), so the
        ///     user edits from there and the confirmed set becomes the new state; out-of-range indices are ignored.
        /// </summary>
        public static void ChooseMany(SimulationApp simulation, string title, IEnumerable<string> options,
            Action<IReadOnlyList<int>> onChosen, Action onCancelled = null, IEnumerable<int> initiallySelected = null)
        {
            if (onChosen == null)
                throw new ArgumentNullException(nameof(onChosen));

            var labels = options?.ToList() ?? new List<string>();
            Show(simulation, title, labels, true, onChosen, onCancelled, initiallySelected);
        }

        /// <summary>
        ///     Lets the user check several items of your own type; the callback receives the chosen items. Pass
        ///     <paramref name="initiallySelected" /> to pre-check the items already in that set (matched against
        ///     <paramref name="options" /> with the default equality comparer); the confirmed items become the new
        ///     state. Items not present in <paramref name="options" /> are ignored.
        /// </summary>
        public static void ChooseMany<T>(SimulationApp simulation, string title, IEnumerable<T> options,
            Func<T, string> label, Action<IReadOnlyList<T>> onChosen, Action onCancelled = null,
            IEnumerable<T> initiallySelected = null)
        {
            if (onChosen == null)
                throw new ArgumentNullException(nameof(onChosen));

            var items = options?.ToList() ?? new List<T>();
            Show(simulation, title, ToLabels(items, label), true,
                indices => onChosen(indices.Select(i => items[i]).ToList()),
                onCancelled, ToIndices(items, initiallySelected));
        }

        /// <summary>
        ///     Maps a set of items to the indices at which they appear in <paramref name="items" /> (default equality).
        ///     Every occurrence of a wanted item is included, so duplicate options all start checked; returns null when
        ///     nothing was requested so the dialog opens with everything unchecked.
        /// </summary>
        private static IEnumerable<int> ToIndices<T>(List<T> items, IEnumerable<T> initiallySelected)
        {
            if (initiallySelected == null)
                return null;

            var wanted = new HashSet<T>(initiallySelected);
            var indices = new List<int>();
            for (var i = 0; i < items.Count; i++)
                if (wanted.Contains(items[i]))
                    indices.Add(i);

            return indices;
        }

        private static List<string> ToLabels<T>(List<T> items, Func<T, string> label)
        {
            return items
                .Select(item => label != null ? label(item) : Convert.ToString(item) ?? string.Empty)
                .ToList();
        }

        private static void Show(SimulationApp simulation, string title, List<string> options, bool multiSelect,
            Action<IReadOnlyList<int>> onChosen, Action onCancelled, IEnumerable<int> initiallySelected = null)
        {
            if (simulation == null)
                throw new ArgumentNullException(nameof(simulation));

            if (!IsAllowed(simulation))
                throw new InvalidOperationException(
                    "To use SelectList, include typeof(WolfCurses.Controls.SelectListWindow) in your " +
                    "SimulationApp.AllowedWindows override.");

            simulation.WindowManager.Add(typeof (SelectListWindow));

            // Windows are single-instance per type, so a second open re-activates the existing window instead of a
            // fresh one. Fail loudly if one is already showing (Initialized) or mid-close (ShouldRemoveMode) rather
            // than silently reconfiguring it and dropping the first caller's callback.
            var focused = simulation.WindowManager.FocusedWindow;
            if (focused is not SelectListWindow window || window.ShouldRemoveMode ||
                focused.UserData is not SelectListData data || data.Initialized)
                throw new InvalidOperationException(
                    "A selection list is already open or closing. Open only one at a time and wait for its callback " +
                    "before opening another.");

            data.Initialize(title, options, multiSelect, onChosen, onCancelled, initiallySelected);
        }

        private static bool IsAllowed(SimulationApp simulation)
        {
            if (simulation.AllowedWindows == null)
                return false;

            foreach (var window in simulation.AllowedWindows)
                if (window == typeof (SelectListWindow))
                    return true;

            return false;
        }
    }
}
