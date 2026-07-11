// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System;

namespace WolfCurses.Controls
{
    /// <summary>
    ///     A ready-made modal text prompt. Call <see cref="Prompt" /> to ask the user for a line of text, optionally
    ///     pre-filled with a default, hidden for passwords, and/or validated. The dialog pushes itself on top of the
    ///     current screen and closes itself when the user submits a valid value or cancels (by submitting a blank
    ///     line).
    /// </summary>
    /// <remarks>
    ///     The prompt is a window, so list <c>typeof(TextInputWindow)</c> in your
    ///     <see cref="SimulationApp.AllowedWindows" />; its form ships in the library and is discovered automatically.
    ///     From inside a window the simulation is available as <c>SimUnit</c>. Because the input line is trimmed,
    ///     leading/trailing whitespace is not preserved and a value cannot be empty (an empty submission cancels).
    /// </remarks>
    public static class TextInputDialog
    {
        /// <summary>Prompts for a line of text.</summary>
        /// <param name="simulation">The running simulation (available as <c>SimUnit</c> inside a window).</param>
        /// <param name="message">The prompt shown to the user.</param>
        /// <param name="onSubmit">Called with the entered value. Required.</param>
        /// <param name="onCancelled">Optional; called if the user submits a blank line.</param>
        /// <param name="defaultValue">Optional; pre-fills the editable input line.</param>
        /// <param name="masked">When true, typed characters are echoed as asterisks (password entry).</param>
        /// <param name="validator">Optional; returns an error message to reject a value (keeping the dialog open) or null to accept it.</param>
        public static void Prompt(SimulationApp simulation, string message, Action<string> onSubmit,
            Action onCancelled = null, string defaultValue = null, bool masked = false,
            Func<string, string> validator = null)
        {
            if (simulation == null)
                throw new ArgumentNullException(nameof(simulation));
            if (onSubmit == null)
                throw new ArgumentNullException(nameof(onSubmit));

            if (!IsAllowed(simulation))
                throw new InvalidOperationException(
                    "To use TextInputDialog, add typeof(WolfCurses.Controls.TextInputWindow) to your " +
                    "SimulationApp.AllowedWindows.");

            simulation.WindowManager.Add(typeof (TextInputWindow));

            // Add re-activates an existing window of this type, so a second open while one is already showing
            // (Initialized) or mid-close (ShouldRemoveMode) must throw rather than silently reconfigure it and drop
            // the first caller's callback.
            var focused = simulation.WindowManager.FocusedWindow;
            if (focused is not TextInputWindow window || window.ShouldRemoveMode ||
                focused.UserData is not TextInputData data || data.Initialized)
                throw new InvalidOperationException(
                    "A text prompt is already open or closing. Open only one at a time and wait for its callback " +
                    "before opening another.");

            data.Initialize(message, masked, validator, onSubmit, onCancelled);

            // Pre-fill the editable input buffer with the default value, if any (the dialog window is now focused and
            // accepting input, so the injected text is accepted and echoed like typed input).
            if (!string.IsNullOrEmpty(defaultValue))
            {
                simulation.InputManager.ClearBuffer();
                simulation.InputManager.AppendToInputBuffer(defaultValue);
            }
        }

        private static bool IsAllowed(SimulationApp simulation)
        {
            if (simulation.AllowedWindows == null)
                return false;

            foreach (var window in simulation.AllowedWindows)
                if (window == typeof (TextInputWindow))
                    return true;

            return false;
        }
    }
}
