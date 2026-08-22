// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/22/2026

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WolfCurses.Graphics;
using WolfCurses.Utility;
using WolfCurses.Window;
using WolfCurses.Window.Control;
using WolfCurses.Window.Form;

namespace WolfCurses.Demo.Screens
{
    /// <summary>
    ///     One month drawn by <see cref="MonthGrid" />, with everything the control refuses to work out for itself
    ///     wired to a key: what today is, which days have something on them, and which day the week starts on. The
    ///     arrow keys move the cursor, PGUP and PGDN page the month, and a click picks the day it landed on.
    ///     <para>
    ///         <b>Page it and watch nothing below the calendar move.</b> A month spans four week rows or six
    ///         depending on how long it is and which day it starts on, and the grid draws six whatever the month
    ///         needs. The readout names how many this one fills, and the cost is a blank row in February; the
    ///         alternative is a screen that jumps every time you page, taking the readout and the key hints with it.
    ///     </para>
    ///     <para>
    ///         <b>Today is told, never read.</b> Nothing here asks the clock while drawing: the wall clock is read
    ///         on the simulation tick, once a second, and handed to the grid, which is the caller keeping it fresh
    ///         and is what moves the highlight in a program left open past midnight. T pins today to the day under
    ///         the cursor to show the other half, which is that the grid believes what it is told and never checks.
    ///         That is the same seam as the injected clocks on <see cref="IntervalTimer" /> and
    ///         <see cref="HeldAxis" />, and it is why a calendar can be tested without waiting for a date to come
    ///         round.
    ///     </para>
    ///     <para>
    ///         <b>Marked is a predicate, and it is handed over exactly once</b>, in
    ///         <see cref="OnFormPostCreate" />. SPACE puts the cursor's day into a set and M switches a weekend rule
    ///         on and off; neither of them tells the grid anything, because the answer is wanted at the moment of
    ///         drawing rather than kept up to date by an owner who has to remember to.
    ///     </para>
    ///     <para>
    ///         <b>The cells either side of the month are blank, and clicking one is worth doing.</b>
    ///         <see cref="MonthGrid.DayAt" /> answers null there, so the cursor stays where it is and the readout
    ///         says why: a click can never quietly land in a month you are not looking at, which is what
    ///         <c>cal</c> has always done and the reason those cells are not the neighbours' greyed days.
    ///     </para>
    ///     <para>
    ///         Pointer motion is deliberately <em>not</em> asked for. A month grid has no hover state to feed, and
    ///         motion is one event for every cell the pointer crosses, so a screen that does not read it should not
    ///         be switching it on for the rest of the session. The sprite collision test is the demo that does want
    ///         it, and hands it back in <c>OnFormClosing</c>.
    ///     </para>
    /// </summary>
    [ParentWindow(typeof (DemoWindow))]
    public sealed class CalendarDialog : Form<DemoWindowInfo>
    {
        /// <summary>Blank columns left between the calendar and the readout beside it.</summary>
        private const int ReadoutGap = 3;

        /// <summary>Narrowest the readout column may get before it is stacked underneath instead of shredded.</summary>
        private const int MinimumReadoutWidth = 30;

        /// <summary>How many columns the readout's label column takes, so its values line up under each other.</summary>
        private const int LabelWidth = 8;

        /// <summary>The screen's heading.</summary>
        private const string Heading = "MonthGrid: drawn and hit-tested from the same arithmetic";

        /// <summary>What every key does, said once under the calendar rather than left to be discovered.</summary>
        private const string Hints = "ARROWS move a day or a week, PGUP/PGDN page the month, SPACE marks a day, " +
                                     "M marks weekends, W changes the week start, T pins today, ENTER returns.";

        /// <summary>
        ///     The control's own rules, drawn under it at full width. Each one is a thing this screen can be made
        ///     to do rather than a claim about it, so a reader has somewhere to point the keys at.
        /// </summary>
        private static readonly string[] _rules =
        {
            "Six weeks always: February leaves a blank row so the grid never changes height.",
            "Blank cells, never the neighbours' days: a click on one answers null and stays.",
            "Today is told: this screen retells it once a second; T pins it to the cursor.",
            "Marked is a predicate asked per day drawn: SPACE and M change what it answers.",
            "W starts the week on Monday, which moves every column and the hit test with it."
        };

        /// <summary>The heading. Bold and a mid cyan, both legible on a light terminal and a dark one.</summary>
        private static readonly TextStyle _headingStyle = new TextStyle(new TextColor(ConsoleColor.DarkCyan), null,
            true);

