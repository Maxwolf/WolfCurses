// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/22/2026

using System;
using System.Collections.Generic;
using System.Globalization;
using WolfCurses.Documents;
using WolfCurses.Graphics;
using WolfCurses.Window;
using WolfCurses.Window.Control;
using WolfCurses.Window.Form;

namespace WolfCurses.Demo.Screens
{
    /// <summary>
    ///     A tiny editor over a string constant, and the demonstration of <see cref="WolfCurses.Documents" />: a
    ///     <see cref="TextBuffer" /> holding the document and the caret, a <see cref="TextViewport" /> deciding what
    ///     is on screen, <see cref="TabStops" /> and <see cref="ControlPictures" /> turning a stored line into one
    ///     that can be drawn, and <see cref="TextSearch" /> and <see cref="TextWords" /> reading it.
    ///     <para>
    ///         <b>The first thing to say is why this screen may exist at all.</b> ENTER and BACKSPACE are otherwise
    ///         <i>unreachable</i>: <c>InputManager.SendConsoleKey</c> spends ENTER submitting the input buffer and
    ///         BACKSPACE rubbing a character out of it before either is ever queued as a key press, so a
    ///         <c>case ConsoleKey.Backspace:</c> in an ordinary form is dead code that compiles and never runs, and
    ///         no editor can be written without a backspace. <see cref="EditsText" /> is the one way out of that,
    ///         and this is the screen that takes it. The price is the buffer's meaning of those
    ///         two keys, which is the point rather than a cost: a screen editing a document is not collecting a
    ///         command. <see cref="InputFillsBuffer" /> goes with it so typed characters land in the document rather
    ///         than piling up in a prompt nobody is reading.
    ///     </para>
    ///     <para>
    ///         <b>Which leaves how the screen is left, and it is not ENTER.</b> ENTER splits a line here, so
    ///         <see cref="OnInputBufferReturned" /> never fires; what this relies on instead is
    ///         <see cref="DemoWindow" />'s global ESC override, which clears the attached form whatever screen is
    ///         showing. Turning the prompt off has a second consequence worth knowing before copying this file:
    ///         <c>SceneGraph</c> only draws the prompt line while the focused window is accepting input, so the key
    ///         strip is composed into the body here rather than left to <c>PromptText</c>.
    ///     </para>
    ///     <para>
    ///         <b>The sample document is chosen to make five traps reachable</b>, each of which the type it belongs
    ///         to names as the thing that goes wrong. It has a short line between two long ones, so walking down
    ///         through it and back shows the caret returning to the column it set out from. It has real tabs, so the
    ///         stored column and the drawn column part company and the readout below can show both. It carries a
    ///         form feed, which text files have used as a page break for fifty years and which a terminal
    ///         <i>obeys</i> rather than draws. It carries an escape sequence, which is what any captured terminal
    ///         session or coloured log looks like. And it says "match" three times, so Find Next has somewhere to
    ///         go.
    ///     </para>
    ///     <para>
    ///         Composing happens where something changed (a key, a click, a resize) and
    ///         <see cref="OnRenderForm" /> hands the result straight back, which is the rule for every screen here:
    ///         the scene graph asks for the text on every system tick, about a thousand times a second.
    ///     </para>
    /// </summary>
    [ParentWindow(typeof (DemoWindow))]
    public sealed class TextEditorDialog : Form<DemoWindowInfo>
    {
        /// <summary>
        ///     Rows this screen spends on everything that is not the document: the heading, the note under it, the
        ///     frame's two edges, three rows of readout, the key strip, and the row the presenter keeps for the
        ///     prompt.
        /// </summary>
        private const int ReservedRows = 11;

        /// <summary>Columns left for the frame either side of the field and a little slack past it.</summary>
        private const int ReservedColumns = 4;

        /// <summary>
        ///     What F3 looks for. A fixed needle rather than a Find box, because the asymmetry between the two
        ///     directions is what this screen is showing and a dialog to type it in adds nothing to that.
        /// </summary>
        private const string Needle = "match";

