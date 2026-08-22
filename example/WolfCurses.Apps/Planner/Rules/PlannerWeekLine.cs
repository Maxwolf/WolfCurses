// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;

namespace WolfCurses.Apps.Planner
{
    /// <summary>
    ///     One line of the week view: either a day's heading, or one of the things on that day.
    ///     <para>
    ///         Both kinds carry the date, which is the point of the type: a click anywhere in a day's block, on its
    ///         heading or on any of its entries, has to choose that day, and a heading-only list could not say
    ///         which day an entry three rows down belonged to.
    ///     </para>
    /// </summary>
    public readonly struct PlannerWeekLine
    {
        /// <summary>Initializes a new instance of the <see cref="PlannerWeekLine" /> struct.</summary>
        /// <param name="date">The day this line belongs to.</param>
        /// <param name="entry">The entry, or null when this line is the day's heading.</param>
        public PlannerWeekLine(DateOnly date, PlannerEvent entry)
        {
            Date = date;
            Entry = entry;
        }

        /// <summary>The day this line belongs to.</summary>
        public DateOnly Date { get; }

        /// <summary>The entry, or null when this line is the day's heading.</summary>
        public PlannerEvent Entry { get; }
    }
}
