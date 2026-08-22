// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;

namespace WolfCurses.Apps.Planner
{
    /// <summary>
    ///     One line of a list: an entry, and the day it falls on.
    ///     <para>
    ///         An entry does not carry the day it is <i>occurring</i> on, because an annual one occurs on a great
    ///         many of them. Pairing the two is what a list needs and what a single day's panel does not, which is
    ///         why this exists rather than the list working with entries alone.
    ///     </para>
    /// </summary>
    public readonly struct PlannerEntryLine
    {
        /// <summary>Initializes a new instance of the <see cref="PlannerEntryLine" /> struct.</summary>
        /// <param name="date">The day it falls on.</param>
        /// <param name="entry">The entry.</param>
        public PlannerEntryLine(DateOnly date, PlannerEvent entry)
        {
            Date = date;
            Entry = entry;
        }

        /// <summary>The day it falls on.</summary>
        public DateOnly Date { get; }

        /// <summary>The entry.</summary>
        public PlannerEvent Entry { get; }
    }
}