        /// <summary>
        ///     The document, written with explicit line endings rather than as a verbatim string so that what the
        ///     buffer detects is a property of this constant and not of however the repository checked this file
        ///     out. LF here, on a host whose own <see cref="Environment.NewLine" /> is CRLF, which is exactly the
        ///     pair that makes the readout's claim worth reading.
        /// </summary>
        private const string SampleText =
            "Press END here, then DOWN twice and UP twice: the caret comes back to\n" +
            "short\n" +
            "this exact column, because TextBuffer remembers the one it set out from.\n" +
            "\n" +
            "A tab advances to the NEXT STOP, which is why these columns line up.\n" +
            "Eight spaces per tab would start each one wherever the word before it\n" +
            "happened to end, so the table would misalign from its second row on:\n" +
            "Name\tStop\tWhat it costs to get wrong\n" +
            "tab\t8\tone character stored, several columns drawn\n" +
            "expand\t8\ta caret placed by index lands in the wrong cell\n" +
            "\n" +
            "Page one ends here.\fPage two starts on this same row. A form feed is\n" +
            "obeyed rather than drawn, so without ControlPictures everything after\n" +
            "it would have landed on the row below, over whatever was there.\n" +
            "A captured log carries escapes as well: \u001B[2J would clear the screen.\n" +
            "\n" +
            "F3 finds the next \"match\", SHIFT+F3 the previous. Forward starts at or\n" +
            "after the caret; backward finds the last match strictly before it,\n" +
            "which is what stops Find Next handing back this same match for ever.\n";

        /// <summary>The screen's own heading, in the same bold cyan the other demo screens use.</summary>
        private static readonly TextStyle _headingStyle = new TextStyle(new TextColor(ConsoleColor.DarkCyan), null,
            true);

        /// <summary>Supporting text that should read as quieter than the document it is explaining.</summary>
        private static readonly TextStyle _dimStyle = new TextStyle(new TextColor(ConsoleColor.DarkGray));

        /// <summary>
        ///     The block caret, and the one styled thing inside the field.
        ///     <para>
        ///         <b>The caret is drawn rather than placed.</b> The library parks the terminal's real cursor at the
        ///         end of the input prompt, so there is no cursor to put inside a document and
        ///         <see cref="DocumentView" /> paints a cell in this style instead. The honest consequence is that a
        ///         terminal resolving to no colour at all (<c>NO_COLOR</c>, or the Force render type screen) shows
        ///         no block whatsoever, which is why the readout below says where the caret is in words as well.
        ///     </para>
        /// </summary>
        private static readonly TextStyle _caretStyle = new TextStyle(new TextColor(ConsoleColor.Black),
            new TextColor(ConsoleColor.Gray));

        /// <summary>The document being edited, loaded once and never written anywhere.</summary>
        private readonly TextBuffer _buffer = new TextBuffer();

        /// <summary>The window onto it.</summary>
        private readonly TextViewport _viewport = new TextViewport();

        /// <summary>The frame around the field. Its title is where the document says it has been edited.</summary>
        private readonly Box _frame = new Box {Title = "sample.txt", BorderStyle = ConsoleColor.DarkGray};

        /// <summary>The whole frame, built where something changed and handed back by every render between.</summary>
        private string _rendered = string.Empty;

        /// <summary>The key strip, drawn in the body because this screen has turned the prompt off.</summary>
        private string _keyHelp = string.Empty;

        /// <summary>
        ///     Which line of the frame the field's first row is drawn on, counted while composing rather than
        ///     written down as a constant. A click is turned back into a document position against this, and a
        ///     constant would go quietly wrong the day a line is added to the header above it.
        /// </summary>
        private int _fieldTopRow = -1;

        /// <summary>
        ///     Whether the last movement made <see cref="TextViewport.EnsureVisible" /> scroll. Shown because
        ///     reporting it is half of what that method is for: a caller that knows the origin did not move knows it
        ///     has nothing to redraw.
        /// </summary>
        private bool _scrolled;

        /// <summary>Initializes a new instance of the <see cref="TextEditorDialog" /> class.</summary>
        /// <param name="window">The parent window.</param>
        // ReSharper disable once UnusedMember.Global
        public TextEditorDialog(IWindow window) : base(window)
        {
        }

        /// <summary>ENTER and BACKSPACE arrive as key presses, which is the only way an editor sees a backspace.</summary>
        public override bool EditsText => true;

