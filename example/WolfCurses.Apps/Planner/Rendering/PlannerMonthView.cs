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
    ///     The month: a calendar, the wall clock under it, and the chosen day's entries beside them. The view that
    ///     answers "which day of the week is that on".
    /// </summary>
    internal static class PlannerMonthView
    {
        /// <summary>How many rows the clock box costs.</summary>
        private const int ClockRows = 6;

        /// <summary>Draws the view.</summary>
        /// <param name="grid">The month, already positioned.</param>
        /// <param name="diary">The planner.</param>
        /// <param name="selected">The day the cursor is on.</param>
        /// <param name="now">The wall clock.</param>
        /// <param name="width">The console width.</param>
        /// <param name="height">How many rows the body gets.</param>
        /// <returns>The view's rows.</returns>
        public static IReadOnlyList<string> Render(MonthGrid grid, PlannerDiary diary, DateOnly selected,
            DateTime now, int width, int height)
        {
            var leftWidth = grid.Width + 2;

            var left = new List<string>(Calendar(grid, leftWidth));
            left.AddRange(Clock(now, leftWidth, height - left.Count));

            var right = Day(diary, selected, Math.Max(16, width - leftWidth - PlannerChrome.Gutter), height);
            var gutter = DosTheme.Field.Apply(new string(' ', PlannerChrome.Gutter));

            var rows = new List<string>(height);

            for (var i = 0; i < height; i++)
                rows.Add(left[i] + gutter + right[i]);

            return rows;
        }

        /// <summary>The month in a box with its name in the top edge.</summary>
        /// <param name="grid">The month.</param>
        /// <param name="width">How wide the box is.</param>
        /// <returns>The box's rows.</returns>
        private static IReadOnlyList<string> Calendar(MonthGrid grid, int width)
        {
            var rows = new List<string> {PlannerChrome.Titled(grid.Title, width)};
            var edge = DosTheme.Frame.Apply("│");

            foreach (var line in grid.Render())
            {
                // No padding inside the border. The grid already leads each cell with a space, and a column of
                // padding here would both widen the box past its own top edge and put every date one column right
                // of where the hit test measured from.
                rows.Add(edge + line + edge);
            }

            rows.Add(PlannerChrome.Bottom(width));

            return rows;
        }

        /// <summary>
        ///     The wall clock.
        ///     <para>
        ///         It is handed the time rather than reading it, which is the same seam the month grid takes for
        ///         today and for the same two reasons: a test cannot wait for a particular second to come round,
        ///         and a screen that read the clock while drawing would read it a thousand times a second.
        ///     </para>
        /// </summary>
        /// <param name="now">The time to show.</param>
        /// <param name="width">How wide the box is.</param>
        /// <param name="height">How many rows are left for it.</param>
        /// <returns>The box's rows.</returns>
        private static IReadOnlyList<string> Clock(DateTime now, int width, int height)
        {
            var inner = Math.Max(1, width - 2);
            var rows = new List<string> {PlannerChrome.Titled("Now", width)};

            var content = new[]
            {
                "  " + now.ToString("dddd", CultureInfo.InvariantCulture),
                "  " + now.ToString("d MMMM yyyy", CultureInfo.InvariantCulture),
                "  " + now.ToString("HH:mm:ss", CultureInfo.InvariantCulture)
            };

            for (var i = 0; i < Math.Max(0, height - 2); i++)
            {
                var line = i < content.Length ? content[i] : string.Empty;

                rows.Add(PlannerChrome.Row(AnsiText.Fit(line, inner),
                    i == 2 ? DosTheme.Title : DosTheme.Field));
            }

            rows.Add(PlannerChrome.Bottom(width));

            return rows;
        }

        /// <summary>The chosen day's entries, holidays first.</summary>
        /// <param name="diary">The planner.</param>
        /// <param name="selected">The day.</param>
        /// <param name="width">How wide the box is.</param>
        /// <param name="height">How many rows it must fill, borders included.</param>
        /// <returns>The box's rows.</returns>
        private static IReadOnlyList<string> Day(PlannerDiary diary, DateOnly selected, int width, int height)
        {
            var inner = Math.Max(1, width - 2);
            var entries = diary.On(selected);

            var rows = new List<string>
            {
                PlannerChrome.Titled(selected.ToString("dddd d MMMM yyyy", CultureInfo.InvariantCulture), width)
            };

            for (var i = 0; i < height - 2; i++)
            {
                if (i >= entries.Count)
                {
                    // The empty day says what to do about it, which is the one place a hint fits without being in
                    // anybody's way: there is nothing else on this row to read.
                    var hint = i == 0 && entries.Count == 0 ? "  Nothing planned. F2 adds something." : string.Empty;

                    rows.Add(PlannerChrome.Row(AnsiText.Fit(hint, inner), DosTheme.Field));
                    continue;
                }

                rows.Add(Entry(entries[i], inner));
            }

            rows.Add(PlannerChrome.Bottom(width));

            return rows;
        }

        /// <summary>One entry, with its time picked out from what it is.</summary>
        /// <param name="entry">The entry.</param>
        /// <param name="inner">How wide the box's inside is.</param>
        /// <returns>The row.</returns>
        private static string Entry(PlannerEvent entry, int inner)
        {
            return new TextRow()
                .Append("│", DosTheme.Frame)
                .Append("  ", DosTheme.Field)
                .Append(AnsiText.Fit(entry.Time, 6), DosTheme.Title)
                .Append(" ", DosTheme.Field)
                .Append(AnsiText.Fit(entry.Title, Math.Max(1, inner - 9)),
                    entry.Kind == PlannerEventKindEnum.Holiday ? DosTheme.Title : DosTheme.Field)
                .Append("│", DosTheme.Frame)
                .Render();
        }
    }
}
