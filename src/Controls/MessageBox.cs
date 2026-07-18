// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System;

namespace WolfCurses.Controls
{
    /// <summary>
    ///     A ready-made modal message box. Use <see cref="Show(SimulationApp,string,Action)" /> to tell the user
    ///     something (an OK dialog), <see cref="Confirm" /> to ask a yes/no question, or
    ///     <see cref="Show(SimulationApp,string,MessageBoxButtonsEnum,Action{MessageBoxResultEnum})" /> for full control over
    ///     the buttons and result. The dialog pushes itself on top of the current screen and closes itself when the
    ///     user answers.
    /// </summary>
    /// <remarks>
    ///     The message box is a window shipped in the library, so the default
    ///     <see cref="SimulationApp.AllowedWindows" /> discovers it (and its form) automatically; an app that
    ///     overrides <c>AllowedWindows</c> must include <c>typeof(MessageBoxWindow)</c> in its list. From inside a
    ///     window the simulation is available as <c>SimUnit</c>.
    /// </remarks>
    public static class MessageBox
    {
        /// <summary>Shows an OK message; <paramref name="onDismissed" /> (optional) runs when the user presses ENTER.</summary>
        public static void Show(SimulationApp simulation, string message, Action onDismissed = null)
        {
            Show(simulation, message, MessageBoxButtonsEnum.Ok, _ => onDismissed?.Invoke());
        }

        /// <summary>Asks a yes/no question; runs <paramref name="onYes" /> or <paramref name="onNo" /> accordingly.</summary>
        public static void Confirm(SimulationApp simulation, string message, Action onYes, Action onNo = null)
        {
            if (onYes == null)
                throw new ArgumentNullException(nameof(onYes));

            Show(simulation, message, MessageBoxButtonsEnum.YesNo,
                result =>
                {
                    if (result == MessageBoxResultEnum.Yes)
                        onYes();
                    else
                        onNo?.Invoke();
                });
        }

        /// <summary>Shows a message with the given buttons; the callback receives the pressed button.</summary>
        public static void Show(SimulationApp simulation, string message, MessageBoxButtonsEnum buttons,
            Action<MessageBoxResultEnum> onResult)
        {
            if (simulation == null)
                throw new ArgumentNullException(nameof(simulation));
            if (onResult == null)
                throw new ArgumentNullException(nameof(onResult));

            if (!IsAllowed(simulation))
                throw new InvalidOperationException(
                    "To use MessageBox, include typeof(WolfCurses.Controls.MessageBoxWindow) in your " +
                    "SimulationApp.AllowedWindows override.");

            simulation.WindowManager.Add(typeof (MessageBoxWindow));

            // Add re-activates an existing window of this type instead of creating a fresh one, so a second open
            // while one is already showing (Initialized) or mid-close (ShouldRemoveMode) must fail loudly rather
            // than silently reconfigure it and drop the first caller's callback.
            var focused = simulation.WindowManager.FocusedWindow;
            if (focused is not MessageBoxWindow window || window.ShouldRemoveMode ||
                focused.UserData is not MessageBoxData data || data.Initialized)
                throw new InvalidOperationException(
                    "A message box is already open or closing. Open only one at a time and wait for its callback " +
                    "before opening another.");

            data.Initialize(message, buttons, onResult);
        }

        private static bool IsAllowed(SimulationApp simulation)
        {
            if (simulation.AllowedWindows == null)
                return false;

            foreach (var window in simulation.AllowedWindows)
                if (window == typeof (MessageBoxWindow))
                    return true;

            return false;
        }
    }
}
