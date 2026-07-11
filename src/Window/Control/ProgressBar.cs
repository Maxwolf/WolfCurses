// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System;
using System.Globalization;
using System.Text;

namespace WolfCurses.Window.Control
{
    /// <summary>
    ///     A configurable determinate progress bar you drop into a window or form's rendered text. Unlike the static
    ///     <see cref="TextProgress" /> helper, this is a reusable, styleable control: set the width, the filled/empty
    ///     glyphs, whether to wrap the bar in brackets, whether to append a percentage, and an optional leading label,
    ///     then call <see cref="Render(double,double)" /> (a value against a maximum) or
    ///     <see cref="Render(double)" /> (a fraction from 0 to 1) each time you draw the screen. The bar and the
    ///     percentage are kept consistent — a completely full bar always reads 100% and a completely empty bar 0%.
    /// </summary>
    /// <example>
    ///     <code>
    ///     private readonly ProgressBar _bar = new ProgressBar { Width = 20, Label = "Download" };
    ///     // ...
    ///     public override string OnRenderForm() => _bar.Render(bytesDone, bytesTotal);
    ///     // Download [██████████░░░░░░░░░░]  50%
    ///     </code>
    /// </example>
    public sealed class ProgressBar
    {
        /// <summary>Number of cells in the bar itself (not counting brackets, label, or percentage). Must be at least 1.</summary>
        public int Width { get; set; } = 30;

        /// <summary>Glyph used for the filled portion of the bar. Defaults to a solid block.</summary>
        public char FilledChar { get; set; } = '█';

        /// <summary>Glyph used for the unfilled portion of the bar. Defaults to a light shade.</summary>
        public char EmptyChar { get; set; } = '░';

        /// <summary>Whether to wrap the bar in <c>[ ]</c> brackets.</summary>
        public bool ShowBrackets { get; set; } = true;

        /// <summary>Whether to append the percentage (rounded to a whole number) after the bar.</summary>
        public bool ShowPercentage { get; set; } = true;

        /// <summary>Optional text placed before the bar, followed by a single space when present.</summary>
        public string Label { get; set; }

        /// <summary>
        ///     Renders the bar for a value measured against a maximum. A non-positive maximum (or a non-finite value)
        ///     renders as empty; values outside the range are clamped so the bar never over- or under-flows.
        /// </summary>
        /// <param name="value">Current progress value.</param>
        /// <param name="maximum">The value that represents a full bar.</param>
        public string Render(double value, double maximum)
        {
            var fraction = maximum > 0 && !double.IsNaN(value) && !double.IsNaN(maximum)
                ? value / maximum
                : 0d;
            return Render(fraction);
        }

        /// <summary>
        ///     Renders the bar for a fraction of completion. The fraction is clamped to the range 0 to 1 (and a
        ///     non-finite fraction is treated as 0), so callers never have to sanitize it themselves.
        /// </summary>
        /// <param name="fraction">Completion from 0 (empty) to 1 (full).</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <see cref="Width" /> is less than 1.</exception>
        public string Render(double fraction)
        {
            if (Width < 1)
                throw new ArgumentOutOfRangeException(nameof(Width), Width,
                    "Progress bar width must be greater than zero.");

            if (double.IsNaN(fraction) || double.IsInfinity(fraction))
                fraction = 0d;

            fraction = Math.Clamp(fraction, 0d, 1d);

            var percent = (int) Math.Round(fraction * 100d, MidpointRounding.AwayFromZero);

            var filled = (int) Math.Round(fraction * Width, MidpointRounding.AwayFromZero);
            filled = Math.Clamp(filled, 0, Width);

            // Keep the bar and the percentage from contradicting each other: the bar is completely full only at
            // 100% and completely empty only at 0%. Without this, a fraction like 0.95 rounds the fill up to a full
            // bar (round(9.5) == Width) while the label still reads 95%.
            if (percent >= 100)
                filled = Width;
            else if (filled >= Width)
                filled = Width - 1;

            if (percent <= 0)
                filled = 0;
            else if (filled <= 0)
                filled = 1;

            var sb = new StringBuilder();

            if (!string.IsNullOrEmpty(Label))
                sb.Append(Label).Append(' ');

            if (ShowBrackets)
                sb.Append('[');

            sb.Append(FilledChar, filled);
            sb.Append(EmptyChar, Width - filled);

            if (ShowBrackets)
                sb.Append(']');

            if (ShowPercentage)
                sb.AppendFormat(CultureInfo.InvariantCulture, " {0,3}%", percent);

            return sb.ToString();
        }
    }
}
