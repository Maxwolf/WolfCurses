// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using WolfCurses.Controls;
using WolfCurses.Documents;
using WolfCurses.Graphics;
using WolfCurses.Window;
using WolfCurses.Window.Control;
using WolfCurses.Window.Form;

namespace WolfCurses.Apps.Spreadsheet
{
    /// <summary>
    ///     The spreadsheet: a grid of cells you can type in, scroll through, total up and draw pictures of.
    ///     <para>
    ///         Almost nothing here is about tables or about drawing. <see cref="Sheet" /> holds the cells and works
    ///         out what they come to, <see cref="TableViewport" /> decides which of them are on screen and which one
    ///         a click landed in, <see cref="TextRow" /> builds each row so the menu panel can be drawn over it,
    ///         <see cref="TextBuffer" /> is the caret inside the cell being edited, and
    ///         <see cref="SheetChrome" /> assembles the picture. What is left in this file is which key means what
    ///         and what the menus contain, which really is this application's own business.
    ///     </para>
    ///     <para>
    ///         <b>Every shortcut is a function key.</b> Not nostalgia: a control combination the console decides to
    ///         keep for itself never arrives here at all, which this suite has already been caught by once, and a
    ///         menu advertising a key that does nothing is worse than a menu advertising none. The clipboard three
    ///         are the exception, because those are the combinations everybody's hands already know, and they are
    ///         the ones the MS-DOS editor bound too.
    ///     </para>
    /// </summary>
    [ParentWindow(typeof (OfficeWindow))]
    public sealed class SpreadsheetDialog : Form<OfficeWindowInfo>, IHandlesEscape
    {
        /// <summary>Rows outside this screen: the scene graph's status line above and the input prompt below.</summary>
        private const int ReservedRows = 3;

        /// <summary>Rows the view moves for one notch of the wheel, which is what every other program uses.</summary>
        private const int WheelRows = 3;

        /// <summary>The grid.</summary>
        private Sheet _sheet = new();

        /// <summary>The window onto it.</summary>
        private readonly TableViewport _viewport = new();

        /// <summary>
        ///     The cell being edited, which is a whole text buffer for one line of text on purpose: it already
        ///     knows about a caret, home and end, word movement and selection, none of which a spreadsheet wants to
        ///     write again. The one thing it must never be asked for is a new line.
        /// </summary>
        private readonly TextBuffer _editor = new();

        /// <summary>The pull-down menus across the top.</summary>
        private MenuBar _menuBar;

        /// <summary>The cell the keyboard is on.</summary>
        private CellAddress _cursor = CellAddress.Origin;

        /// <summary>The other corner of the selection, which is the cursor itself when nothing is swept.</summary>
        private CellAddress _anchor = CellAddress.Origin;

        /// <summary>Whether what is typed goes into the cell rather than moving the cursor.</summary>
        private bool _editing;

        /// <summary>Which picture is being shown instead of the grid, or null when the grid is showing.</summary>
        private SheetChartKindEnum? _chart;

        /// <summary>The file the sheet came from, or null for one that has never been on disk.</summary>
        private string _path;

        /// <summary>What the status strip has to say, when it is not saying where the cursor is.</summary>
        private string _message;

        /// <summary>Whether the left button is down and sweeping a selection across the grid.</summary>
        private bool _draggingCells;

        /// <summary>Whether the left button is down and carrying the scrollbar thumb.</summary>
        private bool _draggingThumb;

        /// <summary>The cell the mouse is over, or null when it is somewhere else.</summary>
        private CellAddress? _pointer;

        /// <summary>Initializes a new instance of the <see cref="SpreadsheetDialog" /> class.</summary>
        /// <param name="window">The parent window.</param>
        public SpreadsheetDialog(IWindow window) : base(window)
        {
        }

        /// <summary>ENTER and BACKSPACE arrive as key presses, which is the only way a cell editor sees a backspace.</summary>
        public override bool EditsText => true;

        /// <summary>Typed characters go into the grid, not into the prompt underneath it.</summary>
        public override bool InputFillsBuffer => false;

        /// <summary>The selected rectangle, which is one cell until something is swept.</summary>
        private CellRange Selection => new(_anchor, _cursor);

        /// <inheritdoc />
        public bool TryHandleEscape()
        {
            // In the order they are stacked on the screen: the menu is over the chart, and the chart is over the
            // cell being edited.
            if (_menuBar != null && _menuBar.IsOpen)
            {
                _menuBar.Close();
                return true;
            }

            if (_chart != null)
            {
                _chart = null;
                return true;
            }

            if (!_editing)
                return false;

            CancelEdit();
            return true;
        }

        /// <inheritdoc />
        public override void OnFormPostCreate()
        {
            base.OnFormPostCreate();

            BuildMenus();
            LoadSheet(SheetLibrary.DefaultSheetPath);
            ResizeViewport();
        }

