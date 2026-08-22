// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using WolfCurses.Controls;
using WolfCurses.Graphics;
using WolfCurses.Window;
using WolfCurses.Window.Control;
using WolfCurses.Window.Form;

namespace WolfCurses.Apps.Planner
{
    /// <summary>
    ///     A calendar and planner: a month you can walk around, a week written out, a whole year at a glance, a
    ///     list of what is coming, and a clock that moves while you look at any of them.
    ///     <para>
    ///         This is the screen in the suite that is about <b>time passing</b>. Everything else here redraws
    ///         because somebody pressed a key; this one changes on its own, and both of the changing things are
    ///         traps. The clock is sampled on the <i>simulation</i> tick and not while drawing, because a render
    ///         runs about a thousand times a second and would ask the operating system for the time as often. And
    ///         <b>today is re-asked every second too</b>, or a planner left open overnight goes on highlighting
    ///         yesterday until somebody restarts it.
    ///     </para>
    ///     <para>
    ///         <b>The four views are a zoom, and the arrow keys mean something different in each.</b> That is
    ///         deliberate rather than inconsistent: stepping a day at a time through a year is useless, and
    ///         stepping a month at a time through a week is meaningless. What stays the same everywhere is that
    ///         there is exactly one chosen day, and every view shows where it is.
    ///     </para>
    /// </summary>
    [ParentWindow(typeof (OfficeWindow))]
    public sealed class PlannerDialog : Form<OfficeWindowInfo>, IHandlesEscape
    {
        /// <summary>How far ahead the list looks, which is a year and a day so an annual entry always turns up.</summary>
        private const int AgendaDays = 366;

        /// <summary>The month on show, which also knows where it was drawn.</summary>
        private readonly MonthGrid _grid = new();

        /// <summary>Everything from the chosen day onwards, worked out when something changes rather than per frame.</summary>
        private readonly List<PlannerEntryLine> _agenda = new();

        /// <summary>The pull-down menus across the top.</summary>
        private MenuBar _menuBar;

        /// <summary>Everything in the planner.</summary>
        private PlannerDiary _diary = new();

        /// <summary>Which way the planner is being looked at.</summary>
        private PlannerViewEnum _view = PlannerViewEnum.Month;

        /// <summary>How far the scrolling views have been scrolled.</summary>
        private int _scroll;

        /// <summary>The day the cursor is on.</summary>
        private DateOnly _selected;

        /// <summary>
        ///     The day the list starts from.
        ///     <para>
        ///         <b>Kept apart from the cursor, and that is the whole of making the list navigable.</b> Building
        ///         it from wherever the cursor happens to be means the cursor is always on the first line, so
        ///         stepping backwards has nowhere to go and the list can only ever be walked forwards. It moves
        ///         only when the cursor leaves the stretch of days the list covers.
        ///     </para>
        /// </summary>
        private DateOnly _agendaFrom;

        /// <summary>The wall clock, sampled once a second rather than read while drawing.</summary>
        private DateTime _now;

        /// <summary>The file the planner came from, or null for one that has never been on disk.</summary>
        private string _path;

        /// <summary>What the status strip has to say, when it is not listing the keys.</summary>
        private string _message;

        /// <summary>Initializes a new instance of the <see cref="PlannerDialog" /> class.</summary>
        /// <param name="window">The parent window.</param>
        public PlannerDialog(IWindow window) : base(window)
        {
        }

        /// <summary>
        ///     ENTER arrives as a key press rather than being spent on the input buffer, which is the only way the
        ///     menu bar can be chosen from with the keyboard: its own ENTER handling is unreachable otherwise, so
        ///     without this the menus open, walk with the arrows and do nothing at the end of it.
        /// </summary>
        public override bool EditsText => true;

        /// <summary>Typed characters do not go into the prompt underneath, since none of them are text here.</summary>
        public override bool InputFillsBuffer => false;

        /// <inheritdoc />
        public bool TryHandleEscape()
        {
            if (_menuBar == null || !_menuBar.IsOpen)
                return false;

            _menuBar.Close();
            return true;
        }

