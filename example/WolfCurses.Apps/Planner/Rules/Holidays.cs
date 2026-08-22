// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;

namespace WolfCurses.Apps.Planner
{
    /// <summary>
    ///     The holidays, worked out for whichever year is being looked at rather than typed into a file.
    ///     <para>
    ///         <b>Computed, so the planner still knows them in 2041.</b> A list of dates would be right for one
    ///         year and quietly wrong for every year after it, and a calendar you can page through is a calendar
    ///         somebody will page past the end of the list. It also means there is nothing to store, which is why
    ///         a holiday cannot be deleted: next year's would come back anyway.
    ///     </para>
    ///     <para>
    ///         Four shapes of rule, which is the whole of what makes this worth writing rather than looking up: a
    ///         fixed date, the n-th weekday of a month, the last weekday of a month, and Easter, which is none of
    ///         those and moves the four days that hang off it.
    ///     </para>
    /// </summary>
    internal static class Holidays
    {
        /// <summary>Worked out years, so paging back and forth does not redo the arithmetic every frame.</summary>
        private static readonly Dictionary<int, IReadOnlyList<PlannerEvent>> _cache = new();

        /// <summary>Every holiday in a year, in date order.</summary>
        /// <param name="year">The year.</param>
        /// <returns>The holidays.</returns>
        public static IReadOnlyList<PlannerEvent> For(int year)
        {
            if (_cache.TryGetValue(year, out var cached))
                return cached;

            var found = Compute(year);
            _cache[year] = found;

            return found;
        }

        /// <summary>Works out a year's holidays.</summary>
        /// <param name="year">The year.</param>
        /// <returns>The holidays, in date order.</returns>
        private static IReadOnlyList<PlannerEvent> Compute(int year)
        {
            var easter = Easter(year);

            var found = new List<PlannerEvent>
            {
                Fixed(year, 1, 1, "New Year's Day"),
                Fixed(year, 2, 14, "Valentine's Day"),
                Fixed(year, 3, 17, "St Patrick's Day"),
                Fixed(year, 4, 1, "April Fools' Day"),
                Fixed(year, 7, 4, "Independence Day"),
                Fixed(year, 10, 31, "Halloween"),
                Fixed(year, 12, 24, "Christmas Eve"),
                Fixed(year, 12, 25, "Christmas Day"),
                Fixed(year, 12, 31, "New Year's Eve"),

                // The moveable feast and the days that hang off it, which is why Easter is worth computing rather
                // than the four of them being listed separately.
                On(easter.AddDays(-2), "Good Friday"),
                On(easter, "Easter Sunday"),
                On(easter.AddDays(1), "Easter Monday"),

                On(NthWeekday(year, 5, DayOfWeek.Sunday, 2), "Mother's Day"),
                On(NthWeekday(year, 6, DayOfWeek.Sunday, 3), "Father's Day"),
                On(NthWeekday(year, 9, DayOfWeek.Monday, 1), "Labor Day"),
                On(NthWeekday(year, 11, DayOfWeek.Thursday, 4), "Thanksgiving"),
                On(LastWeekday(year, 5, DayOfWeek.Monday), "Memorial Day")
            };

            found.Sort((left, right) =>
            {
                var byMonth = left.Month.CompareTo(right.Month);
                return byMonth != 0 ? byMonth : left.Day.CompareTo(right.Day);
            });

            return found;
        }

        /// <summary>
        ///     Easter Sunday, by the anonymous Gregorian algorithm.
        ///     <para>
        ///         Nominally the first Sunday after the first full moon on or after the equinox, except that every
        ///         term in that sentence is an ecclesiastical approximation rather than the astronomical thing it
        ///         is named after. There is no shorter honest way to write it, and the intermediate values have no
        ///         meaning worth naming them for, which is why they are letters here exactly as they are in every
        ///         published statement of it.
        ///     </para>
        /// </summary>
        /// <param name="year">The year.</param>
        /// <returns>Easter Sunday.</returns>
        public static DateOnly Easter(int year)
        {
            var a = year % 19;
            var b = year / 100;
            var c = year % 100;
            var d = b / 4;
            var e = b % 4;
            var f = (b + 8) / 25;
            var g = (b - f + 1) / 3;
            var h = (19 * a + b - d - g + 15) % 30;
            var i = c / 4;
            var k = c % 4;
            var l = (32 + 2 * e + 2 * i - h - k) % 7;
            var m = (a + 11 * h + 22 * l) / 451;
            var n = h + l - 7 * m + 114;

            return new DateOnly(year, n / 31, n % 31 + 1);
        }

        /// <summary>
        ///     The n-th given weekday of a month: the fourth Thursday of November, the second Sunday of May.
        /// </summary>
        /// <param name="year">The year.</param>
        /// <param name="month">The month.</param>
        /// <param name="day">Which weekday.</param>
        /// <param name="n">Which one of them, counting from one.</param>
        /// <returns>That day.</returns>
        public static DateOnly NthWeekday(int year, int month, DayOfWeek day, int n)
        {
            var first = new DateOnly(year, month, 1);
            var forward = ((int) day - (int) first.DayOfWeek + 7) % 7;

            var found = first.AddDays(forward + (Math.Max(1, n) - 1) * 7);

            // Asking for a fifth Thursday of a month that has four would otherwise walk into the next month and
            // report a date nobody meant. The last one it has is the honest answer.
            return found.Month == month ? found : found.AddDays(-7);
        }

        /// <summary>The last given weekday of a month, which is what Memorial Day is defined as.</summary>
        /// <param name="year">The year.</param>
        /// <param name="month">The month.</param>
        /// <param name="day">Which weekday.</param>
        /// <returns>That day.</returns>
        public static DateOnly LastWeekday(int year, int month, DayOfWeek day)
        {
            var last = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
            var back = ((int) last.DayOfWeek - (int) day + 7) % 7;

            return last.AddDays(-back);
        }

        /// <summary>A holiday on a fixed date.</summary>
        /// <param name="year">The year.</param>
        /// <param name="month">The month.</param>
        /// <param name="day">The day.</param>
        /// <param name="title">What it is called.</param>
        /// <returns>The holiday.</returns>
        private static PlannerEvent Fixed(int year, int month, int day, string title)
        {
            return new PlannerEvent(year, month, day, string.Empty, title, PlannerEventKindEnum.Holiday);
        }

        /// <summary>A holiday on a worked-out date.</summary>
        /// <param name="date">The date.</param>
        /// <param name="title">What it is called.</param>
        /// <returns>The holiday.</returns>
        private static PlannerEvent On(DateOnly date, string title)
        {
            return new PlannerEvent(date, string.Empty, title, PlannerEventKindEnum.Holiday);
        }
    }
}
