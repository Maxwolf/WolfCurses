// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Globalization;
using System.IO;
using System.Text;
using WolfCurses.Controls;
using WolfCurses.Documents;
using WolfCurses.Graphics;
using WolfCurses.Window;
using WolfCurses.Window.Form;

namespace WolfCurses.Apps.Basic
{
    /// <summary>
    ///     The BASIC environment: a program in an editor above, and the screen it draws on when you run it.
    ///     <para>
    ///         <b>Almost none of the editing is written here.</b> The document, the scrolling, the tab columns and
    ///         the drawing of it all come from <c>WolfCurses.Documents</c>, which is what the word processor
    ///         established and this is the second user of. What is left in this file is the two things that really
    ///         are its own: which key runs a program, and how a running one is paced.
    ///     </para>
    ///     <para>
    ///         <b>A running program is stepped, not run.</b> A BASIC program is entitled to loop forever, which is
    ///         what a game does between frames, so calling <c>Run</c> from a screen would simply stop the screen.
    ///         Instead a slice of statements is executed per frame and control comes straight back, which is what
    ///         keeps the interface alive and what lets ESC stop a program that has no intention of stopping.
    ///     </para>
    /// </summary>
    [ParentWindow(typeof (OfficeWindow))]
    public sealed class BasicDialog : Form<OfficeWindowInfo>, IHandlesEscape
    {
        /// <summary>How many statements to run per frame. Enough to feel instant, few enough to stay responsive.</summary>
        private const int StatementsPerFrame = 2000;

        /// <summary>Rows outside this screen: the scene graph's status line above and the input prompt below.</summary>
        private const int ReservedRows = 3;

        /// <summary>The program being edited.</summary>
        private readonly TextBuffer _buffer = new();

        /// <summary>How often a slice of the program runs.</summary>
        private readonly IntervalTimer _pace = new(TimeSpan.FromMilliseconds(16));

        /// <summary>The window onto the program text.</summary>
        private readonly TextViewport _viewport = new();

        /// <summary>What has been typed toward the line a waiting INPUT wants.</summary>
        private readonly StringBuilder _typed = new();

        /// <summary>Whether a running program has stopped to ask for a line of input.</summary>
        private bool _awaitingInput;

        /// <summary>Where the running program has got to.</summary>
        private int _index;

        /// <summary>Which statement to run again once a waiting INPUT has its answer.</summary>
        private int _resumeAt;

        /// <summary>What the status line has to say.</summary>
        private string _message;

        /// <summary>The file the program came from, or null.</summary>
        private string _path;

        /// <summary>The compiled program, while one is running.</summary>
        private BasicProgram _program;

        /// <summary>Where a running program keeps its variables.</summary>
        private BasicRuntime _runtime;

        /// <summary>The screen a running program writes on.</summary>
        private BasicScreen _screen;

        /// <summary>Initializes a new instance of the <see cref="BasicDialog" /> class.</summary>
        /// <param name="window">The parent window.</param>
        public BasicDialog(IWindow window) : base(window)
        {
        }

        /// <summary>ENTER and BACKSPACE arrive as key presses, which is the only way to edit a program.</summary>
        public override bool EditsText => true;

        /// <summary>Typed characters go into the program, not into the prompt underneath it.</summary>
        public override bool InputFillsBuffer => false;

        /// <summary>Whether a program is running or has just finished and is still showing its screen.</summary>
        private bool Showing => _screen != null;

        /// <inheritdoc />
        public bool TryHandleEscape()
        {
            if (!Showing)
                return false;

            // ESC takes the output screen away and goes back to the program, which is the only way out of one that
            // loops forever. Only when nothing is showing does it mean "leave the application".
            Stop("Stopped.");
            return true;
        }

        /// <inheritdoc />
        public override void OnFormPostCreate()
        {
            base.OnFormPostCreate();

            Load(BasicLibrary.DefaultProgramPath);
            ResizeViewport();
        }

        /// <inheritdoc />
        public override void OnTick(bool systemTick, bool skipDay)
        {
            base.OnTick(systemTick, skipDay);

            if (!systemTick)
                ResizeViewport();

            // A program waiting for input is not running: the keystrokes it is waiting for arrive through this
            // same screen, so stepping it here would spin without ever letting anybody answer.
            if (_program == null || _awaitingInput || !_pace.TryConsume())
                return;

            RunSlice();
        }

        /// <summary>Runs the next slice of the program and stops when it finishes or goes wrong.</summary>
        private void RunSlice()
        {
            try
            {
                _index = _program.Step(_runtime, _index, StatementsPerFrame);

                if (!_program.IsRunning(_index))
                    Finish("Program finished. ESC returns to the listing.");
            }
            catch (BasicInputRequest request)
            {
                // Not a failure: the program wants a line. The prompt is written here rather than by the host,
                // because the host is asked twice for one INPUT and would print the question twice.
                _awaitingInput = true;
                _resumeAt = request.ResumeAt;
                _typed.Clear();

                _screen.Write(request.Prompt);
                _message = "Type an answer and press ENTER.";
            }
            catch (BasicError error)
            {
                // The line is the whole value of the message, because it is what the user goes and looks at.
                Finish(error.Message);
            }
        }