        /// <inheritdoc />
        public override void OnFormPostCreate()
        {
            base.OnFormPostCreate();

            BuildMenus();

            // Sampled here as well as on the tick, or the first frame is drawn against a date of year one.
            SampleClock();
            _selected = DateOnly.FromDateTime(_now);

            _grid.Show(_selected);
            _grid.Selected = _selected;
            _grid.Row = PlannerChrome.GridRow;
            _grid.Column = PlannerChrome.GridColumn;
            _grid.Marked = date => _diary.HasAnythingOn(date);
            _grid.HeaderStyle = DosTheme.Title;
            _grid.DayStyle = DosTheme.Field;
            _grid.MarkedStyle = DosTheme.Frame;
            _grid.TodayStyle = DosTheme.Highlight;
            _grid.SelectedStyle = DosTheme.Selection;

            LoadPlanner(PlannerLibrary.DefaultPlannerPath);
        }

        /// <inheritdoc />
        public override void OnTick(bool systemTick, bool skipDay)
        {
            base.OnTick(systemTick, skipDay);

            // Once a second, which is exactly the resolution a clock showing seconds needs and about a thousand
            // times less often than a render.
            if (!systemTick)
                SampleClock();
        }

        /// <inheritdoc />
        public override string OnRenderForm()
        {
            ParentWindow.PromptText = "F10 opens the menus, ESC returns to the suite:";

            var width = Math.Max(24, AnsiConsole.SafeWindowWidth() - 1);

            return Environment.NewLine +
                   PlannerChrome.Compose(_menuBar, _view, _grid, _diary, _selected, _agenda, _scroll, _now,
                       StatusText(), width);
        }

        /// <summary>
        ///     Reads the wall clock and tells the grid what today is.
        ///     <para>
        ///         Telling it every second rather than once at start-up is what makes a planner left open past
        ///         midnight move its own highlight, which is the whole reason the grid takes today rather than
        ///         reading it.
        ///     </para>
        /// </summary>
        private void SampleClock()
        {
            _now = DateTime.Now;
            _grid.Today = DateOnly.FromDateTime(_now);
        }

        /// <summary>
        ///     Never called: <see cref="EditsText" /> is precisely the declaration that ENTER should arrive as a
        ///     key press instead, which is where the menu bar hears it.
        /// </summary>
        /// <param name="input">Unused.</param>
        public override void OnInputBufferReturned(string input)
        {
        }

        /// <inheritdoc />
        public override void OnKeyPressed(ConsoleKeyInfo keyInfo)
        {
            base.OnKeyPressed(keyInfo);

            if (_menuBar != null && _menuBar.HandleKey(keyInfo))
                return;

            switch (keyInfo.Key)
            {
                case ConsoleKey.Tab:
                    ShowView((PlannerViewEnum) (((int) _view + 1) % 4));
                    return;

                case ConsoleKey.LeftArrow:
                    MoveDays(-1);
                    return;

                case ConsoleKey.RightArrow:
                    MoveDays(1);
                    return;

                case ConsoleKey.UpArrow:
                    StepBack();
                    return;

                case ConsoleKey.DownArrow:
                    StepOn();
                    return;

                case ConsoleKey.PageUp:
                    MoveMonths(-1);
                    return;

                case ConsoleKey.PageDown:
                    MoveMonths(1);
                    return;

                case ConsoleKey.Home:
                    GoTo(DateOnly.FromDateTime(_now));
                    _message = "Back to today.";
                    return;

                case ConsoleKey.F2:
                    AskWhatHappens();
                    return;

                case ConsoleKey.F3:
                    OpenPlanner();
                    return;

                case ConsoleKey.F4:
                    SavePlanner();
                    return;

                case ConsoleKey.F5:
                    ShowView(PlannerViewEnum.Month);
                    return;

                case ConsoleKey.F6:
                    ShowView(PlannerViewEnum.Week);
                    return;

                case ConsoleKey.F7:
                    ShowView(PlannerViewEnum.Year);
                    return;

                case ConsoleKey.F8:
                    ShowView(PlannerViewEnum.Agenda);
                    return;

                case ConsoleKey.Delete:
                    AskWhatToRemove();
                    return;
            }
        }

