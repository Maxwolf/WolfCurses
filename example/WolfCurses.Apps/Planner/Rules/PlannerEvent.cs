// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Globalization;

namespace WolfCurses.Apps.Planner
{
    /// <summary>
    ///     One thing in the planner: a day it happens on, an optional time, and what it is.
    ///     <para>
    ///         <b>A year of zero means every year.</b> That one convention is what lets a birthday, an anniversary
    ///         and the shipped sample data go on meaning something in 2030, and it is why the date is kept as three
    ///         numbers rather than as a <see cref="DateOnly" />: a date is a point, and half of what a planner
    ///         holds is not a point but a day of the year.
    ///     </para>
    /// </summary>
    public sealed class PlannerEvent
    {
        /// <summary>The year of an entry that comes round every year.</summary>
        public const int EveryYear = 0;

        /// <summary>Initializes a new instance of the <see cref="PlannerEvent" /> class.</summary>
        /// <param name="year">The year, or <see cref="EveryYear" /> for one that comes round annually.</param>
        /// <param name="month">The month, one to twelve.</param>
        /// <param name="day">The day of that month.</param>
        /// <param name="time">When, as text; empty for something that takes the whole day.</param>
        /// <param name="title">What it is.</param>
        /// <param name="kind">Where it came from.</param>
        public PlannerEvent(int year, int month, int day, string time, string title,
            PlannerEventKindEnum kind = PlannerEventKindEnum.Personal)
        {
            Year = Math.Max(EveryYear, year);
            Month = Math.Clamp(month, 1, 12);
            Day = Math.Clamp(day, 1, 31);
            Time = (time ?? string.Empty).Trim();
            Title = (title ?? string.Empty).Trim();
            Kind = kind;
        }

        /// <summary>Initializes an entry on a particular date.</summary>
        /// <param name="date">The date.</param>
        /// <param name="time">When, as text.</param>
        /// <param name="title">What it is.</param>
        /// <param name="kind">Where it came from.</param>
        public PlannerEvent(DateOnly date, string time, string title,
            PlannerEventKindEnum kind = PlannerEventKindEnum.Personal)
            : this(date.Year, date.Month, date.Day, time, title, kind)
        {
        }

        /// <summary>The year, or <see cref="EveryYear" />.</summary>
        public int Year { get; }

        /// <summary>The month, one to twelve.</summary>
        public int Month { get; }

        /// <summary>The day of that month.</summary>
        public int Day { get; }

        /// <summary>When, as text; empty for something that takes the whole day.</summary>
        public string Time { get; }

        /// <summary>What it is.</summary>
        public string Title { get; }

        /// <summary>Where it came from, which decides whether it can be removed.</summary>
        public PlannerEventKindEnum Kind { get; }

        /// <summary>Whether it comes round every year.</summary>
        public bool IsAnnual => Year == EveryYear;

        /// <summary>
        ///     Whether this entry falls on a date.
        ///     <para>
        ///         The twenty-ninth of February is the one that needs saying: an annual entry on a leap day happens
        ///         only in leap years, because there is no other day it could honestly be moved to and quietly
        ///         picking one would be the program inventing an anniversary.
        ///     </para>
        /// </summary>
        /// <param name="date">The date to test.</param>
        /// <returns>TRUE when it happens that day.</returns>
        public bool FallsOn(DateOnly date)
        {
            if (date.Month != Month || date.Day != Day)
                return false;

            return IsAnnual || date.Year == Year;
        }

        /// <summary>
        ///     How the date is written in the file: <c>MM-DD</c> for an annual entry and <c>YYYY-MM-DD</c> for one
        ///     with a year on it, which is the shortest thing that can be read back without a second column.
        /// </summary>
        /// <returns>The date, written out.</returns>
        public string DateText()
        {
            var monthAndDay = Month.ToString("00", CultureInfo.InvariantCulture) + "-" +
                              Day.ToString("00", CultureInfo.InvariantCulture);

            return IsAnnual
                ? monthAndDay
                : Year.ToString("0000", CultureInfo.InvariantCulture) + "-" + monthAndDay;
        }

        /// <summary>Reads a date written the way <see cref="DateText" /> writes it.</summary>
        /// <param name="text">Either <c>MM-DD</c> or <c>YYYY-MM-DD</c>.</param>
        /// <param name="year">The year, or <see cref="EveryYear" />.</param>
        /// <param name="month">The month.</param>
        /// <param name="day">The day.</param>
        /// <returns>TRUE when the text really was a date.</returns>
        public static bool TryParseDate(string text, out int year, out int month, out int day)
        {
            year = EveryYear;
            month = 0;
            day = 0;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            var parts = text.Trim().Split('-');

            if (parts.Length == 3)
            {
                if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out year))
                    return false;

                parts = new[] {parts[1], parts[2]};
            }
            else if (parts.Length != 2)
            {
                return false;
            }

            if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out month) ||
                !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out day))
                return false;

            return month is >= 1 and <= 12 && day is >= 1 and <= 31;
        }

        /// <summary>How the entry reads on one line, for a list or a status line.</summary>
        /// <returns>The entry, written out.</returns>
        public override string ToString()
        {
            return string.IsNullOrEmpty(Time) ? Title : Time + "  " + Title;
        }
    }
}
