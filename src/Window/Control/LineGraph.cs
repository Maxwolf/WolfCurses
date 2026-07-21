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
        /// <summary>The glyph joining the two axes, drawn at the bottom-left corner.</summary>
        private const char AxisCorner = '└';

        /// <summary>The glyph the bottom (X) axis is ruled with.</summary>
        private const char AxisHorizontal = '─';

        /// <summary>
        ///     The glyph the left (Y) axis is drawn with. Held as a string rather than a char so a styled axis can be
        ///     handed to <see cref="TextStyle.Apply(string, AnsiColorModeEnum)" /> without allocating one per row.
        /// </summary>
        private const string AxisVertical = "│";

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
        ///     Which ANSI color mode the styles below are rendered in.
        ///     <see cref="AnsiColorModeEnum.Auto" /> — the default — asks the environment, which is what a real
        ///     application wants; pinning a concrete mode exists for tests and for a host that already knows exactly
        ///     what its terminal can do, and lets them do it without touching process-global environment state.
        ///     <see cref="AnsiColorModeEnum.None" /> guarantees not one escape byte leaves this widget however the
        ///     styles below are set, which is the same promise <c>NO_COLOR</c> gets everywhere else in the library.
        /// </summary>
        public AnsiColorModeEnum ColorMode { get; set; } = AnsiColorModeEnum.Auto;

        /// <summary>
        ///     Style for the plotted points and the vertical segments connecting them. When <see cref="Ramp" /> is
        ///     also set the ramp supplies the foreground and this contributes only its background and bold, so a ramp
        ///     and a bold-on-black line style compose rather than fight.
        /// </summary>
        public TextStyle LineStyle { get; set; }

        /// <summary>
        ///     Style for the area fill drawn beneath the line when <see cref="Fill" /> is on. Separate from
        ///     <see cref="LineStyle" /> because the usual want is a dim wash under a bright line — but when
        ///     <see cref="Ramp" /> is set both take that column's ramp color, so a column still reads as one object
        ///     rather than a line of one hue standing on a puddle of another.
        /// </summary>
        public TextStyle AreaStyle { get; set; }

        /// <summary>
        ///     Style for both axis lines and the corner glyph that joins them. The corner and the bottom rule go out
        ///     as a single escape run, since they are one line to the eye. Inert when <see cref="ShowAxis" /> is off.
        /// </summary>
        public TextStyle AxisStyle { get; set; }

        /// <summary>
        ///     Style for the min/max labels in the left gutter. It wraps the label glyphs only and never the blank
        ///     padding that right-aligns them, so a background color does not paint an otherwise empty column — the
        ///     gutter is layout, not content. Inert when <see cref="ShowScale" /> is off.
        /// </summary>
        public TextStyle ScaleStyle { get; set; }

        /// <summary>
        ///     Colors each plotted column by where its value sits between the scale's minimum and maximum — the
        ///     bottom of the plot takes the ramp's first color and the top its last, so a
        ///     <see cref="ColorRamp.Traffic" /> line reddens as it climbs. Null (the default) leaves
        ///     <see cref="LineStyle" /> and <see cref="AreaStyle" /> to speak for themselves.
        ///     <para>
        ///         Unlike the bar-style widgets there is no <see cref="ColorRampModeEnum" /> here, deliberately: a
        ///         column of a line graph already <em>is</em> a value, so the only reading a ramp can have is the
        ///         value's — the spread-across-the-extent reading would just recolor the X axis, which the X axis
        ///         already describes perfectly well.
        ///     </para>
        ///     <para>
        ///         A degenerate scale (a flat series, or a pin that collapsed the window) has no fraction to offer, so
        ///         it samples the ramp's middle — the same 0.5 that puts a flat series on the middle row.
        ///     </para>
        /// </summary>
        public ColorRamp Ramp { get; set; }

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

            // Color is opt-in, and so is every byte of work it costs. Nothing below allocates a style grid, resolves
            // a color mode or asks a style for an escape unless somebody actually set one — so an untouched graph
            // walks precisely the code path it walked before any of this existed and emits byte-for-byte the same
            // text, right down to the trailing spaces of a blank row.
            var anyStyle = Ramp != null || !LineStyle.IsEmpty || !AreaStyle.IsEmpty ||
                           !AxisStyle.IsEmpty || !ScaleStyle.IsEmpty;
            var mode = anyStyle && ColorMode == AnsiColorModeEnum.Auto ? AnsiConsole.DetectColorMode() : ColorMode;

            // Only the plot area needs a per-cell record of what was drawn; the gutter label and the two axes are
            // each a single run their own style can wrap outright. A resolved mode of None short-circuits the whole
            // apparatus here rather than filtering escapes out later.
            var plotColored = mode != AnsiColorModeEnum.None &&
                              (Ramp != null || !LineStyle.IsEmpty || !AreaStyle.IsEmpty);

            // Blank plot grid, and — only when colored — a parallel grid of indices into a per-render style palette.
            // Indices rather than TextStyle values on purpose: a cell costs four bytes instead of a struct carrying
            // two nullable colors, deciding whether a neighbouring cell continues a run is an int compare instead of
            // a nullable-struct comparison, and each distinct style is asked for its escape sequence once per render
            // rather than once per run. -1 means "no style" — both the initial state and what an empty style interns
            // to — so the emitter has exactly one "nothing is open" test to make.
            var grid = new char[Height][];
            var styleGrid = plotColored ? new int[Height][] : null;
            for (var r = 0; r < Height; r++)
            {
                grid[r] = new char[Width];
                for (var c = 0; c < Width; c++)
                    grid[r][c] = ' ';

                if (styleGrid == null)
                    continue;

                styleGrid[r] = new int[Width];
                Array.Fill(styleGrid[r], -1);
            }

            var styleOpens = plotColored ? new List<string>() : null;
            var styleLookup = plotColored ? new Dictionary<TextStyle, int>() : null;
            var openLookup = plotColored ? new Dictionary<string, int>(StringComparer.Ordinal) : null;

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

                // Resolve this column's two styles once, before anything is drawn, because every cell the column
                // writes shares them. The style is written at the same instant as the character it belongs to —
                // the area fill below is overwritten by the connecting segment where they overlap, so a style grid
                // built from what was *intended* rather than what was *written* would color the wrong cells.
                var lineIndex = -1;
                var areaIndex = -1;
                if (styleGrid != null)
                {
                    var lineStyle = LineStyle;
                    var areaStyle = AreaStyle;
                    if (Ramp != null)
                    {
                        var color = new TextColor(Ramp.Sample(ValueFraction(value, min, max)));
                        lineStyle = lineStyle.WithForeground(color);
                        areaStyle = areaStyle.WithForeground(color);
                    }

                    lineIndex = InternStyle(lineStyle, mode, styleOpens, styleLookup, openLookup);

                    // Only interned when there is a fill to wear it. Interning unconditionally would add a dead
                    // palette entry, and an OpenSequence to build it, for every distinct ramp color on every render
                    // of a graph that draws no area at all.
                    if (Fill)
                        areaIndex = InternStyle(areaStyle, mode, styleOpens, styleLookup, openLookup);
                }

                if (Fill)
                    for (var r = row + 1; r < Height; r++)
                    {
                        grid[r][c] = AreaChar;
                        if (styleGrid != null)
                            styleGrid[r][c] = areaIndex;
                    }

                if (Connected && previousRow >= 0)
                {
                    var lo = Math.Min(previousRow, row);
                    var hi = Math.Max(previousRow, row);
                    for (var r = lo; r <= hi; r++)
                    {
                        grid[r][c] = PointChar;
                        if (styleGrid != null)
                            styleGrid[r][c] = lineIndex;
                    }
                }

                grid[row][c] = PointChar;
                if (styleGrid != null)
                    styleGrid[row][c] = lineIndex;

                previousRow = row;
            }

            return Compose(grid, styleGrid, styleOpens, min, max, mode);
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

        /// <summary>
        ///     Where a value sits between the scale's bounds — 0 at the minimum, 1 at the maximum, clamped into that
        ///     window. A degenerate range (every sample identical, or a pin that collapsed the scale) has no honest
        ///     answer, so it reports the middle: that is what has always put a flat series on the middle row, and it
        ///     is now also what hands <see cref="Ramp" /> a mid color rather than an arbitrary end of itself.
        /// </summary>
        private static double ValueFraction(double value, double min, double max)
        {
            var range = max - min;
            var fraction = range > 0d ? (value - min) / range : 0.5d;
            if (fraction < 0d) fraction = 0d;
            if (fraction > 1d) fraction = 1d;
            return fraction;
        }

        /// <summary>
        ///     Interns a style into this render's small style palette, returning the index the cell grid stores or -1
        ///     when the style would emit nothing at all. A style that resolves to nothing is cached as -1 as well, so
        ///     it is asked for an escape exactly once no matter how many columns share it.
        ///     <para>
        ///         <b>Two lookups, and the second one is the point.</b> Styles are interned by value, which is the
        ///         cheap test, but the palette itself is keyed on the <em>escape sequence</em> they resolve to — and
        ///         in the indexed modes those are many-to-one. <see cref="AnsiColorModeEnum.Grayscale" /> has 26
        ///         answers in total and <see cref="AnsiColorModeEnum.Palette256" /> only 256, so a smooth ramp across
        ///         sixty columns hands out dozens of distinct <see cref="Rgb24" /> values that quantize onto the same
        ///         <c>38;5;n</c>. Keyed by style alone those would each take their own palette slot, and
        ///         <see cref="AppendPlotRow" /> — which coalesces on the index — would close and reopen a run between
        ///         two neighbours the terminal cannot tell apart. Coalescing has to be decided by what actually
        ///         reaches the terminal, not by what the ramp was thinking.
        ///     </para>
        /// </summary>
        /// <param name="style">The style to intern.</param>
        /// <param name="mode">The already-resolved color mode.</param>
        /// <param name="opens">The palette of escape sequences, one per index.</param>
        /// <param name="lookup">Style-to-index cache, so a style is asked for its escape at most once per render.</param>
        /// <param name="openLookup">Escape-to-index map, so styles that quantize together share one palette entry.</param>
        private static int InternStyle(TextStyle style, AnsiColorModeEnum mode, List<string> opens,
            Dictionary<TextStyle, int> lookup, Dictionary<string, int> openLookup)
        {
            if (style.IsEmpty)
                return -1;

            if (lookup.TryGetValue(style, out var existing))
                return existing;

            var open = style.OpenSequence(mode);
            int index;
            if (open.Length == 0)
            {
                index = -1;
            }
            else if (openLookup.TryGetValue(open, out var shared))
            {
                index = shared;
            }
            else
            {
                index = opens.Count;
                opens.Add(open);
                openLookup[open] = index;
            }

            lookup[style] = index;
            return index;
        }

        /// <summary>Maps a value to a grid row (row 0 is the top / maximum, the last row is the bottom / minimum).</summary>
        private int ValueToRow(double value, double min, double max)
        {
            var row = (int) Math.Round((1d - ValueFraction(value, min, max)) * (Height - 1),
                MidpointRounding.AwayFromZero);
            return Math.Clamp(row, 0, Height - 1);
        }

        /// <summary>
        ///     Turns the plotted grid into the final text, adding the scale gutter and axis lines if enabled, and
        ///     wrapping each part in its own style. Every style opened here is closed before the row is handed to the
        ///     newline join — a style that survived a line break would be worn by the rest of the frame.
        /// </summary>
        /// <param name="grid">The plotted characters, row by row.</param>
        /// <param name="styleGrid">Palette indices matching <paramref name="grid" /> cell for cell, or null when the plot is uncolored.</param>
        /// <param name="styleOpens">The escape sequence for each palette index, or null when the plot is uncolored.</param>
        /// <param name="min">The low end of the value scale, for the bottom gutter label.</param>
        /// <param name="max">The high end of the value scale, for the top gutter label.</param>
        /// <param name="mode">The already-resolved color mode.</param>
        private string Compose(char[][] grid, int[][] styleGrid, List<string> styleOpens, double min, double max,
            AnsiColorModeEnum mode)
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

                    // Pad first, style second: the padding is gutter, not label. Splitting the old PadLeft this way
                    // is exact rather than merely equivalent — the label can never be wider than the gutter it was
                    // measured for, and Apply hands an empty style (or an empty label) its input straight back.
                    sb.Append(' ', Math.Max(0, gutterWidth - label.Length));
                    sb.Append(ScaleStyle.Apply(label, mode));
                }

                if (ShowAxis)
                    sb.Append(AxisStyle.Apply(AxisVertical, mode));

                AppendPlotRow(sb, grid[r], styleGrid?[r], styleOpens);
                lines.Add(sb.ToString());
            }

            if (ShowAxis)
            {
                var axis = new StringBuilder();
                if (ShowScale)
                    axis.Append(' ', gutterWidth);

                // Corner and rule go out as one run: they read as a single line, and coalescing them is the
                // difference between one escape and two on a row that has nothing else to say.
                var rule = new StringBuilder(Width + 1).Append(AxisCorner).Append(AxisHorizontal, Width).ToString();
                axis.Append(AxisStyle.Apply(rule, mode));
                lines.Add(axis.ToString());
            }

            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        ///     Writes one row of the plot area, coalescing neighbouring cells that share a style into a single escape
        ///     run and closing whatever it opened before the row ends. A run is opened only where the style actually
        ///     changes, which is what keeps a rainbow row down to a few dozen bytes instead of wrapping every cell.
        /// </summary>
        /// <param name="sb">The row being built.</param>
        /// <param name="row">The characters to write.</param>
        /// <param name="styles">Palette indices for those characters, or null when the plot is uncolored.</param>
        /// <param name="opens">The escape sequence for each palette index.</param>
        private static void AppendPlotRow(StringBuilder sb, char[] row, int[] styles, List<string> opens)
        {
            if (styles == null)
            {
                // The uncolored path, untouched: the whole row appended in one call, blank cells and all.
                sb.Append(row);
                return;
            }

            var open = -1;
            for (var c = 0; c < row.Length; c++)
            {
                var index = styles[c];
                if (index != open)
                {
                    if (open >= 0)
                        sb.Append(TextStyle.ResetSequence);

                    open = index;
                    if (open >= 0)
                        sb.Append(opens[open]);
                }

                sb.Append(row[c]);
            }

            if (open >= 0)
                sb.Append(TextStyle.ResetSequence);
        }
    }
}