        /// <summary>The readout's label column, which should read as quieter than the values beside it.</summary>
        private static readonly TextStyle _labelStyle = new TextStyle(new TextColor(ConsoleColor.DarkCyan));

        /// <summary>The rules and the key hints, which are the least urgent thing on the screen.</summary>
        private static readonly TextStyle _dimStyle = new TextStyle(new TextColor(ConsoleColor.DarkGray));

        /// <summary>
        ///     The days the user has picked out by hand. The grid is never told about this set: it holds the
        ///     predicate that reads it, so adding a day here is the whole of adding a mark.
        /// </summary>
        private readonly HashSet<DateOnly> _marks = new HashSet<DateOnly>();

        /// <summary>
        ///     The month.
        ///     <para>
        ///         <see cref="MonthGrid.SelectedStyle" /> is deliberately left unset so the cursor is drawn in the
        ///         control's own inverse-video fallback. That fallback goes silent where colour does, which is the
        ///         honest behaviour rather than a bug and is why the readout names the cursor's date in words: on a
        ///         terminal reporting no colour the marks (a glyph) survive and the highlight (a style) does not.
        ///     </para>
        /// </summary>
        private readonly MonthGrid _grid = new MonthGrid
        {
            HeaderStyle = ConsoleColor.DarkCyan,
            MarkedStyle = ConsoleColor.Magenta,
            TodayStyle = new TextStyle(new TextColor(ConsoleColor.DarkYellow), null, true)
        };

        /// <summary>The frame around the calendar, titled with the month so paging says where it went.</summary>
        private readonly Box _frame = new Box
        {
            BorderStyle = ConsoleColor.DarkGray,
            TitleStyle = new TextStyle(bold: true)
        };

        /// <summary>What the last click did, in the control's own terms.</summary>
        private string _clicked = string.Empty;

        /// <summary>Whether today is pinned to a day of the caller's choosing rather than refreshed from the clock.</summary>
        private bool _pinnedToday;

        /// <summary>The whole frame, composed off a tick and handed back unchanged by every render between.</summary>
        private string _rendered = string.Empty;

        /// <summary>The day the cursor is on, which is the grid's <see cref="MonthGrid.Selected" />.</summary>
        private DateOnly _selected;

        /// <summary>Whether the weekend rule is switched on inside the predicate the grid holds.</summary>
        private bool _weekendsMarked = true;

        /// <summary>Initializes a new instance of the <see cref="CalendarDialog" /> class.</summary>
        /// <param name="window">The parent window.</param>
        // ReSharper disable once UnusedMember.Global
        public CalendarDialog(IWindow window) : base(window)
        {
        }

        /// <inheritdoc />
        public override void OnFormPostCreate()
        {
            base.OnFormPostCreate();

            var today = DateOnly.FromDateTime(DateTime.Now);

            _selected = today;
            _grid.Selected = today;
            _grid.Today = today;
            _grid.Show(today);

            // Handed over here and never again. Everything the keys do below changes what this closure answers,
            // and none of it goes anywhere near the grid: the predicate is asked per day drawn, so a caller that
            // adds something to a day has nothing left to remember to do about it. A cached set of marked days
            // would put that job back, and it is the half that gets forgotten.
            _grid.Marked = day => _marks.Contains(day) ||
                                  (_weekendsMarked && (day.DayOfWeek == DayOfWeek.Saturday ||
                                                       day.DayOfWeek == DayOfWeek.Sunday));

            _clicked = AnsiConsole.MouseEnabled
                ? "nothing yet: try a day, then a blank"
                : "no mouse on this terminal";

            ParentWindow.PromptText = AnsiConsole.MouseEnabled
                ? "Click a day, arrows move, PGUP/PGDN page months, SPACE/M/W/T retell the grid, ENTER for the menu"
                : "Arrows move, PGUP/PGDN page months, SPACE/M/W/T retell the grid, ENTER for the menu";

            Compose();
        }

