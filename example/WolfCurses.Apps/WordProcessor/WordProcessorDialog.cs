// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/20/2026

using System;
using System.Globalization;
using System.IO;
using WolfCurses.Controls;
using WolfCurses.Documents;
using WolfCurses.Graphics;
using WolfCurses.Window;
using WolfCurses.Window.Control;
using WolfCurses.Window.Form;

namespace WolfCurses.Apps.WordProcessor
{
    /// <summary>
    ///     The word processor, laid out and coloured after the MS-DOS Editor: a silver menu bar, a framed blue field
    ///     with the file name notched into its top edge, scrollbars down the right and along the bottom, and a cyan
    ///     key-hint strip underneath.
    ///     <para>
    ///         Almost nothing here is about editing text or about drawing. <see cref="TextBuffer" /> holds the
    ///         document and moves the caret, <see cref="TextViewport" /> decides what is on screen,
    ///         <see cref="TabStops" /> keeps the stored and drawn columns in step, <see cref="MenuBar" /> runs the
    ///         menus and <see cref="EditorChrome" /> assembles the picture. What is left in this file is which key
    ///         means what and what the menus contain, which really is this application's own business.
    ///     </para>
    ///     <para>
    ///         <b>Three opt-ins make it work.</b> <see cref="EditsText" /> delivers ENTER and BACKSPACE here as key
    ///         presses rather than spending them on the input buffer, without which a backspace is unreachable.
    ///         <see cref="InputFillsBuffer" /> keeps typed characters out of the prompt underneath.
    ///         <see cref="IHandlesEscape" /> takes ESC back from the window while a menu is open, so ESC shuts the
    ///         menu rather than leaving the editor.
    ///     </para>
    /// </summary>
    [ParentWindow(typeof (AppsWindow))]
    public sealed class WordProcessorDialog : Form<AppsWindowInfo>, IHandlesEscape
    {
        /// <summary>Rows outside this screen: the scene graph's status line above and the input prompt below.</summary>
        private const int ReservedRows = 3;

        /// <summary>Lines the view moves for one notch of the wheel, which is what every other program uses.</summary>
        private const int WheelLines = 3;

        /// <summary>The document being edited.</summary>
        private readonly TextBuffer _buffer = new();

        /// <summary>The window onto it.</summary>
        private readonly TextViewport _viewport = new();

        /// <summary>The pull-down menus across the top.</summary>
        private MenuBar _menuBar;

        /// <summary>What the status strip has to say, when it is not saying where the caret is.</summary>
        private string _message;

        /// <summary>The file the document came from, or null for one that has never been on disk.</summary>
        private string _path;

        /// <summary>What was last searched for, which is what Find Next repeats.</summary>
        private string _searchText;

        /// <summary>What was last put in its place.</summary>
        private string _replaceText;

        /// <summary>Whether a search tells upper and lower case apart.</summary>
        private bool _matchCase;

        /// <summary>Whether a search refuses a match with a word character against either end of it.</summary>
        private bool _wholeWord;

        /// <summary>Whether the left button is down and sweeping a selection through the document.</summary>
        private bool _draggingText;

        /// <summary>Whether the left button is down and carrying the scrollbar thumb.</summary>
        private bool _draggingThumb;

        /// <summary>Which field row the pointer is over, or -1 when it is somewhere else.</summary>
        private int _pointerRow = -1;

        /// <summary>Which field column the pointer is over.</summary>
        private int _pointerColumn = -1;

        /// <summary>Initializes a new instance of the <see cref="WordProcessorDialog" /> class.</summary>
        /// <param name="window">The parent window.</param>
        public WordProcessorDialog(IWindow window) : base(window)
        {
        }

        /// <summary>ENTER and BACKSPACE arrive as key presses, which is the only way an editor sees a backspace.</summary>
        public override bool EditsText => true;

        /// <summary>Typed characters go into the document, not into the prompt underneath it.</summary>
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
            LoadDocument(DocumentLibrary.DefaultDocumentPath);
            ResizeViewport();
        }

