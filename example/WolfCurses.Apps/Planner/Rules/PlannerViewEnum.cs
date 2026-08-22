// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

namespace WolfCurses.Apps.Planner
{
    /// <summary>
    ///     How far back the planner is standing from the year.
    ///     <para>
    ///         The four are a zoom, and they are deliberately not four ways of drawing the same thing: each answers
    ///         a question the others cannot. A month says which day of the week something falls on, a week says
    ///         what a working week actually contains, a year says which parts of it are busy, and a list says what
    ///         is coming regardless of how far away it is.
    ///     </para>
    /// </summary>
    public enum PlannerViewEnum
    {
        /// <summary>A month as a calendar, with the chosen day's entries beside it.</summary>
        Month = 0,

        /// <summary>Seven days in a row, each with everything on it written out.</summary>
        Week = 1,

        /// <summary>A whole year as twelve strips of days, marked where something happens.</summary>
        Year = 2,

        /// <summary>Everything from the chosen day onwards, in order, however far ahead it is.</summary>
        Agenda = 3
    }
}
