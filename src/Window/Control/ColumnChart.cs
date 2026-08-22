// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;
using WolfCurses.Graphics;

namespace WolfCurses.Window.Control
{
    /// <summary>
    ///     Values drawn as bars standing up from a baseline: a level meter, a spectrum, a histogram, the last sixty
    ///     seconds of anything. The vertical counterpart of <see cref="BarChart" />, and the fourth of the data
    ///     widgets beside it, <see cref="Sparkline" /> and <see cref="LineGraph" />.
    ///     <para>
    ///         <b>It is <see cref="Sparkline" /> with a height</b>, which is exactly why it is a separate type
    ///         rather than an option on that one: a sparkline's whole contract is that it is a single row and fits
    ///         inside a sentence. What this adds is the arithmetic a height brings, and that arithmetic is the part
    ///         everybody gets wrong.
    ///     </para>
    ///     <para>
    ///         <b>A value that is small but not nothing still draws something.</b> Round the fraction into whole
    ///         cells and a quiet channel comes out blank, so the meter reads as switched off when it is merely low -
    ///         and telling those two apart is the one thing a meter is for. Anything above the bottom of the scale
    ///         gets at least the shortest glyph in the ramp. That is <see cref="ScrollBar" />'s lesson arriving from
    ///         the other end: a length that rounds away to nothing has stopped saying anything.
    ///     </para>
    ///     <para>
    ///         The mirror of it is pinned as well: a value at the top of the scale fills its last row with the
    ///         ramp's last glyph and does not spill into the row above.
    ///     </para>
    ///     <para>
    ///         Rows are returned rather than one string, the same as <see cref="Keypad.Render" /> and
    ///         <see cref="MonthGrid.Render" />, because a caller almost always has something to put beside them and
    ///         splitting a styled block back apart by newline works until a style contains one.
    ///     </para>
    /// </summary>
    /// <example>
    ///     <code>
    ///     private readonly ColumnChart _bars = new() {Height = 8, Minimum = 0, Maximum = 1};
    ///
    ///     foreach (var row in _bars.Render(bands, peaks))
    ///         sb.AppendLine(row);
    ///     </code>
    /// </example>
    public sealed class ColumnChart
    {
        /// <summary>The default low-to-high glyph ramp, the same eight block heights <see cref="Sparkline" /> uses.</summary>
        public const string DefaultRamp = "▁▂▃▄▅▆▇█";

        /// <summary>How many screen rows tall the chart is.</summary>
        public int Height { get; set; } = 8;

        /// <summary>How many screen columns wide one bar is drawn.</summary>
        public int ColumnWidth { get; set; } = 1;

        /// <summary>How many blank columns are left between one bar and the next.</summary>
        public int Gap { get; set; }

        /// <summary>
        ///     The glyphs a partly-filled row is drawn with, shortest first. Its length is how many steps of
        ///     sub-row resolution there are, so a ramp of one glyph gives whole rows only.
        /// </summary>
        public string Ramp { get; set; } = DefaultRamp;

        /// <summary>What an empty cell is drawn with. A space unless a caller wants a visible track.</summary>
        public char EmptyChar { get; set; } = ' ';

        /// <summary>
        ///     What a peak marker is drawn with, when <see cref="Render(IEnumerable{double}, IEnumerable{double})" />
        ///     is given peaks. A peak sitting where the bar already reaches is not drawn, since the bar is there.
        /// </summary>
        public char PeakChar { get; set; } = '▔';

        /// <summary>Pin the low end of the scale; when null the lowest value drawn is used.</summary>
        public double? Minimum { get; set; }

        /// <summary>Pin the high end of the scale; when null the highest value drawn is used.</summary>
        public double? Maximum { get; set; }

        /// <summary>Which colour vocabulary the styles resolve through; pinnable per instance for tests.</summary>
        public AnsiColorModeEnum ColorMode { get; set; } = AnsiColorModeEnum.Auto;

