// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/16/2026

using System;
using System.Globalization;
using System.Text;
using WolfCurses.Graphics;
using WolfCurses.Window;
using WolfCurses.Window.Form;

namespace WolfCurses.Games.Minesweeper
{
    /// <summary>
    ///     Minesweeper, played by typing a square rather than steering at one — the other half of the input story
    ///     <see cref="Snake.SnakeDialog" /> tells.
    ///     <para>
    ///         Everything this form does with input arrives through one method,
    ///         <see cref="OnInputBufferReturned" />: the library collects keystrokes into the buffer, echoes them at
    ///         the prompt, and hands over the finished line when ENTER is pressed. So the form never sees a keystroke,
    ///         has no <see cref="Form{T}.OnKeyPressed(ConsoleKey)" /> override at all, and leaves
    ///         <see cref="Form{T}.InputFillsBuffer" /> at its default — the exact opposite of the snake next door, and
    ///         a third of the code.
    ///     </para>
    ///     <para>
    ///         It also ends differently on purpose. The snake puts up a <c>MessageBox</c>, because a crashed snake has
    ///         nothing left worth looking at; a finished minefield has everything worth looking at, so this one just
    ///         changes its status line and keeps the board on screen until ENTER is pressed. Which ending to use is a
    ///         question about the game, not about the library — both are three lines.
    ///     </para>
    /// </summary>
    [ParentWindow(typeof (GamesWindow))]
    public sealed class MinesweeperDialog : Form<GamesWindowInfo>
    {
        /// <summary>
        ///     The three boards the original shipped with, largest first.
        ///     <para>
        ///         The biggest one that fits the terminal is the one you get, which is the honest answer to "the
        ///         play area feels small": a nine by nine board is the right size for a window, and a terminal is
        ///         usually much larger than a window. Chosen once when the screen opens rather than per frame, so a
        ///         board cannot change size underneath somebody who is halfway through it.
        ///     </para>
        /// </summary>
        private static readonly (int Width, int Height, int Mines)[] _boards =
        {
            (30, 16, 99),
            (16, 16, 40),
            (9, 9, 10),

            // Not one of the originals. A tile is two rows tall because a box has a line above its contents and one
            // below, so even the beginner board wants a terminal about thirty rows deep - and eighty by twenty-four
            // has to get something.
            (9, 6, 8)
        };

        /// <summary>Rows this screen spends on everything that is not the panel, plus the prompt underneath it.</summary>
        private const int ChromeRows = 5;

        /// <summary>The panel that draws it, and the thing that knows where every square landed.</summary>
        private MinesweeperFace _face;

        /// <summary>
        ///     Paces the redraw and is also the clock.
        ///     <para>
        ///         This game had no clock at all until the counters arrived — it is typed, so nothing moved between
        ///         keystrokes — and that was worth keeping until the right-hand readout made it wrong: a timer that
        ///         only advances when you touch something is not a timer. It ticks four times a second rather than
        ///         once, so the digits change on the second they are supposed to instead of up to a second late.
        ///     </para>
        /// </summary>
        private readonly IntervalTimer _tick = new(TimeSpan.FromMilliseconds(250));

        private Minefield _field;
        private MinesweeperBoardMap _map;
        private string _message;
        private string _rendered;

        /// <summary>
        ///     When the clock started, on <see cref="IntervalTimer.TotalElapsed" />, or null before the first square
        ///     is opened. The originals start counting on the first click and not when the board appears, which is
        ///     the difference between a timer and a stopwatch nobody asked for.
        /// </summary>
        private TimeSpan? _startedAt;

        /// <summary>What the clock said when the game ended, so a finished board stops counting.</summary>
        private int _finalSeconds;

        /// <summary>Where the face landed in the frame, so a click on it can be recognised.</summary>
        private int _smileyRow;

        private int _smileyColumn;

        /// <summary>Initializes a new instance of the <see cref="MinesweeperDialog" /> class.</summary>
        /// <param name="window">The parent window.</param>
        // ReSharper disable once UnusedMember.Global
        public MinesweeperDialog(IWindow window) : base(window)
        {
        }