        /// <summary>Typed characters go into the document rather than into the prompt underneath it.</summary>
        public override bool InputFillsBuffer => false;

        /// <inheritdoc />
        public override void OnFormPostCreate()
        {
            base.OnFormPostCreate();

            // A click can place the caret, so the strip says so only where the host asked for the mouse. Motion is
            // deliberately not requested: a click is an instant, and nothing on this screen has a duration the way
            // dragging a penguin does, so there is no flood to pay for or hand back in OnFormClosing.
            _keyHelp = AnsiConsole.MouseEnabled
                ? "ARROWS/PGUP/PGDN move, SHIFT selects, CTRL by word, TAB tab, F3 find, click, ESC out"
                : "ARROWS/PGUP/PGDN move, SHIFT selects, CTRL by word, TAB tab, F3 find, ESC out";

            // Set once and in the right place, even though the scene graph will not draw it while InputFillsBuffer
            // is false. It is the same text the body strip carries, so a reader who turns the prompt back on gets
            // the key map rather than a stale sentence about pressing ENTER.
            ParentWindow.PromptText = _keyHelp;

            _buffer.SetText(SampleText);
            ResizeViewport();
            Compose();
        }

        /// <inheritdoc />
        public override void OnTick(bool systemTick, bool skipDay)
        {
            base.OnTick(systemTick, skipDay);

            // Once a second rather than on the fast ticks: the window size is two live syscalls, and nothing else
            // here changes on its own.
            if (systemTick || !ResizeViewport())
                return;

            EnsureCaretVisible();
            Compose();
        }

        /// <inheritdoc />
        public override string OnRenderForm()
        {
            return _rendered;
        }

        /// <summary>
        ///     Never called. <see cref="EditsText" /> is precisely the declaration that ENTER arrives as a key press
        ///     instead, where it splits the line, so no line is ever submitted. Kept honest rather than deleted: it
        ///     is what closes every other screen here, and it is what this one would want back the moment the
        ///     document stopped being editable.
        /// </summary>
        /// <param name="input">The submitted line, which is never handed over.</param>
        public override void OnInputBufferReturned(string input)
        {
            ClearForm();
        }

        /// <summary>
        ///     Every key the screen acts on. ENTER and BACKSPACE reach here only because of
        ///     <see cref="EditsText" />; everything printable types, and the keys that steer have no character at
        ///     all, which is the library's two input paths showing through in one method.
        /// </summary>
        /// <param name="keyInfo">The key press exactly as the host saw it.</param>
        public override void OnKeyPressed(ConsoleKeyInfo keyInfo)
        {
            base.OnKeyPressed(keyInfo);

            // ESC is absent on purpose rather than by oversight: DemoWindow spends it clearing the form before the
            // key is offered to any screen, so a case for it here would be dead code.
            //
            // The modifiers are read off the whole ConsoleKeyInfo rather than the bare ConsoleKey, which is the
            // reason that overload exists: SHIFT and an arrow are the same key twice otherwise, and a selection
            // cannot be swept without telling them apart. CTRL widens a step to a word, and a word here is exactly
            // the run TextWords walks and a whole-word search matches, since all three ask TextBuffer.
            var extend = (keyInfo.Modifiers & ConsoleModifiers.Shift) != 0;
            var control = (keyInfo.Modifiers & ConsoleModifiers.Control) != 0;

            switch (keyInfo.Key)
            {
                case ConsoleKey.LeftArrow:
                    if (control)
                        _buffer.MoveWordLeft(extend);
                    else
                        _buffer.MoveLeft(extend);

                    break;
                case ConsoleKey.RightArrow:
                    if (control)
                        _buffer.MoveWordRight(extend);
                    else
                        _buffer.MoveRight(extend);

                    break;

                // The two moves that read the desired column instead of setting it. Walk down from the end of the
                // first line, through the short one, and back up: without a remembered column the caret would come
                // to rest wherever the short line ended, having quietly forgotten where it was.
                case ConsoleKey.UpArrow:
                    _buffer.MoveUp(1, extend);
                    break;
                case ConsoleKey.DownArrow:
                    _buffer.MoveDown(1, extend);
                    break;
                case ConsoleKey.Home:
                    if (control)
                        _buffer.MoveToStart(extend);
                    else
                        _buffer.MoveToLineStart(extend);

                    break;
                case ConsoleKey.End:
                    if (control)
                        _buffer.MoveToEnd(extend);
                    else
                        _buffer.MoveToLineEnd(extend);

                    break;
                case ConsoleKey.PageUp:
                    Page(-1, extend);
                    break;
                case ConsoleKey.PageDown:
                    Page(1, extend);
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

                // A real tab, stored as one character. Typing eight spaces instead is the mistake the whole of
                // TabStops exists to talk a caller out of, and it would be a strange thing for this screen to do.
                case ConsoleKey.Tab:
                    _buffer.Insert('\t');
                    break;
                case ConsoleKey.F3:
                    Find(extend);
                    break;
                default:
                    // Everything else is either text or nothing at all. An arrow or a function key carries no
                    // character, which is why the input buffer drops them and the key path exists.
                    if (keyInfo.KeyChar == '\0' || char.IsControl(keyInfo.KeyChar))
                        return;

                    _buffer.Insert(keyInfo.KeyChar);
                    break;
            }

            EnsureCaretVisible();
            Compose();
        }

