// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/22/2026

using System;
using System.Collections.Generic;

namespace WolfCurses.Apps.Planner
{
    /// <summary>
    ///     The list of what is coming: which lines it holds, and which day the cursor moves to next. No console
    ///     anywhere in here, like the rest of the rules.
    ///     <para>
    ///         The stepping lives here rather than in the dialog because it is the half that cannot be checked
    ///         from the screen. Two entries on the same day are two lines and one day, so a step that lands on the
    ///         second of them looks exactly like a step that landed on the first, and the bug this fixes hid
    ///         behind precisely that for as long as the sample had two things on the day after today.
    ///     </para>
    /// </summary>
    public static class PlannerAgenda
    {
        /// <summary>
        ///     Everything happening from a day onwards, in date order.
        ///     <para>
        ///         Walking a year of days is not a thing to do while drawing, so the caller does this when
        ///         something changes and hands the lines to the view.
        ///     </para>
        /// </summary>
        /// <param name="diary">The planner.</param>
        /// <param name="from">The day the list starts on.</param>
        /// <param name="days">How many days it covers.</param>
        /// <returns>The lines.</returns>
        public static IReadOnlyList<PlannerEntryLine> Build(PlannerDiary diary, DateOnly from, int days)
        {
            var lines = new List<PlannerEntryLine>();

            if (diary == null)
                return lines;

            for (var offset = 0; offset < days; offset++)
            {
                // The end of time is a day like any other to walk up to and not one to walk past.
                if (from.DayNumber + offset > DateOnly.MaxValue.DayNumber)
                    break;

                var date = from.AddDays(offset);

                foreach (var entry in diary.On(date))
                    lines.Add(new PlannerEntryLine(date, entry));
            }

            return lines;
        }

        /// <summary>
        ///     The day the cursor moves to, or null when there is nothing that way.
        ///     <para>
        ///         <b>The neighbour is found by date rather than by index, which is the whole of getting this
        ///         right.</b> Most days have nothing on them, so the day the cursor is on is usually not in the
        ///         list at all and there is no index to step from. Treating "not in the list" as index zero is
        ///         what steps over the first thing coming up, and the skip hides itself whenever that first day
        ///         happens to have a second entry on it, since both lines say the same date.
        ///     </para>
        ///     <para>
        ///         Asking for the neighbouring <i>date</i> settles the ordinary case for free as well: a day with
        ///         three things on it is stepped over rather than stepped through three times.
        ///     </para>
        ///     <para>
        ///         <b>Both ends are walls.</b> There is nothing above the day the list starts from, so backing off
        ///         the top stays put rather than jumping forward to the first entry.
        ///     </para>
        /// </summary>
        /// <param name="agenda">The lines, in date order.</param>
        /// <param name="selected">The day the cursor is on, which need not be one the list holds.</param>
        /// <param name="step">Which way; only the sign is read, and negative goes back.</param>
        /// <returns>The day to move to, or null to stay where it is.</returns>
        public static DateOnly? Step(IReadOnlyList<PlannerEntryLine> agenda, DateOnly selected, int step)
        {
            if (agenda == null || step == 0)
                return null;

            if (step > 0)
            {
                for (var i = 0; i < agenda.Count; i++)
                {
                    if (agenda[i].Date > selected)
                        return agenda[i].Date;
                }

                return null;
            }

            for (var i = agenda.Count - 1; i >= 0; i--)
            {
                if (agenda[i].Date < selected)
                    return agenda[i].Date;
            }

            return null;
        }
    }
}