        /// <summary>
        ///     What UP means, which is a different thing in each view. A week back in a month, a month back in a
        ///     year, and the entry before this one in a list, where stepping through empty days would be useless.
        /// </summary>
        private void StepBack()
        {
            switch (_view)
            {
                case PlannerViewEnum.Year:
                    MoveMonths(-1);
                    return;

                case PlannerViewEnum.Agenda:
                    StepAgenda(-1);
                    return;

                default:
                    MoveDays(-MonthGrid.DaysInWeek);
                    return;
            }
        }

        /// <summary>What DOWN means, which is the mirror of <see cref="StepBack" />.</summary>
        private void StepOn()
        {
            switch (_view)
            {
                case PlannerViewEnum.Year:
                    MoveMonths(1);
                    return;

                case PlannerViewEnum.Agenda:
                    StepAgenda(1);
                    return;

                default:
                    MoveDays(MonthGrid.DaysInWeek);
                    return;
            }
        }

        /// <summary>Moves to the entry before or after the one the cursor is on.</summary>
        /// <param name="step">Which way; negative goes back.</param>
        private void StepAgenda(int step)
        {
            if (_agenda.Count == 0)
                return;

            var first = -1;
            var last = -1;

            for (var i = 0; i < _agenda.Count; i++)
            {
                if (_agenda[i].Date != _selected)
                    continue;

                if (first < 0)
                    first = i;

                last = i;
            }

            // Off the last line of the chosen day going forward and the first going back, so a day with three
            // things on it is stepped over rather than stepped through three times.
            var from = step > 0 ? last : first;

            var target = Math.Clamp(Math.Max(0, from) + step, 0, _agenda.Count - 1);

            GoTo(_agenda[target].Date);
            _message = null;
        }

        /// <inheritdoc />
        public override void OnMouseEvent(MouseEvent mouse)
        {
            if (mouse.Kind == MouseEventKindEnum.Press)
            {
                OnMousePressed(mouse);
                return;
            }

            if (mouse.Kind == MouseEventKindEnum.Wheel)
            {
                Scroll(-mouse.WheelDelta * 3);
                return;
            }

            if (mouse.Kind != MouseEventKindEnum.Move)
                return;

            if (_menuBar != null)
                _menuBar.HandleMouseMove(mouse.Row, mouse.Column);
        }

        /// <inheritdoc />
        public override void OnMousePressed(MouseEvent mouse)
        {
            base.OnMousePressed(mouse);

            if (_menuBar != null && _menuBar.HandleMouse(mouse.Row, mouse.Column))
                return;

            if (mouse.Button != MouseButtonEnum.Left)
                return;

            // Each view answers for its own layout, which is what keeps the drawing and the hit test in one file
            // per view rather than in one place that has to know all four.
            var day = _view switch
            {
                PlannerViewEnum.Week => PlannerWeekView.DayAt(_diary, _selected, _scroll, PlannerChrome.BodyRows,
                    mouse.Row),
                PlannerViewEnum.Year => PlannerYearView.DayAt(_selected.Year, mouse.Row, mouse.Column),
                PlannerViewEnum.Agenda => PlannerAgendaView.DayAt(_agenda, _scroll, PlannerChrome.BodyRows,
                    mouse.Row),
                _ => _grid.DayAt(mouse.Row, mouse.Column)
            };

            if (day == null)
                return;

            GoTo(day.Value);
            _message = null;
        }

        /// <summary>Scrolls the views that scroll, and does nothing to the ones that do not.</summary>
        /// <param name="lines">How far; negative goes up.</param>
        private void Scroll(int lines)
        {
            if (_view != PlannerViewEnum.Week && _view != PlannerViewEnum.Agenda)
                return;

            _scroll = Math.Max(0, _scroll + lines);
        }