        /// <inheritdoc />
        public override void OnFormPostCreate()
        {
            base.OnFormPostCreate();

            // The mouse is only advertised when the host actually got one - and where it did, the coordinates come
            // off the board as well, since they are only there to be typed at.
            ParentWindow.PromptText = AnsiConsole.MouseEnabled
                ? "Click to open, right-click to flag, click the face for a new board; or type B4. ESC to quit"
                : "Square (B4), F to flag (F B4), R for a new board, Q or ESC to quit";

            RestartOnActivate(_tick);

            // Sized once, here, and not per frame - see _boards. The coordinate gutter is part of that decision,
            // since it costs a row and three columns and only exists to be typed at.
            // Read once into locals; each of these is a live syscall.
            _face = ChooseBoard(!AnsiConsole.MouseEnabled, AnsiConsole.SafeWindowWidth(),
                AnsiConsole.SafeWindowHeight());

            // Asked for by the screen that wants it and handed back in OnFormClosing. Motion is one event for every
            // cell the pointer crosses, so an arcade whose other games only want clicks should not be paying for it
            // while they are on screen, and the arcade's own menu certainly should not.
            SimUnit.InputManager.ReportsMouseMotion = true;

            StartNewBoard();
        }

        /// <summary>
        ///     Hands pointer reporting back, which is the half that is easy to leave out and impossible to see.
        ///     <para>
        ///         Nothing would look wrong: the menu and every later game would simply be paying for a flood none
        ///         of them read. A form being dropped is no signal at all on its own, which is the whole reason
        ///         <c>IForm.OnFormClosing</c> exists, and the library fires it from every path a form is detached
        ///         by - ESC, ENTER, the window being removed and the program quitting - so there is no way out of
        ///         this screen that skips it.
        ///     </para>
        /// </summary>
        public override void OnFormClosing()
        {
            base.OnFormClosing();

            SimUnit.InputManager.ReportsMouseMotion = false;
        }

        /// <inheritdoc />
        public override void OnTick(bool systemTick, bool skipDay)
        {
            base.OnTick(systemTick, skipDay);

            // Only the clock moves on its own, so this does nothing at all until a square has been opened and stops
            // again the moment the board is finished - which is most of the time this screen is on show.
            if (_startedAt == null || _field.IsOver || !_tick.TryConsume())
                return;

            _rendered = Compose();
        }

        /// <inheritdoc />
        public override string OnRenderForm()
        {
            // Called on every system tick, so it hands back a string built at most four times a second up in OnTick
            // - or, for everything except the clock, whenever the board actually changed.
            return _rendered;
        }

        /// <summary>
        ///     Opens a square with the left button and flags one with the right, which is the only binding this game
        ///     has ever had.
        ///     <para>
        ///         Clicking the face deals a new board, as it always did. That is worth having rather than clever:
        ///         it is where a player's hand already is when a board ends, and the alternative is telling them to
        ///         type <c>R</c> on a screen they are driving with a pointer.
        ///     </para>
        /// </summary>
        /// <summary>
        ///     Lights the square the pointer is over, which is the one thing about this board a player cannot work
        ///     out by looking.
        ///     <para>
        ///         Every line of the lattice is shared by two squares, so the click map hands each one to a square
        ///         on purpose; on screen that boundary is a hairline and the player finds out which side of it they
        ///         were on by clicking. See <see cref="MinesweeperFace.HoveredX" /> for the drawing half.
        ///     </para>
        ///     <para>
        ///         <b>Nothing is recomposed for the pointer crossing a cell inside the square it was already on</b>,
        ///         which is seven cells in eight for a tile four columns wide and two rows tall. Motion is one event
        ///         per cell crossed, so a screen that redrew for each of them would be the <c>ChessDialog</c> fault
        ///         of rebuilding the frame once per input event, which is what that game was measured doing a
        ///         thousand times a second. The library states the same discipline in <c>MenuBar.HandleMouseMove</c>,
        ///         which reports whether anything <i>moved</i> rather than whether the event was over the menu.
        ///     </para>
        /// </summary>
        /// <param name="mouse">What the mouse did, and where.</param>
        public override void OnMouseEvent(MouseEvent mouse)
        {
            // Presses go the old road, so OnMousePressed stays the single place a square is opened or flagged.
            if (mouse.Kind == MouseEventKindEnum.Press)
            {
                OnMousePressed(mouse);
                return;
            }

            if (mouse.Kind != MouseEventKindEnum.Move)
                return;

            if (!_map.TryToSquare(mouse.Row, mouse.Column, out var x, out var y))
            {
                x = -1;
                y = -1;
            }

            if (x == _face.HoveredX && y == _face.HoveredY)
                return;

            _face.HoveredX = x;
            _face.HoveredY = y;
            _rendered = Compose();
        }