        /// <inheritdoc />
        public override void OnKeyPressed(ConsoleKey key)
        {
            base.OnKeyPressed(key);

            // Arrow and paging keys carry no character, so they never land in the input buffer and can only arrive
            // here. ENTER and BACKSPACE never do arrive: the input manager spends both on the buffer before a key
            // press is queued, so a case for either below would compile and never run. ENTER reaches this screen as
            // OnInputBufferReturned instead, which is where leaving is handled.
            switch (key)
            {
                case ConsoleKey.LeftArrow:
                    MoveCursor(-1);
                    break;
                case ConsoleKey.RightArrow:
                    MoveCursor(1);
                    break;
                case ConsoleKey.UpArrow:
                    MoveCursor(-MonthGrid.DaysInWeek);
                    break;
                case ConsoleKey.DownArrow:
                    MoveCursor(MonthGrid.DaysInWeek);
                    break;
                case ConsoleKey.PageUp:
                    // The month moves and the cursor does not, deliberately: the readout then says the selected day
                    // is not on the month being shown, because TryPositionOf answers false, and nothing is drawn
                    // picked out. Dragging the cursor along would hide that answer, and it is the one a caller has
                    // to have handled before it happens for real.
                    _grid.MoveMonths(-1);
                    break;
                case ConsoleKey.PageDown:
                    _grid.MoveMonths(1);
                    break;
                case ConsoleKey.Spacebar:
                    ToggleMark();
                    break;
                case ConsoleKey.M:
                    _weekendsMarked = !_weekendsMarked;
                    break;
                case ConsoleKey.W:
                    // Every column moves. A hit test written by hand against a Sunday-first assumption lands on the
                    // wrong day from here on; the grid's own DayAt cannot, because the layout it was drawn from is
                    // the layout it is asked about.
                    _grid.FirstDayOfWeek = _grid.FirstDayOfWeek == DayOfWeek.Sunday
                        ? DayOfWeek.Monday
                        : DayOfWeek.Sunday;
                    break;
                case ConsoleKey.T:
                    PinToday();
                    break;
                default:
                    return;
            }

            // SPACE and the letters are printable, so each one lands in the input buffer as well as reaching this
            // handler - the two input paths working as designed. Scrubbing the buffer keeps the echoed prompt clean
            // without the screen having to refuse typed text outright, which would cost it ENTER.
            SimUnit?.InputManager?.ClearBuffer();

            Compose();
        }

        /// <summary>
        ///     Picks the day the pointer landed on, or says why it did not.
        ///     <para>
        ///         <b>A press arrives here and not at <c>OnMouseEvent</c>.</b> <c>Window.OnMouseEvent</c> hands a
        ///         press to this method and returns, so a hit test written in the other override compiles and runs
        ///         never. Move and release are the ones that go there, and this screen wants neither.
        ///     </para>
        ///     <para>
        ///         The hit test is the grid's own, which is the entire justification for the control: it answers
        ///         from the layout it drew rather than from a second copy of the arithmetic that agrees with the
        ///         drawing until the day somebody changes the cell width or the first day of the week.
        ///     </para>
        /// </summary>
        /// <param name="mouse">Where the press landed and which button it was.</param>
        public override void OnMousePressed(MouseEvent mouse)
        {
            base.OnMousePressed(mouse);

            if (mouse.Button != MouseButtonEnum.Left)
                return;

            var day = _grid.DayAt(mouse.Row, mouse.Column);

            if (day == null)
            {
                // Two different nulls, said apart because they teach different things: off the grid entirely, and
                // one of the blank cells either side of the month. The second is the interesting one - that cell is
                // drawn, it is inside the rectangle, and it still refuses to be a day, so the cursor cannot be
                // moved into a month nobody is looking at.
                _clicked = string.Format(CultureInfo.InvariantCulture, "row {0} col {1} {2}", mouse.Row,
                    mouse.Column, InsideGrid(mouse.Row, mouse.Column) ? "blank: null, no move" : "off the grid");
            }
            else
            {
                _selected = day.Value;
                _grid.Selected = _selected;

                _clicked = string.Format(CultureInfo.InvariantCulture, "row {0} col {1} picked {2}", mouse.Row,
                    mouse.Column, _selected.ToString("d MMM", CultureInfo.InvariantCulture));
            }

            Compose();
        }

        /// <inheritdoc />
        public override void OnTick(bool systemTick, bool skipDay)
        {
            base.OnTick(systemTick, skipDay);

            // Once a second, not on the thousand system ticks between. This is the caller keeping today fresh, and
            // it is the whole of what the grid asked for by refusing to read the clock itself: a program left open
            // past midnight moves its highlight because this line runs, not because anything noticed.
            if (systemTick)
                return;

            if (!_pinnedToday)
                _grid.Today = DateOnly.FromDateTime(DateTime.Now);

            // Composed here as well so a console resized behind the screen is picked up on the next beat.
            Compose();
        }

        /// <inheritdoc />
        public override string OnRenderForm()
        {
            // A pure read of what the tick and the key handlers built. The scene graph calls this on every system
            // tick, about a thousand times a second, and composing in here would rebuild the calendar every one of
            // them for a screen that changes when a key is pressed.
            return _rendered;
        }

        /// <inheritdoc />
        public override void OnInputBufferReturned(string input)
        {
            ClearForm();
        }