        /// <summary>Switches which way the planner is being looked at.</summary>
        /// <param name="view">The view to show.</param>
        private void ShowView(PlannerViewEnum view)
        {
            _view = view;
            _message = null;
            _scroll = 0;

            // Coming into the list starts it where you are, which is what "coming up" means.
            _agendaFrom = _selected;

            RefreshAgenda();
            Reveal();
        }

        /// <summary>Moves the cursor by a number of days, following it into the next month when it goes there.</summary>
        /// <param name="days">How many days; negative goes back.</param>
        private void MoveDays(int days)
        {
            var target = _selected.DayNumber + days;

            if (target < DateOnly.MinValue.DayNumber || target > DateOnly.MaxValue.DayNumber)
                return;

            GoTo(_selected.AddDays(days));
            _message = null;
        }

        /// <summary>
        ///     Pages the month, keeping the cursor on the same day number where that month has one.
        ///     <para>
        ///         The thirty-first of a month paged into February has nowhere to land, so the cursor comes to rest
        ///         on the last day there is rather than spilling into March, which is what plain date arithmetic
        ///         would do and would look like the page key skipping a month.
        ///     </para>
        /// </summary>
        /// <param name="months">How many months; negative goes back.</param>
        private void MoveMonths(int months)
        {
            _grid.MoveMonths(months);

            var days = DateTime.DaysInMonth(_grid.Year, _grid.Month);

            GoTo(new DateOnly(_grid.Year, _grid.Month, Math.Min(_selected.Day, days)));
            _message = null;
        }

        /// <summary>Puts the cursor on a day, brings its month into view, and scrolls it into sight.</summary>
        /// <param name="date">The day.</param>
        private void GoTo(DateOnly date)
        {
            var moved = _selected != date;

            _selected = date;
            _grid.Selected = date;

            if (_grid.Year != date.Year || _grid.Month != date.Month)
                _grid.Show(date);

            // The list only starts again when the cursor has walked off the end of what it covers. Re-anchoring on
            // every move would put the cursor back on the first line each time, which is what stops it going back.
            if (date < _agendaFrom || date.DayNumber - _agendaFrom.DayNumber >= AgendaDays)
                _agendaFrom = date;

            if (moved)
                RefreshAgenda();

            Reveal();
        }

        /// <summary>
        ///     Works out what is coming up from the chosen day.
        ///     <para>
        ///         Done when something changes rather than while drawing, because it walks a year of days and a
        ///         render happens about a thousand times a second. The same reasoning as sampling the clock on the
        ///         tick, and the reason the list view is handed its lines rather than finding them.
        ///     </para>
        /// </summary>
        private void RefreshAgenda()
        {
            _agenda.Clear();

            if (_view != PlannerViewEnum.Agenda)
                return;

            for (var offset = 0; offset < AgendaDays; offset++)
            {
                if (_agendaFrom.DayNumber + offset > DateOnly.MaxValue.DayNumber)
                    break;

                var date = _agendaFrom.AddDays(offset);

                foreach (var entry in _diary.On(date))
                    _agenda.Add(new PlannerEntryLine(date, entry));
            }
        }

        /// <summary>Scrolls whichever view scrolls so that the chosen day is on screen.</summary>
        private void Reveal()
        {
            var visible = Math.Max(1, PlannerChrome.BodyRows - 2);

            switch (_view)
            {
                case PlannerViewEnum.Week:
                    _scroll = Math.Clamp(_scroll, Math.Max(0, PlannerWeekView.LineOf(_diary, _selected) - visible + 1),
                        PlannerWeekView.LineOf(_diary, _selected));
                    return;

                case PlannerViewEnum.Agenda:
                    var at = 0;

                    for (var i = 0; i < _agenda.Count; i++)
                    {
                        if (_agenda[i].Date != _selected)
                            continue;

                        at = i;
                        break;
                    }

                    _scroll = Math.Clamp(_scroll, Math.Max(0, at - visible + 1), at);
                    return;

                default:
                    _scroll = 0;
                    return;
            }
        }