        /// <param name="mouse">Where the press landed and which button it was.</param>
        public override void OnMousePressed(MouseEvent mouse)
        {
            base.OnMousePressed(mouse);

            if (mouse.Row == _smileyRow && mouse.Column >= _smileyColumn &&
                mouse.Column < _smileyColumn + _face.SmileyWidth)
            {
                StartNewBoard();
                return;
            }

            if (_field.IsOver || !_map.TryToSquare(mouse.Row, mouse.Column, out var x, out var y))
                return;

            if (mouse.Button == MouseButtonEnum.Right)
            {
                _field.ToggleFlag(x, y);
            }
            else if (mouse.Button == MouseButtonEnum.Left)
            {
                StartClock();
                _field.Reveal(x, y);
            }
            else
            {
                return;
            }

            _message = DescribeState();
            _rendered = Compose();
        }

        /// <summary>
        ///     Every input this game has. Reached when the player presses ENTER, carrying whatever they typed; an
        ///     empty line is a bare ENTER, which quits once the game is over and is ignored while it is not.
        /// </summary>
        /// <param name="input">The finished line from the input buffer.</param>
        public override void OnInputBufferReturned(string input)
        {
            var command = (input ?? string.Empty).Trim();

            if (command.Length == 0)
            {
                if (_field.IsOver)
                    ClearForm();

                return;
            }

            if (string.Equals(command, "q", StringComparison.OrdinalIgnoreCase))
            {
                ClearForm();
                return;
            }

            if (string.Equals(command, "r", StringComparison.OrdinalIgnoreCase))
            {
                StartNewBoard();
                return;
            }

            if (_field.IsOver)
            {
                _message = "This board is finished - R for a new one, ENTER to return to the menu.";
                _rendered = Compose();
                return;
            }

            if (!TryParseSquare(command, out var x, out var y, out var flagging))
            {
                _message = $"\"{command}\" is not a square. Try B4, or F B4 to flag it.";
                _rendered = Compose();
                return;
            }

            if (!_field.Contains(x, y))
            {
                _message = $"\"{command}\" is off the board.";
                _rendered = Compose();
                return;
            }

            if (flagging)
            {
                _field.ToggleFlag(x, y);
            }
            else
            {
                StartClock();
                _field.Reveal(x, y);
            }

            _message = DescribeState();
            _rendered = Compose();
        }

        /// <summary>Deals a fresh board and clears whatever the last one had to say.</summary>
        private void StartNewBoard()
        {
            _field = new Minefield(_face.BoardWidth, _face.BoardHeight, MinesFor(_face), SimUnit.Random);
            _startedAt = null;
            _finalSeconds = 0;

            _message = AnsiConsole.MouseEnabled
                ? "Click a square to open it. The first one you open is never a mine."
                : "Type a square to open it. The first one you open is never a mine.";

            _rendered = Compose();
        }

        /// <summary>
        ///     Picks the largest of the original three boards that fits the terminal, and builds the panel for it.
        /// </summary>
        /// <param name="labelled">Whether the coordinate gutter has to be drawn, which costs a row and three columns.</param>
        /// <param name="columns">How many columns the terminal has.</param>
        /// <param name="rows">How many rows the terminal has.</param>
        /// <returns>The panel to play on.</returns>
        internal static MinesweeperFace ChooseBoard(bool labelled, int columns, int rows)
        {
            foreach (var (width, height, _) in _boards)
            {
                // A board wider than the alphabet cannot name its own columns, so it is only offered to a terminal
                // that has a pointer to click with. Nobody is typing "AD7".
                if (labelled && width > MinesweeperFace.WidestLabelledBoard)
                    continue;

                if (MinesweeperFace.ColumnsFor(width, labelled) <= columns &&
                    MinesweeperFace.RowsFor(height, labelled) <= rows - ChromeRows)
                    return new MinesweeperFace(width, height, labelled);
            }

            var (fallbackWidth, fallbackHeight, _) = _boards[_boards.Length - 1];
            return new MinesweeperFace(fallbackWidth, fallbackHeight, labelled);
        }

        /// <summary>How many mines the chosen board carries.</summary>
        /// <param name="face">The panel that was chosen.</param>
        /// <returns>The mine count.</returns>
        internal static int MinesFor(MinesweeperFace face)
        {
            foreach (var (width, height, mines) in _boards)
            {
                if (width == face.BoardWidth && height == face.BoardHeight)
                    return mines;
            }

            return _boards[_boards.Length - 1].Mines;
        }

