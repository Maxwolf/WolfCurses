// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Globalization;
using WolfCurses.Graphics;
using WolfCurses.Window;
using WolfCurses.Window.Control;
using WolfCurses.Window.Form;

namespace WolfCurses.Apps.Calculator
{
    /// <summary>
    ///     A desk calculator with a paper tape: keys you can click, keys you can type, and a record of what you did.
    ///     <para>
    ///         This is the screen in the suite that is about <b>the mouse as labelled buttons</b>. The arcade's
    ///         Minesweeper divides a coordinate to find a cell and its Missile Command aims at a continuum; neither
    ///         needs to remember where anything was drawn. A keypad does, because its keys are not all the same
    ///         width, and <see cref="Keypad" /> is the library answering that: the layout is worked out once and
    ///         read by both the drawing and the hit test, so the key a click lands on cannot be a different key
    ///         from the one drawn there.
    ///     </para>
    ///     <para>
    ///         <b>Every key works from the keyboard as well, the number pad included.</b> The pad's keys are
    ///         handled by name rather than only by the character they carry, because that is what makes them arrive
    ///         reliably: with NUM LOCK on they come through as <c>NumPad7</c> and friends, and a screen that only
    ///         read characters would be at the mercy of which of the two rows of digits somebody used.
    ///     </para>
    /// </summary>
    [ParentWindow(typeof (OfficeWindow))]
    public sealed class CalculatorDialog : Form<OfficeWindowInfo>, IHandlesEscape
    {
        /// <summary>The calculator itself, which knows nothing about any of this.</summary>
        private readonly CalculatorEngine _engine = new();

        /// <summary>The keys.</summary>
        private Keypad _keypad;

        /// <summary>The pull-down menus across the top.</summary>
        private MenuBar _menuBar;

        /// <summary>What the status strip has to say, when it is not listing the keys.</summary>
        private string _message;

        /// <summary>Initializes a new instance of the <see cref="CalculatorDialog" /> class.</summary>
        /// <param name="window">The parent window.</param>
        public CalculatorDialog(IWindow window) : base(window)
        {
        }

        /// <summary>
        ///     BACKSPACE and ENTER arrive as key presses rather than being spent on the input buffer, which is the
        ///     only way this screen can have a rub-out key and an equals key at all.
        /// </summary>
        public override bool EditsText => true;

        /// <summary>Typed digits go into the calculator, not into the prompt underneath it.</summary>
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

            BuildKeypad();
            BuildMenus();
        }

        /// <inheritdoc />
        public override string OnRenderForm()
        {
            ParentWindow.PromptText = "F10 opens the menus, ESC returns to the suite:";

            var width = Math.Max(24, AnsiConsole.SafeWindowWidth() - 1);

            // The leading newline is load-bearing, not spacing. SceneGraph appends a window's text straight onto
            // its own status row with no separator, and every row offset the hit tests use counts from there.
            return Environment.NewLine + CalculatorChrome.Compose(_menuBar, _keypad, _engine, StatusText(), width);
        }

        /// <summary>
        ///     Never called: <see cref="EditsText" /> is precisely the declaration that ENTER should arrive as a
        ///     key press instead, so nothing is ever collected into the buffer to submit.
        /// </summary>
        /// <param name="input">Unused.</param>
        public override void OnInputBufferReturned(string input)
        {
        }

        /// <inheritdoc />
        public override void OnKeyPressed(ConsoleKeyInfo keyInfo)
        {
            base.OnKeyPressed(keyInfo);

            // The menus get every key first and report what they spent. While one is open that is everything,
            // which is what stops a keystroke reaching the calculator behind it.
            if (_menuBar != null && _menuBar.HandleKey(keyInfo))
                return;

            _message = null;

            switch (keyInfo.Key)
            {
                // The number pad, by name. With NUM LOCK on these carry their digit as a character too, so the
                // path below would catch them anyway; with it off they are arrows and cursor keys and are none of
                // this screen's business, which is why there is no attempt to reinterpret those.
                case >= ConsoleKey.NumPad0 and <= ConsoleKey.NumPad9:
                    _engine.Digit((char) ('0' + (keyInfo.Key - ConsoleKey.NumPad0)));
                    return;

                case ConsoleKey.Add:
                    _engine.Operator(CalculatorOperatorEnum.Add);
                    return;

                case ConsoleKey.Subtract:
                    _engine.Operator(CalculatorOperatorEnum.Subtract);
                    return;

                case ConsoleKey.Multiply:
                    _engine.Operator(CalculatorOperatorEnum.Multiply);
                    return;

                case ConsoleKey.Divide:
                    _engine.Operator(CalculatorOperatorEnum.Divide);
                    return;

                case ConsoleKey.Decimal:
                    _engine.Point();
                    return;

                case ConsoleKey.Enter:
                    _engine.Equals();
                    return;

                case ConsoleKey.Backspace:
                    _engine.Backspace();
                    return;

                case ConsoleKey.Delete:
                    _engine.ClearEntry();
                    return;

                // Function keys for the rest, because a control combination the console decides to keep never
                // arrives here at all and a key that does nothing is worse than no key.
                case ConsoleKey.F5:
                    _engine.MemoryClear();
                    return;

                case ConsoleKey.F6:
                    _engine.MemoryRecall();
                    return;

                case ConsoleKey.F7:
                    _engine.MemoryAdd();
                    return;

                case ConsoleKey.F8:
                    _engine.MemorySubtract();
                    return;

                case ConsoleKey.F9:
                    _engine.Negate();
                    return;
            }

            Typed(keyInfo.KeyChar);
        }

