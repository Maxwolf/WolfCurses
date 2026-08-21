// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

namespace WolfCurses.Apps.Spreadsheet
{
    /// <summary>
    ///     Which picture to draw of a selection.
    ///     <para>
    ///         Two, because they answer different questions and the library already draws both. A bar chart
    ///         compares things that are not in any particular order; a line graph shows a thing changing along one,
    ///         and drawing months as bars or categories as a line is the commonest way to make a chart say
    ///         something untrue.
    ///     </para>
    /// </summary>
    public enum SheetChartKindEnum
    {
        /// <summary>Labelled horizontal bars, one per value.</summary>
        Bars = 0,

        /// <summary>A line across the values in order, which only means anything when the order does.</summary>
        Line = 1
    }
}