        /// <inheritdoc />
        public override string OnRenderForm()
        {
            ParentWindow.PromptText = Showing
                ? "ESC returns to the listing:"
                : "F5 runs the program, ESC returns to the suite:";

            var width = Math.Max(24, AnsiConsole.SafeWindowWidth() - 1);

            // The leading newline is load-bearing rather than spacing: the scene graph appends this text straight
            // onto its own status row, so a screen that does not start one has its first line printed on the end
            // of that row.
            return Environment.NewLine + (Showing ? RenderOutput(width) : RenderListing(width));
        }

        /// <summary>Draws the program listing.</summary>
        /// <param name="width">The console width.</param>
        /// <returns>The screen.</returns>
        private string RenderListing(int width)
        {
            var screen = new StringBuilder();

            screen.Append(DosTheme.Title.Apply(Fit(" " + Title(), width))).Append(Environment.NewLine);

            // The library draws the document. That is the whole reason DocumentView is in the package rather than
            // in the word processor's folder: this is the second screen to want exactly the same thing.
            foreach (var row in DocumentView.Render(_buffer, _viewport, DosTheme.Field, DosTheme.Selection))
                screen.Append(row).Append(Environment.NewLine);

            screen.Append(DosTheme.Status.Apply(Fit(" " + Status(), width)));
            return screen.ToString();
        }

        /// <summary>Draws whatever the running program has put on its screen.</summary>
        /// <param name="width">The console width.</param>
        /// <returns>The screen.</returns>
        private string RenderOutput(int width)
        {
            var screen = new StringBuilder();

            // Told the room it has rather than left to guess: a picture is fitted to the space it is given, and
            // the screen has no way of knowing how much of the terminal this form kept for itself.
            screen.Append(_screen.Render(width, Math.Max(1, AnsiConsole.SafeWindowHeight() - ReservedRows - 1)))
                .Append(Environment.NewLine);
            screen.Append(DosTheme.Status.Apply(Fit(" " + (_message ?? "Running..."), width)));

            return screen.ToString();
        }

        /// <summary>
        ///     Never called: <see cref="EditsText" /> is the declaration that ENTER arrives as a key press instead,
        ///     so nothing is ever collected into the buffer to submit. The base class declares it abstract, so an
        ///     empty body is the honest implementation rather than an oversight.
        /// </summary>
        /// <param name="input">Unused.</param>
        public override void OnInputBufferReturned(string input)
        {
        }

        /// <inheritdoc />
        public override void OnKeyPressed(ConsoleKeyInfo keyInfo)
        {
            base.OnKeyPressed(keyInfo);

            if (_awaitingInput)
            {
                TypeAnswer(keyInfo);
                return;
            }

            if (Showing)
            {
                // Everything typed while a program is up belongs to the program, which is what INKEY$ reads. ESC is
                // the exception and the window has already had it.
                _screen.PendingKey = keyInfo.KeyChar == '\0' ? string.Empty : keyInfo.KeyChar.ToString();
                return;
            }

            var shift = (keyInfo.Modifiers & ConsoleModifiers.Shift) != 0;
            var control = (keyInfo.Modifiers & ConsoleModifiers.Control) != 0;

            switch (keyInfo.Key)
            {
                case ConsoleKey.F5:
                    Start();
                    return;
                case ConsoleKey.F3:
                    Open();
                    return;
                case ConsoleKey.A when control:
                    _buffer.SelectAll();
                    break;
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
                case ConsoleKey.Home:
                    _buffer.MoveToLineStart(shift);
                    break;
                case ConsoleKey.End:
                    _buffer.MoveToLineEnd(shift);
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
                    _buffer.Insert('\t');
                    break;
                default:
                    if (control || keyInfo.KeyChar == '\0' || char.IsControl(keyInfo.KeyChar))
                        return;

                    _buffer.Insert(keyInfo.KeyChar);
                    break;
            }

            _message = null;
            _viewport.EnsureVisible(CaretOnScreen());
        }

        /// <summary>
        ///     Collects the line a waiting INPUT asked for, echoing it where the program left its cursor, which is
        ///     what makes an answer appear after the question rather than somewhere else.
        /// </summary>
        /// <param name="keyInfo">The key that was pressed.</param>
        private void TypeAnswer(ConsoleKeyInfo keyInfo)
        {
            switch (keyInfo.Key)
            {
                case ConsoleKey.Enter:
                    _screen.WriteLine();
                    _screen.SupplyAnswer(_typed.ToString());

                    // Back to the statement that asked. It runs again from the top, and this time the host has an
                    // answer to give it.
                    _awaitingInput = false;
                    _index = _resumeAt;
                    _message = null;
                    return;
                case ConsoleKey.Backspace:
                    if (_typed.Length == 0)
                        return;

                    _typed.Length--;
                    _screen.Backspace();
                    return;
                default:
                    if (keyInfo.KeyChar == '\0' || char.IsControl(keyInfo.KeyChar))
                        return;

                    _typed.Append(keyInfo.KeyChar);
                    _screen.Write(keyInfo.KeyChar.ToString());
                    return;
            }
        }

