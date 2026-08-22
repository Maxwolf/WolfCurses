// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;
using WolfCurses.Graphics;
using WolfCurses.Window.Control;

namespace WolfCurses.Apps.Planner
{
    /// <summary>
    ///     The list: everything from the chosen day onwards, in order, with the empty days simply not there. The
    ///     view that answers "what is coming", which every calendar view answers badly because a calendar has to
    ///     give the same room to a quiet fortnight as to a busy afternoon.
    ///     <para>
    ///         It is handed the lines rather than working them out, because finding them means walking a year of
    ///         days and this is drawn a thousand times a second. The caller recomputes them when something
    ///         actually changes, which is the same discipline the wall clock follows.
    ///     </para>
    /// </summary>
    internal static class PlannerAgendaView
    {
        /// <summary>Draws the view.</summary>
        /// <param name="agenda">The lines, worked out by the caller.</param>
        /// <param name="selected">The day the cursor is on.</param>
        /// <param name="scroll">How far down the list has been scrolled.</param>
        /// <param name="width">The console width.</param>
        /// <param name="height">How many rows the body gets.</param>
        /// <returns>The view's rows.</returns>
        public static IReadOnlyList<string> Render(IReadOnlyList<PlannerEntryLine> agenda, DateOnly selected,
            int scroll, int width, int height)
        {
            var inner = Math.Max(1, width - 2);
            var visible = Math.Max(0, height - 2);
            var first = Math.Clamp(scroll, 0, Math.Max(0, agenda.Count - visible));

            var rows = new List<string> {PlannerChrome.Titled("Coming up", width)};

            var bar = new ScrollBar
            {
                Length = visible,
                Total = Math.Max(agenda.Count, visible),
                Visible = visible,
                Position = first,
                ArrowStyle = DosTheme.ScrollArrow,
                TrackStyle = DosTheme.ScrollTrack,
                ThumbStyle = DosTheme.ScrollThumb
            };

            var cells = bar.Cells();
            var previous = DateOnly.MinValue;

            for (var i = 0; i < visible; i++)
            {
                var at = first + i;

                if (at >= agenda.Count)
                {
                    var hint = i == 0 && agenda.Count == 0
                        ? "  Nothing coming up in the next year."
                        : string.Empty;

                    rows.Add(Bordered(AnsiText.Fit(hint, inner - 1), DosTheme.Field, cells[i]));
                    continue;
                }

                var line = agenda[at];

                // The date is written only when it changes, which is what turns a list of entries into a list of
                // days: repeating it against every entry buries the one thing the eye is scanning for.
                var date = line.Date == previous ? string.Empty : PlannerChrome.ShortDate(line.Date);
                previous = line.Date;

                rows.Add(Draw(line, date, line.Date == selected, inner - 1, cells[i]));
            }

            rows.Add(PlannerChrome.Bottom(width));

            return rows;
        }

        /// <summary>Which day a row of the list belongs to, or null for the blanks below it.</summary>
        /// <param name="agenda">The lines.</param>
        /// <param name="scroll">How far down the list has been scrolled.</param>
        /// <param name="height">How many rows the body gets.</param>
        /// <param name="row">The screen row pressed.</param>
        /// <returns>The day, or null.</returns>
        public static DateOnly? DayAt(IReadOnlyList<PlannerEntryLine> agenda, int scroll, int height, int row)
        {
            var visible = Math.Max(0, height - 2);
            var first = Math.Clamp(scroll, 0, Math.Max(0, agenda.Count - visible));

            var at = first + (row - PlannerChrome.BodyRow - 1);

            if (at < first || at >= first + visible || at >= agenda.Count)
                return null;

            return agenda[at].Date;
        }

        /// <summary>Draws one line of the list.</summary>
        /// <param name="line">The entry and its day.</param>
        /// <param name="date">The date to write, or empty to leave it out.</param>
        /// <param name="chosen">Whether this is the day the cursor is on.</param>
        /// <param name="inner">How wide the box's inside is, less the scrollbar.</param>
        /// <param name="scrollCell">This row's cell of the scrollbar.</param>
        /// <returns>The row.</returns>
        private static string Draw(PlannerEntryLine line, string date, bool chosen, int inner, string scrollCell)
        {
            var body = chosen ? DosTheme.Highlight : DosTheme.Field;

            return new TextRow()
                .Append("│", DosTheme.Frame)
                .Append("  ", body)
                .Append(AnsiText.Fit(date, 11), chosen ? DosTheme.Highlight : DosTheme.Title)
                .Append(AnsiText.Fit(line.Entry.Time, 7), chosen ? DosTheme.Highlight : DosTheme.Title)
                .Append(AnsiText.Fit(line.Entry.Title, Math.Max(1, inner - 20)), body)
                .Append(scrollCell)
                .Append("│", DosTheme.Frame)
                .Render();
        }

        /// <summary>A blank row with its share of the scrollbar on the end.</summary>
        /// <param name="content">What goes inside.</param>
        /// <param name="style">How to paint it.</param>
        /// <param name="scrollCell">This row's cell of the scrollbar.</param>
        /// <returns>The row.</returns>
        private static string Bordered(string content, TextStyle style, string scrollCell)
        {
            return new TextRow()
                .Append("│", DosTheme.Frame)
                .Append(content, style)
                .Append(scrollCell)
                .Append("│", DosTheme.Frame)
                .Render();
        }
    }
}
