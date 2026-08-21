// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;
using System.Text;
using WolfCurses.Graphics;

namespace WolfCurses.Window.Control
{
    /// <summary>
    ///     One screen row built out of styled runs, which can then be drawn whole or by a range of columns.
    ///     <para>
    ///         <b>It exists for the range.</b> Anything that draws a panel, a menu, a tooltip or a pointer over
    ///         something already on screen has to render part of a row, skip some columns and render the rest, and a
    ///         finished styled row cannot be cut that way: it is several hundred characters long for twenty columns
    ///         of width, so <c>Substring</c> by column lands in the middle of an escape sequence and spills the
    ///         remainder into the terminal as text. This library has documented that trap in three separate places
    ///         and worked around it three separate ways.
    ///     </para>
    ///     <para>
    ///         The way round it is to keep the row as runs of <b>plain</b> text with a style beside each, and only
    ///         resolve the colour at the moment of drawing, when the columns wanted are already known. Slicing is
    ///         then ordinary arithmetic on plain strings.
    ///     </para>
    ///     <para>
    ///         <b>Run text is plain and that is the contract.</b> Appending text that already carries escapes gives
    ///         a row whose columns do not line up with its characters, which is the exact problem this type is here
    ///         to remove. Style is what the second argument is for.
    ///     </para>
    ///     <para>
    ///         Adjacent runs are coalesced on their <i>resolved</i> escape rather than on the style value, because
    ///         two different colours quantize to the same sequence in the 256-colour and grayscale modes, and
    ///         spending a reset and a re-open between two cells the terminal draws identically is measurable on a
    ///         screen redrawn every frame. Where no run carries any style at all, nothing is emitted: a row nobody
    ///         coloured is byte-identical to the plain text it was built from.
    ///     </para>
    /// </summary>
    public sealed class TextRow
    {
        /// <summary>The runs, in the order they were appended.</summary>
        private readonly List<Run> _runs = new();

        /// <summary>How many columns have been appended so far.</summary>
        public int Width { get; private set; }

        /// <summary>
        ///     Colour mode for the whole row, passed through to every run. Left at
        ///     <see cref="AnsiColorModeEnum.Auto" /> it follows the terminal, which is what a screen wants; a test
        ///     pins it instead, for the same reason every other widget here takes this knob.
        /// </summary>
        public AnsiColorModeEnum ColorMode { get; set; } = AnsiColorModeEnum.Auto;

        /// <summary>Adds a run of plain text.</summary>
        /// <param name="text">The text. Null and empty add nothing at all, not even an empty run.</param>
        /// <param name="style">How to draw it. Left out, the text is drawn in whatever the terminal is using.</param>
        /// <returns>This row, so appends can be chained.</returns>
        public TextRow Append(string text, TextStyle style = default)
        {
            if (string.IsNullOrEmpty(text))
                return this;

            _runs.Add(new Run(text, style));
            Width += text.Length;

            return this;
        }

        /// <summary>Adds a run of one repeated character, which is how rules, gaps and padding are drawn.</summary>
        /// <param name="glyph">The character.</param>
        /// <param name="count">How many. Zero or fewer adds nothing.</param>
        /// <param name="style">How to draw it.</param>
        /// <returns>This row, so appends can be chained.</returns>
        public TextRow Append(char glyph, int count, TextStyle style = default)
        {
            return count <= 0 ? this : Append(new string(glyph, count), style);
        }

        /// <summary>
        ///     Adds spaces until the row is at least this wide, and does nothing at all when it already is.
        ///     <para>
        ///         Worth doing on every row that has a background colour: a style only covers what was actually
        ///         written, and the presenter erases the rest of the line to whatever the terminal's own background
        ///         is, so a row that stops after its last word ends in a ragged edge.
        ///     </para>
        /// </summary>
        /// <param name="width">The width to reach.</param>
        /// <param name="style">How to draw the padding.</param>
        /// <returns>This row, so appends can be chained.</returns>
        public TextRow PadTo(int width, TextStyle style = default)
        {
            return Append(' ', width - Width, style);
        }

        /// <summary>Draws the whole row.</summary>
        /// <returns>The row, styled.</returns>
        public string Render()
        {
            return Render(0, Width);
        }

        /// <summary>
        ///     Draws a range of the row's columns, which is what makes something else drawable over the top of it.
        ///     <para>
        ///         Asking for columns the row does not have gives only the part it does have, rather than padding
        ///         out to the count: the caller compositing an overlay knows the widths it asked for, and inventing
        ///         cells here would quietly widen a row that was already the right size.
        ///     </para>
        /// </summary>
        /// <param name="fromColumn">The first column wanted, counting from the row's left edge.</param>
        /// <param name="count">How many columns. Zero or fewer draws nothing.</param>
        /// <returns>That part of the row, styled.</returns>
        public string Render(int fromColumn, int count)
        {
            if (count <= 0)
                return string.Empty;

            var from = Math.Max(0, fromColumn);
            var end = fromColumn + count;

            var sb = new StringBuilder();
            var pending = new StringBuilder();
            var pendingStyle = default(TextStyle);
            string pendingOpen = null;
            var at = 0;

            foreach (var run in _runs)
            {
                var runEnd = at + run.Text.Length;

                if (runEnd > from && at < end)
                {
                    var start = Math.Max(from, at) - at;
                    var stop = Math.Min(end, runEnd) - at;

                    if (stop > start)
                    {
                        var open = run.Style.OpenSequence(ColorMode);

                        // Broken on the resolved escape, so two cells the terminal would draw the same way stay in
                        // one run whatever colours they were asked for.
                        if (pendingOpen != null && !string.Equals(open, pendingOpen, StringComparison.Ordinal))
                        {
                            sb.Append(pendingStyle.Apply(pending.ToString(), ColorMode));
                            pending.Clear();
                        }

                        pendingOpen = open;
                        pendingStyle = run.Style;
                        pending.Append(run.Text, start, stop - start);
                    }
                }

                at = runEnd;

                if (at >= end)
                    break;
            }

            if (pending.Length > 0)
                sb.Append(pendingStyle.Apply(pending.ToString(), ColorMode));

            return sb.ToString();
        }

        /// <summary>The whole row, so a row can be used where a string is expected.</summary>
        /// <returns>The row, styled.</returns>
        public override string ToString()
        {
            return Render();
        }

        /// <summary>One stretch of plain text and how it is drawn.</summary>
        private readonly struct Run
        {
            /// <summary>Initializes a new instance of the <see cref="Run" /> struct.</summary>
            /// <param name="text">The plain text.</param>
            /// <param name="style">How to draw it.</param>
            public Run(string text, TextStyle style)
            {
                Text = text;
                Style = style;
            }

            /// <summary>The plain text, whose length is therefore also its width.</summary>
            public string Text { get; }

            /// <summary>How to draw it.</summary>
            public TextStyle Style { get; }
        }
    }
}