        /// <summary>
        ///     Moves the cursor and lets the month follow it, which is the second way to page and the one that
        ///     shows the six rows holding still while the dates under them change.
        /// </summary>
        /// <param name="days">How many days to move; negative goes back.</param>
        private void MoveCursor(int days)
        {
            // Clamped against what a date can hold rather than left to throw at the ends of time. The grid clamps
            // its own month the same way, which is what makes walking off either end of the calendar boring.
            var target = Math.Clamp(_selected.DayNumber + days, DateOnly.MinValue.DayNumber,
                DateOnly.MaxValue.DayNumber);

            _selected = DateOnly.FromDayNumber(target);
            _grid.Selected = _selected;

            if (_selected.Year != _grid.Year || _selected.Month != _grid.Month)
                _grid.Show(_selected);
        }

        /// <summary>Marks the day under the cursor, or takes the mark off it.</summary>
        private void ToggleMark()
        {
            if (!_marks.Remove(_selected))
                _marks.Add(_selected);
        }

        /// <summary>
        ///     Pins today to the day under the cursor, or hands it back to the wall clock.
        ///     <para>
        ///         Nothing checks that the pinned day is anywhere near today, and that is the point being made: the
        ///         grid draws whatever it was told, so a test can put any date in front of it and a screen can move
        ///         it deliberately. A control that read the clock itself could do neither.
        ///     </para>
        /// </summary>
        private void PinToday()
        {
            _pinnedToday = !_pinnedToday;
            _grid.Today = _pinnedToday ? _selected : DateOnly.FromDateTime(DateTime.Now);
        }

        /// <summary>Whether a cell is inside the drawn grid, which is not the same question as whether it is a day.</summary>
        /// <param name="row">The screen row.</param>
        /// <param name="column">The screen column.</param>
        /// <returns>TRUE when that cell is one of the six weeks' worth the grid draws.</returns>
        private bool InsideGrid(int row, int column)
        {
            return row > _grid.Row && row <= _grid.Row + MonthGrid.Weeks &&
                   column >= _grid.Column && column < _grid.Column + _grid.Width;
        }

        /// <summary>
        ///     Builds the whole frame, and positions the grid while it is building it.
        ///     <para>
        ///         <b>The grid's row is counted from the builder, never written down.</b> A constant is right until
        ///         somebody adds a line to the heading, after which every click lands one row out and nothing says
        ///         so. The one number that cannot be counted from here is how many rows sit above the form, which
        ///         is measured instead.
        ///     </para>
        /// </summary>
        private void Compose()
        {
            var width = Math.Max(40, AnsiConsole.SafeWindowWidth() - 1);
            var sb = new StringBuilder();

            sb.AppendLine();
            sb.AppendLine(_headingStyle.Apply(Heading));
            sb.AppendLine();

            // The day-name row sits one row below the box's top edge, and the dates start one column past its left
            // border because the box carries no padding. Both halves have to be told to the grid before anything
            // asks it where a date is drawn or which date a cell holds.
            _grid.Row = FrameLayout.CountLineBreaks(sb) + 1 + FrameLayout.RowsAboveForm(SimUnit);
            _grid.Column = 1;

            _frame.Title = _grid.Title;

            var calendar = _frame.Render(string.Join(Environment.NewLine, _grid.Render()));
            var readoutWidth = width - (_grid.Width + 2) - ReadoutGap;
            var beside = readoutWidth >= MinimumReadoutWidth;

            sb.AppendLine(beside
                ? TextColumns.Join(ReadoutGap, calendar, Readout(readoutWidth))
                : calendar + Environment.NewLine + Readout(width));

            sb.AppendLine();

            // Rows are the binding budget here, not columns, and the arithmetic is worth spelling out because the
            // row it costs is invisible: the presenter draws only the first height-1 rows, the scene graph puts the
            // prompt below this form with a blank row of its own between, and the form's first line is not row
            // zero. Overrun by one and what falls off the bottom is the line the user is typing into, which is the
            // same bug menu column reflow exists to fix. So the rules go in while whole ones fit, and the key hints
            // always do: a screen whose keys are undocumented is worse off than one whose reasons are.
            var hints = Wrap(_dimStyle, Hints, width);
            var spare = AnsiConsole.SafeWindowHeight() - 3 - FrameLayout.RowsAboveForm(SimUnit) - FrameLayout.CountLineBreaks(sb) - RowsIn(hints);

            foreach (var rule in _rules)
            {
                var wrapped = Wrap(_dimStyle, rule, width);
                var rows = RowsIn(wrapped);

                if (rows > spare)
                    break;

                sb.AppendLine(wrapped);
                spare -= rows;
            }

            sb.Append(hints);

            _rendered = sb.ToString();
        }

