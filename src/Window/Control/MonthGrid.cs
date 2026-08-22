// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;
using System.Globalization;
using WolfCurses.Graphics;

namespace WolfCurses.Window.Control
{
    /// <summary>
    ///     One month laid out as a calendar: a row of day names and six rows of dates, with today picked out, one
    ///     day selected, and whichever days have something on them marked.
    ///     <para>
    ///         <b>It keeps its layout</b>, for the third time in this namespace and the same reason as
    ///         <see cref="MenuBar" /> and <see cref="Keypad" />: the grid is worked out once and read by both the
    ///         drawing and the hit test, so the date a click lands on cannot be a different date from the one drawn
    ///         there. Off-by-one is the whole difficulty of a calendar, and computing the arithmetic twice is how
    ///         it arrives.
    ///     </para>
    ///     <para>
    ///         <b>Today is told, never read from the clock.</b> A control that asked <c>DateTime.Now</c> itself
    ///         could not be tested without waiting for a particular date to come round, and would quietly go on
    ///         highlighting yesterday in a program left open past midnight, because nothing would ever ask it
    ///         again. The same seam as the injected clocks on <see cref="IntervalTimer" /> and
    ///         <see cref="HeldAxis" />, and the caller's job to keep fresh.
    ///     </para>
    ///     <para>
    ///         <b>Always six weeks, whatever the month needs.</b> A month can span four rows or six depending on
    ///         its length and which day it starts on, and a grid that changed height would move everything under it
    ///         as you paged from one month to the next. The cost is a blank row in February; the alternative is a
    ///         screen that jumps.
    ///     </para>
    /// </summary>
    public sealed class MonthGrid
    {
        /// <summary>How many week rows are always drawn, so paging between months moves nothing.</summary>
        public const int Weeks = 6;

        /// <summary>How many days are in a week, which is the one number here that is not going to change.</summary>
        public const int DaysInWeek = 7;

        /// <summary>Initializes a grid showing a month.</summary>
        /// <param name="year">The year; clamped to what a date can hold.</param>
        /// <param name="month">The month, one to twelve.</param>
        public MonthGrid(int year = 2000, int month = 1)
        {
            Show(year, month);
        }

        /// <summary>The year on show.</summary>
        public int Year { get; private set; }

        /// <summary>The month on show, one to twelve.</summary>
        public int Month { get; private set; }

        /// <summary>
        ///     Which day the week starts on. The one setting people genuinely differ about, and it moves every
        ///     column in the grid, which is why it is here rather than assumed.
        /// </summary>
        public DayOfWeek FirstDayOfWeek { get; set; } = DayOfWeek.Sunday;

        /// <summary>The day the cursor is on, drawn picked out. Null for none.</summary>
        public DateOnly? Selected { get; set; }

        /// <summary>What the caller says today is. Null for none; see the note on the class about why it is told.</summary>
        public DateOnly? Today { get; set; }

        /// <summary>
        ///     Asked, per day drawn, whether that day has anything on it. A predicate rather than a set the caller
        ///     keeps up to date, for the same reason <see cref="MenuBarEntry.EnabledWhen" /> is one: the answer is
        ///     wanted at the moment of drawing, and an owner refreshing a cached set has to remember to.
        /// </summary>
        public Func<DateOnly, bool> Marked { get; set; }

        /// <summary>How many columns one day's cell is drawn in.</summary>
        public int CellWidth { get; set; } = 4;

        /// <summary>What is drawn against a day that has something on it, in the cell's last column.</summary>
        public char MarkGlyph { get; set; } = '·';

        /// <summary>The screen row the day-name header is drawn on, which a hit test is measured against.</summary>
        public int Row { get; set; }

        /// <summary>The screen column the grid's left edge is in.</summary>
        public int Column { get; set; }

        /// <summary>How the day names are painted.</summary>
        public TextStyle HeaderStyle { get; set; } = TextStyle.None;

        /// <summary>How an ordinary day is painted.</summary>
        public TextStyle DayStyle { get; set; } = TextStyle.None;

        /// <summary>How a day with something on it is painted.</summary>
        public TextStyle MarkedStyle { get; set; } = TextStyle.None;