        /// <summary>
        ///     Places the caret where the pointer landed, which is the whole of a hit test and needs both
        ///     translations. The viewport turns a cell into a line and a screen column;
        ///     <see cref="TabStops.ToDocumentColumn" /> turns that screen column into a character index, and the two
        ///     are different numbers the moment the line is indented. Clicking the middle of an indent lands on the
        ///     tab itself, which is somewhere a following BACKSPACE removes in one press.
        ///     <para>
        ///         It has to be this override and not <c>OnMouseEvent</c>: <c>Window.OnMouseEvent</c> hands a press
        ///         to <c>OnMousePressed</c> and returns, so a press written into the other method compiles and never
        ///         runs.
        ///     </para>
        /// </summary>
        /// <param name="mouse">Where the press landed and which button it was.</param>
        public override void OnMousePressed(MouseEvent mouse)
        {
            base.OnMousePressed(mouse);

            if (mouse.Button != MouseButtonEnum.Left || _fieldTopRow < 0)
                return;

            var row = mouse.Row - _fieldTopRow;
            var column = mouse.Column - 1; // One column of frame sits to the left of the field.
            if (row < 0 || row >= _viewport.Height || column < 0 || column >= _viewport.Width)
                return;

            var at = _viewport.ToDocument(row, column);
            var line = Math.Clamp(at.Line, 0, _buffer.LineCount - 1);

            // MoveTo clamps the column for itself, which is what makes clicking past the end of a line land after
            // its last character rather than somewhere the line has not got.
            _buffer.MoveTo(new TextPosition(line,
                TabStops.ToDocumentColumn(_buffer.GetLine(line), at.Column, _buffer.TabWidth)));

            EnsureCaretVisible();
            Compose();
        }

        /// <summary>
        ///     Moves a screenful and takes the view with it.
        ///     <para>
        ///         <see cref="TextViewport.ClampToDocument" /> is what stops the last page from being the last
        ///         <i>line</i>: without it, PageDown at the end of the document walks the origin off into empty
        ///         space and the text scrolls away entirely, which is the classic version of this bug and is
        ///         reachable on this screen in two presses.
        ///     </para>
        /// </summary>
        /// <param name="direction">Negative for up, positive for down.</param>
        /// <param name="extend">TRUE to select over the move.</param>
        private void Page(int direction, bool extend)
        {
            // One row of overlap, so the line the eye was reading is still on screen after the jump.
            var lines = Math.Max(1, _viewport.Height - 1);

            if (direction < 0)
                _buffer.MoveUp(lines, extend);
            else
                _buffer.MoveDown(lines, extend);

            _viewport.ScrollBy(direction * lines, 0);
            _viewport.ClampToDocument(_buffer.LineCount);
        }