        /// <summary>Compiles the program and starts it, or says why it will not compile.</summary>
        private void Start()
        {
            var height = AnsiConsole.SafeWindowHeight();
            var width = Math.Max(24, AnsiConsole.SafeWindowWidth() - 1);

            // Audible here and nowhere else. The screen is silent by construction so that a test run does not
            // beep its way through the shipped programs.
            _screen = new BasicScreen(width, Math.Max(1, height - ReservedRows - 1), true);
            _runtime = new BasicRuntime(_screen);
            _awaitingInput = false;
            _typed.Clear();
            _message = null;

            try
            {
                // Compiling and running are separated so a program that will not compile never shows an output
                // screen at all: the mistake is in the listing, which is what you want to be looking at.
                _program = BasicProgram.Compile(_buffer.GetText());
                _index = 0;
            }
            catch (BasicError error)
            {
                _screen = null;
                _program = null;
                _message = error.Message;
            }
        }

        /// <summary>Stops a running program and takes its screen away.</summary>
        /// <param name="reason">What to say about it.</param>
        private void Stop(string reason)
        {
            // A tune going on after ESC would be the clearest possible sign that ESC had not worked.
            _screen?.Silence();

            _awaitingInput = false;
            _typed.Clear();
            _program = null;
            _runtime = null;
            _screen = null;
            _message = reason;
        }

        /// <summary>Ends a running program but leaves its screen up, which is what you want to look at.</summary>
        /// <param name="reason">What to say about it.</param>
        private void Finish(string reason)
        {
            _awaitingInput = false;
            _typed.Clear();
            _program = null;
            _message = reason;
        }

        /// <summary>Opens a program from disk.</summary>
        private void Open()
        {
            FileDialog.OpenFile(
                SimUnit,
                BasicLibrary.BrowseFolder,
                new[] {".bas", ".txt"},
                Load,
                () => _message = "Open cancelled.");
        }

        /// <summary>Reads a program, or says why it could not.</summary>
        /// <param name="path">The file.</param>
        private void Load(string path)
        {
            var text = BasicLibrary.TryLoad(path, out var error);
            if (text == null)
            {
                _message = "Could not open " + Path.GetFileName(path) + ": " + error;
                return;
            }

            _buffer.SetText(text);
            _path = path;
            _message = null;
            _viewport.ScrollTo(0, 0);
        }

        /// <summary>Sizes the viewport to what is left after the title and the status line.</summary>
        private void ResizeViewport()
        {
            var width = Math.Max(24, AnsiConsole.SafeWindowWidth() - 1);
            var height = AnsiConsole.SafeWindowHeight();

            var rows = Math.Max(1, height - ReservedRows - 2);
            var resized = width != _viewport.Width || rows != _viewport.Height;

            _viewport.Resize(width, rows);
            _viewport.ClampToDocument(_buffer.LineCount);

            if (resized)
                _viewport.EnsureVisible(CaretOnScreen());
        }

        /// <summary>Where the caret is drawn, which is a different column once the line contains a tab.</summary>
        /// <returns>The caret in screen columns.</returns>
        private TextPosition CaretOnScreen()
        {
            var caret = _buffer.Caret;
            var column = TabStops.ToDisplayColumn(_buffer.GetLine(caret.Line), caret.Column, _buffer.TabWidth);

            return new TextPosition(caret.Line, column);
        }

        /// <summary>What the title bar reads.</summary>
        /// <returns>The title.</returns>
        private string Title()
        {
            var name = _path == null ? "Untitled" : Path.GetFileName(_path);
            return (_buffer.IsModified ? name + " *" : name) + "   BASIC";
        }

        /// <summary>What the status line reads.</summary>
        /// <returns>The status.</returns>
        private string Status()
        {
            var position = string.Format(CultureInfo.InvariantCulture, "Ln {0}, Col {1}   {2} lines",
                _buffer.Caret.Line + 1, _buffer.Caret.Column + 1, _buffer.LineCount);

            return string.IsNullOrEmpty(_message)
                ? "F5=Run  F3=Open  ESC=Suite   " + position
                : _message + "   " + position;
        }

        /// <summary>Pads or trims text to exactly a width, so a styled strip covers its whole row and no more.</summary>
        private static string Fit(string text, int width)
        {
            text ??= string.Empty;
            return text.Length > width ? text.Substring(0, width) : text.PadRight(width);
        }
    }
}