        /// <summary>How a bar is painted. Left unset it is drawn in whatever the terminal is already using.</summary>
        public TextStyle ColumnStyle { get; set; }

        /// <summary>How the empty space above a bar is painted.</summary>
        public TextStyle EmptyStyle { get; set; }

        /// <summary>How a peak marker is painted.</summary>
        public TextStyle PeakStyle { get; set; }

        /// <summary>
        ///     An optional colour ramp for the bars. With <see cref="ColorRampModeEnum.Spread" /> the colour varies
        ///     across the chart; with <see cref="ColorRampModeEnum.Level" /> it is chosen by how tall the bar is,
        ///     which is what makes a meter go red at the top.
        ///     <para>
        ///         Named in full for the reason <see cref="Sparkline.SparklineColorRamp" /> is: <see cref="Ramp" />
        ///         was already taken by the <i>glyph</i> ramp, and two properties one letter apart, one a string of
        ///         block characters and one a <see cref="ColorRamp" />, is a mistake waiting to be made.
        ///     </para>
        /// </summary>
        public ColorRamp ColumnColorRamp { get; set; }

        /// <summary>How <see cref="ColumnColorRamp" /> is applied.</summary>
        public ColorRampModeEnum RampMode { get; set; } = ColorRampModeEnum.Spread;

        /// <summary>How many screen columns the whole chart occupies for a given number of bars.</summary>
        /// <param name="bars">How many bars will be drawn.</param>
        /// <returns>The width in columns.</returns>
        public int WidthFor(int bars)
        {
            if (bars <= 0)
                return 0;

            return bars * Math.Max(1, ColumnWidth) + (bars - 1) * Math.Max(0, Gap);
        }

        /// <summary>Draws the chart.</summary>
        /// <param name="values">The bars, left to right. Null or empty draws blank rows.</param>
        /// <returns>The rows, top to bottom.</returns>
        public IReadOnlyList<string> Render(IEnumerable<double> values)
        {
            return Render(values, null);
        }

        /// <summary>
        ///     Draws the chart with a peak marker floating above each bar.
        ///     <para>
        ///         The peaks are the caller's to keep, for the reason <see cref="MonthGrid.Today" /> is told rather
        ///         than read: they are a fact about the values <i>over time</i>, and how fast a peak falls back is a
        ///         decision about the thing being measured rather than about drawing it.
        ///     </para>
        /// </summary>
        /// <param name="values">The bars, left to right.</param>
        /// <param name="peaks">
        ///     One peak per bar, on the same scale. Null for none; a shorter list simply marks fewer bars.
        /// </param>
        /// <returns>The rows, top to bottom.</returns>
        public IReadOnlyList<string> Render(IEnumerable<double> values, IEnumerable<double> peaks)
        {
            var height = Math.Max(1, Height);
            var series = ToList(values);
            var caps = ToList(peaks);
            var rows = new List<string>(height);

            if (series.Count == 0)
            {
                for (var row = 0; row < height; row++)
                    rows.Add(string.Empty);

                return rows;
            }

            Scale(series, caps, out var low, out var span);

            var steps = Math.Max(1, Ramp == null ? 0 : Ramp.Length);
            var width = Math.Max(1, ColumnWidth);
            var gap = Math.Max(0, Gap);

            var bars = new int[series.Count];
            var marks = new int[series.Count];

            for (var i = 0; i < series.Count; i++)
            {
                bars[i] = ToUnits(series[i], low, span, height, steps);
                marks[i] = i < caps.Count ? ToUnits(caps[i], low, span, height, steps) : 0;
            }

            for (var row = 0; row < height; row++)
            {
                var fromBottom = height - 1 - row;
                var line = new TextRow {ColorMode = ColorMode};

                for (var i = 0; i < series.Count; i++)
                {
                    if (i > 0 && gap > 0)
                        line.Append(' ', gap, EmptyStyle);

                    var inRow = bars[i] - fromBottom * steps;

                    if (inRow > 0)
                    {
                        var glyph = inRow >= steps ? Ramp[steps - 1] : Ramp[inRow - 1];

                        line.Append(new string(glyph, width), ColumnStyleFor(series[i], i, series.Count, low, span));
                        continue;
                    }

                    // A peak is only ever drawn above the bar it belongs to; where the bar reaches, the bar wins.
                    var marked = marks[i] > bars[i] && (marks[i] - 1) / steps == fromBottom;

                    line.Append(new string(marked ? PeakChar : EmptyChar, width), marked ? PeakStyle : EmptyStyle);
                }

                rows.Add(line.Render());
            }

            return rows;
        }