        /// <summary>Asks what happens on the chosen day, and then when.</summary>
        private void AskWhatHappens()
        {
            var day = _selected;

            TextInputDialog.Prompt(
                SimUnit,
                "What happens on " + day.ToString("d MMMM yyyy", CultureInfo.InvariantCulture) + "?",
                title => TextInputDialog.Prompt(
                    SimUnit,
                    "At what time? Leave it blank for all day.",
                    time => Add(day, time, title),

                    // Cancelling the second question still adds the entry, as an all-day one. Throwing away
                    // something already typed because the optional half was declined would be the worse answer.
                    () => Add(day, string.Empty, title)),
                () => _message = "Nothing added.");
        }

        /// <summary>Adds an entry.</summary>
        /// <param name="day">The day.</param>
        /// <param name="time">When, or empty for all day.</param>
        /// <param name="title">What it is.</param>
        private void Add(DateOnly day, string time, string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                _message = "Nothing added.";
                return;
            }

            _diary.Add(new PlannerEvent(day, time, title));

            RefreshAgenda();
            _message = "Added to " + day.ToString("d MMMM", CultureInfo.InvariantCulture) + ".";
        }

        /// <summary>
        ///     Asks which of the day's entries to remove.
        ///     <para>
        ///         Holidays are not offered, because there is nothing to remove: they are worked out from the year
        ///         rather than stored, so next year's would come back regardless.
        ///     </para>
        /// </summary>
        private void AskWhatToRemove()
        {
            var removable = new List<PlannerEvent>();

            foreach (var entry in _diary.On(_selected))
            {
                if (entry.Kind == PlannerEventKindEnum.Personal)
                    removable.Add(entry);
            }

            if (removable.Count == 0)
            {
                _message = "Nothing on that day to remove.";
                return;
            }

            var labels = new List<string>(removable.Count);

            foreach (var entry in removable)
                labels.Add(entry.ToString());

            SelectList.Choose(
                SimUnit,
                "Remove which?",
                labels,
                chosen =>
                {
                    if (chosen < 0 || chosen >= removable.Count)
                        return;

                    _diary.Remove(removable[chosen]);

                    RefreshAgenda();
                    _message = "Removed.";
                },
                () => _message = "Nothing removed.");
        }

        /// <summary>Builds the pull-downs.</summary>
        private void BuildMenus()
        {
            _menuBar = new MenuBar(
                new MenuBarMenu("File",
                    new MenuBarEntry("Open...", OpenPlanner, "F3"),
                    new MenuBarEntry("Save", SavePlanner, "F4"),
                    new MenuBarEntry("Save As...", SavePlannerAs),
                    MenuBarEntry.Separator(),
                    new MenuBarEntry("Exit", () => ParentWindow.ClearForm(), "Esc")),
                new MenuBarMenu("Edit",
                    new MenuBarEntry("Add...", AskWhatHappens, "F2"),
                    new MenuBarEntry("Remove...", AskWhatToRemove, "Del")),
                new MenuBarMenu("View",
                    // Marked rather than four entries that say nothing about which one you are looking at, the
                    // same reasoning as the word processor's tab widths.
                    new MenuBarEntry("Month", () => ShowView(PlannerViewEnum.Month), "F5")
                        {CheckedWhen = () => _view == PlannerViewEnum.Month},
                    new MenuBarEntry("Week", () => ShowView(PlannerViewEnum.Week), "F6")
                        {CheckedWhen = () => _view == PlannerViewEnum.Week},
                    new MenuBarEntry("Year", () => ShowView(PlannerViewEnum.Year), "F7")
                        {CheckedWhen = () => _view == PlannerViewEnum.Year},
                    new MenuBarEntry("Coming Up", () => ShowView(PlannerViewEnum.Agenda), "F8")
                        {CheckedWhen = () => _view == PlannerViewEnum.Agenda},
                    MenuBarEntry.Separator(),
                    new MenuBarEntry("Today", () => GoTo(DateOnly.FromDateTime(_now)), "Home"),
                    new MenuBarEntry("Week Starts Monday", ToggleFirstDay)
                        {CheckedWhen = () => _grid.FirstDayOfWeek == DayOfWeek.Monday}),
                new MenuBarMenu("Help",
                    new MenuBarEntry("About", ShowAbout)) {AlignRight = true})
            {
                BarStyle = DosTheme.MenuBar,
                HighlightStyle = DosTheme.MenuHighlight,
                PanelStyle = DosTheme.MenuPanel,
                PanelHighlightStyle = DosTheme.MenuHighlight,
                DisabledStyle = DosTheme.MenuDisabled,
                CheckMark = '√',
                BarRow = PlannerChrome.BarRow,
                PanelRow = PlannerChrome.BodyRow
            };
        }

