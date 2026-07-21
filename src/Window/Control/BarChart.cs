// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WolfCurses.Graphics;

namespace WolfCurses.Window.Control
{
    /// <summary>
    ///     Draws a horizontal bar chart: one row per item, each with its label padded to a common width, a bar whose
    ///     length is proportional to the largest value in the set, and (optionally) the value printed after the bar.
    ///     The result is a multi-line block of text (rows joined by <see cref="Environment.NewLine" />, no trailing
    ///     newline) you return from a window or form's render.
    ///     <para>
    ///         Every part of a row can be colored — the label, the separator, the bar, the track behind it and the
    ///         printed value each have their own <see cref="TextStyle" /> — and the bars can additionally take their
    ///         colors from a <see cref="ColorRamp" />. All of it is off by default, and "off" is not a cheaper shade
    ///         of on: with every style empty and <see cref="Ramp" /> null the renderer never emits a single escape,
    ///         so an uncolored chart is byte-for-byte what this class produced before color existed.
    ///     </para>
    ///     <para>
    ///         <see cref="ColorRampModeEnum.Spread" /> is where this widget earns its keep: it hands row <c>i</c> of
    ///         <c>n</c> the color at <see cref="ColorRamp.SampleIndex" />, so a stepped ramp of <c>n</c> stops drawn
    ///         over <c>n</c> full-width rows lays its stops out one per row — which is to say a bar chart with no
    ///         labels, no values, no separator and equal values <em>is</em> a flag.
    ///     </para>
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
    ///
    ///     // The same widget, drawing a flag: equal values, nothing but bars, one stripe per row.
    ///     var flag = new BarChart
    ///     {
    ///         Width = 30, ShowValues = false, Separator = "", Ramp = ColorRamp.PrideRainbow
    ///     };
    ///     string stripes = flag.Render(rows);   // rows: six BarChartValue("", 1)
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
        ///     What sits between a row's label and its bar. Defaults to the light vertical rule this class has always
        ///     drawn (space, <c>U+2502</c>, space) — the property exists so it can be turned off, not to change what
        ///     an existing chart looks like.
        ///     <para>
        ///         Set it to an empty string for a chart that is meant to read as a solid block of color rather than
        ///         as data with a gutter; a null is treated as empty rather than throwing, since "no separator" is
        ///         obviously what a null means here.
        ///     </para>
        /// </summary>
        public string Separator { get; set; } = " │ ";

        /// <summary>
        ///     Which escape sequences this chart is allowed to emit. <see cref="AnsiColorModeEnum.Auto" /> asks
        ///     <see cref="AnsiConsole.DetectColorMode" /> (which is process-cached and honours <c>NO_COLOR</c>), and
        ///     <see cref="AnsiColorModeEnum.None" /> guarantees not one escape leaves this widget no matter what the
        ///     styles below say. Exposed per instance — mirroring <see cref="AnsiImageOptions.ColorMode" /> — so a
        ///     caller can pin a mode for a single chart without reaching for process-wide environment state.
        /// </summary>
        public AnsiColorModeEnum ColorMode { get; set; } = AnsiColorModeEnum.Auto;

        /// <summary>
        ///     How the filled part of each bar is drawn. Overridden per row by a <see cref="Ramp" /> color and per
        ///     item by <see cref="BarChartValue.Style" />, but its background and bold survive a ramp — the ramp
        ///     supplies a foreground, not a whole look.
        /// </summary>
        public TextStyle BarStyle { get; set; }

        /// <summary>
        ///     How the empty remainder of each bar is drawn when <see cref="ShowTrack" /> is on. Deliberately not
        ///     touched by <see cref="Ramp" />: the track is the absence of the bar, and coloring it the same as the
        ///     bar would erase the very distinction it exists to draw.
        /// </summary>
        public TextStyle TrackStyle { get; set; }

        /// <summary>
        ///     How each row's label is drawn. Applied to the label text only — the spaces that pad it out to the
        ///     common column width stay unstyled, so a background does not bleed across the gutter, and the padding
        ///     itself is still measured on the raw label so no amount of styling can skew the columns.
        /// </summary>
        public TextStyle LabelStyle { get; set; }