        /// <summary>
        ///     A value's height in ramp units, which is rows multiplied by the ramp's own resolution.
        ///     <para>
        ///         The floor at one unit is the whole reason this is a method rather than a multiplication: a
        ///         channel at one percent rounds to nothing and the meter looks switched off. Only a value at or
        ///         below the bottom of the scale draws nothing at all.
        ///     </para>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="low">The bottom of the scale.</param>
        /// <param name="span">How much the scale covers.</param>
        /// <param name="height">How many rows tall the chart is.</param>
        /// <param name="steps">How many ramp units make one whole row.</param>
        /// <returns>The height in ramp units.</returns>
        private static int ToUnits(double value, double low, double span, int height, int steps)
        {
            if (double.IsNaN(value))
                return 0;

            var fraction = span <= 0d ? 0d : (value - low) / span;

            if (fraction <= 0d)
                return 0;

            if (fraction >= 1d)
                return height * steps;

            return Math.Max(1, (int) Math.Round(fraction * height * steps));
        }

        /// <summary>How a bar's own cells are painted, which is where a colour ramp is applied.</summary>
        /// <param name="value">The bar's value.</param>
        /// <param name="index">Which bar.</param>
        /// <param name="count">How many bars.</param>
        /// <param name="low">The bottom of the scale.</param>
        /// <param name="span">How much the scale covers.</param>
        /// <returns>The style.</returns>
        private TextStyle ColumnStyleFor(double value, int index, int count, double low, double span)
        {
            var ramp = ColumnColorRamp;

            if (ramp == null)
                return ColumnStyle;

            var color = RampMode == ColorRampModeEnum.Level
                ? ramp.Sample(span <= 0d ? 0d : Math.Clamp((value - low) / span, 0d, 1d))
                : ramp.SampleIndex(index, count);

            return ColumnStyle.WithForeground(new TextColor(color));
        }

        /// <summary>Works out the scale, honouring whichever ends were pinned.</summary>
        /// <param name="series">The values.</param>
        /// <param name="caps">The peaks, which share the scale and so have to fit inside it.</param>
        /// <param name="low">The bottom of the scale.</param>
        /// <param name="span">How much the scale covers.</param>
        private void Scale(List<double> series, List<double> caps, out double low, out double span)
        {
            var min = Minimum ?? double.MaxValue;
            var max = Maximum ?? double.MinValue;

            if (Minimum == null || Maximum == null)
            {
                foreach (var value in series)
                {
                    if (double.IsNaN(value))
                        continue;

                    if (Minimum == null && value < min)
                        min = value;

                    if (Maximum == null && value > max)
                        max = value;
                }

                // A peak above every bar would otherwise sit off the top of a chart scaled to the bars alone.
                foreach (var value in caps)
                {
                    if (!double.IsNaN(value) && Maximum == null && value > max)
                        max = value;
                }
            }

            if (min > max)
            {
                min = 0d;
                max = 0d;
            }

            low = min;
            span = max - min;
        }

        /// <summary>Takes a copy of a sequence, since it is walked more than once.</summary>
        /// <param name="values">The sequence, or null.</param>
        /// <returns>The values.</returns>
        private static List<double> ToList(IEnumerable<double> values)
        {
            var list = new List<double>();

            if (values != null)
                list.AddRange(values);

            return list;
        }
    }
}
