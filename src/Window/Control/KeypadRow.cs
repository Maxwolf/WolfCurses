// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System.Collections.Generic;

namespace WolfCurses.Window.Control
{
    /// <summary>
    ///     One row of a <see cref="Keypad" />.
    ///     <para>
    ///         Rows are given rather than inferred from a column count, because a row is where a button's
    ///         <see cref="KeypadButton.Span" /> is resolved: two rows of the same pad may divide the same width
    ///         differently, and a flat list of buttons could not say where one row ends and the next begins without
    ///         the caller counting spans in its head.
    ///     </para>
    /// </summary>
    public sealed class KeypadRow
    {
        /// <summary>The buttons, left to right.</summary>
        private readonly List<KeypadButton> _buttons;

        /// <summary>Initializes a new instance of the <see cref="KeypadRow" /> class.</summary>
        /// <param name="buttons">The buttons, left to right.</param>
        public KeypadRow(params KeypadButton[] buttons)
        {
            _buttons = new List<KeypadButton>(buttons ?? System.Array.Empty<KeypadButton>());
        }

        /// <summary>The buttons, left to right.</summary>
        public IReadOnlyList<KeypadButton> Buttons => _buttons;

        /// <summary>How many of the pad's columns this row accounts for, spans included.</summary>
        public int Columns
        {
            get
            {
                var columns = 0;

                foreach (var button in _buttons)
                    columns += button.Span;

                return columns;
            }
        }
    }
}
