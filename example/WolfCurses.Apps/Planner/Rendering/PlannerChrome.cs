// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WolfCurses.Graphics;
using WolfCurses.Window.Control;

namespace WolfCurses.Apps.Planner
{
    /// <summary>
    ///     Assembles the planner screen, whichever way it is being looked at, and owns the pieces the four views
    ///     share: the boxes they are drawn in and the menu panel drawn over them.
    ///     <para>
    ///         <b>Every view is exactly the same height</b>, so switching between them moves nothing on screen. It
    ///         is the same reason the month grid always draws six weeks: a layout that changed size as you looked
    ///         at it differently would make the zoom feel like a redraw rather than a step back.
    ///     </para>
    ///     <para>
    ///         The menu panel is drawn over the finished rows with <see cref="AnsiText.Slice" />, the same as the
    ///         calculator and for the same reason: the month grid arrives as strings a widget has already styled,
    ///         so there are no runs left to slice.
    ///     </para>
    /// </summary>
    internal static class PlannerChrome
    {
        /// <summary>The screen row the menu bar is drawn on.</summary>
        public const int BarRow = 1;

        /// <summary>The screen row every view's first box starts on, which is where an open panel starts too.</summary>
        public const int BodyRow = 2;

        /// <summary>How many rows the body gets, whichever view is showing.</summary>
        public const int BodyRows = 15;

        /// <summary>The screen row the month view's day names are on, which its hit test is measured against.</summary>
        public const int GridRow = BodyRow + 1;

        /// <summary>The screen column the month grid's left edge is in, one past the box's border.</summary>
        public const int GridColumn = 1;

        /// <summary>How many columns separate the calendar from the day's entries.</summary>
        public const int Gutter = 2;

        /// <summary>Composes the screen.</summary>
        /// <param name="menuBar">The menu bar, already told how wide it is.</param>
        /// <param name="view">Which way the planner is being looked at.</param>
        /// <param name="grid">The month, already positioned.</param>
        /// <param name="diary">The planner.</param>
        /// <param name="selected">The day the cursor is on.</param>
        /// <param name="agenda">Everything from the chosen day onwards, worked out by the caller.</param>
        /// <param name="scroll">How far the scrolling views have been scrolled.</param>
        /// <param name="now">The wall clock, sampled by the caller rather than read here.</param>
        /// <param name="status">The key-hint strip's text.</param>
        /// <param name="width">The console width.</param>
        /// <returns>The whole screen, newline separated.</returns>
        public static string Compose(MenuBar menuBar, PlannerViewEnum view, MonthGrid grid, PlannerDiary diary,
            DateOnly selected, IReadOnlyList<PlannerEntryLine> agenda, int scroll, DateTime now, string status,
            int width)
        {
            var sb = new StringBuilder();

            sb.Append(menuBar.RenderTitleBar(width)).Append(Environment.NewLine);

            var rows = view switch
            {
                PlannerViewEnum.Week => PlannerWeekView.Render(diary, selected, scroll, width, BodyRows),
                PlannerViewEnum.Year => PlannerYearView.Render(diary, selected, now, width, BodyRows),
                PlannerViewEnum.Agenda => PlannerAgendaView.Render(agenda, selected, scroll, width, BodyRows),
                _ => PlannerMonthView.Render(grid, diary, selected, now, width, BodyRows)
            };

            var panel = menuBar.IsOpen ? menuBar.DropdownRows() : (IReadOnlyList<string>) Array.Empty<string>();
            var panelWidth = Math.Min(menuBar.DropdownWidth, width);
            var panelColumn = panelWidth <= 0
                ? 0
                : Math.Clamp(menuBar.DropdownColumn, 0, Math.Max(0, width - panelWidth));

            for (var i = 0; i < rows.Count; i++)
                sb.Append(Overlay(rows[i], panel, i, panelColumn, panelWidth, width)).Append(Environment.NewLine);

            sb.Append(DosTheme.Status.Apply(AnsiText.Fit(status, width)));

            return sb.ToString();
        }

        /// <summary>Draws a row with the menu panel over the top of it, when the panel reaches that row.</summary>
        /// <param name="row">The finished row.</param>
        /// <param name="panel">The panel's rows.</param>
        /// <param name="panelRow">Which of the panel's rows belongs on this screen row.</param>
        /// <param name="panelColumn">Which screen column the panel starts at.</param>
        /// <param name="panelWidth">How wide the panel is.</param>
        /// <param name="width">The console width.</param>
        /// <returns>The finished row.</returns>
        private static string Overlay(string row, IReadOnlyList<string> panel, int panelRow, int panelColumn,
            int panelWidth, int width)
        {
            if (panelRow < 0 || panelRow >= panel.Count || panelWidth <= 0)
                return row;

            return AnsiText.Slice(row, 0, panelColumn) + panel[panelRow] +
                   AnsiText.Slice(row, panelColumn + panelWidth, width - panelColumn - panelWidth);
        }

        /// <summary>A box's top edge with a title notched into it.</summary>
        /// <param name="title">What the tab reads.</param>
        /// <param name="width">How wide the box is.</param>
        /// <returns>The row.</returns>
        public static string Titled(string title, int width)
        {
            var inner = Math.Max(0, width - 2);
            var tab = " " + (title ?? string.Empty) + " ";

            if (tab.Length > inner)
                tab = tab.Substring(0, inner);

            return new TextRow()
                .Append("┌", DosTheme.Frame)
                .Append(tab, DosTheme.Title)
                .Append('─', inner - tab.Length, DosTheme.Frame)
                .Append("┐", DosTheme.Frame)
                .Render();
        }

        /// <summary>A box's bottom edge.</summary>
        /// <param name="width">How wide the box is.</param>
        /// <returns>The row.</returns>
        public static string Bottom(int width)
        {
            return DosTheme.Frame.Apply("└" + new string('─', Math.Max(0, width - 2)) + "┘");
        }

        /// <summary>One row of a box's inside, bordered either end.</summary>
        /// <param name="content">What goes between the borders, already the right width.</param>
        /// <param name="style">How to paint it.</param>
        /// <returns>The row.</returns>
        public static string Row(string content, TextStyle style)
        {
            return new TextRow()
                .Append("│", DosTheme.Frame)
                .Append(content, style)
                .Append("│", DosTheme.Frame)
                .Render();
        }

        /// <summary>How a date reads at the head of a day's entries.</summary>
        /// <param name="date">The date.</param>
        /// <returns>Something like <c>Fri 21 Aug</c>.</returns>
        public static string ShortDate(DateOnly date)
        {
            return date.ToString("ddd d MMM", CultureInfo.InvariantCulture);
        }
    }
}