        /// <summary>
        ///     How the number after each bar is drawn when <see cref="ShowValues" /> is on. Covers the digits only;
        ///     the single space that separates them from the bar stays plain, because that space belongs to the
        ///     layout rather than to the number.
        /// </summary>
        public TextStyle ValueStyle { get; set; }

        /// <summary>How the <see cref="Separator" /> between label and bar is drawn.</summary>
        public TextStyle SeparatorStyle { get; set; }

        /// <summary>
        ///     Where the bars get their colors, or null (the default) to leave that to <see cref="BarStyle" />. What
        ///     the ramp is asked is decided by <see cref="RampMode" />.
        /// </summary>
        public ColorRamp Ramp { get; set; }

        /// <summary>
        ///     Whether <see cref="Ramp" /> is read across the rows or by each row's value.
        ///     <para>
        ///         <see cref="ColorRampModeEnum.Spread" /> (the default) gives row <c>i</c> of <c>n</c> the color at
        ///         <see cref="ColorRamp.SampleIndex" /> — the decorative reading, and the one that turns a stepped
        ///         ramp into stripes. <see cref="ColorRampModeEnum.Level" /> instead colors each row by its own
        ///         magnitude as a fraction of the largest in the set, so a <see cref="ColorRamp.Traffic" /> chart
        ///         reddens the tall bars; with every value zero there is no scale to read against and every row takes
        ///         the ramp's start.
        ///     </para>
        /// </summary>
        public ColorRampModeEnum RampMode { get; set; } = ColorRampModeEnum.Spread;

        /// <summary>
        ///     Renders the chart. An empty (or null) set yields an empty string. Bars are scaled to the largest value;
        ///     negative or non-finite values are treated as zero for bar length (so a bar is never longer than
        ///     <see cref="Width" /> and never negative), while the printed value still reflects the original number
        ///     when finite.
        ///     <para>
        ///         Color, if any, is opened and closed within a row: nothing an escape does can survive the
        ///         <see cref="Environment.NewLine" /> the rows are joined with, so a styled chart cannot leak its
        ///         colors into whatever a window draws underneath it.
        ///     </para>
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

            // Widest label so every bar starts at the same column. Measured on the raw label, deliberately: an
            // escape sequence has no width on screen but plenty of Length, so measuring styled text would push
            // every bar in the chart out by however many bytes its own label's color cost.
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

            // The escape that opens each part of a row, resolved once for the whole chart instead of per row. Each
            // one is an empty string when its style is empty or the resolved color mode is None, and that empty
            // string is what sends the append helpers below down their plain path — which is the whole of how an
            // uncolored chart stays byte-for-byte identical to what this class drew before color existed.
            var separator = Separator ?? string.Empty;
            var labelOpen = LabelStyle.OpenSequence(ColorMode);
            var separatorOpen = SeparatorStyle.OpenSequence(ColorMode);
            var trackOpen = TrackStyle.OpenSequence(ColorMode);
            var valueOpen = ValueStyle.OpenSequence(ColorMode);
            var barOpen = BarStyle.OpenSequence(ColorMode);

            var rows = new List<string>(list.Count);
            for (var index = 0; index < list.Count; index++)
            {
                var item = list[index];
                var magnitude = Magnitude(item.Value);
                var length = maxValue > 0d
                    ? (int) Math.Round(magnitude / maxValue * Width, MidpointRounding.AwayFromZero)
                    : 0;
                length = Math.Clamp(length, 0, Width);

                var rowOpen = ResolveBarOpen(item, index, list.Count, magnitude, maxValue, barOpen);
                var label = item.Label ?? string.Empty;

                var sb = new StringBuilder();
                AppendStyled(sb, label, labelOpen);
                sb.Append(' ', labelWidth - label.Length);
                AppendStyled(sb, separator, separatorOpen);
                AppendRun(sb, FilledChar, length, rowOpen);

                if (ShowTrack)
                    AppendRun(sb, TrackChar, Width - length, trackOpen);

                if (ShowValues)
                {
                    var display = double.IsNaN(item.Value) || double.IsInfinity(item.Value) ? 0d : item.Value;
                    sb.Append(' ');
                    AppendStyled(sb, display.ToString(ValueFormat, CultureInfo.InvariantCulture), valueOpen);
                }

                rows.Add(sb.ToString());
            }

