// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System.Collections.Generic;
using System.Text;
using WolfCurses.Graphics;

namespace WolfCurses.Window.Control
{
    /// <summary>
    ///     Renders a compact, single-line "sparkline" — a whole series of numbers drawn as one string of block glyphs
    ///     of varying height (<c>▁▂▃▄▅▆▇█</c>), so a trend fits inline next to a label. Each value is mapped onto the
    ///     glyph ramp between the series minimum and maximum (or a fixed range you pin with <see cref="Minimum" /> /
    ///     <see cref="Maximum" />). A flat series, or one value, renders as the lowest glyph.
    ///     <para>
    ///         Color is optional and entirely opt-in. <see cref="Style" /> paints the whole line one way;
    ///         <see cref="SparklineColorRamp" /> instead paints every glyph by its own value, so the line warms up as it
    ///         climbs. Leave both alone and not one escape byte is emitted — the string is exactly what this class
    ///         produced before it knew what a color was.
    ///     </para>
    /// </summary>
    /// <example>
    ///     <code>
    ///     var spark = new Sparkline();
    ///     string line = spark.Render(new double[] { 1, 5, 2, 8, 3, 7, 4 }); // ▁▅▂█▃▇▄
    ///     </code>
    /// </example>
    public sealed class Sparkline
    {
        /// <summary>The default low-to-high glyph ramp (eight Unicode block-element heights).</summary>
        public const string DefaultRamp = "▁▂▃▄▅▆▇█";

        /// <summary>
        ///     The low-to-high glyphs a value can be mapped to, ordered from smallest to largest. An empty or null ramp
        ///     falls back to <see cref="DefaultRamp" />. A string keeps the ramp immutable so instances cannot corrupt
        ///     each other's.
        /// </summary>
        public string Ramp { get; set; } = DefaultRamp;

        /// <summary>Pin the low end of the scale; when null the series minimum is used.</summary>
        public double? Minimum { get; set; }

        /// <summary>Pin the high end of the scale; when null the series maximum is used.</summary>
        public double? Maximum { get; set; }

        /// <summary>
        ///     Which escape sequences the colors below are allowed to use.
        ///     <see cref="AnsiColorModeEnum.Auto" /> — the default — asks the environment through
        ///     <see cref="AnsiConsole.DetectColorMode" />, and <see cref="AnsiColorModeEnum.None" /> suppresses every
        ///     escape this class could emit, however loudly <see cref="Style" /> and <see cref="SparklineColorRamp" /> were set.
        ///     <para>
        ///         It is a per-instance property rather than a global because that is what lets a test (or a host
        ///         rendering into something other than the terminal it is running on) pin a concrete mode without
        ///         reaching for <c>NO_COLOR</c> and the process-wide detection cache, which every other widget in the
        ///         process is reading at the same time.
        ///     </para>
        /// </summary>
        public AnsiColorModeEnum ColorMode { get; set; } = AnsiColorModeEnum.Auto;

        /// <summary>
        ///     How the line as a whole is painted. With no <see cref="SparklineColorRamp" /> set the finished string is wrapped
        ///     in this style exactly once — one open, one reset, nothing per glyph. With a ramp set the ramp takes over
        ///     the foreground and whatever else is here (a background, bold) still rides along on every glyph, so a
        ///     value-colored sparkline can still sit on a highlighted row.
        ///     <para>
        ///         The default is <see cref="TextStyle.None" />, which is not "black" or "the terminal default" but
        ///         genuinely nothing at all: an empty style is the identity function on the text, so the uncolored
        ///         output is byte-identical to what this class produced before color existed.
        ///     </para>
        /// </summary>
        public TextStyle Style { get; set; }

        /// <summary>
        ///     An optional color ramp that paints each glyph by that sample's position between the low and high ends
        ///     of the scale — the same fraction the glyph's own height is chosen from, so color and height always
        ///     agree. Null (the default) leaves the line a single flat <see cref="Style" />.
        ///     <para>
        ///         Samples with nothing meaningful to say about height — a NaN or an infinity, or any sample at all
        ///         when the range is flat — already draw as the lowest glyph, and they take the ramp's first color
        ///         (<c>Sample(0)</c>) for the same reason. Deciding otherwise would color a glyph as something its
        ///         height is not.
        ///     </para>
        ///     <para>
        ///         Named <see cref="SparklineColorRamp" /> rather than <c>Ramp</c> (which every other widget uses)
        ///         because <see cref="Ramp" /> was already taken, years earlier, by the string of <em>glyphs</em>.
        ///         Renaming that one to free up <c>Ramp</c> would break every existing caller and the pinned tests,
        ///         so this one carries the longer name. It is spelled out in full rather than left as bare
        ///         <c>ColorRamp</c> so that reading <c>spark.SparklineColorRamp</c> can never be mistaken for the
        ///         <see cref="WolfCurses.Graphics.ColorRamp" /> type itself.
        ///     </para>
        /// </summary>
        public ColorRamp SparklineColorRamp { get; set; }