        /// <summary>Starts the clock, if it is not already running. Called by whatever opens the first square.</summary>
        private void StartClock()
        {
            // TotalElapsed, deliberately: it is the one reading nothing resets, so the clock survives the form being
            // left and come back to rather than starting again from wherever the pacing timer happened to be.
            _startedAt ??= _tick.TotalElapsed;
        }

        /// <summary>What the right-hand counter shows: running while the game is, frozen once it is not.</summary>
        private int Seconds()
        {
            if (_startedAt == null)
                return 0;

            if (_field.IsOver)
                return _finalSeconds;

            _finalSeconds = (int) (_tick.TotalElapsed - _startedAt.Value).TotalSeconds;
            return _finalSeconds;
        }

        /// <summary>What the status line says after a move, including the two ways the game can end.</summary>
        private string DescribeState()
        {
            if (_field.Won)
            {
                UserData.MinefieldsCleared++;
                return "Cleared it. ENTER to return to the menu, R to play again.";
            }

            if (_field.HitMine)
                return "That was a mine. ENTER to return to the menu, R to play again.";

            return "Type a square to open it, or F before it to plant a flag.";
        }

        /// <summary>
        ///     Reads "b4", "B4", "f b4" or "fb4" into coordinates. Columns are letters and rows are numbers, both
        ///     counted from one on screen and from zero underneath, which is the only translation this game does.
        /// </summary>
        /// <param name="command">The trimmed line the player typed.</param>
        /// <param name="x">The column, counting from zero.</param>
        /// <param name="y">The row, counting from zero.</param>
        /// <param name="flagging">Whether the line asked for a flag rather than an opening.</param>
        /// <returns>True when the line named a square.</returns>
        private static bool TryParseSquare(string command, out int x, out int y, out bool flagging)
        {
            x = 0;
            y = 0;
            flagging = false;

            var text = command.Replace(" ", string.Empty).ToUpperInvariant();

            // A leading F is the flag prefix, but only when something follows it — "F" alone is not a square, and
            // "F" is also a perfectly good column letter, which is why this checks the length before consuming it.
            if (text.Length > 1 && text[0] == 'F' && !char.IsDigit(text[1]))
            {
                flagging = true;
                text = text[1..];
            }

            if (text.Length < 2 || text[0] < 'A' || text[0] > 'Z')
                return false;

            if (!int.TryParse(text[1..], NumberStyles.None, CultureInfo.InvariantCulture, out var row))
                return false;

            x = text[0] - 'A';
            y = row - 1;
            return true;
        }

        /// <summary>
        ///     Draws the panel and the status line under it, and works out where the squares ended up.
        /// </summary>
        /// <returns>The whole screen.</returns>
        private string Compose()
        {
            var body = new StringBuilder();
            body.AppendLine();

            // COUNTED, never written down as a constant. The library contributes exactly one un-terminated line
            // above the form, so the leading AppendLine above terminates it rather than making a blank one, and
            // counting the breaks already in the builder cannot drift when any of this changes - where a hardcoded
            // number quietly becomes wrong and puts every click a row out. Missile Command learned this first.
            var panelRow = CountLineBreaks(body);

            _map = new MinesweeperBoardMap(panelRow + _face.BoardOriginRow, _face.BoardOriginColumn,
                _field.Width, _field.Height, MinesweeperFace.TileWidth, MinesweeperFace.TileHeight);

            _smileyRow = panelRow + _face.SmileyRow;
            _smileyColumn = _face.SmileyOriginColumn;

            // The coordinates are only there to be typed at, so a terminal with a working mouse gets the panel the
            // way it actually looked.
            body.AppendLine(_face.Render(_field, Seconds()));
            body.AppendLine();
            body.Append(_message);
            return body.ToString();
        }

        /// <summary>How many line breaks are already in the builder, which is the row the next line will occupy.</summary>
        /// <param name="builder">The frame being composed.</param>
        /// <returns>The count.</returns>
        private static int CountLineBreaks(StringBuilder builder)
        {
            var breaks = 0;
            for (var i = 0; i < builder.Length; i++)
            {
                if (builder[i] == (char) 10)
                    breaks++;
            }

            return breaks;
        }

    }
}