        /// <inheritdoc />
        public override void OnTick(bool systemTick, bool skipDay)
        {
            base.OnTick(systemTick, skipDay);

            // Once a second rather than per frame: reading the console size is a live syscall and OnRenderForm runs
            // about a thousand times a second, so sizing there would spend two syscalls a frame to notice a resize
            // that happens approximately never.
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
            // the end of "[ - ] - Window(1): ... - WolfCurses Apps". That row cannot be replaced, so everything here
            // starts below it, and every row offset the mouse hit test uses counts from there.
            return Environment.NewLine +
                   EditorChrome.Compose(_menuBar, _buffer, _viewport, Title(), StatusText(), width, _pointerRow,
                       _pointerColumn);
        }

        /// <summary>
        ///     The two mouse events that have a duration: the pointer moving, and a button coming back up. Presses
        ///     arrive at <see cref="OnMousePressed" /> instead, which stays the single routing point for them.
        ///     <para>
        ///         Everything with a beginning and an end is built here. A press starts a drag, the moves carry it,
        ///         and the release ends it, which is the shape of both sweeping a selection and carrying a scrollbar
        ///         thumb. Neither can be done with presses alone, however many arrive.
        ///     </para>
        /// </summary>
        /// <param name="mouse">What happened, and where.</param>
        public override void OnMouseEvent(MouseEvent mouse)
        {
            if (mouse.Kind == MouseEventKindEnum.Press)
            {
                OnMousePressed(mouse);
                return;
            }

            if (mouse.Kind == MouseEventKindEnum.Wheel)
            {
                // Scrolls the view and leaves the caret alone, which is what a wheel means everywhere: you are
                // looking somewhere else, not typing somewhere else. Three lines a notch is the usual step.
                _viewport.ScrollTo(_viewport.FirstLine - mouse.WheelDelta * WheelLines, _viewport.FirstColumn);
                _viewport.ClampToDocument(_buffer.LineCount);
                return;
            }

            TrackPointer(mouse);

            if (mouse.Kind == MouseEventKindEnum.Release)
            {
                _draggingText = false;
                _draggingThumb = false;
                return;
            }

            // A move with no button on it is a bare hover: it moves the drawn pointer and nothing else.
            if (mouse.Button != MouseButtonEnum.Left)
                return;

            var draggedRow = mouse.Row - FieldTopRow;

            if (_draggingThumb)
            {
                _viewport.ScrollTo(VerticalBar().PositionForDrag(draggedRow), _viewport.FirstColumn);
                _viewport.ClampToDocument(_buffer.LineCount);
                return;
            }

            if (!_draggingText || draggedRow < 0 || draggedRow >= _viewport.Height)
                return;

            // Extending rather than moving, which is the whole of a sweep: the anchor was dropped by the press, and
            // every move since drags the other end of the selection along behind the pointer.
            _buffer.MoveTo(DocumentAt(draggedRow, mouse.Column - 1), true);
            _viewport.EnsureVisible(CaretOnScreen());
        }

        /// <summary>Remembers where the pointer is so the screen can draw one, since the terminal no longer does.</summary>
        /// <param name="mouse">The event carrying the position.</param>
        private void TrackPointer(MouseEvent mouse)
        {
            var row = mouse.Row - FieldTopRow;
            var column = mouse.Column - 1;
            var inside = row >= 0 && row < _viewport.Height && column >= 0 && column < _viewport.Width;

            _pointerRow = inside ? row : -1;
            _pointerColumn = inside ? column : -1;
        }

        /// <summary>
        ///     The document position under a field cell. Two translations and both are needed: the viewport turns a
        ///     cell into a line, and <see cref="TabStops" /> turns the screen column into a character index, which
        ///     are different numbers the moment the line is indented.
        /// </summary>
        /// <param name="row">Field row.</param>
        /// <param name="column">Field column.</param>
        /// <returns>Where in the document that cell is.</returns>
        private TextPosition DocumentAt(int row, int column)
        {
            var at = _viewport.ToDocument(row, Math.Max(0, column));
            var line = Math.Clamp(at.Line, 0, _buffer.LineCount - 1);

            return new TextPosition(line,
                TabStops.ToDocumentColumn(_buffer.GetLine(line), at.Column, _buffer.TabWidth));
        }