            return string.Join(Environment.NewLine, rows);
        }

        /// <summary>
        ///     The escape that opens one row's bar, following the precedence the class documents: the item's own
        ///     style if it has one, otherwise the <see cref="Ramp" />'s color for this row, otherwise
        ///     <see cref="BarStyle" /> — for which the caller's already-resolved sequence is handed in, since with no
        ///     ramp and no per-item styles every row wants the identical answer.
        ///     <para>
        ///         A ramp contributes a <em>foreground</em> laid over <see cref="BarStyle" /> rather than replacing
        ///         it outright, so a chart that asked for bold bars on a dark background still gets them when a ramp
        ///         is added — the ramp is answering "what color", not "what look".
        ///     </para>
        /// </summary>
        /// <param name="item">The row's datum.</param>
        /// <param name="index">Which row this is, counting from zero.</param>
        /// <param name="count">How many rows the chart has in total.</param>
        /// <param name="magnitude">The row's non-negative, finite magnitude.</param>
        /// <param name="maxValue">The largest magnitude in the set, which sets the scale.</param>
        /// <param name="barOpen">The already-resolved sequence for <see cref="BarStyle" />.</param>
        private string ResolveBarOpen(BarChartValue item, int index, int count, double magnitude, double maxValue,
            string barOpen)
        {
            if (item.Style.HasValue)
                return item.Style.Value.OpenSequence(ColorMode);

            var ramp = Ramp;
            if (ramp == null)
                return barOpen;

            // Level reads the row's own value against the scale; with nothing to scale against (every value zero or
            // negative) there is no fraction to compute, so the ramp's start stands in rather than a division by zero.
            var color = RampMode == ColorRampModeEnum.Level
                ? ramp.Sample(maxValue > 0d ? magnitude / maxValue : 0d)
                : ramp.SampleIndex(index, count);

            return BarStyle.WithForeground(new TextColor(color)).OpenSequence(ColorMode);
        }

        /// <summary>
        ///     Appends text, wrapped in an already-resolved opening sequence when there is one. Empty text is
        ///     appended as nothing at all rather than as an empty styled run: a bar chart draws zero-length runs
        ///     routinely — the bar of a negative value, the separator when it has been turned off — and an
        ///     open/close pair around nothing would land two escapes between two spaces that the layout depends on.
        /// </summary>
        /// <param name="sb">The row being built.</param>
        /// <param name="text">The text to append.</param>
        /// <param name="open">The opening escape sequence, or an empty string to append the text plain.</param>
        private static void AppendStyled(StringBuilder sb, string text, string open)
        {
            if (string.IsNullOrEmpty(text))
                return;

            if (open.Length == 0)
            {
                sb.Append(text);
                return;
            }

            sb.Append(open).Append(text).Append(TextStyle.ResetSequence);
        }

        /// <summary>
        ///     Appends a run of one repeated glyph, wrapped in an already-resolved opening sequence when there is
        ///     one. The uncolored path appends straight into the builder without materializing the run as a string,
        ///     which is both what the original code did and what keeps a per-frame dashboard cheap.
        /// </summary>
        /// <param name="sb">The row being built.</param>
        /// <param name="glyph">The character to repeat.</param>
        /// <param name="count">How many times to repeat it; zero or less appends nothing, escapes included.</param>
        /// <param name="open">The opening escape sequence, or an empty string to append the run plain.</param>
        private static void AppendRun(StringBuilder sb, char glyph, int count, string open)
        {
            if (count <= 0)
                return;

            if (open.Length == 0)
            {
                sb.Append(glyph, count);
                return;
            }

            sb.Append(open).Append(glyph, count).Append(TextStyle.ResetSequence);
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