        /// <inheritdoc />
        public override void OnTick(bool systemTick, bool skipDay)
        {
            base.OnTick(systemTick, skipDay);

            // Once a second rather than per frame: reading the console size is a live syscall and OnRenderForm runs
            // about a thousand times a second.
            if (!systemTick)
                ResizeViewport();
        }

        /// <inheritdoc />
        public override string OnRenderForm()
        {
            ParentWindow.PromptText = "F10 opens the menus, ESC returns to the suite:";

            var width = Math.Max(24, AnsiConsole.SafeWindowWidth() - 1);

            // The leading newline is load-bearing, not spacing. SceneGraph appends a window's text straight onto its
            // own status row with no separator, so a screen that does not start one gets its first line printed on
            // the end of that row. Every row offset the mouse hit test uses counts from there.
            if (_chart != null)
                return Environment.NewLine + ChartScreen(width);

            return Environment.NewLine +
                   SheetChrome.Compose(_menuBar, _sheet, _viewport, _cursor, Selection, _pointer, Title(),
                       EntryLine(width), StatusText(), width);
        }

        /// <summary>The picture, framed, with the menu bar still above it so nothing jumps when it opens.</summary>
        /// <param name="width">The console width.</param>
        /// <returns>The screen.</returns>
        private string ChartScreen(int width)
        {
            // The same row count the grid gets, so that swapping one for the other moves nothing on screen and
            // leaves nothing of the sheet showing underneath.
            var rows = SheetChrome.Rows(AnsiConsole.SafeWindowHeight(), ReservedRows);
            var chart = SheetChart.Render(_sheet, Selection, _chart.Value, width, rows);

            var title = _chart.Value == SheetChartKindEnum.Line ? "Line graph" : "Bar chart";

            return _menuBar.RenderTitleBar(width) + Environment.NewLine +
                   SheetChart.Frame(chart, title + " of " + SheetChart.Caption(Selection), width) +
                   DosTheme.Status.Apply(AnsiText.Fit("  Any key returns to the sheet.", width));
        }

        /// <summary>
        ///     The line under the grid: which cell the cursor is on, and what is actually stored in it.
        ///     <para>
        ///         <b>What is stored, not what it shows.</b> That difference is the whole of what makes a
        ///         spreadsheet one: the cell shows a number and holds the formula that worked it out, and there has
        ///         to be somewhere the formula can be read.
        ///     </para>
        /// </summary>
        /// <param name="width">The console width.</param>
        /// <returns>The line, as runs.</returns>
        private TextRow EntryLine(int width)
        {
            var row = new TextRow().Append(" " + _cursor + ": ", DosTheme.Header);

            if (!_editing)
                return row.Append(_sheet.GetText(_cursor), DosTheme.Field).PadTo(width, DosTheme.Field);

            var text = _editor.GetText();
            var caret = Math.Clamp(_editor.Caret.Column, 0, text.Length);

            // The caret is drawn rather than placed: the library parks the terminal's real cursor at the prompt
            // underneath, so there is no cursor to put in here, and a lit cell is what a block cursor looks like.
            row.Append(text.Substring(0, caret), DosTheme.Field);
            row.Append(caret < text.Length ? text.Substring(caret, 1) : " ", DosTheme.Selection);

            if (caret < text.Length)
                row.Append(text.Substring(caret + 1), DosTheme.Field);

            return row.PadTo(width, DosTheme.Field);
        }

        /// <summary>
        ///     Never called: <see cref="EditsText" /> is precisely the declaration that ENTER should arrive as a key
        ///     press instead, so nothing is ever collected into the buffer to submit.
        /// </summary>
        /// <param name="input">Unused.</param>
        public override void OnInputBufferReturned(string input)
        {
        }

        /// <inheritdoc />
        public override void OnKeyPressed(ConsoleKeyInfo keyInfo)
        {
            base.OnKeyPressed(keyInfo);

            // The menus get every key first and report what they spent. While one is open that is everything, which
            // is what stops a keystroke landing in the grid behind it.
            if (_menuBar != null && _menuBar.HandleKey(keyInfo))
            {
                ResizeViewport();
                return;
            }

            // A picture has nothing to type into, so anything at all puts the grid back. ESC never reaches here,
            // having been claimed above by TryHandleEscape.
            if (_chart != null)
            {
                _chart = null;
                return;
            }

            if (_editing)
            {
                EditKey(keyInfo);
                return;
            }

            GridKey(keyInfo);
        }

