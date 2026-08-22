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
    ///     The week: seven days one under the other, each with everything on it written out in full. The view that
    ///     answers "what is this week actually like", which a month grid cannot, because a month grid has room for
    ///     a number and a dot.
    ///     <para>
    ///         <b>The lines are built once and read by both the drawing and the hit test</b>, which is the same
    ///         discipline the library's own controls follow. A week whose days are different heights cannot have
    ///         its rows worked out twice and stay in step.
    ///     </para>
    /// </summary>
    internal static class PlannerWeekView
    {
        /// <summary>Draws the view.</summary>
        /// <param name="diary">The planner.</param>
        /// <param name="selected">The day the cursor is on.</param>
        /// <param name="scroll">How far down the week has been scrolled.</param>
        /// <param name="width">The console width.</param>
        /// <param name="height">How many rows the body gets.</param>
        /// <returns>The view's rows.</returns>
        public static IReadOnlyList<string> Render(PlannerDiary diary, DateOnly selected, int scroll, int width,
            int height)
        {
            var start = StartOfWeek(selected);
            var lines = Lines(diary, start);
            var inner = Math.Max(1, width - 2);
            var visible = Math.Max(0, height - 2);

            var first = Math.Clamp(scroll, 0, Math.Max(0, lines.Count - visible));

            var rows = new List<string>
            {
                PlannerChrome.Titled(
                    "Week of " + start.ToString("d MMMM yyyy", CultureInfo.InvariantCulture), width)
            };

            for (var i = 0; i < visible; i++)
            {
                var at = first + i;

                if (at >= lines.Count)
                {
                    rows.Add(PlannerChrome.Row(AnsiText.Fit(string.Empty, inner), DosTheme.Field));
                    continue;
                }

                rows.Add(Draw(lines[at], selected, inner));
            }

            rows.Add(PlannerChrome.Bottom(width));

            return rows;
        }

        /// <summary>
        ///     Which day a row of the view belongs to, or null for the blanks below the week.
        ///     <para>
        ///         Worked out from the same line list the drawing uses, so a day whose entries pushed the ones
        ///         below it down cannot be clicked into the wrong day.
        ///     </para>
        /// </summary>
        /// <param name="diary">The planner.</param>
        /// <param name="selected">The day the cursor is on.</param>
        /// <param name="scroll">How far down the week has been scrolled.</param>
        /// <param name="height">How many rows the body gets.</param>
        /// <param name="row">The screen row pressed.</param>
        /// <returns>The day, or null.</returns>
        public static DateOnly? DayAt(PlannerDiary diary, DateOnly selected, int scroll, int height, int row)
        {
            var lines = Lines(diary, StartOfWeek(selected));
            var visible = Math.Max(0, height - 2);
            var first = Math.Clamp(scroll, 0, Math.Max(0, lines.Count - visible));

            // The box's own top border is the first row, so the first line of the week is the one after it.
            var at = first + (row - PlannerChrome.BodyRow - 1);

            if (at < first || at >= first + visible || at >= lines.Count)
                return null;

            return lines[at].Date;
        }

        /// <summary>How many lines the week comes to, which is what a caller needs to scroll it.</summary>
        /// <param name="diary">The planner.</param>
        /// <param name="selected">The day the cursor is on.</param>
        /// <returns>The line count.</returns>
        public static int LineCount(PlannerDiary diary, DateOnly selected)
        {
            return Lines(diary, StartOfWeek(selected)).Count;
        }

        /// <summary>Where in the week's lines a day begins, so a caller can scroll it into view.</summary>
        /// <param name="diary">The planner.</param>
        /// <param name="selected">The day to find.</param>
        /// <returns>The line index, or zero.</returns>
        public static int LineOf(PlannerDiary diary, DateOnly selected)
        {
            var lines = Lines(diary, StartOfWeek(selected));

            for (var i = 0; i < lines.Count; i++)
            {
                if (lines[i].Date == selected)
                    return i;
            }

            return 0;
        }

        /// <summary>
        ///     The Monday of the week a day is in.
        ///     <para>
        ///         Monday rather than whatever the month grid is set to, deliberately: this view is a list of days
        ///         rather than a grid of columns, so the setting that moves the grid's columns has nothing to move
        ///         here, and a working week is what somebody looking at a week wants.
        ///     </para>
        /// </summary>
        /// <param name="date">A day in the week.</param>
        /// <returns>Its Monday.</returns>
        public static DateOnly StartOfWeek(DateOnly date)
        {
            var back = ((int) date.DayOfWeek + 6) % 7;

            return date.DayNumber < back ? date : date.AddDays(-back);
        }

        /// <summary>Every line of the week: a heading for each day, then whatever is on it.</summary>
        /// <param name="diary">The planner.</param>
        /// <param name="start">The Monday.</param>
        /// <returns>The lines.</returns>
        private static IReadOnlyList<PlannerWeekLine> Lines(PlannerDiary diary, DateOnly start)
        {
            var lines = new List<PlannerWeekLine>();

            for (var day = 0; day < MonthGrid.DaysInWeek; day++)
            {
                if (start.DayNumber + day > DateOnly.MaxValue.DayNumber)
                    break;

                var date = start.AddDays(day);
                var entries = diary.On(date);

                lines.Add(new PlannerWeekLine(date, null));

                foreach (var entry in entries)
                    lines.Add(new PlannerWeekLine(date, entry));
            }

            return lines;
        }

        /// <summary>Draws one line of the week: either a day's heading or one of its entries.</summary>
        /// <param name="line">The line.</param>
        /// <param name="selected">The day the cursor is on.</param>
        /// <param name="inner">How wide the box's inside is.</param>
        /// <returns>The row.</returns>
        private static string Draw(PlannerWeekLine line, DateOnly selected, int inner)
        {
            var chosen = line.Date == selected;

            if (line.Entry == null)
            {
                var heading = "  " + line.Date.ToString("dddd d MMMM", CultureInfo.InvariantCulture);

                return PlannerChrome.Row(AnsiText.Fit(heading, inner),
                    chosen ? DosTheme.Selection : DosTheme.Title);
            }

            return new TextRow()
                .Append("│", DosTheme.Frame)
                .Append("      ", chosen ? DosTheme.Highlight : DosTheme.Field)
                .Append(AnsiText.Fit(line.Entry.Time, 6), chosen ? DosTheme.Highlight : DosTheme.Title)
                .Append(" ", chosen ? DosTheme.Highlight : DosTheme.Field)
                .Append(AnsiText.Fit(line.Entry.Title, Math.Max(1, inner - 13)),
                    chosen ? DosTheme.Highlight : DosTheme.Field)
                .Append("│", DosTheme.Frame)
                .Render();
        }
    }
}
