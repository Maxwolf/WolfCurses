// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System;
using WolfCurses.Window;

namespace WolfCurses.Controls
{
    /// <summary>
    ///     Holds the prompt, options, and callbacks of a <see cref="TextInputDialog" />, plus the latest validation
    ///     error to show. It is the <see cref="WindowData" /> of the <see cref="TextInputWindow" />.
    /// </summary>
    public sealed class TextInputData : WindowData
    {
        /// <summary>The prompt shown above the input line.</summary>
        public string Message { get; private set; }

        /// <summary>When true, typed characters are echoed as asterisks (password entry).</summary>
        public bool Masked { get; private set; }

        /// <summary>
        ///     Optional validator: given the submitted value, it returns an error message to reject it (the dialog
        ///     stays open and shows the message) or null/empty to accept it.
        /// </summary>
        public Func<string, string> Validator { get; private set; }

        /// <summary>Invoked with the accepted value.</summary>
        public Action<string> OnSubmit { get; private set; }

        /// <summary>Invoked if the user cancels (submits a blank line). Optional.</summary>
        public Action OnCancelled { get; private set; }

        /// <summary>The most recent validation error to display, or null when there is none.</summary>
        public string Error { get; set; }

        /// <summary>True once <see cref="Initialize" /> has run.</summary>
        public bool Initialized { get; private set; }

        /// <summary>Configures the dialog.</summary>
        public void Initialize(string message, bool masked, Func<string, string> validator,
            Action<string> onSubmit, Action onCancelled)
        {
            Message = message ?? string.Empty;
            Masked = masked;
            Validator = validator;
            OnSubmit = onSubmit;
            OnCancelled = onCancelled;
            Error = null;
            Initialized = true;
        }
    }
}