        /// <summary>
        ///     Finds the next or previous <see cref="Needle" /> and selects it.
        ///     <para>
        ///         <b>The two directions are asked from different ends of the current match on purpose.</b> Forward
        ///         returns the first match starting at or after the position it is given, so searching from a caret
        ///         sitting on a match would find that same match for ever; backward returns the last one starting
        ///         strictly before. Handing forward the far end and backward the near end is what makes both land on
        ///         the neighbour.
        ///     </para>
        ///     <para>
        ///         <see cref="TextBuffer.Select" /> rather than a move with an extend flag, because extending keeps
        ///         whatever anchor is already there and would stretch the previous selection over this one, so every
        ///         Find would select more of the document than the last.
        ///     </para>
        /// </summary>
        /// <param name="backwards">TRUE to look towards the start of the document.</param>
        private void Find(bool backwards)
        {
            var from = _buffer.HasSelection
                ? backwards ? _buffer.SelectionStart : _buffer.SelectionEnd
                : _buffer.Caret;

            var hit = TextSearch.Next(_buffer.Lines, Needle, from, backwards: backwards);
            if (hit == null)
                return;

            _buffer.Select(hit.Value, new TextPosition(hit.Value.Line, hit.Value.Column + Needle.Length));
        }

        /// <summary>
        ///     Keeps the caret on screen with the least scrolling that does it, and remembers whether that moved
        ///     anything.
        ///     <para>
        ///         <b>The viewport is asked in screen columns, not in character indexes.</b> They stop being the
        ///         same number the moment a line has a tab, and handing over the stored column would scroll to the
        ///         wrong place on exactly the indented lines somebody is most likely to be editing.
        ///     </para>
        /// </summary>
        private void EnsureCaretVisible()
        {
            var caret = _buffer.Caret;
            var drawn = TabStops.ToDisplayColumn(_buffer.GetLine(caret.Line), caret.Column, _buffer.TabWidth);

            _scrolled = _viewport.EnsureVisible(new TextPosition(caret.Line, drawn));
        }

        /// <summary>Sizes the field to the console, and says whether that changed anything.</summary>
        /// <returns>TRUE when the viewport was actually resized.</returns>
        private bool ResizeViewport()
        {
            // Read once into a local rather than per row: each of these is a live syscall.
            var width = Math.Clamp(AnsiConsole.SafeWindowWidth() - ReservedColumns, 24, 76);
            var height = Math.Clamp(AnsiConsole.SafeWindowHeight() - ReservedRows, 4, 12);

            if (_viewport.Width == width && _viewport.Height == height)
                return false;

            _viewport.Resize(width, height);
            _viewport.ClampToDocument(_buffer.LineCount);
            return true;
        }

        /// <summary>
        ///     Builds the whole frame. Called where something changed and never from
        ///     <see cref="OnRenderForm" />, which is asked for the screen about a thousand times a second.
        /// </summary>
        private void Compose()
        {
            var width = Math.Max(20, AnsiConsole.SafeWindowWidth() - 1);
            var rows = new List<string>();

            // The first row closes the scene graph's own status line, which the window's text is appended to.
            rows.Add(string.Empty);
            rows.Add(Clip(_headingStyle.Apply("Documents: TextBuffer, TextViewport, TabStops, ControlPictures"),
                width));
            rows.Add(Clip(_dimStyle.Apply(AnsiConsole.MouseEnabled
                ? "EditsText delivers ENTER and BACKSPACE here; a click places the caret."
                : "EditsText delivers ENTER and BACKSPACE here; no other screen sees them."), width));

            // Whether the document has been touched, said where a person looks for it. Nothing writes it back, so
            // the asterisk is the only consequence an edit has.
            _frame.Title = _buffer.IsModified ? "sample.txt *" : "sample.txt";

            // Counted from the builder rather than written down, because the click arithmetic reads it and a
            // constant would go wrong the day a line is added above.
            _fieldTopRow = rows.Count + 1;
            rows.Add(_frame.Render(string.Join(Environment.NewLine,
                DocumentView.Render(_buffer, _viewport, TextStyle.None, _caretStyle))));

            rows.Add(Clip(_dimStyle.Apply(CaretRow()), width));
            rows.Add(Clip(_dimStyle.Apply(DocumentRow()), width));
            rows.Add(Clip(_dimStyle.Apply(ViewRow()), width));
            rows.Add(Clip(_dimStyle.Apply(_keyHelp), width));

            _rendered = string.Join(Environment.NewLine, rows);
        }

