// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WolfCurses.Window.Control
{
    /// <summary>
    ///     One labelled datum in a <see cref="BarChart" />: a category name and its numeric magnitude.
    /// </summary>
    public readonly struct BarChartValue
    {
        /// <summary>Initializes a new instance of the <see cref="BarChartValue" /> struct.</summary>
        /// <param name="label">The category label shown to the left of the bar.</param>
        /// <param name="value">The magnitude of the bar.</param>
        public BarChartValue(string label, double value)
        {
            Label = label;
            Value = value;
        }

        /// <summary>The category label shown to the left of the bar.</summary>
        public string Label { get; }

        /// <summary>The magnitude of the bar.</summary>
        public double Value { get; }
    }

    /// <summary>
    ///     Draws a horizontal bar chart: one row per item, each with its label padded to a common width, a bar whose
    ///     length is proportional to the largest value in the set, and (optionally) the value printed after the bar.
    ///     The result is a multi-line block of text (rows joined by <see cref="Environment.NewLine" />, no trailing
    ///     newline) you return from a window or form's render.
    /// </summary>
    /// <example>
    ///     <code>
    ///     var chart = new BarChart { Width = 20 };
    ///     string block = chart.Render(new[]
    ///     {
    ///         new BarChartValue("Wood", 12),
    ///         new BarChartValue("Iron", 5),
    ///         new BarChartValue("Gold", 20),
    ///     });
    ///     // Wood │ ████████████ 12
    ///     // Iron │ █████ 5
    ///     // Gold │ ████████████████████ 20
    ///     </code>
    /// </example>
    public sealed class BarChart
    {
        /// <summary>Length in characters of the longest bar (the item with the largest value). Must be at least 1.</summary>
        public int Width { get; set; } = 40;

        /// <summary>Glyph used to draw the bars. Defaults to a solid block.</summary>
        public char FilledChar { get; set; } = '█';

        /// <summary>Glyph used to pad the remainder of each bar to <see cref="Width" /> when <see cref="ShowTrack" /> is on.</summary>
        public char TrackChar { get; set; } = '░';

        /// <summary>Whether to draw the empty remainder of each bar (an aligned "track"); off by default.</summary>
        public bool ShowTrack { get; set; }

        /// <summary>Whether to print each item's value after its bar.</summary>
        public bool ShowValues { get; set; } = true;

        /// <summary>Numeric format (invariant culture) used when <see cref="ShowValues" /> is on.</summary>
        public string ValueFormat { get; set; } = "0.##";

        /// <summary>
        ///     Renders the chart. An empty (or null) set yields an empty string. Bars are scaled to the largest value;
        ///     negative or non-finite values are treated as zero for bar length (so a bar is never longer than
        ///     <see cref="Width" /> and never negative), while the printed value still reflects the original number
        ///     when finite.
        /// </summary>
        /// <param name="items">The labelled values to chart, top to bottom.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <see cref="Width" /> is less than 1.</exception>
        public string Render(IEnumerable<BarChartValue> items)
        {
            if (Width < 1)
                throw new ArgumentOutOfRangeException(nameof(Width), Width,
                    "Bar chart width must be greater than zero.");

            if (items == null)
                return string.Empty;

            var list = new List<BarChartValue>(items);
            if (list.Count == 0)
                return string.Empty;

            // Widest label so every bar starts at the same column.
            var labelWidth = 0;
            foreach (var item in list)
            {
                var length = (item.Label ?? string.Empty).Length;
                if (length > labelWidth)
                    labelWidth = length;
            }

            // Largest non-negative, finite value sets the scale for the full-width bar.
            var maxValue = 0d;
            foreach (var item in list)
            {
                var magnitude = Magnitude(item.Value);
                if (magnitude > maxValue)
                    maxValue = magnitude;
            }

            var rows = new List<string>(list.Count);
            foreach (var item in list)
            {
                var magnitude = Magnitude(item.Value);
                var length = maxValue > 0d
                    ? (int) Math.Round(magnitude / maxValue * Width, MidpointRounding.AwayFromZero)
                    : 0;
                length = Math.Clamp(length, 0, Width);

                var sb = new StringBuilder();
                sb.Append((item.Label ?? string.Empty).PadRight(labelWidth));
                sb.Append(" │ ");
                sb.Append(FilledChar, length);

                if (ShowTrack)
                    sb.Append(TrackChar, Width - length);

                if (ShowValues)
                {
                    var display = double.IsNaN(item.Value) || double.IsInfinity(item.Value) ? 0d : item.Value;
                    sb.Append(' ');
                    sb.Append(display.ToString(ValueFormat, CultureInfo.InvariantCulture));
                }

                rows.Add(sb.ToString());
            }

            return string.Join(Environment.NewLine, rows);
        }

        /// <summary>Non-negative, finite magnitude used for bar length (negatives and non-finite values become zero).</summary>
        private static double Magnitude(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
                return 0d;
            return value;
        }
    }
}
