// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/20/2026

namespace WolfCurses.Graphics
{
    /// <summary>
    ///     What a widget's <see cref="ColorRamp" /> is being asked to mean. The same ramp over the same data draws
    ///     two completely different pictures depending on this, and both are wanted often enough that neither could
    ///     be the only behaviour.
    /// </summary>
    public enum ColorRampModeEnum
    {
        /// <summary>
        ///     The color varies <em>across</em> the drawn extent: cell by cell along a progress bar, row by row down
        ///     a chart. The datum's value decides how much is drawn, never what color it is. This is what makes a
        ///     rainbow bar, and — with a stepped ramp sampled once per row — what makes a flag out of a bar chart.
        ///     The default, because it is the decorative reading and a ramp set on a widget that carries no scale
        ///     has nothing else it could mean.
        /// </summary>
        Spread = 0,

        /// <summary>
        ///     One color for the whole drawn extent, chosen by the datum's value relative to the scale. This is the
        ///     informative reading: a traffic-light bar that goes green to amber to red as it fills, a heat ramp
        ///     where a taller bar is a hotter bar. The color is redundant with the length, which is the point —
        ///     redundancy is what makes a dashboard readable at a glance.
        /// </summary>
        Level = 1
    }
}