        /// <summary>
        ///     Builds the sparkline string. An empty (or null) series yields an empty string. Non-finite values (NaN /
        ///     infinity) are drawn as the lowest glyph and are ignored when working out the automatic range.
        /// </summary>
        /// <param name="values">The series to plot, left to right.</param>
        public string Render(IEnumerable<double> values)
        {
            if (values == null)
                return string.Empty;

            var series = new List<double>(values);
            if (series.Count == 0)
                return string.Empty;

            var ramp = string.IsNullOrEmpty(Ramp) ? DefaultRamp : Ramp;
            var lastLevel = ramp.Length - 1;

            // Resolve the color mode once for the whole line rather than per glyph. Resolving here also lets a mode
            // of None drop the color ramp on the floor before the loop, so "no color" costs nothing per glyph and
            // walks exactly the same code the class walked before it had colors.
            //
            // The anyStyle gate matters beyond the saved call: an untouched sparkline must never consult the
            // environment at all, the same property ProgressBar and LineGraph state in their own docs. Auto is safe
            // to leave unresolved here because everything downstream of it is skipped when nothing is styled, and
            // TextStyle.Apply resolves Auto for itself anyway.
            var style = Style;
            var anyStyle = SparklineColorRamp != null || !style.IsEmpty;
            var mode = anyStyle && ColorMode == AnsiColorModeEnum.Auto
                ? AnsiConsole.DetectColorMode()
                : ColorMode;
            var colors = mode == AnsiColorModeEnum.None ? null : SparklineColorRamp;

            // Work out the range from the finite samples only, unless the caller pinned it.
            var haveFinite = false;
            var dataMin = 0d;
            var dataMax = 0d;
            foreach (var value in series)
            {
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
            var range = max - min;

            var sb = new StringBuilder(series.Count);

            // The escape currently in effect (empty means nothing is open) and the style that produced it. Adjacent
            // glyphs that want the same color share one escape: a rising series repeats colors constantly, and
            // re-wrapping every single glyph would spend a few hundred bytes saying the same thing over and over.
            //
            // Two keys rather than one, because they answer different questions. The style compare is the cheap test
            // that skips the work entirely; the escape compare is the *correct* one, since in the indexed modes many
            // distinct ramp colors quantize onto the same sequence — Grayscale has 26 answers in all — and breaking
            // a run between two neighbours the terminal renders identically spends a reset and an open to change
            // nothing. What counts as "the same color" has to be decided by what reaches the terminal.
            var openStyle = TextStyle.None;
            var haveStyle = false;
            var openSequence = string.Empty;

            foreach (var value in series)
            {
                int level;
                var fraction = 0d;
                if (double.IsNaN(value) || double.IsInfinity(value) || range <= 0d)
                {
                    // Non-finite or a flat/degenerate range has no meaningful height: draw the lowest glyph. The
                    // fraction stays at zero, so such a sample also takes the ramp's first color — the only answer
                    // that agrees with the glyph it is already being drawn as.
                    level = 0;
                }
                else
                {
                    fraction = (value - min) / range;
                    if (fraction < 0d) fraction = 0d;
                    if (fraction > 1d) fraction = 1d;

                    level = (int) System.Math.Round(fraction * lastLevel, System.MidpointRounding.AwayFromZero);
                    if (level < 0) level = 0;
                    if (level > lastLevel) level = lastLevel;
                }

                if (colors != null)
                {
                    // The ramp owns the foreground and the caller's style keeps everything else it asked for.
                    var cellStyle = style.WithForeground(new TextColor(colors.Sample(fraction)));
                    if (!haveStyle || cellStyle != openStyle)
                    {
                        var open = cellStyle.OpenSequence(mode);
                        if (!string.Equals(open, openSequence, System.StringComparison.Ordinal))
                        {
                            if (openSequence.Length > 0)
                                sb.Append(TextStyle.ResetSequence);

                            sb.Append(open);
                            openSequence = open;
                        }

                        openStyle = cellStyle;
                        haveStyle = true;
                    }
                }

                sb.Append(ramp[level]);
            }

            // Never a reset unless something was opened — a stray reset is still an escape sequence, and this class
            // promises none at all when it was not colored.
            if (openSequence.Length > 0)
                sb.Append(TextStyle.ResetSequence);

            var line = sb.ToString();

            // Without a per-glyph ramp the whole line takes one style, and for the default empty style Apply is the
            // identity: it hands the very same string straight back with nothing wrapped around it.
            return colors == null ? style.Apply(line, mode) : line;
        }
    }
}
