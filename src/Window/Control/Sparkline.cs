// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System.Collections.Generic;
using System.Text;

namespace WolfCurses.Window.Control
{
    /// <summary>
    ///     Renders a compact, single-line "sparkline" — a whole series of numbers drawn as one string of block glyphs
    ///     of varying height (<c>▁▂▃▄▅▆▇█</c>), so a trend fits inline next to a label. Each value is mapped onto the
    ///     glyph ramp between the series minimum and maximum (or a fixed range you pin with <see cref="Minimum" /> /
    ///     <see cref="Maximum" />). A flat series, or one value, renders as the lowest glyph.
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
            foreach (var value in series)
            {
                int level;
                if (double.IsNaN(value) || double.IsInfinity(value) || range <= 0d)
                {
                    // Non-finite or a flat/degenerate range has no meaningful height: draw the lowest glyph.
                    level = 0;
                }
                else
                {
                    var fraction = (value - min) / range;
                    if (fraction < 0d) fraction = 0d;
                    if (fraction > 1d) fraction = 1d;

                    level = (int) System.Math.Round(fraction * lastLevel, System.MidpointRounding.AwayFromZero);
                    if (level < 0) level = 0;
                    if (level > lastLevel) level = lastLevel;
                }

                sb.Append(ramp[level]);
            }

            return sb.ToString();
        }
    }
}