        /// <inheritdoc />
        public override void OnMousePressed(MouseEvent mouse)
        {
            base.OnMousePressed(mouse);

            TrackPointer(mouse);

            // The menus get the press first, exactly as they get keys first. A press that shuts an open menu is
            // consumed, so dismissing one does not also move the caret to whatever was underneath it.
            if (_menuBar != null && _menuBar.HandleMouse(mouse.Row, mouse.Column))
            {
                ResizeViewport();
                return;
            }

            if (mouse.Button != MouseButtonEnum.Left)
                return;

            var row = mouse.Row - FieldTopRow;
            var column = mouse.Column - 1;

            if (row < 0 || row >= _viewport.Height)
                return;

            // The scrollbar occupies the column just past the field. Its arrow caps step a line and its track pages,
            // which is everything a press can mean: dragging the thumb needs pointer motion the library does not
            // report, so the bar answers -1 there rather than jumping somewhere nobody asked for.
            if (column == _viewport.Width)
            {
                var bar = VerticalBar();

                // Taking hold of the thumb starts a drag rather than jumping anywhere: the moves that follow carry
                // it and the release lets go.
                if (bar.IsOnThumb(row))
                {
                    _draggingThumb = true;
                    return;
                }

                var scrolled = bar.PositionForPress(row);
                if (scrolled >= 0)
                {
                    _viewport.ScrollTo(scrolled, _viewport.FirstColumn);
                    _viewport.ClampToDocument(_buffer.LineCount);
                }

                return;
            }

            if (column < 0 || column >= _viewport.Width)
                return;

            // The press drops the selection anchor; every move until the release drags the other end behind it.
            _draggingText = true;

            _buffer.MoveTo(DocumentAt(row, column));
            _message = null;
            _viewport.EnsureVisible(CaretOnScreen());
        }

        /// <summary>
        ///     The screen row the document's first line is drawn on: the scene graph's status row, this screen's
        ///     leading newline, the menu bar and the frame's top edge, plus whatever an open menu panel is covering.
        /// </summary>
        private int FieldTopRow => 3;

        /// <summary>
        ///     Never called: <see cref="EditsText" /> is precisely the declaration that ENTER should arrive as a key
        ///     press instead, so nothing is ever collected into the buffer to submit. The base class declares it
        ///     abstract, so an empty body is the honest implementation rather than an oversight.
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
            // is what stops a keystroke landing in the document behind it.
            if (_menuBar != null && _menuBar.HandleKey(keyInfo))
            {
                ResizeViewport();
                return;
            }

            var shift = (keyInfo.Modifiers & ConsoleModifiers.Shift) != 0;
            var control = (keyInfo.Modifiers & ConsoleModifiers.Control) != 0;

