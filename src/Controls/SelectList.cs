// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System;
using System.Collections.Generic;
using System.Linq;

namespace WolfCurses.Controls
{
    /// <summary>
    ///     A ready-made list picker for WolfCurses applications. Call <see cref="Choose(SimulationApp,string,IEnumerable{string},Action{int},Action)" />
    ///     to let the user pick one option (chosen by number) or <see cref="ChooseMany(SimulationApp,string,IEnumerable{string},Action{IReadOnlyList{int}},Action)" />
    ///     to let them check several before confirming. Generic overloads take your own item type plus a label
    ///     selector and hand the chosen item(s) straight back to you. The dialog pushes itself on top of the current
    ///     screen and closes itself once the user chooses or cancels.
    /// </summary>
    /// <remarks>
    ///     The picker is a window, so the application must list <c>typeof(SelectListWindow)</c> in its
    ///     <see cref="SimulationApp.AllowedWindows" />; its form ships in the library and is discovered automatically.
    ///     From inside a window the simulation is available as <c>SimUnit</c>.
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

        /// <summary>Lets the user check several options; the callback receives the chosen option indices (ascending).</summary>
        public static void ChooseMany(SimulationApp simulation, string title, IEnumerable<string> options,
            Action<IReadOnlyList<int>> onChosen, Action onCancelled = null)
        {
            if (onChosen == null)
                throw new ArgumentNullException(nameof(onChosen));

            var labels = options?.ToList() ?? new List<string>();
            Show(simulation, title, labels, true, onChosen, onCancelled);
        }

        /// <summary>Lets the user check several items of your own type; the callback receives the chosen items.</summary>
        public static void ChooseMany<T>(SimulationApp simulation, string title, IEnumerable<T> options,
            Func<T, string> label, Action<IReadOnlyList<T>> onChosen, Action onCancelled = null)
        {
            if (onChosen == null)
                throw new ArgumentNullException(nameof(onChosen));

            var items = options?.ToList() ?? new List<T>();
            Show(simulation, title, ToLabels(items, label), true,
                indices => onChosen(indices.Select(i => items[i]).ToList()),
                onCancelled);
        }

        private static List<string> ToLabels<T>(List<T> items, Func<T, string> label)
        {
            return items
                .Select(item => label != null ? label(item) : Convert.ToString(item) ?? string.Empty)
                .ToList();
        }

        private static void Show(SimulationApp simulation, string title, List<string> options, bool multiSelect,
            Action<IReadOnlyList<int>> onChosen, Action onCancelled)
        {
            if (simulation == null)
                throw new ArgumentNullException(nameof(simulation));

            if (!IsAllowed(simulation))
                throw new InvalidOperationException(
                    "To use SelectList, add typeof(WolfCurses.Controls.SelectListWindow) to your " +
                    "SimulationApp.AllowedWindows.");

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

            data.Initialize(title, options, multiSelect, onChosen, onCancelled);
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
