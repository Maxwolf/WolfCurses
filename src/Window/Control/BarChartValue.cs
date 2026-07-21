// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using WolfCurses.Graphics;

namespace WolfCurses.Window.Control
{
    /// <summary>
    ///     One labelled datum in a <see cref="BarChart" />: a category name, its numeric magnitude, and optionally the
    ///     exact color its bar should be drawn in.
    /// </summary>
    public readonly struct BarChartValue
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="BarChartValue" /> struct with no style of its own, so its
        ///     bar takes whatever the chart decides — a <see cref="BarChart.Ramp" /> color if one is set, otherwise
        ///     <see cref="BarChart.BarStyle" />.
        /// </summary>
        /// <param name="label">The category label shown to the left of the bar.</param>
        /// <param name="value">The magnitude of the bar.</param>
        public BarChartValue(string label, double value)
        {
            Label = label;
            Value = value;
            Style = null;
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="BarChartValue" /> struct that carries its own bar style.
        ///     <para>
        ///         This is the escape hatch for the case a ramp cannot express: a chart where the colors mean
        ///         something categorical rather than positional — this series is the error count and it is red, that
        ///         one is the cache and it is blue — where the answer belongs to the datum and not to its row number.
        ///         A style given here beats both the ramp and <see cref="BarChart.BarStyle" />, on the reasoning that
        ///         the more specific instruction wins.
        ///     </para>
        ///     <para>
        ///         Because <see cref="TextStyle" /> converts implicitly from <see cref="System.ConsoleColor" /> and
        ///         <see cref="Rgb24" />, and a plain value converts on to its own nullable form for free, this reads
        ///         as <c>new BarChartValue("Errors", 12, System.ConsoleColor.Red)</c> with no ceremony.
        ///     </para>
        /// </summary>
        /// <param name="label">The category label shown to the left of the bar.</param>
        /// <param name="value">The magnitude of the bar.</param>
        /// <param name="style">The style for this item's bar, or null to let the chart decide.</param>
        public BarChartValue(string label, double value, TextStyle? style)
        {
            Label = label;
            Value = value;
            Style = style;
        }

        /// <summary>The category label shown to the left of the bar.</summary>
        public string Label { get; }

        /// <summary>The magnitude of the bar.</summary>
        public double Value { get; }

        /// <summary>
        ///     The style for this item's bar, or null to let the chart decide. Null — the value the two-argument
        ///     constructor leaves behind — is what keeps every existing call site drawing exactly as it did.
        /// </summary>
        public TextStyle? Style { get; }
    }
}