        /// <summary>
        ///     Where the caret is, twice: as the character it sits on and as the column it is drawn in. The two
        ///     numbers are the same until the line has a tab in front of the caret, and watching them separate on
        ///     the table rows is the shortest statement of what <see cref="TabStops" /> is for.
        /// </summary>
        /// <returns>The caret readout.</returns>
        private string CaretRow()
        {
            var caret = _buffer.Caret;
            var drawn = TabStops.ToDisplayColumn(_buffer.GetLine(caret.Line), caret.Column, _buffer.TabWidth);

            return string.Format(CultureInfo.InvariantCulture, "caret {0}   screen column {1}   {2}", caret,
                drawn + 1, SelectionText());
        }

        /// <summary>
        ///     What is selected, in document order.
        ///     <para>
        ///         <b>A selection reads low to high whichever end the caret is on</b>, which is what every consumer
        ///         of one wants and is why the buffer sorts the pair for itself. Selecting upwards with SHIFT and UP
        ///         is what shows it: the range below stays the right way round while the caret is reported at its
        ///         start.
        ///     </para>
        /// </summary>
        /// <returns>The selection readout.</returns>
        private string SelectionText()
        {
            if (!_buffer.HasSelection)
                return "no selection";

            return string.Format(CultureInfo.InvariantCulture, "selection {0}..{1}, {2} chars, caret at the {3}",
                _buffer.SelectionStart, _buffer.SelectionEnd, _buffer.GetSelectedText().Length,
                _buffer.Caret == _buffer.SelectionStart ? "start" : "end");
        }

        /// <summary>
        ///     What the document is: how much of it there is, and which line ending it will be written back with.
        ///     <para>
        ///         <b>The line ending is remembered rather than normalized</b>, so opening a file and saving it
        ///         untouched really does produce the same bytes. That is the difference between an editor and a
        ///         reformatter, and this constant is loaded with LF on a host that writes CRLF precisely so the two
        ///         halves of the claim can be read off one row.
        ///     </para>
        /// </summary>
        /// <returns>The document readout.</returns>
        private string DocumentRow()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "{0} lines, {1} words   line ending {2} kept, this host writes {3}", _buffer.LineCount,
                TextWords.Count(_buffer.Lines), Name(_buffer.NewLine), Name(Environment.NewLine));
        }

        /// <summary>
        ///     What the window onto the document is doing, and how much there is to find. The scroll note is there
        ///     because <see cref="TextViewport.EnsureVisible" /> reporting whether it moved is half of what it is
        ///     for.
        /// </summary>
        /// <returns>The viewport readout.</returns>
        private string ViewRow()
        {
            var last = Math.Min(_viewport.LastLineExclusive, _buffer.LineCount);

            return string.Format(CultureInfo.InvariantCulture,
                "view lines {0}-{1} of {2}{3}   \"{4}\" occurs {5} times", _viewport.FirstLine + 1, last,
                _buffer.LineCount, _scrolled ? " (EnsureVisible scrolled)" : string.Empty, Needle,
                TextSearch.Count(_buffer.Lines, Needle));
        }

        /// <summary>The name a person knows a line ending by.</summary>
        /// <param name="newLine">The line ending itself.</param>
        /// <returns>Its name.</returns>
        private static string Name(string newLine)
        {
            return string.Equals(newLine, "\r\n", StringComparison.Ordinal) ? "CRLF" : "LF";
        }

        /// <summary>
        ///     Cuts a row to the console's width. Measured and cut with <see cref="AnsiText" /> rather than with
        ///     <c>Length</c> and <c>Substring</c>, because every row here carries colour: an escape has length but
        ///     no width, so counting characters would report a styled row as hundreds of columns wide and cutting by
        ///     them would land inside a sequence and spill its tail onto the screen as text.
        /// </summary>
        /// <param name="text">The row.</param>
        /// <param name="width">How many visible columns there is room for.</param>
        /// <returns>The row, no wider than the console.</returns>
        private static string Clip(string text, int width)
        {
            return AnsiText.VisibleLength(text) <= width ? text : AnsiText.Slice(text, 0, width);
        }
    }
}