        /// <summary>Switches the week between starting on Sunday and starting on Monday.</summary>
        private void ToggleFirstDay()
        {
            _grid.FirstDayOfWeek = _grid.FirstDayOfWeek == DayOfWeek.Monday ? DayOfWeek.Sunday : DayOfWeek.Monday;
            _message = "The week now starts on " + _grid.FirstDayOfWeek + ".";
        }

        /// <summary>Opens the file browser, starting where the sample is.</summary>
        private void OpenPlanner()
        {
            FileDialog.OpenFile(
                SimUnit,
                PlannerLibrary.BrowseFolder,
                PlannerLibrary.Extensions,
                LoadPlanner,
                () => _message = "Open cancelled.");
        }

        /// <summary>Writes the planner back where it came from, or asks where to put it.</summary>
        private void SavePlanner()
        {
            if (string.IsNullOrEmpty(_path))
            {
                SavePlannerAs();
                return;
            }

            WriteTo(_path);
        }

        /// <summary>Asks for a folder and then a name, since the library still has no Save As of its own.</summary>
        private void SavePlannerAs()
        {
            FileDialog.SelectFolder(
                SimUnit,
                PlannerLibrary.BrowseFolder,
                folder => TextInputDialog.Prompt(
                    SimUnit,
                    "Save as which file name?",
                    name => WriteTo(Path.Combine(folder, name)),
                    () => _message = "Save cancelled.",
                    _path == null ? "planner.csv" : Path.GetFileName(_path)),
                () => _message = "Save cancelled.");
        }

        /// <summary>Writes the planner to a path and says how it went.</summary>
        /// <param name="path">Where to write.</param>
        private void WriteTo(string path)
        {
            if (!PlannerLibrary.TrySave(_diary, path, out var error))
            {
                _message = "Could not save " + Path.GetFileName(path) + ": " + error;
                return;
            }

            _path = path;
            _diary.MarkSaved();
            _message = "Saved " + Path.GetFileName(path) + ".";
        }

        /// <summary>Reads a planner, or leaves the one loaded alone and says why it could not.</summary>
        /// <param name="path">The file to read.</param>
        private void LoadPlanner(string path)
        {
            var loaded = PlannerLibrary.TryLoad(path, out var error);

            if (loaded == null)
            {
                _message = "Could not open " + Path.GetFileName(path) + ": " + error;
                return;
            }

            _diary = loaded;
            _path = path;
            _message = null;

            RefreshAgenda();
            Reveal();
        }

        /// <summary>Says what this is.</summary>
        private void ShowAbout()
        {
            _message = "WolfCurses planner - the holidays are worked out, not looked up, so any year knows them.";
        }

        /// <summary>The key-hint strip, or whatever the last action had to say.</summary>
        /// <returns>The status text.</returns>
        private string StatusText()
        {
            var where = _selected.ToString("ddd d MMM yyyy", CultureInfo.InvariantCulture);

            if (!string.IsNullOrEmpty(_message))
                return "  " + where + "   " + _message;

            return "  " + where + "   TAB=View  F2=Add  Del=Remove  Home=Today  F10=Menu";
        }
    }
}
