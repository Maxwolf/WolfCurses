// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;

namespace WolfCurses.Window.Control
{
    /// <summary>
    ///     One labelled, clickable cell of a <see cref="Keypad" />.
    /// </summary>
    public sealed class KeypadButton
    {
        /// <summary>What <see cref="IsEnabled" /> answers when no <see cref="EnabledWhen" /> was given.</summary>
        private bool _isEnabled = true;

        /// <summary>Initializes a new instance of the <see cref="KeypadButton" /> class.</summary>
        /// <param name="label">What is written on it.</param>
        /// <param name="action">What pressing it does. A button with nothing to do cannot be pressed.</param>
        /// <param name="span">
        ///     How many of the pad's columns it covers, at least one. A button spanning two absorbs the rule that
        ///     would have run between them, which is how a calculator's wide zero key is drawn.
        /// </param>
        public KeypadButton(string label, Action action = null, int span = 1)
        {
            Label = label ?? string.Empty;
            Action = action;
            Span = Math.Max(1, span);
        }

        /// <summary>What is written on it.</summary>
        public string Label { get; set; }

        /// <summary>What pressing it does.</summary>
        public Action Action { get; set; }

        /// <summary>How many of the pad's columns it covers.</summary>
        public int Span { get; }

        /// <summary>
        ///     Whether it can be pressed just now. Assigning works for a button switched on or off once;
        ///     <see cref="EnabledWhen" /> is for one whose answer changes as the program runs.
        /// </summary>
        public bool IsEnabled
        {
            get => EnabledWhen?.Invoke() ?? _isEnabled;
            set => _isEnabled = value;
        }

        /// <summary>
        ///     Asked afresh whenever the answer is needed, rather than a flag an owner has to keep up to date.
        ///     <para>
        ///         The same reasoning as <see cref="MenuBarEntry.EnabledWhen" />: the answer is wanted at three
        ///         separate moments (drawing the pad, hovering it, pressing it) and an owner refreshing a flag has
        ///         to remember before all three. A recall key that lights up when the memory is empty is what
        ///         missing one looks like.
        ///     </para>
        /// </summary>
        public Func<bool> EnabledWhen { get; set; }

        /// <summary>
        ///     Whether pressing it would do anything: it has to be switched on and have something to do. This is
        ///     the one question the drawing, the hover and the press all ask, so what is drawn dead is exactly what
        ///     refuses to be pressed.
        /// </summary>
        public bool IsPressable => IsEnabled && Action != null;
    }
}
