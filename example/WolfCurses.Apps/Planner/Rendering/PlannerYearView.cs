// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;
using System.Globalization;
using WolfCurses.Graphics;
using WolfCurses.Window.Control;

namespace WolfCurses.Apps.Planner
{
    /// <summary>
    ///     The year: twelve strips of days, one row per month, marked wherever something happens. The view that
    ///     answers "which parts of the year are busy", which is a question no calendar showing one month at a time
    ///     can be asked.
    ///     <para>
    ///         <b>A strip rather than twelve little calendars</b>, because twelve of those need thirty-two rows and
    ///         a terminal has twenty-four. A strip gives up which weekday a date falls on, which is exactly the
    ///         thing the month view is for, and buys the whole year at once, which is exactly the thing it is not.
    ///     </para>
    /// </summary>
    internal static class PlannerYearView
    {
        /// <summary>The longest a month gets, which is how wide every strip is drawn.</summary>
        private const int LongestMonth = 31;

        /// <summary>How many columns the month's name and the space after it take.</summary>
        private const int LabelWidth = 6;

        /// <summary>What is drawn against a day with nothing on it.</summary>
        private const char Quiet = '·';

        /// <summary>What is drawn against a day with something on it.</summary>
        private const char Busy = '●';

        /// <summary>The screen column the first day of every strip is drawn in.</summary>
        public const int FirstDayColumn = 1 + LabelWidth;

        /// <summary>Draws the view.</summary>
        /// <param name="diary">The planner.</param>
        /// <param name="selected">The day the cursor is on, whose year is the one shown.</param>
        /// <param name="now">The wall clock, for picking today out.</param>
        /// <param name="width">The console width.</param>
        /// <param name="height">How many rows the body gets.</param>
        /// <returns>The view's rows.</returns>
        public static IReadOnlyList<string> Render(PlannerDiary diary, DateOnly selected, DateTime now, int width,
            int height)
        {
            var inner = Math.Max(1, width - 2);
            var today = DateOnly.FromDateTime(now);

            var rows = new List<string>
            {
                PlannerChrome.Titled(selected.Year.ToString("0000", CultureInfo.InvariantCulture), width),
                PlannerChrome.Row(AnsiText.Fit(new string(' ', LabelWidth) + Ruler(), inner), DosTheme.Title)
            };

            for (var month = 1; month <= 12 && rows.Count < height - 1; month++)
                rows.Add(Strip(diary, selected, today, month, inner));

            while (rows.Count < height - 1)
                rows.Add(PlannerChrome.Row(AnsiText.Fit(string.Empty, inner), DosTheme.Field));

            rows.Add(PlannerChrome.Bottom(width));

            return rows;
        }

        /// <summary>
        ///     Which day a cell of the year belongs to, or null for the label, the ruler and the days a month does
        ///     not have.
        /// </summary>
        /// <param name="year">The year on show.</param>
        /// <param name="row">The screen row pressed.</param>
        /// <param name="column">The screen column pressed.</param>
        /// <returns>The day, or null.</returns>
        public static DateOnly? DayAt(int year, int row, int column)
        {
            // The box's top border and the ruler under it come first, so January is the third row.
            var month = row - PlannerChrome.BodyRow - 1;
            var day = column - FirstDayColumn + 1;

            if (month < 1 || month > 12 || day < 1 || day > LongestMonth)
                return null;

            // The thirty-first of a month with thirty days is a cell that was drawn blank, and clicking a blank
            // must not quietly land on the first of the next month.
            return day > DateTime.DaysInMonth(year, month) ? null : new DateOnly(year, month, day);
        }

        /// <summary>The ruler along the top, with a number every five days.</summary>
        /// <returns>The ruler, exactly as wide as a strip.</returns>
        private static string Ruler()
        {
            var ruler = new char[LongestMonth];

            for (var i = 0; i < ruler.Length; i++)
                ruler[i] = ' ';

            foreach (var day in new[] {1, 5, 10, 15, 20, 25, 30})
            {
                var text = day.ToString(CultureInfo.InvariantCulture);

                for (var i = 0; i < text.Length && day - 1 + i < ruler.Length; i++)
                    ruler[day - 1 + i] = text[i];
            }

            return new string(ruler);
        }

        /// <summary>One month's strip, and how many things are on it.</summary>
        /// <param name="diary">The planner.</param>
        /// <param name="selected">The day the cursor is on.</param>
        /// <param name="today">Today.</param>
        /// <param name="month">Which month.</param>
        /// <param name="inner">How wide the box's inside is.</param>
        /// <returns>The row.</returns>
        private static string Strip(PlannerDiary diary, DateOnly selected, DateOnly today, int month, int inner)
        {
            var year = selected.Year;
            var days = DateTime.DaysInMonth(year, month);
            var busy = 0;

            var row = new TextRow()
                .Append("│", DosTheme.Frame)
                .Append(AnsiText.Fit(
                    " " + new DateOnly(year, month, 1).ToString("MMM", CultureInfo.InvariantCulture) + " ",
                    LabelWidth), DosTheme.Title);

            for (var day = 1; day <= LongestMonth; day++)
            {
                if (day > days)
                {
                    row.Append(' ', 1, DosTheme.Field);
                    continue;
                }

                var date = new DateOnly(year, month, day);
                var marked = diary.HasAnythingOn(date);

                if (marked)
                    busy++;

                row.Append(marked ? Busy : Quiet, 1, StyleFor(date, selected, today, marked));
            }

            var summary = busy == 0 ? string.Empty : "  " + busy.ToString(CultureInfo.InvariantCulture);

            row.Append(AnsiText.Fit(summary, Math.Max(0, inner - LabelWidth - LongestMonth)), DosTheme.Frame);

            return row.Append("│", DosTheme.Frame).Render();
        }

        /// <summary>How a day of the strip is painted, cursor first and today second.</summary>
        /// <param name="date">The day.</param>
        /// <param name="selected">The day the cursor is on.</param>
        /// <param name="today">Today.</param>
        /// <param name="marked">Whether anything is on it.</param>
        /// <returns>Its style.</returns>
        private static TextStyle StyleFor(DateOnly date, DateOnly selected, DateOnly today, bool marked)
        {
            if (date == selected)
                return DosTheme.Selection;

            if (date == today)
                return DosTheme.Highlight;

            return marked ? DosTheme.Title : DosTheme.Frame;
        }
    }
}
