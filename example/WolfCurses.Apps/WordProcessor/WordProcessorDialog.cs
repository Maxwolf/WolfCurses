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
                   EditorChrome.Compose(_menuBar, _buffer, _viewport, Title(), StatusText(), width);
        }

        /// <inheritdoc />
        public override void OnMousePressed(MouseEvent mouse)
        {
            base.OnMousePressed(mouse);

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
                var scrolled = VerticalBar().PositionForPress(row);
                if (scrolled >= 0)
                {
                    _viewport.ScrollTo(scrolled, _viewport.FirstColumn);
                    _viewport.ClampToDocument(_buffer.LineCount);
                }

                return;
            }

            if (column < 0 || column >= _viewport.Width)
                return;

            // Two translations, and both are needed. The viewport turns a screen cell into a place in the document,
            // and TabStops turns the screen column into a character index, which are different numbers the moment
            // the line is indented.
            var at = _viewport.ToDocument(row, column);
            var line = Math.Clamp(at.Line, 0, _buffer.LineCount - 1);
            var documentColumn = TabStops.ToDocumentColumn(_buffer.GetLine(line), at.Column, _buffer.TabWidth);

            _buffer.MoveTo(new TextPosition(line, documentColumn));
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

        /// <summary>Builds the pull-downs, styled to match the field they sit over.</summary>
        private void BuildMenus()
        {
            _menuBar = new MenuBar(
                new MenuBarMenu("File",
                    new MenuBarEntry("New", NewDocument),
                    new MenuBarEntry("Open...", OpenDocument, "F3"),
                    new MenuBarEntry("Save", SaveDocument, "F2"),
                    new MenuBarEntry("Save As...", SaveDocumentAs),
                    MenuBarEntry.Separator(),
                    new MenuBarEntry("Exit", () => ParentWindow.ClearForm(), "Esc")),
                new MenuBarMenu("Edit",
                    new MenuBarEntry("Cut", null, "Ctrl+X") {IsEnabled = false},
                    new MenuBarEntry("Copy", null, "Ctrl+C") {IsEnabled = false},
                    new MenuBarEntry("Paste", null, "Ctrl+V") {IsEnabled = false},
                    MenuBarEntry.Separator(),
                    new MenuBarEntry("Select All", _buffer.SelectAll, "Ctrl+A"),
                    new MenuBarEntry("Clear", _buffer.DeleteSelection, "Del")),
                new MenuBarMenu("Search",
                    new MenuBarEntry("Find...", null, "Ctrl+F") {IsEnabled = false}),
                new MenuBarMenu("Options",
                    new MenuBarEntry("Tab width 4", () => SetTabWidth(4)),
                    new MenuBarEntry("Tab width 8", () => SetTabWidth(8))),
                new MenuBarMenu("Help",
                    new MenuBarEntry("About", ShowAbout)) {AlignRight = true})
            {
                BarStyle = DosTheme.MenuBar,
                HighlightStyle = DosTheme.MenuHighlight,
                PanelStyle = DosTheme.MenuPanel,
                PanelHighlightStyle = DosTheme.MenuHighlight,

                // The bar is the first row this form draws, and the scene graph puts its own status line above it.
                BarRow = 1,

                // The panel is drawn over the field rather than under the bar, so the frame's top edge sits between
                // them and the panel's own first row is the field's.
                PanelRow = FieldTopRow
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


            _viewport.Resize(width - EditorChrome.ChromeColumns, EditorChrome.Rows(height, ReservedRows));
            _viewport.ClampToDocument(_buffer.LineCount);
            _viewport.EnsureVisible(CaretOnScreen());
        }

        /// <summary>
        ///     Where the caret is <i>drawn</i>, which is not where it is stored once the line contains a tab. The
        ///     viewport scrolls in screen columns, so this is what it has to be told about.
        /// </summary>
        /// <returns>The caret's position in screen coordinates.</returns>
        private TextPosition CaretOnScreen()
        {
            var caret = _buffer.Caret;
            var column = TabStops.ToDisplayColumn(_buffer.GetLine(caret.Line), caret.Column, _buffer.TabWidth);

            return new TextPosition(caret.Line, column);
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