        /// <summary>What a key means while a cell is being typed into.</summary>
        /// <param name="keyInfo">The key.</param>
        private void EditKey(ConsoleKeyInfo keyInfo)
        {
            var control = (keyInfo.Modifiers & ConsoleModifiers.Control) != 0;
            var shift = (keyInfo.Modifiers & ConsoleModifiers.Shift) != 0;

            switch (keyInfo.Key)
            {
                // Committing and moving in one keystroke, which is how a table is filled in: down the column with
                // ENTER, along the row with TAB.
                case ConsoleKey.Enter:
                    CommitEdit();
                    MoveCursor(1, 0, false);
                    return;

                case ConsoleKey.Tab:
                    CommitEdit();
                    MoveCursor(0, shift ? -1 : 1, false);
                    return;

                case ConsoleKey.UpArrow:
                    CommitEdit();
                    MoveCursor(-1, 0, false);
                    return;

                case ConsoleKey.DownArrow:
                    CommitEdit();
                    MoveCursor(1, 0, false);
                    return;

                case ConsoleKey.LeftArrow when control:
                    _editor.MoveWordLeft();
                    return;

                case ConsoleKey.RightArrow when control:
                    _editor.MoveWordRight();
                    return;

                case ConsoleKey.LeftArrow:
                    _editor.MoveLeft();
                    return;

                case ConsoleKey.RightArrow:
                    _editor.MoveRight();
                    return;

                case ConsoleKey.Home:
                    _editor.MoveToLineStart();
                    return;

                case ConsoleKey.End:
                    _editor.MoveToLineEnd();
                    return;

                case ConsoleKey.Backspace:
                    _editor.Backspace();
                    return;

                case ConsoleKey.Delete:
                    _editor.Delete();
                    return;

                default:
                    // Anything carrying a printable character is text. Control characters are not, which keeps
                    // CTRL combinations from being typed into the cell as gibberish.
                    if (control || keyInfo.KeyChar == '\0' || char.IsControl(keyInfo.KeyChar))
                        return;

                    _editor.Insert(keyInfo.KeyChar);
                    return;
            }
        }

        /// <summary>What a key means while the grid has the keyboard.</summary>
        /// <param name="keyInfo">The key.</param>
        private void GridKey(ConsoleKeyInfo keyInfo)
        {
            var shift = (keyInfo.Modifiers & ConsoleModifiers.Shift) != 0;
            var control = (keyInfo.Modifiers & ConsoleModifiers.Control) != 0;

            switch (keyInfo.Key)
            {
                case ConsoleKey.UpArrow:
                    MoveCursor(-1, 0, shift);
                    return;

                case ConsoleKey.DownArrow:
                    MoveCursor(1, 0, shift);
                    return;

                case ConsoleKey.LeftArrow:
                    MoveCursor(0, -1, shift);
                    return;

                case ConsoleKey.RightArrow:
                    MoveCursor(0, 1, shift);
                    return;

                // The view moves with the cursor rather than being dragged along behind it. Moving the cursor
                // alone would scroll by the least it could, which lands the cursor on the last visible row and
                // leaves all but one row of the previous screenful still showing: a page key that turns the page
                // one line at a time.
                case ConsoleKey.PageUp:
                    _viewport.ScrollBy(-_viewport.Rows, 0);
                    MoveCursor(-_viewport.Rows, 0, shift);
                    return;

                case ConsoleKey.PageDown:
                    _viewport.ScrollBy(_viewport.Rows, 0);
                    MoveCursor(_viewport.Rows, 0, shift);
                    return;

                case ConsoleKey.Home when control:
                    GoTo(CellAddress.Origin, shift);
                    return;

                case ConsoleKey.End when control:
                    // The end of the data rather than the end of the grid, which is two hundred empty rows away and
                    // is never where anybody meant.
                    GoTo(new CellAddress(Math.Max(0, _sheet.UsedRowCount - 1),
                        Math.Max(0, _sheet.UsedColumnCount - 1)), shift);
                    return;

                case ConsoleKey.Home:
                    GoTo(new CellAddress(_cursor.Row, 0), shift);
                    return;

                case ConsoleKey.End:
                    GoTo(new CellAddress(_cursor.Row, Math.Max(0, _sheet.UsedColumnCount - 1)), shift);
                    return;

                case ConsoleKey.Enter:
                    MoveCursor(1, 0, false);
                    return;

                case ConsoleKey.Tab:
                    MoveCursor(0, shift ? -1 : 1, false);
                    return;

                case ConsoleKey.F2:
                    BeginEdit(_sheet.GetText(_cursor));
                    return;

                case ConsoleKey.F3:
                    OpenSheet();
                    return;

                case ConsoleKey.F4:
                    SaveSheet();
                    return;

                case ConsoleKey.F6:
                    ShowChart(SheetChartKindEnum.Bars);
                    return;

                case ConsoleKey.F7:
                    ShowChart(SheetChartKindEnum.Line);
                    return;

                case ConsoleKey.F8:
                    AskWhereToGo();
                    return;

                // The guarded DELETE has to come before the bare one, or the compiler refuses the switch outright:
                // an unguarded case swallows every shifted press as well, so SHIFT+DEL would clear the cells
                // instead of cutting them. Caught at build time rather than at run time, which is the good case.
                //
                // There is no CTRL+C here and adding one would be dead code that compiles: the console turns it
                // into a signal before anything can read it as a key, which is what keeps it the way out of a
                // program. CTRL+INS is the key that copies.
                case ConsoleKey.X when control:
                case ConsoleKey.Delete when shift:
                    Cut();
                    return;

                case ConsoleKey.Delete:
                    ClearSelection();
                    return;

                case ConsoleKey.Backspace:
                    // The other way people empty a cell, and the one that then lets them type a replacement.
                    BeginEdit(string.Empty);
                    return;

                case ConsoleKey.A when control:
                    SelectAll();
                    return;

                case ConsoleKey.Insert when control:
                    Copy();
                    return;

                case ConsoleKey.V when control:
                case ConsoleKey.Insert when shift:
                    Paste();
                    return;

                default:
                    // Typing anything printable starts editing the cell with that character already in it, which is
                    // how every spreadsheet behaves and is why F2 exists separately: F2 keeps what is there.
                    if (control || keyInfo.KeyChar == '\0' || char.IsControl(keyInfo.KeyChar))
                        return;

                    BeginEdit(keyInfo.KeyChar.ToString());
                    return;
            }
        }