            switch (keyInfo.Key)
            {
                case ConsoleKey.LeftArrow when control:
                    _buffer.MoveWordLeft(shift);
                    break;
                case ConsoleKey.RightArrow when control:
                    _buffer.MoveWordRight(shift);
                    break;
                case ConsoleKey.LeftArrow:
                    _buffer.MoveLeft(shift);
                    break;
                case ConsoleKey.RightArrow:
                    _buffer.MoveRight(shift);
                    break;
                case ConsoleKey.UpArrow:
                    _buffer.MoveUp(1, shift);
                    break;
                case ConsoleKey.DownArrow:
                    _buffer.MoveDown(1, shift);
                    break;
                case ConsoleKey.PageUp:
                    _buffer.MoveUp(_viewport.Height, shift);
                    break;
                case ConsoleKey.PageDown:
                    _buffer.MoveDown(_viewport.Height, shift);
                    break;
                case ConsoleKey.Home when control:
                    _buffer.MoveToStart(shift);
                    break;
                case ConsoleKey.End when control:
                    _buffer.MoveToEnd(shift);
                    break;
                case ConsoleKey.Home:
                    _buffer.MoveToLineStart(shift);
                    break;
                case ConsoleKey.End:
                    _buffer.MoveToLineEnd(shift);
                    break;
                case ConsoleKey.A when control:
                    _buffer.SelectAll();
                    break;

                // The clipboard three, each returning rather than breaking: they have something to say afterwards
                // and the tail below this switch clears the status line, which is right for typing and would wipe
                // them. They reveal the caret themselves for the same reason, since the menu reaches them too.
                //
                // There is no CTRL+C case and adding one would be dead code that compiles. The console processes
                // CTRL+C into a signal before anything can read it as a key, which is what keeps it the way out of
                // a program, so the key that copies is CTRL+INS.
                case ConsoleKey.X when control:
                case ConsoleKey.Delete when shift:
                    Cut();
                    return;
                case ConsoleKey.Insert when control:
                    Copy();
                    return;

                // The search keys return for the same reason the clipboard ones do: each has something to say and
                // the tail below this switch clears the status line.
                case ConsoleKey.F when control:
                    AskWhatToFind();
                    return;
                case ConsoleKey.H when control:
                    AskWhatToChange();
                    return;
                case ConsoleKey.F3 when shift:
                    FindNext(true);
                    return;
                case ConsoleKey.F3:
                    FindNext(false);
                    return;

                // The File menu has advertised this one all along without anything answering it.
                case ConsoleKey.F2:
                    SaveDocument();
                    return;
                case ConsoleKey.V when control:
                case ConsoleKey.Insert when shift:
                    Paste();
                    return;
                case ConsoleKey.Enter:
                    _buffer.InsertNewLine();
                    break;
                case ConsoleKey.Backspace:
                    _buffer.Backspace();
                    break;
                case ConsoleKey.Delete:
                    _buffer.Delete();
                    break;
                case ConsoleKey.Tab:
                    // One character in the document and several columns on screen; TabStops keeps those in step.
                    _buffer.Insert('\t');
                    break;
                default:
                    // Anything carrying a printable character is text. Control characters are not, which keeps CTRL
                    // combinations from being typed into the document as gibberish.
                    if (control || keyInfo.KeyChar == '\0' || char.IsControl(keyInfo.KeyChar))
                        return;

                    _buffer.Insert(keyInfo.KeyChar);
                    break;
            }