        /// <summary>
        ///     What a printable character means, which covers the top row of digits and the punctuation beside it.
        /// </summary>
        /// <param name="character">The character typed.</param>
        private void Typed(char character)
        {
            switch (character)
            {
                case >= '0' and <= '9':
                    _engine.Digit(character);
                    return;

                // A comma as well as a point, since half the world's keyboards put one on the number pad.
                case '.':
                case ',':
                    _engine.Point();
                    return;

                case '+':
                    _engine.Operator(CalculatorOperatorEnum.Add);
                    return;

                case '-':
                    _engine.Operator(CalculatorOperatorEnum.Subtract);
                    return;

                case '*':
                case 'x':
                case 'X':
                    _engine.Operator(CalculatorOperatorEnum.Multiply);
                    return;

                case '/':
                    _engine.Operator(CalculatorOperatorEnum.Divide);
                    return;

                case '=':
                    _engine.Equals();
                    return;

                case '%':
                    _engine.Percent();
                    return;

                case 'c':
                case 'C':
                    _engine.ClearAll();
                    return;

                case 'r':
                case 'R':
                    _engine.SquareRoot();
                    return;

                case 'n':
                case 'N':
                    _engine.Negate();
                    return;
            }
        }

        /// <inheritdoc />
        public override void OnMouseEvent(MouseEvent mouse)
        {
            if (mouse.Kind == MouseEventKindEnum.Press)
            {
                OnMousePressed(mouse);
                return;
            }

            if (mouse.Kind != MouseEventKindEnum.Move)
                return;

            // The menus get the pointer first, exactly as they get keys and presses first.
            if (_menuBar != null && _menuBar.HandleMouseMove(mouse.Row, mouse.Column))
                return;

            // A key nobody is pointing at must not stay lit, which is what the pad answers when the pointer has
            // moved off it entirely.
            _keypad.Hover(mouse.Row, mouse.Column);
        }

        /// <inheritdoc />
        public override void OnMousePressed(MouseEvent mouse)
        {
            base.OnMousePressed(mouse);

            if (_menuBar != null && _menuBar.HandleMouse(mouse.Row, mouse.Column))
                return;

            if (mouse.Button != MouseButtonEnum.Left)
                return;

            if (_keypad.Press(mouse.Row, mouse.Column))
                _message = null;
        }

        /// <summary>
        ///     Builds the keys.
        ///     <para>
        ///         The layout is the one every calculator has, and the wide zero is why the control supports spans
        ///         at all: it is the one key nobody accepts at the same width as its neighbours.
        ///     </para>
        /// </summary>
        private void BuildKeypad()
        {
            _keypad = new Keypad(
                new KeypadRow(
                    new KeypadButton("MC", _engine.MemoryClear) {EnabledWhen = () => _engine.HasMemory},
                    new KeypadButton("MR", _engine.MemoryRecall) {EnabledWhen = () => _engine.HasMemory},
                    new KeypadButton("M+", _engine.MemoryAdd),
                    new KeypadButton("M-", _engine.MemorySubtract),
                    new KeypadButton("←", _engine.Backspace)),
                new KeypadRow(
                    new KeypadButton("CE", _engine.ClearEntry),
                    new KeypadButton("C", _engine.ClearAll),
                    new KeypadButton("%", _engine.Percent),
                    new KeypadButton("√", _engine.SquareRoot),
                    new KeypadButton("±", _engine.Negate)),
                new KeypadRow(
                    new KeypadButton("7", () => _engine.Digit('7')),
                    new KeypadButton("8", () => _engine.Digit('8')),
                    new KeypadButton("9", () => _engine.Digit('9')),
                    new KeypadButton("÷", () => _engine.Operator(CalculatorOperatorEnum.Divide)),
                    new KeypadButton("1/x", _engine.Reciprocal)),
                new KeypadRow(
                    new KeypadButton("4", () => _engine.Digit('4')),
                    new KeypadButton("5", () => _engine.Digit('5')),
                    new KeypadButton("6", () => _engine.Digit('6')),
                    new KeypadButton("×", () => _engine.Operator(CalculatorOperatorEnum.Multiply)),
                    new KeypadButton("x²", _engine.Square)),
                new KeypadRow(
                    new KeypadButton("1", () => _engine.Digit('1')),
                    new KeypadButton("2", () => _engine.Digit('2')),
                    new KeypadButton("3", () => _engine.Digit('3')),
                    new KeypadButton("-", () => _engine.Operator(CalculatorOperatorEnum.Subtract)),
                    new KeypadButton("MS", _engine.MemoryStore)),
                new KeypadRow(
                    new KeypadButton("0", () => _engine.Digit('0'), 2),
                    new KeypadButton(".", _engine.Point),
                    new KeypadButton("+", () => _engine.Operator(CalculatorOperatorEnum.Add)),
                    new KeypadButton("=", _engine.Equals)))
            {
                ButtonWidth = 5,

                // Where the pad is drawn is where a press on it is measured from, and the two are the same number
                // rather than two numbers that have to agree.
                Row = CalculatorChrome.KeypadRow,
                Column = 0,
                BorderStyle = DosTheme.Frame,
                ButtonStyle = DosTheme.MenuPanel,
                HoverStyle = DosTheme.MenuHighlight,

                // Without this the two memory keys go inert with an empty memory and stay looking live, which
                // reads as a broken pad rather than as greyed keys.
                DisabledStyle = DosTheme.MenuDisabled
            };
        }

