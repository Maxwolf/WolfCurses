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
    ///     A stamp pad built out of <see cref="Keypad" />: eight keys that write a box-drawing glyph into the strip
    ///     below, two that edit the strip, and four that change the pad itself while you watch. Click a key, or walk
    ///     the ARROW KEYS over the pad and press SPACE. ENTER or ESC goes back to the menu.
    ///     <para>
    ///         <b>The reason the control exists is that it keeps its layout</b>, the same reason
    ///         <see cref="MenuBar" /> does: the geometry is worked out once and read by <i>both</i> the drawing and
    ///         the hit test, so the key a press lands on cannot be a different key from the one drawn there. Every
    ///         hand-rolled keypad computes that arithmetic twice, in two places, and the two drift the first time a
    ///         key is widened. That is precisely what WIDE, THIN and the wheel are here for: widen the keys and
    ///         press one, and the presses have moved with the drawing because there is only one of them.
    ///     </para>
    ///     <para>
    ///         <b>UNDO and CLEAR span two columns each, and a span is the whole of why the geometry is not
    ///         uniform.</b> Spans are horizontal only, the same limit and the same reason as a spreadsheet's merged
    ///         cells: a key spanning rows would mean no row could be drawn without knowing which columns an earlier
    ///         row had already claimed, and every hit test would have to ask the same question. A wide zero key is a
    ///         row's business; a tall equals key is the whole pad's. The rules come out right for free because they
    ///         are drawn through <see cref="BoxDrawing.Junction" />: a key that absorbs the rule beside it simply
    ///         puts no cut there, and the junctions above and below change shape on their own, each chosen from the
    ///         lines that actually meet it rather than from a fixed table of corners. LINE cycles the border style
    ///         to show that the same connections give the same shape in every one of them.
    ///     </para>
    ///     <para>
    ///         <b>The rules belong to no key.</b> Point at one and the readout says so: a press there presses
    ///         nothing, rather than rounding to the nearer key, which is the answer <see cref="TableViewport" />
    ///         gives for the ground past its last column and for the same reason - rounding presses a key nobody
    ///         pointed at.
    ///     </para>
    ///     <para>
    ///         <b>Hover puts the light out when the pointer leaves</b>, which is the half that gets left out: a key
    ///         still lit after the pointer has gone is one the user believes they are about to press. A key that
    ///         cannot be pressed does not light either, which is why the ARROW KEYS can park the cursor on UNDO with
    ///         an empty strip and nothing lights up. That is the pad refusing to promise something, not a bug, and
    ///         the readout says which.
    ///     </para>
    ///     <para>
    ///         <b>GREY toggles <see cref="Keypad.DisabledStyle" /> between a grey and null</b>, and null is the
    ///         interesting half: it means "not specified" rather than "no colour", so it falls back to
    ///         <see cref="Keypad.ButtonStyle" /> and a pad that never sets it draws exactly what it always drew.
    ///         Switch it off here with the strip empty and the two dead keys go on looking live, which is what a
    ///         pad using <see cref="KeypadButton.EnabledWhen" /> without it reads as: broken, rather than greyed.
    ///     </para>
    ///     <para>
    ///         The keyboard route is worth reading rather than skimming. It never works out where a key was drawn:
    ///         it asks the pad through <see cref="Keypad.ButtonAt" /> and then presses through the very same
    ///         <see cref="Keypad.Press" /> a click goes through, so the key SPACE presses cannot be a different key
    ///         from the one a click on that cell would press. Recomputing the cell here would be the second copy of
    ///         the arithmetic this control exists to remove, inside its own demonstration.
    ///     </para>
    /// </summary>
    [ParentWindow(typeof (DemoWindow))]
    public sealed class KeypadDialog : Form<DemoWindowInfo>
    {
        /// <summary>
        ///     Narrowest a key may be drawn: four columns, which is exactly what WIDE, THIN, LINE and GREY need.
        ///     Anything under it is legal and unreadable, since a label wider than its key is trimmed to fit.
        /// </summary>
        private const int MinButtonWidth = 4;

        /// <summary>Widest a key may be drawn, chosen so the pad plus its notes still fit an 80 column console.</summary>
        private const int MaxButtonWidth = 9;

        /// <summary>Blank columns between the pad and the notes beside it.</summary>
        private const int NoteGap = 3;

        /// <summary>Narrower than this and the notes go under the pad instead of beside it.</summary>
        private const int MinNoteWidth = 26;

        /// <summary>How wide the readout's label column is, so the values below the pad line up.</summary>
        private const int LabelWidth = 9;

        /// <summary>The sentence above the pad.</summary>
        private const string Intro =
            "The pad below is drawn and hit-tested from one layout, so the key a press lands on cannot be a " +
            "different key from the one drawn there. Resize the keys and try it.";

        /// <summary>The notes beside the pad.</summary>
        private const string Notes =
            "WIDE, THIN and the wheel resize the keys live: the presses move with the drawing, because one " +
            "arithmetic does both. UNDO and CLEAR span two columns each. A span simply leaves out the rule " +
            "beside it, and BoxDrawing.Junction reshapes the rules above and below on its own. Spans are " +
            "horizontal only: a tall key would leave no row drawable without knowing what an earlier row had " +
            "claimed. LINE cycles the border style, GREY switches DisabledStyle off.";

        /// <summary>
        ///     The border styles LINE walks through. <see cref="BoxBorderEnum.None" /> is deliberately not among
        ///     them: it draws every rule as a space, which is a legitimate answer and shows nothing at all about
        ///     junctions, and on this screen it would read as the pad having disappeared.
        /// </summary>
        private static readonly BoxBorderEnum[] _borders =
        {
            BoxBorderEnum.Single, BoxBorderEnum.Double, BoxBorderEnum.Rounded, BoxBorderEnum.Ascii
        };

        /// <summary>What a key that cannot be pressed looks like while GREY is on.</summary>
        private static readonly TextStyle _deadKeyStyle = new TextStyle(new TextColor(ConsoleColor.DarkGray));

        private static readonly TextStyle _headingStyle = new TextStyle(new TextColor(ConsoleColor.DarkCyan), null,
            true);

        private static readonly TextStyle _labelStyle = new TextStyle(new TextColor(ConsoleColor.DarkCyan));
        private static readonly TextStyle _dimStyle = new TextStyle(new TextColor(ConsoleColor.DarkGray));

        /// <summary>What the stamp keys have written, which is also what UNDO and CLEAR are asked about.</summary>
        private readonly StringBuilder _strip = new StringBuilder();

        private int _borderIndex;

        /// <summary>Which key the ARROW KEYS are on, as a row and a position in that row rather than a cell.</summary>
        private int _cursorIndex;

        private int _cursorRow;

        /// <summary>Whether <see cref="Keypad.DisabledStyle" /> is set at all, which GREY toggles.</summary>
        private bool _greyDeadKeys = true;

        private string _lastEvent = "nothing yet";
        private Keypad _pad;

        /// <summary>Where the pointer was last seen, in the screen cells a press is measured in.</summary>
        private int _pointerColumn = -1;

        private int _pointerRow = -1;

        /// <summary>Whether the pointer or the ARROW KEYS moved most recently, which is what owns the lit key.</summary>
        private bool _pointerDrivesLight;

        /// <summary>The whole frame, built whenever something changed and handed back unchanged by every render.</summary>
        private string _rendered = string.Empty;

        /// <summary>Initializes a new instance of the <see cref="KeypadDialog" /> class.</summary>
        /// <param name="window">The parent window.</param>
        // ReSharper disable once UnusedMember.Global
        public KeypadDialog(IWindow window) : base(window)
        {
        }

        /// <summary>The key the ARROW KEYS are parked on.</summary>
        private KeypadButton Cursor => _pad.Rows[_cursorRow].Buttons[_cursorIndex];

        /// <inheritdoc />
        public override void OnFormPostCreate()
        {
            base.OnFormPostCreate();

            ParentWindow.PromptText = AnsiConsole.MouseEnabled
                ? "Click a key, or ARROW KEYS move the cursor and SPACE presses; ENTER or ESC leaves"
                : "ARROW KEYS move the cursor, SPACE presses the key it is on, ENTER or ESC leaves";

            // Asked for by the screen that wants it and handed back in OnFormClosing. Motion is one event for every
            // cell the pointer crosses, so no other demo should be paying for it while this one is not on screen.
            SimUnit.InputManager.ReportsMouseMotion = true;

            BuildPad();
            Compose();
        }

        /// <summary>
        ///     Hands pointer reporting back, which is the half a form has nowhere else to put.
        ///     <para>
        ///         Being dropped is no signal at all, which is why <c>IForm.OnFormClosing</c> exists. The library
        ///         fires it from every path a form is detached by, so ESC, ENTER, the window being removed and
        ///         quitting are all covered by this one override rather than by four call sites that each have to
        ///         remember.
        ///     </para>
        /// </summary>
        public override void OnFormClosing()
        {
            base.OnFormClosing();

            SimUnit.InputManager.ReportsMouseMotion = false;
        }

        /// <inheritdoc />
        public override void OnKeyPressed(ConsoleKey key)
        {
            base.OnKeyPressed(key);

            if (_pad == null)
                return;

            switch (key)
            {
                case ConsoleKey.LeftArrow:
                    MoveCursor(0, -1);
                    break;
                case ConsoleKey.RightArrow:
                    MoveCursor(0, 1);
                    break;
                case ConsoleKey.UpArrow:
                    MoveCursor(-1, 0);
                    break;
                case ConsoleKey.DownArrow:
                    MoveCursor(1, 0);
                    break;
                case ConsoleKey.Spacebar:
                    PressCursor();

                    // A space is printable, so it lands in the input buffer as well as arriving here. Scrubbing it
                    // keeps the echoed prompt clean without this screen having to refuse buffered text outright,
                    // which would cost it ENTER. The alternative, taking ENTER as a key through IForm.EditsText,
                    // would also make this screen own BACKSPACE, and it has nothing to do with either.
                    SimUnit?.InputManager?.ClearBuffer();
                    break;
                default:
                    return;
            }

            Compose();
        }

        /// <summary>
        ///     Moving, and the wheel. Presses are not here and cannot be: <c>Window.OnMouseEvent</c> hands a press to
        ///     <see cref="OnMousePressed" /> and returns, so a click written in this method compiles and runs never.
        /// </summary>
        /// <param name="mouse">What the mouse did, and where.</param>
        public override void OnMouseEvent(MouseEvent mouse)
        {
            if (_pad == null)
                return;

            if (mouse.Kind == MouseEventKindEnum.Move)
            {
                // One event per cell the pointer crosses, which is what motion costs and why it is opt-in. The frame
                // is rebuilt only when a reader would see a difference: the lit key moved, or the cell named in the
                // readout changed.
                var moved = _pad.Hover(mouse.Row, mouse.Column);
                var elsewhere = mouse.Row != _pointerRow || mouse.Column != _pointerColumn;

                _pointerRow = mouse.Row;
                _pointerColumn = mouse.Column;
                _pointerDrivesLight = true;

                if (moved || elsewhere)
                    Compose();

                return;
            }

            // A notch arrives as its own kind rather than as a button, which is the distinction that keeps a scroll
            // from being read as a click. Handling it here can therefore never be reached by a press.
            if (mouse.Kind == MouseEventKindEnum.Wheel && mouse.WheelDelta != 0)
            {
                Resize(mouse.WheelDelta > 0 ? 1 : -1);
                Compose();
            }
        }

        /// <summary>
        ///     Presses whatever key is under the pointer, and says what happened when there was none.
        ///     <para>
        ///         <b>This has to be its own override.</b> <c>Window.OnMouseEvent</c> routes a press to
        ///         <c>OnMousePressed</c> and returns, so a press never reaches the form's <c>OnMouseEvent</c> at all.
        ///         Same shape as the library's note about <c>case ConsoleKey.Enter:</c> being dead code in an
        ///         override, and it has already cost this app a red test once.
        ///     </para>
        /// </summary>
        /// <param name="mouse">Where the press landed and which button it was.</param>
        public override void OnMousePressed(MouseEvent mouse)
        {
            base.OnMousePressed(mouse);

            if (_pad == null || mouse.Button != MouseButtonEnum.Left)
                return;

            // Asked before the press rather than after it, because pressing CLEAR is exactly what makes UNDO dead:
            // reading the key back afterwards would describe the pad the press produced instead of the key it hit.
            var button = _pad.ButtonAt(mouse.Row, mouse.Column);

            Describe("the mouse", button, _pad.Press(mouse.Row, mouse.Column), mouse.Row, mouse.Column);
            Compose();
        }

        /// <inheritdoc />
        public override void OnTick(bool systemTick, bool skipDay)
        {
            base.OnTick(systemTick, skipDay);

            // Once a second, not on the fast system ticks. Nothing here animates; this is so a console resized while
            // the screen is up re-lays the notes and moves the pad, and the hit test follows because the pad's row is
            // recounted from the frame rather than remembered. The scene graph only redraws when the text changed, so
            // a second where nothing moved costs a comparison.
            if (!systemTick)
                Compose();
        }

        /// <inheritdoc />
        public override string OnRenderForm()
        {
            // A pure read of what the last change composed. The scene graph asks for this on every system tick,
            // about a thousand times a second, so anything built in here would be built a thousand times a second.
            return _rendered;
        }

        /// <inheritdoc />
        public override void OnInputBufferReturned(string input)
        {
            ClearForm();
        }

        /// <summary>
        ///     Builds the keys.
        ///     <para>
        ///         The span row is the point of the shape: two keys covering four columns, so the row divides the pad
        ///         differently from the three around it and the rules have to bend for it. The two pairs of keys with
        ///         an <see cref="KeypadButton.EnabledWhen" /> are the other point, and both are predicates rather
        ///         than flags because the answer is wanted at three separate moments (drawing the pad, hovering it,
        ///         pressing it) and an owner keeping a flag fresh has to remember before all three. WIDE going dead
        ///         the moment the keys reach their widest is what missing one would look like from the other side.
        ///     </para>
        /// </summary>
        private void BuildPad()
        {
            _pad = new Keypad(
                new KeypadRow(Stamp('─'), Stamp('│'), Stamp('┌'), Stamp('┐')),
                new KeypadRow(Stamp('└'), Stamp('┘'), Stamp('├'), Stamp('┼')),
                new KeypadRow(
                    new KeypadButton("UNDO", Undo, 2) {EnabledWhen = () => _strip.Length > 0},
                    new KeypadButton("CLEAR", Clear, 2) {EnabledWhen = () => _strip.Length > 0}),
                new KeypadRow(
                    new KeypadButton("WIDE", () => Resize(1))
                        {EnabledWhen = () => _pad.ButtonWidth < MaxButtonWidth},
                    new KeypadButton("THIN", () => Resize(-1))
                        {EnabledWhen = () => _pad.ButtonWidth > MinButtonWidth},
                    new KeypadButton("LINE", CycleBorder),
                    new KeypadButton("GREY", ToggleDeadKeyStyle)))
            {
                ButtonWidth = 5,

                // Drawn from the left edge, which is also where a press on it is measured from. Row is set in
                // Compose, where the frame it lands in is being counted.
                Column = 0,
                Border = _borders[_borderIndex],
                BorderStyle = ConsoleColor.DarkGray,

                // ButtonStyle and HoverStyle are both left alone on purpose. Nobody has coloured the faces, so they
                // emit no escape at all, and the pointer still shows: with no HoverStyle the pad falls back to
                // inverse video, through the same gate every other highlight in the library uses, so it also emits
                // nothing when colour is switched off.
                DisabledStyle = _deadKeyStyle
            };
        }

        /// <summary>One stamp key: it writes its own label into the strip.</summary>
        /// <param name="glyph">The glyph it writes.</param>
        /// <returns>The key.</returns>
        private KeypadButton Stamp(char glyph)
        {
            return new KeypadButton(glyph.ToString(), () => _strip.Append(glyph));
        }

        /// <summary>Takes the last stamp back off the strip.</summary>
        private void Undo()
        {
            if (_strip.Length > 0)
                _strip.Remove(_strip.Length - 1, 1);
        }

        /// <summary>Empties the strip, which is also what puts UNDO and CLEAR back to sleep.</summary>
        private void Clear()
        {
            _strip.Clear();
        }

        /// <summary>Widens or narrows every key, which is the claim this screen exists to prove.</summary>
        /// <param name="step">How many columns to add to each key.</param>
        private void Resize(int step)
        {
            _pad.ButtonWidth = Math.Clamp(_pad.ButtonWidth + step, MinButtonWidth, MaxButtonWidth);
        }

        /// <summary>Walks the border styles, since the same connections give the same shape in every one of them.</summary>
        private void CycleBorder()
        {
            _borderIndex = (_borderIndex + 1) % _borders.Length;
            _pad.Border = _borders[_borderIndex];
        }

        /// <summary>Switches <see cref="Keypad.DisabledStyle" /> between a grey and not being specified at all.</summary>
        private void ToggleDeadKeyStyle()
        {
            _greyDeadKeys = !_greyDeadKeys;
            _pad.DisabledStyle = _greyDeadKeys ? (TextStyle?) _deadKeyStyle : null;
        }

        /// <summary>
        ///     Moves the ARROW KEY cursor, and lights whatever it lands on.
        ///     <para>
        ///         The row is a starting point rather than a requirement, the same clamp
        ///         <c>MenuLayout.StepColumn</c> makes: the span row has two keys where the rows around it have four,
        ///         so stepping into it lands on a key it actually has. The outer edges are walls rather than wraps,
        ///         for the reason recorded there too - a wrap throws the cursor the whole width of the pad.
        ///     </para>
        /// </summary>
        /// <param name="rowStep">How many rows to move.</param>
        /// <param name="indexStep">How many keys along the row to move.</param>
        private void MoveCursor(int rowStep, int indexStep)
        {
            _cursorRow = Math.Clamp(_cursorRow + rowStep, 0, _pad.Rows.Count - 1);
            _cursorIndex = Math.Clamp(_cursorIndex + indexStep, 0, _pad.Rows[_cursorRow].Buttons.Count - 1);
            _pointerDrivesLight = false;
        }

        /// <summary>Presses the key the cursor is on, through the same hit test a click goes through.</summary>
        private void PressCursor()
        {
            var button = Cursor;
            _pointerDrivesLight = false;

            if (!TryFindCell(button, out var row, out var column))
                return;

            Describe("SPACE", button, _pad.Press(row, column), row, column);
        }

        /// <summary>
        ///     Which cell the pad drew a key in, found by asking the pad rather than by working it out again here.
        ///     <para>
        ///         <b>This is the whole reason the keyboard route is honest.</b> The arithmetic that turns a key into
        ///         a cell already exists inside the control, and writing a second copy of it here - in the screen
        ///         demonstrating a control whose entire purpose is that there is only one - is how the drawn key and
        ///         the pressed key come apart. Walking the pad's own rectangle asking
        ///         <see cref="Keypad.ButtonAt" /> costs a few hundred comparisons on a key press and cannot drift.
        ///     </para>
        /// </summary>
        /// <param name="button">The key to find.</param>
        /// <param name="row">The screen row it was drawn on.</param>
        /// <param name="column">The screen column it was drawn in.</param>
        /// <returns>TRUE when the pad had drawn it.</returns>
        private bool TryFindCell(KeypadButton button, out int row, out int column)
        {
            row = -1;
            column = -1;

            // Null would match every rule on the pad, since that is what ButtonAt answers for one.
            if (button == null)
                return false;

            for (var r = _pad.Row; r < _pad.Row + _pad.Height; r++)
            {
                for (var c = _pad.Column; c < _pad.Column + _pad.Width; c++)
                {
                    if (!ReferenceEquals(_pad.ButtonAt(r, c), button))
                        continue;

                    row = r;
                    column = c;
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether a cell is anywhere inside the pad, rules and all.</summary>
        /// <param name="row">The row to test.</param>
        /// <param name="column">The column to test.</param>
        /// <returns>TRUE when the cell is on the pad.</returns>
        private bool OnPad(int row, int column)
        {
            return row >= _pad.Row && row < _pad.Row + _pad.Height &&
                   column >= _pad.Column && column < _pad.Column + _pad.Width;
        }

        /// <summary>
        ///     Says what a press did, including the two ways of pressing nothing: a rule, which belongs to no key,
        ///     and a key that is switched off.
        /// </summary>
        /// <param name="how">What did the pressing.</param>
        /// <param name="button">The key that was under it, or null for a rule and for the ground off the pad.</param>
        /// <param name="pressed">Whether the pad spent the press.</param>
        /// <param name="row">The pressed row.</param>
        /// <param name="column">The pressed column.</param>
        private void Describe(string how, KeypadButton button, bool pressed, int row, int column)
        {
            var at = string.Format(CultureInfo.InvariantCulture, "row {0}, col {1}", row, column);

            if (pressed)
            {
                _lastEvent = $"{how} pressed {button.Label} at {at}";
                return;
            }

            if (button != null)
            {
                _lastEvent = $"{how} hit {button.Label} at {at}, which is dead just now";
                return;
            }

            _lastEvent = OnPad(row, column)
                ? $"{how} hit a rule at {at}, which belongs to no key, so nothing was pressed"
                : $"{how} hit {at}, which is off the pad";
        }

        /// <summary>
        ///     Builds the whole frame, and tells the pad which screen row it is being drawn on while it is doing it.
        ///     <para>
        ///         <b>The row is counted from the frame being built, never written down as a constant.</b> A constant
        ///         is right until somebody adds a line above the pad, at which point every press lands on the rule
        ///         above the key it was aimed at and nothing reports a problem. Where the pad is drawn and where a
        ///         press on it is measured from are then the same number rather than two numbers that have to agree.
        ///     </para>
        ///     <para>
        ///         Composing here rather than in <see cref="OnRenderForm" /> is the app's standing rule, and this
        ///         screen also needs it: the render must not move the pad, or the hit test would depend on how many
        ///         times a frame had been asked for.
        ///     </para>
        /// </summary>
        private void Compose()
        {
            // One syscall, read into a local. Every SafeWindowWidth call is a live console read.
            var width = Math.Max(MinNoteWidth, AnsiConsole.SafeWindowWidth());

            var body = new List<string> {string.Empty, _headingStyle.Apply("Keypad (a stamp pad)")};
            body.AddRange(Wrapped(_dimStyle, Intro, width - 1));
            body.Add(string.Empty);

            _pad.Row = HeaderRows() + CountRows(body);

            // The pad remembers which KEY is lit rather than which cell, so after the keys have changed size the
            // pointer is likely to be over a different one; it is told again now that the pad knows where it is
            // being drawn. Whichever of the two last moved owns the light, or the arrows would walk the cursor
            // across a pad whose light stayed under a pointer nobody had touched for a minute.
            if (_pointerDrivesLight && _pointerRow >= 0)
                _pad.Hover(_pointerRow, _pointerColumn);
            else if (TryFindCell(Cursor, out var cursorRow, out var cursorColumn))
                _pad.Hover(cursorRow, cursorColumn);

            body.Add(PadBlock(width));
            body.Add(string.Empty);
            body.AddRange(Readout());

            _rendered = string.Join(Environment.NewLine, body);
        }

        /// <summary>
        ///     How many screen rows the scene graph puts in front of the form's own text, which is what a hit test on
        ///     this form has to be measured from.
        ///     <para>
        ///         <b>Asked of the app rather than written down.</b> Every frame opens with the scene graph's own
        ///         spinner line, which carries no newline, followed by whatever <c>SimulationApp.OnPreRender</c>
        ///         returns - and the hosts in this repository disagree about that: this one ends its pre-render with
        ///         a newline and the office suite does not. A constant here would be right in one program and one
        ///         row out in the other, and one row out puts every press on a rule.
        ///     </para>
        /// </summary>
        /// <returns>The number of complete rows drawn before the form's first line.</returns>
        private int HeaderRows()
        {
            // Less one, because the last of those rows is the one the form's own text continues on rather than a
            // row of its own: a pre-render ending in a newline hands the form a fresh line, one that does not
            // shares its last.
            return FrameLayout.RowsAboveForm(SimUnit);
        }

        /// <summary>The pad with its notes beside it, or under it when there is no room for both.</summary>
        /// <param name="width">The console width.</param>
        /// <returns>The composed block.</returns>
        private string PadBlock(int width)
        {
            var pad = string.Join(Environment.NewLine, _pad.Render());
            var noteWidth = width - _pad.Width - NoteGap;

            // Auto-wrap is off while a frame is drawn, so a row wider than the console is truncated rather than
            // reflowed and the notes would quietly lose their right-hand ends with nothing on screen saying so.
            // Under the pad costs rows and keeps every word, which is the cheaper mistake to make.
            if (noteWidth < MinNoteWidth)
                return pad + Environment.NewLine +
                       string.Join(Environment.NewLine, Wrapped(_dimStyle, Notes, width - 1));

            return TextColumns.Join(pad, string.Join(Environment.NewLine, Wrapped(_dimStyle, Notes, noteWidth)),
                NoteGap);
        }

        /// <summary>
        ///     The lines under the pad. Deliberately short enough to be one row each on an eighty column console,
        ///     since a status row wider than the window is truncated rather than wrapped.
        /// </summary>
        /// <returns>The readout, one row per entry.</returns>
        private IEnumerable<string> Readout()
        {
            var pad = string.Format(CultureInfo.InvariantCulture, "keys {0} wide, {1} rules, {2}",
                _pad.ButtonWidth, _pad.Border,
                _greyDeadKeys ? "dead keys greyed" : "DisabledStyle unset, so dead keys look live");

            return new List<string>
            {
                Line("strip", _strip.Length == 0
                    ? "empty, which is what UNDO and CLEAR ask EnabledWhen about"
                    : _strip.ToString()),
                Line("cursor", CursorText()),
                Line("pointer", PointerText()),
                Line("last", _lastEvent),
                Line("pad", pad)
            };
        }

        /// <summary>Where the ARROW KEYS are, and whether the pad was willing to light it.</summary>
        /// <returns>The cursor's line.</returns>
        private string CursorText()
        {
            var button = Cursor;
            var where = string.Format(CultureInfo.InvariantCulture, "row {0} of {1}, key {2} of {3}",
                _cursorRow + 1, _pad.Rows.Count, _cursorIndex + 1, _pad.Rows[_cursorRow].Buttons.Count);

            return button.IsPressable
                ? $"{button.Label} ({where})"
                : $"{button.Label} ({where}), dead, so the pad refuses to light it";
        }

        /// <summary>
        ///     What the pointer is over, which is where three of this control's rules are visible at once: a rule
        ///     belongs to no key, a key that cannot be pressed is not lit, and leaving the pad puts the light out.
        /// </summary>
        /// <returns>The pointer's line.</returns>
        private string PointerText()
        {
            if (!AnsiConsole.MouseEnabled)
                return "this terminal reported no mouse, so the ARROW KEYS drive the light instead";

            if (_pointerRow < 0)
                return "not moved yet";

            var at = string.Format(CultureInfo.InvariantCulture, "row {0}, col {1}", _pointerRow, _pointerColumn);
            var button = _pad.ButtonAt(_pointerRow, _pointerColumn);

            if (button == null)
                return OnPad(_pointerRow, _pointerColumn)
                    ? $"{at} is a rule, which belongs to no key"
                    : $"{at} is off the pad, so the light is out";

            return button.IsPressable
                ? $"{at} is {button.Label}, lit"
                : $"{at} is {button.Label}, dead, so it is not lit either";
        }

        /// <summary>One readout row: a labelled column, then the value.</summary>
        /// <param name="label">What the row is about.</param>
        /// <param name="text">What it says.</param>
        /// <returns>The row.</returns>
        private static string Line(string label, string text)
        {
            return new TextRow()
                .Append(AnsiText.Fit(label, LabelWidth), _labelStyle)
                .Append(text)
                .Render();
        }

        /// <summary>
        ///     Word-wraps prose and styles it one row at a time. Wrapping first and styling second, because an escape
        ///     sequence has length but no width: wrapping already styled text spends the budget on bytes and bleeds
        ///     the style across the newline the wrap puts in.
        /// </summary>
        /// <param name="style">The style each produced row is painted in.</param>
        /// <param name="text">The prose.</param>
        /// <param name="width">The widest a row may be.</param>
        /// <returns>The rows.</returns>
        private static IEnumerable<string> Wrapped(TextStyle style, string text, int width)
        {
            var rows = new List<string>();

            foreach (var line in text.WordWrap(Math.Max(MinNoteWidth, width)).Split(Environment.NewLine))
            {
                if (line.Length == 0)
                    continue;

                rows.Add(style.Apply(line));
            }

            return rows;
        }

        /// <summary>How many screen rows a set of composed entries occupies, counting any that hold several.</summary>
        /// <param name="entries">The entries.</param>
        /// <returns>The row count.</returns>
        private static int CountRows(IEnumerable<string> entries)
        {
            var rows = 0;

            foreach (var entry in entries)
                rows += CountRows(entry);

            return rows;
        }

        /// <summary>
        ///     How many screen rows one composed entry occupies.
        ///     <para>
        ///         <b>An empty entry is one row, not none</b>, because a blank line between two blocks is a row of
        ///         the screen like any other. Answering zero for it is what put this screen's pad two rows above
        ///         where its presses were being measured, which drew a pad whose keys all pressed the key two rows
        ///         below them. Only a null string is nothing at all, which is what a host with no pre-render text
        ///         hands back.
        ///     </para>
        /// </summary>
        /// <param name="entry">The entry, which may hold several rows.</param>
        /// <returns>The row count.</returns>
        private static int CountRows(string entry)
        {
            if (entry == null)
                return 0;

            var rows = 1;

            foreach (var glyph in entry)
            {
                if (glyph == '\n')
                    rows++;
            }

            return rows;
        }
    }
}