        /// <summary>How today is painted.</summary>
        public TextStyle TodayStyle { get; set; } = TextStyle.None;

        /// <summary>How the selected day is painted; falls back to inverse video when left unset.</summary>
        public TextStyle SelectedStyle { get; set; } = TextStyle.None;

        /// <summary>Which colour vocabulary the styles resolve through; pinnable per instance for tests.</summary>
        public AnsiColorModeEnum ColorMode { get; set; } = AnsiColorModeEnum.Auto;

        /// <summary>How many screen columns the grid occupies.</summary>
        public int Width => DaysInWeek * Math.Max(2, CellWidth);

        /// <summary>How many screen rows it occupies: the day names, and the weeks under them.</summary>
        public int Height => Weeks + 1;

        /// <summary>The first of the month on show.</summary>
        public DateOnly FirstOfMonth => new(Year, Month, 1);

        /// <summary>How the month is written out, for a caller to put in a title.</summary>
        public string Title => FirstOfMonth.ToString("MMMM yyyy", CultureInfo.InvariantCulture);

        /// <summary>
        ///     The date drawn in the grid's very first cell, which is usually in the previous month. Public because
        ///     it is the one number the whole layout hangs off, and a caller checking the arithmetic should be able
        ///     to see it rather than reconstruct it.
        /// </summary>
        public DateOnly FirstCell
        {
            get
            {
                var first = FirstOfMonth;
                var back = ((int) first.DayOfWeek - (int) FirstDayOfWeek + DaysInWeek) % DaysInWeek;

                // January of year one has nothing behind it to back into, so the grid simply starts there and its
                // leading cells are blank, which is what they would have been anyway.
                return back == 0 || first.DayNumber < back ? first : first.AddDays(-back);
            }
        }

        /// <summary>Shows a month, clamped to what a date can hold.</summary>
        /// <param name="year">The year.</param>
        /// <param name="month">The month, one to twelve.</param>
        public void Show(int year, int month)
        {
            Year = Math.Clamp(year, DateOnly.MinValue.Year, DateOnly.MaxValue.Year);
            Month = Math.Clamp(month, 1, 12);
        }

        /// <summary>Shows the month a date falls in.</summary>
        /// <param name="date">The date.</param>
        public void Show(DateOnly date)
        {
            Show(date.Year, date.Month);
        }

        /// <summary>
        ///     Moves the month on show, stopping at the ends of what a date can hold rather than throwing there.
        /// </summary>
        /// <param name="months">How many months to move; negative goes back.</param>
        public void MoveMonths(int months)
        {
            var target = Year * 12 + (Month - 1) + months;

            var lowest = DateOnly.MinValue.Year * 12;
            var highest = DateOnly.MaxValue.Year * 12 + 11;

            target = Math.Clamp(target, lowest, highest);

            Show(target / 12, target % 12 + 1);
        }

        /// <summary>
        ///     The date drawn in a cell, or null for the blanks either side of the month and everywhere off the
        ///     grid.
        ///     <para>
        ///         Days belonging to the neighbouring months are drawn as blanks rather than as greyed numbers,
        ///         which is what <c>cal</c> has always done: it means a cell either is a day of this month or is
        ///         nothing at all, and a click can never quietly land in a month you are not looking at.
        ///     </para>
        /// </summary>
        /// <param name="row">The screen row.</param>
        /// <param name="column">The screen column.</param>
        /// <returns>The date, or null.</returns>
        public DateOnly? DayAt(int row, int column)
        {
            var week = row - Row - 1;
            var offset = column - Column;

            if (week < 0 || week >= Weeks || offset < 0 || offset >= Width)
                return null;

            return DayInCell(week * DaysInWeek + offset / Math.Max(2, CellWidth));
        }

