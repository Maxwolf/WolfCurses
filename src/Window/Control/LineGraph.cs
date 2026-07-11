// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WolfCurses.Window.Control
{
    /// <summary>
    ///     Plots a series of numbers as a 2-D line graph on a character grid: the values are spread across
    ///     <see cref="Width" /> columns and scaled to <see cref="Height" /> rows (top = highest value, bottom = lowest),
    ///     optionally with connecting segments, an area fill, a left Y-axis with min/max scale labels, and a bottom
    ///     X-axis. The result is a multi-line block of text (rows joined by <see cref="Environment.NewLine" />, no
    ///     trailing newline) you return from a window or form's render — handy for a rolling metric such as a live
    ///     value over time.
    /// </summary>
    /// <example>
    ///     <code>
    ///     var graph = new LineGraph { Width = 40, Height = 10 };
    ///     string block = graph.Render(samples); // samples is IReadOnlyList&lt;double&gt;
    ///     </code>
    /// </example>
    public sealed class LineGraph
    {
        /// <summary>Number of columns in the plot area. Must be at least 1.</summary>
        public int Width { get; set; } = 60;

        /// <summary>Number of rows in the plot area. Must be at least 1.</summary>
        public int Height { get; set; } = 15;

        /// <summary>Glyph used for plotted points and connecting segments.</summary>
        public char PointChar { get; set; } = '•';

        /// <summary>Glyph used to fill the area beneath the line when <see cref="Fill" /> is on.</summary>
        public char AreaChar { get; set; } = '░';

        /// <summary>Whether to fill the area beneath the line.</summary>
        public bool Fill { get; set; }

        /// <summary>Whether to draw vertical connecting segments between consecutive points so the line is continuous.</summary>
        public bool Connected { get; set; } = true;

        /// <summary>Whether to draw the left (Y) and bottom (X) axis lines around the plot.</summary>
        public bool ShowAxis { get; set; } = true;

        /// <summary>Whether to show the max value (top) and min value (bottom) as labels in the left gutter.</summary>
        public bool ShowScale { get; set; } = true;

        /// <summary>Pin the low end of the value scale; when null the series minimum is used.</summary>
        public double? Minimum { get; set; }

        /// <summary>Pin the high end of the value scale; when null the series maximum is used.</summary>
        public double? Maximum { get; set; }

        /// <summary>Numeric format (invariant culture) used for the scale labels.</summary>
        public string ScaleFormat { get; set; } = "0.##";

        /// <summary>
        ///     Renders the graph. A null or empty series draws an empty plot (still with axes/scale if enabled) so the
        ///     surrounding layout stays stable. Non-finite samples (NaN / infinity) leave a gap in the line and are
        ///     ignored when computing the automatic range.
        /// </summary>
        /// <param name="series">The values to plot, left to right.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <see cref="Width" /> or <see cref="Height" /> is less than 1.</exception>
        public string Render(IReadOnlyList<double> series)
        {
            if (Width < 1)
                throw new ArgumentOutOfRangeException(nameof(Width), Width,
                    "Line graph width must be greater than zero.");
            if (Height < 1)
                throw new ArgumentOutOfRangeException(nameof(Height), Height,
                    "Line graph height must be greater than zero.");

            var count = series?.Count ?? 0;

            // Determine the value range from the finite samples, unless the caller pinned it.
            var haveFinite = false;
            var dataMin = 0d;
            var dataMax = 0d;
            for (var i = 0; i < count; i++)
            {
                var value = series[i];
                if (double.IsNaN(value) || double.IsInfinity(value))
                    continue;

                if (!haveFinite)
                {
                    dataMin = dataMax = value;
                    haveFinite = true;
                }
                else
                {
                    if (value < dataMin) dataMin = value;
                    if (value > dataMax) dataMax = value;
                }
            }

            var min = Minimum ?? (haveFinite ? dataMin : 0d);
            var max = Maximum ?? (haveFinite ? dataMax : 0d);

            // A single pinned bound can fall entirely on the wrong side of the data (e.g. Maximum pinned below every
            // value), leaving min > max: an inverted scale that would plot every point on the middle row and print
            // reversed gutter labels. Reconcile to a valid, non-inverted window — the pinned bound stays
            // authoritative; a pin that excludes all of the data collapses the scale, which then renders flat.
            if (min > max)
            {
                if (Minimum.HasValue && Maximum.HasValue)
                    (min, max) = (max, min);
                else if (Maximum.HasValue)
                    min = max;
                else
                    max = min;
            }

            // Blank plot grid.
            var grid = new char[Height][];
            for (var r = 0; r < Height; r++)
            {
                grid[r] = new char[Width];
                for (var c = 0; c < Width; c++)
                    grid[r][c] = ' ';
            }

            // Plot each column by sampling the series at the matching position, connecting to the previous point.
            var previousRow = -1;
            for (var c = 0; c < Width && count > 0; c++)
            {
                var index = SampleIndex(c, Width, count);
                var value = series[index];
                if (double.IsNaN(value) || double.IsInfinity(value))
                {
                    // Gap: break the line so we do not connect across missing data.
                    previousRow = -1;
                    continue;
                }

                var row = ValueToRow(value, min, max);

                if (Fill)
                    for (var r = row + 1; r < Height; r++)
                        grid[r][c] = AreaChar;

                if (Connected && previousRow >= 0)
                {
                    var lo = Math.Min(previousRow, row);
                    var hi = Math.Max(previousRow, row);
                    for (var r = lo; r <= hi; r++)
                        grid[r][c] = PointChar;
                }

                grid[row][c] = PointChar;
                previousRow = row;
            }

            return Compose(grid, min, max);
        }

        /// <summary>Maps a plot column to the index of the sample it should show.</summary>
        private static int SampleIndex(int column, int width, int count)
        {
            if (count <= 1 || width <= 1)
                return 0;

            var index = (int) Math.Round((double) column / (width - 1) * (count - 1),
                MidpointRounding.AwayFromZero);
            return Math.Clamp(index, 0, count - 1);
        }

        /// <summary>Maps a value to a grid row (row 0 is the top / maximum, the last row is the bottom / minimum).</summary>
        private int ValueToRow(double value, double min, double max)
        {
            var range = max - min;
            var fraction = range > 0d ? (value - min) / range : 0.5d;
            if (fraction < 0d) fraction = 0d;
            if (fraction > 1d) fraction = 1d;

            var row = (int) Math.Round((1d - fraction) * (Height - 1), MidpointRounding.AwayFromZero);
            return Math.Clamp(row, 0, Height - 1);
        }

        /// <summary>Turns the plotted grid into the final text, adding the scale gutter and axis lines if enabled.</summary>
        private string Compose(char[][] grid, double min, double max)
        {
            var maxLabel = ShowScale ? max.ToString(ScaleFormat, CultureInfo.InvariantCulture) : string.Empty;
            var minLabel = ShowScale ? min.ToString(ScaleFormat, CultureInfo.InvariantCulture) : string.Empty;
            var gutterWidth = ShowScale ? Math.Max(maxLabel.Length, minLabel.Length) : 0;

            var lines = new List<string>(Height + 1);
            for (var r = 0; r < Height; r++)
            {
                var sb = new StringBuilder();

                if (ShowScale)
                {
                    string label;
                    if (r == 0)
                        label = maxLabel;
                    else if (r == Height - 1)
                        label = minLabel;
                    else
                        label = string.Empty;

                    sb.Append(label.PadLeft(gutterWidth));
                }

                if (ShowAxis)
                    sb.Append('│');

                sb.Append(grid[r]);
                lines.Add(sb.ToString());
            }

            if (ShowAxis)
            {
                var axis = new StringBuilder();
                if (ShowScale)
                    axis.Append(' ', gutterWidth);
                axis.Append('└');
                axis.Append('─', Width);
                lines.Add(axis.ToString());
            }

            return string.Join(Environment.NewLine, lines);
        }
    }
}
