// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

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
}