        /// <summary>Where a date is drawn, when it is on the grid at all.</summary>
        /// <param name="date">The date.</param>
        /// <param name="row">Its screen row.</param>
        /// <param name="column">Its screen column.</param>
        /// <returns>TRUE when that date is on the month being shown.</returns>
        public bool TryPositionOf(DateOnly date, out int row, out int column)
        {
            row = 0;
            column = 0;

            if (date.Year != Year || date.Month != Month)
                return false;

            var cell = date.DayNumber - FirstCell.DayNumber;

            if (cell < 0 || cell >= Weeks * DaysInWeek)
                return false;

            row = Row + 1 + cell / DaysInWeek;
            column = Column + cell % DaysInWeek * Math.Max(2, CellWidth);

            return true;
        }

        /// <summary>Draws the grid, one string per screen row.</summary>
        /// <returns>The day names, then six weeks.</returns>
        public IReadOnlyList<string> Render()
        {
            var width = Math.Max(2, CellWidth);
            var rows = new List<string>(Height) {Header(width)};

            for (var week = 0; week < Weeks; week++)
            {
                var line = new TextRow {ColorMode = ColorMode};

                for (var day = 0; day < DaysInWeek; day++)
                {
                    var date = DayInCell(week * DaysInWeek + day);

                    if (date == null)
                    {
                        line.Append(' ', width, DayStyle);
                        continue;
                    }

                    var marked = Marked != null && Marked(date.Value);

                    line.Append(
                        AnsiText.Fit(date.Value.Day.ToString(CultureInfo.InvariantCulture), width - 1,
                            AnsiHorizontalAlignmentEnum.Right) + (marked ? MarkGlyph : ' '),
                        StyleFor(date.Value, marked));
                }

                rows.Add(line.Render());
            }

            return rows;
        }

        /// <summary>Draws the row of day names, starting on whichever day the week does.</summary>
        /// <param name="width">How wide one cell is.</param>
        /// <returns>The header row.</returns>
        private string Header(int width)
        {
            var names = DateTimeFormatInfo.InvariantInfo.ShortestDayNames;
            var row = new TextRow {ColorMode = ColorMode};

            for (var day = 0; day < DaysInWeek; day++)
            {
                var name = names[((int) FirstDayOfWeek + day) % DaysInWeek];

                // Right aligned with the marker column left blank, so a name sits over the numbers beneath it
                // rather than over the space where their marks go.
                row.Append(AnsiText.Fit(name, width - 1, AnsiHorizontalAlignmentEnum.Right) + " ", HeaderStyle);
            }

            return row.Render();
        }

        /// <summary>The date in a cell of the grid, counting across each week and then down.</summary>
        /// <param name="cell">Which cell.</param>
        /// <returns>The date, or null when that cell belongs to a neighbouring month.</returns>
        private DateOnly? DayInCell(int cell)
        {
            if (cell < 0 || cell >= Weeks * DaysInWeek)
                return null;

            var start = FirstCell;

            if (start.DayNumber + cell > DateOnly.MaxValue.DayNumber)
                return null;

            var date = start.AddDays(cell);

            return date.Year == Year && date.Month == Month ? date : null;
        }

        /// <summary>
        ///     How a day is painted. The order is the priority: the cursor wins over today, and today wins over
        ///     merely having something on it, because the further down that list a thing is the longer it stays
        ///     true and the less it needs pointing out.
        /// </summary>
        /// <param name="date">The day.</param>
        /// <param name="marked">Whether it has anything on it.</param>
        /// <returns>Its style.</returns>
        private TextStyle StyleFor(DateOnly date, bool marked)
        {
            if (Selected.HasValue && Selected.Value == date)
            {
                if (!SelectedStyle.IsEmpty)
                    return SelectedStyle;

                // Inverse video where nobody chose a colour, through the same gate every other highlight in this
                // namespace uses, so an unstyled grid still shows which day the cursor is on and still emits
                // nothing at all when colour is switched off.
                return AnsiConsole.DetectColorMode() == AnsiColorModeEnum.None
                    ? DayStyle
                    : DayStyle.WithForeground(ConsoleColor.Black).WithBackground(ConsoleColor.Gray);
            }

            if (Today.HasValue && Today.Value == date && !TodayStyle.IsEmpty)
                return TodayStyle;

            return marked && !MarkedStyle.IsEmpty ? MarkedStyle : DayStyle;
        }
    }
}