        /// <summary>Builds the pull-downs, styled to match the keys they sit over.</summary>
        private void BuildMenus()
        {
            _menuBar = new MenuBar(
                new MenuBarMenu("File",
                    new MenuBarEntry("Exit", () => ParentWindow.ClearForm(), "Esc")),
                new MenuBarMenu("Edit",
                    // The suite clipboard rather than the operating system's, so a total worked out here can be
                    // pasted into a spreadsheet cell next door.
                    new MenuBarEntry("Copy", Copy, "Ctrl+Ins"),
                    new MenuBarEntry("Paste", Paste, "Ctrl+V") {EnabledWhen = () => UserData.HasClipboard}),
                new MenuBarMenu("Tape",
                    new MenuBarEntry("Clear Tape", ClearTape)
                        {EnabledWhen = () => _engine.Tape.Count > 0}),
                new MenuBarMenu("Help",
                    new MenuBarEntry("About", ShowAbout)) {AlignRight = true})
            {
                BarStyle = DosTheme.MenuBar,
                HighlightStyle = DosTheme.MenuHighlight,
                PanelStyle = DosTheme.MenuPanel,
                PanelHighlightStyle = DosTheme.MenuHighlight,
                DisabledStyle = DosTheme.MenuDisabled,
                CheckMark = '√',
                BarRow = CalculatorChrome.BarRow,
                PanelRow = CalculatorChrome.DisplayRow
            };
        }

        /// <summary>Puts the display on the suite clipboard.</summary>
        private void Copy()
        {
            // The plain number rather than what is drawn, because the separators are for reading and a spreadsheet
            // asked to take "1,234.56" as a number will decline.
            UserData.Clipboard = _engine.Value.ToString(CultureInfo.InvariantCulture);
            _message = "Copied " + UserData.Clipboard + ".";
        }

        /// <summary>Types the clipboard in, if it is a number.</summary>
        private void Paste()
        {
            if (!UserData.HasClipboard)
                return;

            var text = UserData.Clipboard.Trim();

            if (!decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                _message = "The clipboard does not hold a number.";
                return;
            }

            // Typed in a digit at a time rather than assigned, so every rule about what may be typed still holds:
            // the digit limit, the single decimal point, and the sign.
            _engine.ClearEntry();

            foreach (var character in text)
            {
                if (character == '-')
                    continue;

                Typed(character);
            }

            if (text.StartsWith("-", StringComparison.Ordinal))
                _engine.Negate();
        }

        /// <summary>Throws the paper tape away.</summary>
        private void ClearTape()
        {
            _engine.ClearTape();
            _message = "Tape cleared.";
        }

        /// <summary>Says what this is.</summary>
        private void ShowAbout()
        {
            _message = "WolfCurses calculator - it works left to right, so 2 + 3 x 4 is 20. The tape shows why.";
        }

        /// <summary>The key-hint strip, or whatever the last action had to say.</summary>
        /// <returns>The status text.</returns>
        private string StatusText()
        {
            if (!string.IsNullOrEmpty(_message))
                return "  " + _message;

            // Short enough to survive the fit at eighty columns. The longer version read "C rese".
            return "  ENTER=Total  BACKSPACE=Rub out  DEL=Clear  C=Reset  F10=Menu  ESC=Suite";
        }
    }
}