        /// <summary>
        ///     Everything the grid has been told, read back out of it. It is here because most of it is invisible
        ///     without colour, and because a screen about a hit test should say which cell it thinks the cursor is
        ///     drawn in.
        /// </summary>
        /// <param name="width">How many columns the readout has to itself.</param>
        /// <returns>The block, one label and value per row.</returns>
        private string Readout(int width)
        {
            // Where the cursor is drawn, asked of the grid rather than worked out here - and the answer is
            // sometimes that it is not drawn at all, which is what paging past it does.
            var cursor = _grid.TryPositionOf(_selected, out var row, out var column)
                ? string.Format(CultureInfo.InvariantCulture, "{0}, row {1} col {2}", Spell(_selected), row, column)
                : string.Format(CultureInfo.InvariantCulture, "{0}, not on this month", Spell(_selected));

            var told = _grid.Today == null
                ? "nothing"
                : string.Format(CultureInfo.InvariantCulture, "{0}, {1}",
                    _grid.Today.Value.ToString("d MMM yyyy", CultureInfo.InvariantCulture),
                    _pinnedToday ? "pinned by T" : "retold each second");

            var marks = string.Format(CultureInfo.InvariantCulture, "{0} by hand{1}", _marks.Count,
                _weekendsMarked ? " + weekends" : string.Empty);

            var weeks = string.Format(CultureInfo.InvariantCulture, "fills {0} of the {1} always drawn", WeeksUsed(),
                MonthGrid.Weeks);

            var rows = new[]
            {
                Field("cursor", cursor, width),
                Field("today", told, width),
                Field("marks", marks, width),
                Field("week", "starts " + _grid.FirstDayOfWeek, width),
                Field("weeks", weeks, width),
                Field("click", _clicked, width)
            };

            return string.Join(Environment.NewLine, rows);
        }

        /// <summary>How many week rows this month actually fills, which is what the six always drawn hide.</summary>
        /// <returns>Four, five or six.</returns>
        private int WeeksUsed()
        {
            // FirstCell is public precisely so a caller can check this arithmetic rather than rebuild it: it is the
            // date in the very first cell, so the gap back to the first of the month is the run of leading blanks.
            var leading = _grid.FirstOfMonth.DayNumber - _grid.FirstCell.DayNumber;
            var days = DateTime.DaysInMonth(_grid.Year, _grid.Month);

            return (leading + days + MonthGrid.DaysInWeek - 1)/MonthGrid.DaysInWeek;
        }

        /// <summary>One labelled row of the readout, cut to the column it has rather than allowed to widen it.</summary>
        /// <param name="label">The label.</param>
        /// <param name="value">What to show against it.</param>
        /// <param name="width">How many columns the row may take.</param>
        /// <returns>The row.</returns>
        private static string Field(string label, string value, int width)
        {
            // Fit pads the label to an exact visible width and Slice trims the finished row to one, both of which
            // count columns rather than characters - which is the only reason a styled label can be measured at all.
            return AnsiText.Slice(_labelStyle.Apply(AnsiText.Fit(label, LabelWidth)) + value, 0, width);
        }

        /// <summary>A date written out the way the readout wants it.</summary>
        /// <param name="date">The date.</param>
        /// <returns>The text.</returns>
        private static string Spell(DateOnly date)
        {
            return date.ToString("ddd d MMM yyyy", CultureInfo.InvariantCulture);
        }

        /// <summary>
        ///     Word-wraps prose to the console and styles it one produced row at a time.
        ///     <para>
        ///         Wrap first, style second. An escape has length but no width, so wrapping already-styled text
        ///         spends the budget on bytes, and a style opened before the wrap bleeds across the newline the
        ///         wrap inserted.
        ///     </para>
        /// </summary>
        /// <param name="style">The style each produced row is wrapped in.</param>
        /// <param name="text">The prose.</param>
        /// <param name="width">The widest a row may be.</param>
        /// <returns>The styled rows, with no trailing newline.</returns>
        private static string Wrap(TextStyle style, string text, int width)
        {
            var sb = new StringBuilder();

            foreach (var line in text.WordWrap(width).Split(Environment.NewLine))
            {
                if (line.Length == 0)
                    continue;

                if (sb.Length > 0)
                    sb.AppendLine();

                sb.Append(style.Apply(line));
            }

            return sb.ToString();
        }

        /// <summary>How many screen rows a finished block of text occupies.</summary>
        /// <param name="text">The block.</param>
        /// <returns>The count, which is one more than the breaks inside it.</returns>
        private static int RowsIn(string text)
        {
            var rows = 1;

            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                    rows++;
            }

            return rows;
        }
    }
}