        /// <summary>Starts typing into the current cell.</summary>
        /// <param name="text">What the cell starts out containing.</param>
        private void BeginEdit(string text)
        {
            _editing = true;
            _message = null;

            _editor.SetText(text ?? string.Empty);
            _editor.MoveToLineEnd();
        }

        /// <summary>
        ///     Puts what was typed into the cell.
        ///     <para>
        ///         The buffer's first line and not its whole text: nothing here can insert a line break, but a
        ///         paste could, and a cell holding a newline would be written back out as a quoted field spanning
        ///         two rows of the file.
        ///     </para>
        /// </summary>
        private void CommitEdit()
        {
            if (!_editing)
                return;

            _editing = false;
            _sheet.SetText(_cursor, _editor.LineCount > 0 ? _editor.GetLine(0) : string.Empty);
        }

        /// <summary>Abandons what was typed and leaves the cell as it was.</summary>
        private void CancelEdit()
        {
            _editing = false;
            _message = "Edit cancelled.";
        }

        /// <summary>Moves the cursor by a delta, keeping it on the grid and on the screen.</summary>
        /// <param name="rows">Rows to move down; negative moves up.</param>
        /// <param name="columns">Columns to move right; negative moves left.</param>
        /// <param name="extend">TRUE to drag the selection along behind it.</param>
        private void MoveCursor(int rows, int columns, bool extend)
        {
            GoTo(new CellAddress(_cursor.Row + rows, _cursor.Column + columns), extend);
        }

        /// <summary>Puts the cursor somewhere, keeping it on the grid and on the screen.</summary>
        /// <param name="address">Where to put it.</param>
        /// <param name="extend">TRUE to drag the selection along behind it.</param>
        private void GoTo(CellAddress address, bool extend)
        {
            CommitEdit();

            _cursor = new CellAddress(Math.Clamp(address.Row, 0, _sheet.RowCount - 1),
                Math.Clamp(address.Column, 0, _sheet.ColumnCount - 1));

            // Not extending means the selection collapses onto the cursor, which is what makes a bare arrow key
            // clear a swept range rather than quietly leaving it behind.
            if (!extend)
                _anchor = _cursor;

            _message = null;
            Reveal();
        }

        /// <summary>Brings the cursor into view, scrolling the least it can.</summary>
        private void Reveal()
        {
            _viewport.EnsureVisible(_cursor.Row, _cursor.Column, _sheet.ColumnWidths);
            _viewport.ClampToTable(_sheet.RowCount, _sheet.ColumnWidths);
        }

        /// <summary>Selects everything that has anything in it, which is more useful than selecting the empty grid.</summary>
        private void SelectAll()
        {
            _anchor = CellAddress.Origin;
            _cursor = new CellAddress(Math.Max(0, _sheet.UsedRowCount - 1),
                Math.Max(0, _sheet.UsedColumnCount - 1));

            Reveal();
            _message = "Selected " + Selection + ".";
        }

        /// <summary>Empties every cell in the selection, leaving the clipboard alone.</summary>
        private void ClearSelection()
        {
            foreach (var address in Selection.Cells())
                _sheet.SetText(address, string.Empty);

            _message = "Cleared " + Selection + ".";
        }

        /// <summary>
        ///     Takes the selection onto the suite clipboard as tab separated text, and removes it.
        ///     <para>
        ///         Tabs rather than commas, because the clipboard is shared with the word processor next door: a
        ///         range pasted there should arrive as a table somebody can read, and a comma separated one would
        ///         not line up. It is also what every other spreadsheet puts on a clipboard.
        ///     </para>
        /// </summary>
        private void Cut()
        {
            Copy();
            ClearSelection();

            _message = "Cut " + Selection + ".";
        }

        /// <summary>Takes the selection onto the suite clipboard and leaves the grid alone.</summary>
        private void Copy()
        {
            var range = Selection;
            var rows = new List<string[]>(range.RowCount);

            for (var row = range.FirstRow; row <= range.LastRow; row++)
            {
                var line = new string[range.ColumnCount];

                for (var column = range.FirstColumn; column <= range.LastColumn; column++)
                {
                    // What the cell shows rather than what it holds, because a formula pasted somewhere else would
                    // refer to whatever cells happened to be there. Copying values is the answer that keeps
                    // meaning what it meant.
                    line[column - range.FirstColumn] = _sheet.GetValue(row, column).Display();
                }

                rows.Add(line);
            }

            UserData.Clipboard = DelimitedText.Write(rows, '\t', "\n");
            _message = "Copied " + range + ".";
        }

