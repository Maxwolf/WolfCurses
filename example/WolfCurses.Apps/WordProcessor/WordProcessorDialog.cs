// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/20/2026

using System;
using System.Globalization;
using System.IO;
using System.Text;
using WolfCurses.Documents;
using WolfCurses.Graphics;
using WolfCurses.Window;
using WolfCurses.Window.Form;

namespace WolfCurses.Apps.WordProcessor
{
    /// <summary>
    ///     The word processor. The only screen in this repository with a caret in it, and the reason
    ///     <see cref="TextBuffer" /> and <see cref="TextViewport" /> exist in the library at all.
    ///     <para>
    ///         Almost nothing here is about editing text. The buffer holds the document and moves the caret, the
    ///         viewport decides what is on screen and keeps the caret inside it, and <see cref="DocumentView" />
    ///         draws it. What is left in this file is the part that really is application-specific: which key means
    ///         what, and what the status line says.
    ///     </para>
    ///     <para>
    ///         <b>Two opt-ins make it work at all.</b> <see cref="EditsText" /> is what delivers ENTER and BACKSPACE
    ///         here as key presses instead of spending them on the input buffer, without which a backspace is
    ///         unreachable. <see cref="InputFillsBuffer" /> is false so that typing a document does not also fill the
    ///         prompt underneath it with the same characters.
    ///     </para>
    ///     <para>
    ///         ESC is not handled here and must not be: <c>AppsWindow</c> catches it for every application at once.
    ///     </para>
    /// </summary>
    [ParentWindow(typeof (AppsWindow))]
    public sealed class WordProcessorDialog : Form<AppsWindowInfo>
    {
        /// <summary>
        ///     Console rows this screen spends on everything that is not the document: the scene graph's own status
        ///     line above, this screen's status line, and the input prompt below, plus slack.
        /// </summary>
        private const int ChromeRows = 6;

        /// <summary>The document being edited.</summary>
        private readonly TextBuffer _buffer = new();

        /// <summary>The window onto it.</summary>
        private readonly TextViewport _viewport = new();

        /// <summary>What the status line has to say, such as why a file would not open.</summary>
        private string _message;

        /// <summary>The file the document came from, or null for a document that has never been on disk.</summary>
        private string _path;

        /// <summary>Initializes a new instance of the <see cref="WordProcessorDialog" /> class.</summary>
        /// <param name="window">The parent window.</param>
        public WordProcessorDialog(IWindow window) : base(window)
        {
        }

        /// <summary>
        ///     ENTER and BACKSPACE arrive as key presses rather than as input-buffer control, which is the only way
        ///     an editor can see a backspace at all.
        /// </summary>
        public override bool EditsText => true;

        /// <summary>Typed characters go into the document, not into the prompt underneath it.</summary>
        public override bool InputFillsBuffer => false;

        /// <inheritdoc />
        public override void OnFormPostCreate()
        {
            base.OnFormPostCreate();

            LoadDocument(DocumentLibrary.DefaultDocumentPath);
            ResizeViewport();
        }

        /// <inheritdoc />
        public override void OnTick(bool systemTick, bool skipDay)
        {
            base.OnTick(systemTick, skipDay);

            // Once a second rather than per frame. Reading the console size is a live syscall and OnRenderForm runs
            // about a thousand times a second, so sizing there would spend two syscalls a frame to notice a resize
            // that happens approximately never. The same reasoning as the arcade choosing its board size once.
            if (!systemTick)
                ResizeViewport();
        }

        /// <inheritdoc />
        public override string OnRenderForm()
        {
            ParentWindow.PromptText = "Arrows move, SHIFT selects, ESC returns to the menu:";

            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine(StatusLine());
            sb.Append(DocumentView.Render(_buffer, _viewport));
            return sb.ToString();
        }

        /// <summary>
        ///     Never called, and it has to exist anyway.
        ///     <para>
        ///         This is the method ENTER would normally arrive at, and <see cref="EditsText" /> is precisely the
        ///         declaration that this screen wants ENTER as a key press instead. The base class still declares it
        ///         abstract, so the empty body is the honest implementation rather than an oversight: a line can
        ///         never be submitted here because nothing is ever collected into the buffer.
        ///     </para>
        /// </summary>
        /// <param name="input">Unused.</param>
        public override void OnInputBufferReturned(string input)
        {
        }

        /// <inheritdoc />
        public override void OnKeyPressed(ConsoleKeyInfo keyInfo)
        {
            base.OnKeyPressed(keyInfo);

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
                    // A tab is one character in the document and several columns on screen; TabStops is what keeps
                    // those two facts in step everywhere it matters.
                    _buffer.Insert('\t');
                    break;
                default:
                    // Anything that carries a printable character is text. Control characters are not, which is what
                    // keeps CTRL combinations and TAB from being typed into the document as gibberish.
                    if (control || keyInfo.KeyChar == '\0' || char.IsControl(keyInfo.KeyChar))
                        return;

                    _buffer.Insert(keyInfo.KeyChar);
                    break;
            }

            _message = null;
            _viewport.EnsureVisible(CaretOnScreen());
        }

        /// <summary>
        ///     Where the caret is <i>drawn</i>, which is not where it is stored the moment the line contains a tab.
        ///     The viewport scrolls in screen columns, so this is what it has to be told about; the buffer keeps the
        ///     character index, and <see cref="TabStops" /> is the bridge.
        /// </summary>
        /// <returns>The caret's position in screen coordinates.</returns>
        private TextPosition CaretOnScreen()
        {
            var caret = _buffer.Caret;
            var column = TabStops.ToDisplayColumn(_buffer.GetLine(caret.Line), caret.Column, _buffer.TabWidth);

            return new TextPosition(caret.Line, column);
        }

        /// <summary>
        ///     Reads a document into the buffer, or leaves the buffer alone and says why it could not.
        ///     <para>
        ///         A failed open is deliberately not fatal and not a dialog: the samples folder is missing entirely
        ///         when the app is run from a build that did not copy content, and an editor that refused to start in
        ///         that case would be harder to diagnose than one that opens empty and says so on its status line.
        ///     </para>
        /// </summary>
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

        /// <summary>Sizes the viewport to the console and keeps the caret inside it.</summary>
        private void ResizeViewport()
        {
            var width = AnsiConsole.SafeWindowWidth();
            var height = AnsiConsole.SafeWindowHeight();

            // One column short of the console, because a row that fills the last cell scrolls a classic console.
            _viewport.Resize(Math.Max(20, width - 1), Math.Max(4, height - ChromeRows));
            _viewport.ClampToDocument(_buffer.LineCount);
            _viewport.EnsureVisible(CaretOnScreen());
        }

        /// <summary>The line above the document: what is being edited, where the caret is, and anything gone wrong.</summary>
        /// <returns>The status line.</returns>
        private string StatusLine()
        {
            if (!string.IsNullOrEmpty(_message))
                return _message;

            var name = _path == null ? "Untitled" : Path.GetFileName(_path);

            // The column a person means is the one they can see, so this reports the drawn column rather than the
            // character index. On a line with no tabs the two are the same; on an indented one they are not, and
            // the visible number is the useful one.
            var caret = CaretOnScreen();

            return string.Format(CultureInfo.InvariantCulture,
                "{0}{1}   Ln {2}, Col {3}   {4} lines{5}",
                name,
                _buffer.IsModified ? " *" : string.Empty,
                caret.Line + 1,
                caret.Column + 1,
                _buffer.LineCount,
                _buffer.HasSelection ? $"   {_buffer.GetSelectedText().Length} selected" : string.Empty);
        }
    }
}
