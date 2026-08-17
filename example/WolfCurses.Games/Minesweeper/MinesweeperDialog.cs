// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/16/2026

using System;
using System.Globalization;
using System.Text;
using WolfCurses.Graphics;
using WolfCurses.Window;
using WolfCurses.Window.Control;
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
        /// <summary>Beginner's board: nine by nine, ten mines. The proportions every version of this game opens with.</summary>
        private const int BoardWidth = 9;

        private const int BoardHeight = 9;
        private const int Mines = 10;

        /// <summary>The board's frame, and the same widget the snake plays inside.</summary>
        private readonly Box _frame = new() {Title = "Minesweeper", Padding = 0};

        private Minefield _field;
        private string _message;
        private string _rendered;

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

            ParentWindow.PromptText = "Square (B4), F to flag (F B4), R for a new board, Q or ESC to quit";
            StartNewBoard();
        }

        /// <inheritdoc />
        public override string OnRenderForm()
        {
            // Called on every system tick. Nothing here moves on its own — the board changes only when a line is
            // typed — so the string is built by OnInputBufferReturned and simply handed back here.
            return _rendered;
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
                _field.ToggleFlag(x, y);
            else
                _field.Reveal(x, y);

            _message = DescribeState();
            _rendered = Compose();
        }

        /// <summary>Deals a fresh board and clears whatever the last one had to say.</summary>
        private void StartNewBoard()
        {
            _field = new Minefield(BoardWidth, BoardHeight, Mines, SimUnit.Random);
            _message = "Type a square to open it. The first one you open is never a mine.";
            _rendered = Compose();
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

        /// <summary>Draws the header, the board inside its frame, and the status line under it.</summary>
        private string Compose()
        {
            var body = new StringBuilder();
            body.AppendLine();
            body.AppendLine($"Mines {_field.MineCount - _field.FlagsPlaced} left    " +
                            $"Cleared this session: {UserData.MinefieldsCleared}");
            body.AppendLine();
            body.AppendLine(_frame.Render(ComposeBoard()));
            body.AppendLine();
            body.Append(_message);
            return body.ToString();
        }

        /// <summary>Draws the squares, with the column letters along the top and the row numbers down the side.</summary>
        private string ComposeBoard()
        {
            var sb = new StringBuilder();

            sb.Append("   ");
            for (var x = 0; x < _field.Width; x++)
                sb.Append((char) ('A' + x)).Append(' ');

            for (var y = 0; y < _field.Height; y++)
            {
                sb.AppendLine();
                sb.Append((y + 1).ToString(CultureInfo.InvariantCulture).PadLeft(2)).Append(' ');

                for (var x = 0; x < _field.Width; x++)
                    sb.Append(DrawSquare(x, y)).Append(' ');
            }

            return sb.ToString();
        }

        /// <summary>
        ///     One square, glyph and color. The numbers take the palette every version of this game has used since
        ///     1990 — and take it as <see cref="ConsoleColor" /> rather than exact RGB, so they follow whatever theme
        ///     the terminal is wearing instead of fighting it.
        /// </summary>
        /// <param name="x">Column, counting from zero.</param>
        /// <param name="y">Row, counting from zero.</param>
        /// <returns>The styled single character to draw.</returns>
        private string DrawSquare(int x, int y)
        {
            if (_field.IsFlagged(x, y))
                return new TextStyle(ConsoleColor.Yellow).Apply("F");

            if (!_field.IsRevealed(x, y))
                return "·";

            if (_field.IsMine(x, y))
                return new TextStyle(ConsoleColor.Red, bold: true).Apply("*");

            var adjacent = _field.AdjacentMines(x, y);
            if (adjacent == 0)
                return " ";

            var color = adjacent switch
            {
                1 => ConsoleColor.Blue,
                2 => ConsoleColor.DarkGreen,
                3 => ConsoleColor.Red,
                4 => ConsoleColor.DarkBlue,
                5 => ConsoleColor.DarkRed,
                6 => ConsoleColor.DarkCyan,
                7 => ConsoleColor.DarkMagenta,
                _ => ConsoleColor.DarkGray
            };

            return new TextStyle(color).Apply(adjacent.ToString(CultureInfo.InvariantCulture));
        }
    }
}