        /// <summary>Drops the clipboard in with its top left corner at the cursor.</summary>
        private void Paste()
        {
            if (!UserData.HasClipboard)
                return;

            // Read back the same way it was written, which also means a paragraph copied in the word processor
            // arrives as a column of lines rather than one very wide cell.
            var rows = DelimitedText.Read(UserData.Clipboard, '\t');

            for (var row = 0; row < rows.Count; row++)
            {
                for (var column = 0; column < rows[row].Count; column++)
                    _sheet.SetText(_cursor.Row + row, _cursor.Column + column, rows[row][column]);
            }

            _message = string.Format(CultureInfo.InvariantCulture, "Pasted {0} row{1}.", rows.Count,
                rows.Count == 1 ? string.Empty : "s");
        }

        /// <summary>
        ///     Writes a total underneath the selection, which is the one thing every spreadsheet user does by hand
        ///     and the shortest possible demonstration that the formulas are real.
        /// </summary>
        private void SumSelection()
        {
            var range = Selection;
            var target = new CellAddress(range.LastRow + 1, range.FirstColumn);

            if (target.Row >= _sheet.RowCount)
            {
                _message = "There is no room below the selection for a total.";
                return;
            }

            _sheet.SetText(target, "=SUM(" + range + ")");

            GoTo(target, false);
            _message = "Totalled " + range + " into " + target + ".";
        }

        /// <summary>Asks which cell to jump to, and jumps to it.</summary>
        private void AskWhereToGo()
        {
            TextInputDialog.Prompt(
                SimUnit,
                "Go to which cell?",
                text =>
                {
                    if (!CellAddress.TryParse(text, out var address))
                    {
                        _message = "\"" + text + "\" is not a cell reference.";
                        return;
                    }

                    GoTo(address, false);
                },
                () => _message = "Go to cancelled.",
                _cursor.ToString(),
                false,
                text => CellAddress.TryParse(text, out _) ? null : "Type a cell reference such as B7.");
        }