            _message = null;
            _viewport.EnsureVisible(CaretOnScreen());
        }

        /// <summary>
        ///     Takes the selection onto the clipboard and removes it. Three lines because
        ///     <see cref="TextBuffer" /> already knows what is selected and how to remove it; all this adds is
        ///     where the text goes in between.
        /// </summary>
        private void Cut()
        {
            if (!_buffer.HasSelection)
                return;

            var text = _buffer.GetSelectedText();
            UserData.Clipboard = text;
            _buffer.DeleteSelection();

            _message = Describe("Cut", text);
            _viewport.EnsureVisible(CaretOnScreen());
        }

        /// <summary>
        ///     Takes the selection onto the clipboard and leaves the document alone. It says how much it took
        ///     because it is the one edit with nothing to show for itself: without the status line, a copy and a key
        ///     that did nothing look exactly alike.
        /// </summary>
        private void Copy()
        {
            if (!_buffer.HasSelection)
                return;

            var text = _buffer.GetSelectedText();
            UserData.Clipboard = text;
            _message = Describe("Copied", text);
        }

        /// <summary>
        ///     Drops the clipboard in at the caret, over the selection when there is one, which is what every editor
        ///     does and what makes paste a replace as well as an insert. <c>TextBuffer.Insert</c> honours the
        ///     newlines inside it, so a paragraph arrives as a paragraph.
        /// </summary>
        private void Paste()
        {
            if (!UserData.HasClipboard)
                return;

            _buffer.Insert(UserData.Clipboard);

            _message = Describe("Pasted", UserData.Clipboard);
            _viewport.EnsureVisible(CaretOnScreen());
        }

        /// <summary>Removes the selection without touching the clipboard, which is the difference from Cut.</summary>
        private void ClearSelection()
        {
            if (!_buffer.HasSelection)
                return;

            _buffer.DeleteSelection();
            _viewport.EnsureVisible(CaretOnScreen());
        }

        /// <summary>Says what an edit moved, counted in whichever unit the amount makes readable.</summary>
        /// <param name="verb">What happened to it.</param>
        /// <param name="text">The text it happened to.</param>
        /// <returns>The status line to show.</returns>
        private static string Describe(string verb, string text)
        {
            var lines = 1;
            foreach (var character in text)
            {
                if (character == '\n')
                    lines++;
            }

            return lines > 1
                ? string.Format(CultureInfo.InvariantCulture, "{0} {1} lines.", verb, lines)
                : string.Format(CultureInfo.InvariantCulture, "{0} {1} characters.", verb, text.Length);
        }

        /// <summary>Asks what to look for, then looks for it.</summary>
        private void AskWhatToFind()
        {
            TextInputDialog.Prompt(
                SimUnit,
                "Find what?",
                text =>
                {
                    _searchText = text;
                    FindNext(false);
                },
                () => _message = "Find cancelled.",
                _searchText);
        }

        /// <summary>
        ///     Finds the next occurrence and selects it, or says it could not.
        ///     <para>
        ///         Where it searches from is the whole of making this work twice running. With a match selected it
        ///         resumes from the far end of that match rather than from the caret, which is what stops Find Next
        ///         from finding the same match forever; going backwards it starts from the near end for the mirror
        ///         of the same reason.
        ///     </para>
        /// </summary>
        /// <param name="backwards">TRUE to look towards the start of the document.</param>
        private void FindNext(bool backwards)
        {
            if (string.IsNullOrEmpty(_searchText))
            {
                AskWhatToFind();
                return;
            }

            var from = _buffer.HasSelection
                ? backwards ? _buffer.SelectionStart : _buffer.SelectionEnd
                : _buffer.Caret;

            var hit = TextSearch.Next(_buffer.Lines, _searchText, from, _matchCase, _wholeWord, backwards);
            if (hit == null)
            {
                _message = $"Cannot find \"{_searchText}\".";
                return;
            }

            SelectMatch(hit.Value);
            _message = null;
        }

        /// <summary>Selects a match and brings it on screen, start first so a long line does not hide it.</summary>
        /// <param name="start">Where the match begins.</param>
        private void SelectMatch(TextPosition start)
        {
            var end = new TextPosition(start.Line, start.Column + _searchText.Length);

            _buffer.Select(start, end);

            // Revealed from both ends. Revealing only the caret would scroll a wide line so that the end of the
            // match is on screen and its beginning is off the left edge, which is the half a person is reading.
            _viewport.EnsureVisible(OnScreen(start));
            _viewport.EnsureVisible(CaretOnScreen());
        }

        /// <summary>Asks what to change and what to change it to, then changes every one of them.</summary>
        private void AskWhatToChange()
        {
            // Two prompts because the library has no dialog that asks two things, exactly as Save As composes a
            // folder picker with a name prompt. Nested rather than sequential: the second only means anything once
            // the first has been answered.
            TextInputDialog.Prompt(
                SimUnit,
                "Change what?",
                find =>
                {
                    _searchText = find;

                    TextInputDialog.Prompt(
                        SimUnit,
                        "Change to what?",
                        ChangeAll,
                        () => _message = "Change cancelled.",
                        _replaceText);
                },
                () => _message = "Change cancelled.",
                _searchText);
        }

        /// <summary>
        ///     Replaces every occurrence, walking forward from the start of the document.
        ///     <para>
        ///         It searches without wrapping and resumes past what it just wrote, which is what makes it
        ///         terminate: changing "a" into "aa" would otherwise find its own output and keep going until the
        ///         document filled the machine.
        ///     </para>
        /// </summary>
        /// <param name="replacement">What to put in each occurrence's place.</param>
        private void ChangeAll(string replacement)
        {
            _replaceText = replacement ?? string.Empty;

            if (string.IsNullOrEmpty(_searchText))
                return;

            var changed = 0;
            var at = TextPosition.Start;

            while (true)
            {
                var hit = TextSearch.Next(_buffer.Lines, _searchText, at, _matchCase, _wholeWord, false, false);
                if (hit == null)
                    break;

                var start = hit.Value;
                _buffer.Select(start, new TextPosition(start.Line, start.Column + _searchText.Length));

                // Insert does nothing at all with empty text, so deleting the selection is the only way to say
                // "change this into nothing", which is a thing people really do want a Change All to do.
                if (_replaceText.Length == 0)
                    _buffer.DeleteSelection();
                else
                    _buffer.Insert(_replaceText);

                changed++;
                at = _buffer.Caret;
            }

            _message = changed == 0
                ? $"Cannot find \"{_searchText}\"."
                : $"Changed {changed} occurrence{(changed == 1 ? string.Empty : "s")}.";

            _viewport.EnsureVisible(CaretOnScreen());
        }

        /// <summary>Builds the pull-downs, styled to match the field they sit over.</summary>
        private void BuildMenus()
        {
            _menuBar = new MenuBar(
                new MenuBarMenu("File",
                    new MenuBarEntry("New", NewDocument),
                    new MenuBarEntry("Open...", OpenDocument),
                    new MenuBarEntry("Save", SaveDocument, "F2"),
                    new MenuBarEntry("Save As...", SaveDocumentAs),
                    MenuBarEntry.Separator(),
                    new MenuBarEntry("Exit", () => ParentWindow.ClearForm(), "Esc")),
                new MenuBarMenu("Edit",
                    // Each says for itself when it means anything, rather than being switched on and off from
                    // wherever the selection last changed. The menu asks at the moment it is drawn and at the
                    // moment an entry is chosen, so the greyed lines are always telling the truth.
                    //
                    // Copy answers to CTRL+INS rather than to CTRL+C, and that is not nostalgia. The console keeps
                    // ENABLE_PROCESSED_INPUT switched on so that CTRL+C stays the way out of a program, which means
                    // it is turned into a signal and never arrives here as a key at all. Printing "Ctrl+C" beside
                    // Copy would advertise a shortcut that quits the suite. CTRL+INS is what the editor this
                    // imitates used, and it arrives.
                    new MenuBarEntry("Cut", Cut, "Ctrl+X") {EnabledWhen = () => _buffer.HasSelection},
                    new MenuBarEntry("Copy", Copy, "Ctrl+Ins") {EnabledWhen = () => _buffer.HasSelection},
                    new MenuBarEntry("Paste", Paste, "Ctrl+V") {EnabledWhen = () => UserData.HasClipboard},
                    MenuBarEntry.Separator(),
                    new MenuBarEntry("Select All", _buffer.SelectAll, "Ctrl+A"),
                    new MenuBarEntry("Clear", ClearSelection, "Del") {EnabledWhen = () => _buffer.HasSelection}),
                new MenuBarMenu("Search",
                    new MenuBarEntry("Find...", AskWhatToFind, "Ctrl+F"),

                    // F3 rather than the F3 that used to sit beside Open, which was never wired to anything. It is
                    // also what the editor this imitates bound it to.
                    new MenuBarEntry("Find Next", () => FindNext(false), "F3")
                        {EnabledWhen = () => !string.IsNullOrEmpty(_searchText)},
                    new MenuBarEntry("Find Previous", () => FindNext(true), "Shift+F3")
                        {EnabledWhen = () => !string.IsNullOrEmpty(_searchText)},
                    MenuBarEntry.Separator(),
                    new MenuBarEntry("Change All...", AskWhatToChange, "Ctrl+H"),
                    MenuBarEntry.Separator(),

                    // Toggles rather than another dialog. The library has no dialog that asks a question and offers
                    // two checkboxes beside it, and a menu that shows its own state is the better answer anyway:
                    // the setting is visible without opening anything, which a checkbox in a dialog is not.
                    new MenuBarEntry("Match Case", () => _matchCase = !_matchCase)
                        {CheckedWhen = () => _matchCase},
                    new MenuBarEntry("Whole Word", () => _wholeWord = !_wholeWord)
                        {CheckedWhen = () => _wholeWord}),
                new MenuBarMenu("Options",
                    // Marked, because two entries offering a choice with no sign of which one is in force is a menu
                    // that makes you change the setting to find out what it was.
                    new MenuBarEntry("Tab width 4", () => SetTabWidth(4))
                        {CheckedWhen = () => _buffer.TabWidth == 4},
                    new MenuBarEntry("Tab width 8", () => SetTabWidth(8))
                        {CheckedWhen = () => _buffer.TabWidth == 8}),
                new MenuBarMenu("Help",
                    new MenuBarEntry("About", ShowAbout)) {AlignRight = true})
            {
                BarStyle = DosTheme.MenuBar,
                HighlightStyle = DosTheme.MenuHighlight,
                PanelStyle = DosTheme.MenuPanel,
                PanelHighlightStyle = DosTheme.MenuHighlight,

                // The square root sign is what an MS-DOS editor ticked a menu entry with, and the console's own
                // code page is where that glyph came from.
                CheckMark = '\u221A',

                // The bar is the first row this form draws, and the scene graph puts its own status line above it.
                BarRow = 1,

                // The panel hangs from the bar and its first row covers the frame's top edge, one above the field.
                // A menu that started lower would leave the frame's border showing between it and its own title.
                PanelRow = FieldTopRow - 1
            };
        }

        /// <summary>
        ///     The scrollbar as the frame draws it, so a press is measured against the same bar that is on screen.
        ///     Rebuilt rather than kept, because every number in it is derived from the document and the viewport.
        /// </summary>
        /// <returns>The vertical bar.</returns>
        private ScrollBar VerticalBar()
        {
            return new ScrollBar
            {
                Length = _viewport.Height,
                Total = _buffer.LineCount,
                Visible = _viewport.Height,
                Position = _viewport.FirstLine
            };
        }

        /// <summary>Opens the file browser, starting where the samples are.</summary>
        private void OpenDocument()
        {
            FileDialog.OpenFile(
                SimUnit,
                DocumentLibrary.BrowseFolder,
                new[] {".txt", ".md", ".log", ".csv"},
                LoadDocument,
                () => _message = "Open cancelled.");
        }

        /// <summary>Writes the document back where it came from, or asks where to put it when it has no home yet.</summary>
        private void SaveDocument()
        {
            if (string.IsNullOrEmpty(_path))
            {
                SaveDocumentAs();
                return;
            }

            WriteTo(_path);
        }

        /// <summary>
        ///     Asks for a folder and then a name.
        ///     <para>
        ///         Two dialogs because the library has no Save As: <c>FileDialog</c> offers opening a file and
        ///         picking a folder, and neither of those is "name a file that does not exist yet". Composing the
        ///         two here is the honest version, and if it proves out it is what a <c>FileDialog.SaveFile</c>
        ///         would be built from.
        ///     </para>
        /// </summary>
        private void SaveDocumentAs()
        {
            FileDialog.SelectFolder(
                SimUnit,
                DocumentLibrary.BrowseFolder,
                folder => TextInputDialog.Prompt(
                    SimUnit,
                    "Save as which file name?",
                    name => WriteTo(Path.Combine(folder, name)),
                    () => _message = "Save cancelled.",
                    _path == null ? "untitled.txt" : Path.GetFileName(_path),
                    false,
                    Validate),
                () => _message = "Save cancelled.");
        }

        /// <summary>Refuses a name that is not one, before a dialog closes on it.</summary>
        /// <param name="name">What the user typed.</param>
        /// <returns>The complaint, or null when the name is usable.</returns>
        private static string Validate(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "A file name is needed.";

            return name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ? "That name has characters a file cannot have." : null;
        }

        /// <summary>Writes the document to a path and says how it went.</summary>
        /// <param name="path">Where to write.</param>
        private void WriteTo(string path)
        {
            if (!DocumentLibrary.TrySave(path, _buffer.GetText(), out var error))
            {
                _message = $"Could not save {Path.GetFileName(path)}: {error}";
                return;
            }

            _path = path;
            _buffer.MarkSaved();
            _message = $"Saved {Path.GetFileName(path)}.";
        }

        /// <summary>Starts an empty document.</summary>
        private void NewDocument()
        {
            _buffer.SetText(string.Empty);
            _path = null;
            _message = null;
            _viewport.ScrollTo(0, 0);
        }

        /// <summary>Changes how wide a tab is drawn, which is a view setting rather than an edit.</summary>
        /// <param name="width">Columns between tab stops.</param>
        private void SetTabWidth(int width)
        {
            _buffer.TabWidth = width;
            _message = $"Tab width is now {width}.";
        }

        /// <summary>Says what this is.</summary>
        private void ShowAbout()
        {
            _message = "WolfCurses word processor - a text editor built on the WolfCurses library.";
        }

        /// <summary>Reads a document, or leaves the buffer alone and says why it could not.</summary>
        /// <param name="path">The file to read.</param>
        private void LoadDocument(string path)
        {
            var text = DocumentLibrary.TryLoad(path, out var error);
            if (text == null)
            {
                _message = $"Could not open {Path.GetFileName(path)}: {error}";
                return;
            }

            _buffer.SetText(text);
            _path = path;
            _message = null;
            _viewport.ScrollTo(0, 0);
        }

        /// <summary>Sizes the viewport to what the frame leaves it, and keeps the caret inside.</summary>
        private void ResizeViewport()
        {
            var width = Math.Max(24, AnsiConsole.SafeWindowWidth() - 1);
            var height = AnsiConsole.SafeWindowHeight();


            var columns = width - EditorChrome.ChromeColumns;
            var rows = EditorChrome.Rows(height, ReservedRows);
            var resized = columns != _viewport.Width || rows != _viewport.Height;

            _viewport.Resize(columns, rows);
            _viewport.ClampToDocument(_buffer.LineCount);

            // Only when the window really changed shape.
            //
            // This runs on every simulation tick, and revealing the caret unconditionally means the view is dragged
            // back to it once a second: scrolling with the scrollbar would move the document and then snap straight
            // back, which looks like the scrollbar not working at all. Scrolling and the caret are different things,
            // and the caret is revealed by the things that move it.
            if (resized)
                _viewport.EnsureVisible(CaretOnScreen());
        }

        /// <summary>
        ///     Where the caret is <i>drawn</i>, which is not where it is stored once the line contains a tab. The
        ///     viewport scrolls in screen columns, so this is what it has to be told about.
        /// </summary>
        /// <returns>The caret's position in screen coordinates.</returns>
        private TextPosition CaretOnScreen()
        {
            return OnScreen(_buffer.Caret);
        }

        /// <summary>Where a stored position is drawn, which is a different column as soon as the line has a tab.</summary>
        /// <param name="position">The position as it is stored.</param>
        /// <returns>The same position in screen columns.</returns>
        private TextPosition OnScreen(TextPosition position)
        {
            var column = TabStops.ToDisplayColumn(_buffer.GetLine(position.Line), position.Column, _buffer.TabWidth);

            return new TextPosition(position.Line, column);
        }

        /// <summary>What the frame's tab reads: the file, and whether it has been touched.</summary>
        /// <returns>The title.</returns>
        private string Title()
        {
            var name = _path == null ? "Untitled" : Path.GetFileName(_path);
            return _buffer.IsModified ? name + " *" : name;
        }

        /// <summary>The key-hint strip, or whatever the last action had to say.</summary>
        /// <returns>The status text.</returns>
        private string StatusText()
        {
            // The column a person means is the one they can see, so this reports the drawn column rather than the
            // character index. On a line with no tabs the two are the same.
            var caret = CaretOnScreen();

            var position = string.Format(CultureInfo.InvariantCulture,
                "Ln {0}, Col {1}   {2} lines{3}",
                caret.Line + 1,
                caret.Column + 1,
                _buffer.LineCount,
                _buffer.HasSelection ? $"   {_buffer.GetSelectedText().Length} selected" : string.Empty);

            return string.IsNullOrEmpty(_message)
                ? $"  ALT=Menu   ESC=Suite   {position}"
                : $"  {_message}   {position}";
        }
    }
}