        /// <summary>Asks how wide the cursor's column should be drawn.</summary>
        private void AskColumnWidth()
        {
            var column = _cursor.Column;

            TextInputDialog.Prompt(
                SimUnit,
                "How wide should column " + CellAddress.ColumnName(column) + " be?",
                text =>
                {
                    if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var width))
                        return;

                    _sheet.SetColumnWidth(column, width);
                    Reveal();

                    _message = "Column " + CellAddress.ColumnName(column) + " is now " +
                               _sheet.GetColumnWidth(column).ToString(CultureInfo.InvariantCulture) + " wide.";
                },
                () => _message = "Column width unchanged.",
                _sheet.GetColumnWidth(column).ToString(CultureInfo.InvariantCulture),
                false,
                Validate);
        }

        /// <summary>Refuses a width that is not a number in range, before the dialog closes on it.</summary>
        /// <param name="text">What the user typed.</param>
        /// <returns>The complaint, or null when the width is usable.</returns>
        private static string Validate(string text)
        {
            if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var width))
                return "Type a number.";

            return width < Sheet.MinimumColumnWidth || width > Sheet.MaximumColumnWidth
                ? "Between " + Sheet.MinimumColumnWidth + " and " + Sheet.MaximumColumnWidth + ", please."
                : null;
        }

        /// <summary>Draws the selected columns of the cursor's row as one wide cell.</summary>
        private void MergeSelection()
        {
            var range = Selection;

            if (range.ColumnCount < 2)
            {
                _message = "Select the cells across a row first.";
                return;
            }

            _sheet.Merge(range.FirstRow, range.FirstColumn, range.ColumnCount);
            _message = "Merged " + range + ".";
        }

        /// <summary>Puts a merged run back to ordinary cells.</summary>
        private void UnmergeSelection()
        {
            _message = _sheet.Unmerge(_cursor.Row, _cursor.Column)
                ? "Unmerged."
                : "That cell is not merged.";
        }

        /// <summary>Shows a picture of whatever is selected.</summary>
        /// <param name="kind">Which picture.</param>
        private void ShowChart(SheetChartKindEnum kind)
        {
            CommitEdit();
            _chart = kind;
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
                // Scrolls the view and leaves the cursor alone, which is what a wheel means everywhere: you are
                // looking somewhere else, not typing somewhere else.
                _viewport.ScrollBy(-mouse.WheelDelta * WheelRows, 0);
                _viewport.ClampToTable(_sheet.RowCount, _sheet.ColumnWidths);
                return;
            }

            // The menus get the pointer before anything else does, exactly as they get keys and presses first.
            // While one is open, moving over its entries walks the highlight through them, and moving along the bar
            // opens each menu in turn; a shut bar answers no and nothing here changes.
            if (mouse.Kind == MouseEventKindEnum.Move && _menuBar != null &&
                _menuBar.HandleMouseMove(mouse.Row, mouse.Column))
                return;

            TrackPointer(mouse);

            if (mouse.Kind == MouseEventKindEnum.Release)
            {
                _draggingCells = false;
                _draggingThumb = false;
                return;
            }

            // A move with no button on it is a bare hover: it moves the drawn pointer and nothing else.
            if (mouse.Button != MouseButtonEnum.Left)
                return;

            if (_draggingThumb)
            {
                var scrolled = SheetChrome.VerticalBar(_sheet, _viewport)
                    .PositionForDrag(mouse.Row - SheetChrome.GridTopRow);

                _viewport.ScrollTo(scrolled, _viewport.FirstColumn);
                _viewport.ClampToTable(_sheet.RowCount, _sheet.ColumnWidths);
                return;
            }

            // Extending rather than moving, which is the whole of a sweep: the anchor was dropped by the press and
            // every move since drags the other corner of the rectangle along behind the pointer.
            if (_draggingCells && _pointer.HasValue)
                GoTo(_pointer.Value, true);
        }

        /// <summary>Remembers which cell the pointer is over, since the terminal draws none once reporting is on.</summary>
        /// <param name="mouse">The event carrying the position.</param>
        private void TrackPointer(MouseEvent mouse)
        {
            _pointer = CellAt(mouse.Row, mouse.Column);
        }

        /// <summary>
        ///     Which cell a screen position is over, or null when it is not over one at all. Two translations and
        ///     both are needed: the viewport turns a row into a sheet row, and it turns a screen offset into a
        ///     column by adding up the widths of the columns before it.
        /// </summary>
        /// <param name="screenRow">The screen row.</param>
        /// <param name="screenColumn">The screen column.</param>
        /// <returns>The cell, or null.</returns>
        private CellAddress? CellAt(int screenRow, int screenColumn)
        {
            var row = screenRow - SheetChrome.GridTopRow;
            var offset = screenColumn - 1 - SheetChrome.GutterWidth;

            if (row < 0 || row >= _viewport.Rows || offset < 0)
                return null;

            var column = _viewport.ColumnAt(offset, _sheet.ColumnWidths);
            if (column < 0)
                return null;

            var sheetRow = _viewport.RowAt(row);

            return sheetRow >= _sheet.RowCount ? null : new CellAddress(sheetRow, column);
        }

        /// <inheritdoc />
        public override void OnMousePressed(MouseEvent mouse)
        {
            base.OnMousePressed(mouse);

            TrackPointer(mouse);

            // The menus get the press first, exactly as they get keys first. A press that shuts an open menu is
            // consumed, so dismissing one does not also move the cursor to whatever was underneath it.
            if (_menuBar != null && _menuBar.HandleMouse(mouse.Row, mouse.Column))
            {
                ResizeViewport();
                return;
            }

            if (_chart != null)
            {
                _chart = null;
                return;
            }

            if (mouse.Button != MouseButtonEnum.Left)
                return;

            if (PressedHeader(mouse))
                return;

            var row = mouse.Row - SheetChrome.GridTopRow;

            if (row < 0 || row >= _viewport.Rows)
                return;

            // The scrollbar occupies the last column of the frame. Taking hold of the thumb starts a drag; the
            // arrow caps step a row and the track pages.
            if (mouse.Column == ScrollColumn())
            {
                var bar = SheetChrome.VerticalBar(_sheet, _viewport);

                if (bar.IsOnThumb(row))
                {
                    _draggingThumb = true;
                    return;
                }

                var scrolled = bar.PositionForPress(row);

                if (scrolled >= 0)
                {
                    _viewport.ScrollTo(scrolled, _viewport.FirstColumn);
                    _viewport.ClampToTable(_sheet.RowCount, _sheet.ColumnWidths);
                }

                return;
            }

            if (!_pointer.HasValue)
                return;

            // The press drops the selection anchor; every move until the release drags the other corner behind it.
            _draggingCells = true;
            GoTo(_pointer.Value, false);
        }

        /// <summary>
        ///     A press on a column letter or a row number, which selects the whole of it.
        ///     <para>
        ///         Worth having because it is the shortest path to the thing this application is for: click a
        ///         column heading, press F6, and there is a chart of that column. Limited to the rows and columns
        ///         that have anything in them, since selecting two hundred empty rows would chart nothing.
        ///     </para>
        /// </summary>
        /// <param name="mouse">The press.</param>
        /// <returns>TRUE when the press was on a heading and has been dealt with.</returns>
        private bool PressedHeader(MouseEvent mouse)
        {
            var lastRow = Math.Max(0, _sheet.UsedRowCount - 1);
            var lastColumn = Math.Max(0, _sheet.UsedColumnCount - 1);

            if (mouse.Row == SheetChrome.HeaderRow)
            {
                var column = _viewport.ColumnAt(mouse.Column - 1 - SheetChrome.GutterWidth, _sheet.ColumnWidths);

                if (column < 0)
                    return false;

                _anchor = new CellAddress(0, column);
                _cursor = new CellAddress(lastRow, column);

                Reveal();
                _message = "Selected column " + CellAddress.ColumnName(column) + ".";

                return true;
            }

            var gridRow = mouse.Row - SheetChrome.GridTopRow;

            if (gridRow < 0 || gridRow >= _viewport.Rows || mouse.Column < 1 ||
                mouse.Column > SheetChrome.GutterWidth)
                return false;

            var sheetRow = _viewport.RowAt(gridRow);

            if (sheetRow >= _sheet.RowCount)
                return false;

            _anchor = new CellAddress(sheetRow, 0);
            _cursor = new CellAddress(sheetRow, lastColumn);

            Reveal();
            _message = "Selected row " + (sheetRow + 1).ToString(CultureInfo.InvariantCulture) + ".";

            return true;
        }

        /// <summary>Which screen column the vertical scrollbar is drawn in.</summary>
        /// <returns>The column.</returns>
        private static int ScrollColumn()
        {
            return Math.Max(24, AnsiConsole.SafeWindowWidth() - 1) - 1;
        }

        /// <summary>Builds the pull-downs, styled to match the grid they sit over.</summary>
        private void BuildMenus()
        {
            _menuBar = new MenuBar(
                new MenuBarMenu("File",
                    new MenuBarEntry("New", NewSheet),
                    new MenuBarEntry("Open...", OpenSheet, "F3"),
                    new MenuBarEntry("Save", SaveSheet, "F4"),
                    new MenuBarEntry("Save As...", SaveSheetAs),
                    MenuBarEntry.Separator(),
                    new MenuBarEntry("Exit", () => ParentWindow.ClearForm(), "Esc")),
                new MenuBarMenu("Edit",
                    // Each says for itself when it means anything, rather than being switched on and off from
                    // wherever the selection last changed. The menu asks at the moment it is drawn and again at the
                    // moment an entry is chosen, so the greyed lines are always telling the truth.
                    new MenuBarEntry("Cut", Cut, "Ctrl+X"),
                    new MenuBarEntry("Copy", Copy, "Ctrl+Ins"),
                    new MenuBarEntry("Paste", Paste, "Ctrl+V") {EnabledWhen = () => UserData.HasClipboard},
                    MenuBarEntry.Separator(),
                    new MenuBarEntry("Edit Cell", () => BeginEdit(_sheet.GetText(_cursor)), "F2"),
                    new MenuBarEntry("Clear", ClearSelection, "Del"),
                    new MenuBarEntry("Select All", SelectAll, "Ctrl+A")),
                new MenuBarMenu("Data",
                    new MenuBarEntry("Go To Cell...", AskWhereToGo, "F8"),
                    new MenuBarEntry("Total Selection", SumSelection),
                    MenuBarEntry.Separator(),
                    new MenuBarEntry("Column Width...", AskColumnWidth),
                    new MenuBarEntry("Merge Across", MergeSelection)
                        {EnabledWhen = () => Selection.ColumnCount > 1},
                    new MenuBarEntry("Unmerge", UnmergeSelection)
                        {EnabledWhen = () => _sheet.MergeAt(_cursor.Row, _cursor.Column) != null}),
                new MenuBarMenu("Chart",
                    new MenuBarEntry("Bar Chart", () => ShowChart(SheetChartKindEnum.Bars), "F6"),
                    new MenuBarEntry("Line Graph", () => ShowChart(SheetChartKindEnum.Line), "F7")),
                new MenuBarMenu("Help",
                    new MenuBarEntry("About", ShowAbout)) {AlignRight = true})
            {
                BarStyle = DosTheme.MenuBar,
                HighlightStyle = DosTheme.MenuHighlight,
                PanelStyle = DosTheme.MenuPanel,
                PanelHighlightStyle = DosTheme.MenuHighlight,

                // Without this the EnabledWhen predicates above are invisible: an entry that is switched off draws
                // exactly like a live one and simply refuses to answer, which reads as a broken menu rather than
                // as a greyed entry.
                DisabledStyle = DosTheme.MenuDisabled,

                // The square root sign is what an MS-DOS editor ticked a menu entry with, and the console's own
                // code page is where that glyph came from.
                CheckMark = '\u221A',
                BarRow = SheetChrome.BarRow,
                PanelRow = SheetChrome.BorderRow
            };
        }

        /// <summary>Opens the file browser, starting where the sample sheet is.</summary>
        private void OpenSheet()
        {
            FileDialog.OpenFile(
                SimUnit,
                SheetLibrary.BrowseFolder,
                SheetLibrary.Extensions,
                LoadSheet,
                () => _message = "Open cancelled.");
        }

        /// <summary>Writes the sheet back where it came from, or asks where to put it when it has no home yet.</summary>
        private void SaveSheet()
        {
            if (string.IsNullOrEmpty(_path))
            {
                SaveSheetAs();
                return;
            }

            WriteTo(_path);
        }

        /// <summary>
        ///     Asks for a folder and then a name, because the library still has no Save As: <c>FileDialog</c> offers
        ///     opening a file and picking a folder, and neither of those is "name a file that does not exist yet".
        /// </summary>
        private void SaveSheetAs()
        {
            FileDialog.SelectFolder(
                SimUnit,
                SheetLibrary.BrowseFolder,
                folder => TextInputDialog.Prompt(
                    SimUnit,
                    "Save as which file name?",
                    name => WriteTo(Path.Combine(folder, name)),
                    () => _message = "Save cancelled.",
                    _path == null ? "untitled.csv" : Path.GetFileName(_path),
                    false,
                    ValidateName),
                () => _message = "Save cancelled.");
        }

        /// <summary>Refuses a name that is not one, before a dialog closes on it.</summary>
        /// <param name="name">What the user typed.</param>
        /// <returns>The complaint, or null when the name is usable.</returns>
        private static string ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "A file name is needed.";

            return name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                ? "That name has characters a file cannot have."
                : null;
        }

        /// <summary>Writes the sheet to a path and says how it went.</summary>
        /// <param name="path">Where to write.</param>
        private void WriteTo(string path)
        {
            CommitEdit();

            if (!SheetLibrary.TrySave(_sheet, path, out var error))
            {
                _message = "Could not save " + Path.GetFileName(path) + ": " + error;
                return;
            }

            _path = path;
            _sheet.MarkSaved();
            _message = "Saved " + Path.GetFileName(path) + ".";
        }

        /// <summary>Starts an empty sheet.</summary>
        private void NewSheet()
        {
            _sheet = new Sheet();
            _path = null;
            _message = null;
            _editing = false;

            _cursor = CellAddress.Origin;
            _anchor = CellAddress.Origin;

            _viewport.ScrollTo(0, 0);
        }

        /// <summary>Reads a sheet, or leaves the grid alone and says why it could not.</summary>
        /// <param name="path">The file to read.</param>
        private void LoadSheet(string path)
        {
            var sheet = SheetLibrary.TryLoad(path, out var error);

            if (sheet == null)
            {
                _message = "Could not open " + Path.GetFileName(path) + ": " + error;
                return;
            }

            _sheet = sheet;
            _path = path;
            _message = null;
            _editing = false;

            _cursor = CellAddress.Origin;
            _anchor = CellAddress.Origin;

            _viewport.ScrollTo(0, 0);
        }

        /// <summary>Says what this is.</summary>
        private void ShowAbout()
        {
            _message = "WolfCurses spreadsheet - a grid, some formulas and a chart, built on the WolfCurses library.";
        }

        /// <summary>Sizes the viewport to what the frame leaves it, and keeps the cursor inside.</summary>
        private void ResizeViewport()
        {
            var width = Math.Max(24, AnsiConsole.SafeWindowWidth() - 1);
            var height = AnsiConsole.SafeWindowHeight();

            var columns = Math.Max(1, width - SheetChrome.ChromeColumns - SheetChrome.GutterWidth);
            var rows = SheetChrome.Rows(height, ReservedRows);
            var resized = columns != _viewport.Width || rows != _viewport.Rows;

            _viewport.Resize(columns, rows);
            _viewport.ClampToTable(_sheet.RowCount, _sheet.ColumnWidths);

            // Only when the window really changed shape. This runs every simulation tick, and revealing the cursor
            // unconditionally drags the view back to it once a second, which makes the scrollbar look broken:
            // scrolling and moving the cursor are different things, and the cursor is revealed by what moves it.
            if (resized)
                Reveal();
        }

        /// <summary>What the frame's tab reads: the file, and whether it has been touched.</summary>
        /// <returns>The title.</returns>
        private string Title()
        {
            var name = _path == null ? "Untitled" : Path.GetFileName(_path);

            return _sheet.IsModified ? name + " *" : name;
        }

        /// <summary>The key-hint strip, or whatever the last action had to say.</summary>
        /// <returns>The status text.</returns>
        private string StatusText()
        {
            var selection = Selection;

            var where = selection.CellCount > 1
                ? string.Format(CultureInfo.InvariantCulture, "{0}   {1}x{2} selected", selection,
                    selection.RowCount, selection.ColumnCount)
                : _cursor.ToString();

            // Where the cursor is comes FIRST, unlike the editor next door, and that is not a style choice: the
            // strip is cut to the console width, and what changes as somebody works has to be the half that
            // survives. Hints that fall off the end cost nothing; a cell reference that falls off the end is the
            // one thing the line was for.
            if (!string.IsNullOrEmpty(_message))
                return "  " + where + "   " + _message;

            return _editing
                ? "  " + where + "   ENTER=Accept   ESC=Cancel"
                : "  " + where + "   F2=Edit  F6=Bars  F7=Line  F8=Go to  F10=Menu  ESC=Suite";
        }
    }
}
