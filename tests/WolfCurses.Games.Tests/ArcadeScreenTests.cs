using System;
using System.Collections.Generic;
using System.Globalization;
using WolfCurses.Games.Minesweeper;
using WolfCurses.Games.Tests.Support;
using Xunit;

namespace WolfCurses.Games.Tests
{
    /// <summary>
    ///     The arcade as a player meets it: keys go in, frames come out, and the assertions are about what is on
    ///     screen. Nothing here reaches inside a form.
    /// </summary>
    [Collection("GamesApp")]
    public class ArcadeScreenTests
    {
        /// <summary>The panel's own geometry, so a test reading squares off the screen cannot disagree with it.</summary>
        private static readonly MinesweeperFace _minesweeperBoard = new(9, 9);

        [Fact]
        public void TheMenuOffersEveryGame()
        {
            using var game = new DrivenGamesApp();

            Assert.Contains("Snake", game.Screen, StringComparison.Ordinal);
            Assert.Contains("Minesweeper", game.Screen, StringComparison.Ordinal);
            Assert.Contains("Tetris", game.Screen, StringComparison.Ordinal);
            Assert.Contains("WolfChess 5000", game.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void EscapeBacksOutOfAGameToTheMenu()
        {
            // One override on the window covers every game, because they are all forms on that one window. This is
            // the app-level idiom the library deliberately does not ship - see CLAUDE.md on ESC in src/Controls.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Snake);
            Assert.Contains("Score", game.Screen, StringComparison.Ordinal);

            game.Escape();

            Assert.Contains("Which game?", game.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void SnakeAdvancesOnItsOwnClockAndSteers()
        {
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Snake);

            var opening = game.Screen;
            game.Press(ConsoleKey.DownArrow);

            // Real elapsed time has to pass, because the step is paced by an IntervalTimer rather than by tick
            // counting - which is the point of the timer.
            for (var i = 0; i < 6; i++)
            {
                System.Threading.Thread.Sleep(150);
                game.Tick();
            }

            Assert.Contains("Heading Down", game.Screen, StringComparison.Ordinal);
            Assert.NotEqual(opening, game.Screen);
        }

        [Fact]
        public void ASteeredGameKeepsTypedCharactersOutOfThePrompt()
        {
            // InputFillsBuffer => false is what stops WASD accumulating in the echoed prompt. Tetris matters most
            // because its hard drop is SPACE, which is printable.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Tetris);

            game.PressChar(' ', ConsoleKey.Spacebar);
            game.PressChar('w', ConsoleKey.W);

            Assert.Equal(string.Empty, game.App.InputManager.InputBuffer);
        }

        [Fact]
        public void TetrisPutsItsPanelsBesideTheWellAtOneColumn()
        {
            // The two-column layout, which only lines up because TextColumns measures visible width rather than
            // string length. Asserted on the stripped screen so the escapes cannot flatter it.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Tetris);

            var starts = new System.Collections.Generic.HashSet<int>();
            foreach (var line in game.Screen.Split('\n'))
            {
                var row = line.TrimEnd('\r');
                if (row.Length <= 24 || (row[0] != '│' && row[0] != '┌' && row[0] != '└'))
                    continue;

                starts.Add(row.IndexOfAny(new[] {'│', '┌', '└'}, 22));
            }

            Assert.Single(starts);
            Assert.DoesNotContain(-1, starts);
        }

        [Fact]
        public void MinesweeperIsPlayedByTypingASquare()
        {
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Minesweeper);

            // Read off the counter the panel actually draws, which is three red digits counting DOWN from the mine
            // total as flags are planted - the same readout the original had, and now the only place the number
            // appears at all.
            Assert.Equal(10, MinesLeft(game.Screen));

            var hiddenBefore = CountHidden(game.Screen);
            game.Type("e5");

            // Counted rather than pattern-matched: a small cascade can leave a whole row of dots standing, so
            // "the all-hidden row is gone" is a test that fails on a legal board.
            Assert.True(CountHidden(game.Screen) < hiddenBefore,
                "opening a square revealed nothing:\n" + game.Describe());

            // Flagged wherever the board still shows a face-down square, read off the screen. Naming a fixed
            // square is flaky and was: flagging a face-up square is correctly a no-op, the app's randomiser is not
            // seeded, and whether the opening cascade reached a1 is a coin toss.
            var hidden = FirstHiddenSquareName(game.Screen);
            Assert.NotNull(hidden);

            game.Type("f " + hidden);
            Assert.Equal(9, MinesLeft(game.Screen));
        }

        [Fact]
        public void MinesweeperStartsItsClockOnTheFirstSquareRatherThanOnTheBoard()
        {
            // The originals start counting when you open something, not when the board appears — which is the
            // difference between a timer and a stopwatch nobody asked for. It has to really sleep, because the
            // readout is driven by elapsed time rather than by ticks.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Minesweeper);

            for (var i = 0; i < 10; i++)
            {
                System.Threading.Thread.Sleep(150);
                game.Tick();
            }

            Assert.Equal(0, Clock(game.Screen));

            game.Type("e5");
            for (var i = 0; i < 12; i++)
            {
                System.Threading.Thread.Sleep(150);
                game.Tick();
            }

            Assert.True(Clock(game.Screen) > 0, "the clock never started:\n" + game.Describe());
        }

        [Fact]
        public void MinesweeperStopsItsClockWhenTheBoardIsFinished()
        {
            // A finished board keeps its time on show - it is the score. The board has to be REDRAWN after the wait
            // for this to be able to fail at all, because nothing recomposes on its own once the game is over; so it
            // ends the game, waits, and then types something that forces a redraw.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Minesweeper);

            game.Type("e5");
            for (var i = 0; i < 10; i++)
            {
                System.Threading.Thread.Sleep(150);
                game.Tick();
            }

            // Opened one at a time until one of them ends it, which on a ten-mine board takes very few squares.
            for (var y = 1; y <= 9 && !IsFinished(game.Screen); y++)
            for (var x = 0; x < 9 && !IsFinished(game.Screen); x++)
                game.Type($"{(char) ('A' + x)}{y}");

            Assert.True(IsFinished(game.Screen), "the board never finished:\n" + game.Describe());

            var stopped = Clock(game.Screen);
            Assert.True(stopped > 0, "the clock never ran at all");

            for (var i = 0; i < 12; i++)
            {
                System.Threading.Thread.Sleep(150);
                game.Tick();
            }

            game.Type("a1");

            Assert.Equal(stopped, Clock(game.Screen));
        }

        /// <summary>Whether the minesweeper board on screen has been either cleared or blown up.</summary>
        /// <param name="screen">The frame.</param>
        /// <returns>True when the game is over.</returns>
        private static bool IsFinished(string screen)
        {
            return screen.Contains("R to play again", StringComparison.Ordinal) ||
                   screen.Contains("This board is finished", StringComparison.Ordinal);
        }

        [Fact]
        public void MinesweeperRejectsNonsenseWithoutChangingTheBoard()
        {
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Minesweeper);
            game.Type("e5");

            var before = game.Screen;
            game.Type("zz9");

            Assert.Contains("is not a square", game.Screen, StringComparison.Ordinal);
            Assert.Equal(BoardRows(before), BoardRows(game.Screen));
        }

        [Fact]
        public void ChessPlaysAMoveAndAnswersIt()
        {
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Chess);

            game.Type("e4");
            Assert.True(game.TickUntil("WolfChess 5000 (depth"), "the bot never replied:\n" + game.Describe());

            Assert.Contains("1.e4", game.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void ChessAnswersTheKeyboardWhileItIsThinking()
        {
            // The reason the search is sliced at all: while a Think call runs, the input manager reads no keys. The
            // budget is 15ms, so a tick that runs to hundreds of milliseconds means the slicing has regressed and
            // ESC would be dead for that long.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Chess);

            game.Type("e4");
            var (replied, worst) = game.TickUntilTimed("WolfChess 5000 (depth");

            Assert.True(replied, "the bot never replied");
            Assert.True(worst < TimeSpan.FromMilliseconds(400),
                $"a single tick took {worst.TotalMilliseconds:F0}ms, so the search is not being sliced");
        }

        [Fact]
        public void ChessRefusesAMoveWhileTheBotIsThinking()
        {
            // Otherwise the player can type Black's reply while the bot is working out Black's reply, and the search
            // finishes and plays a second one from a position that has moved on.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Chess);

            game.Type("e4");
            game.Type("e5");

            Assert.True(game.TickUntil("WolfChess 5000 (depth"));
            Assert.Contains("1.e4", game.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void ChessOffersBothBoardsAndTextIsAlwaysOneOfThem()
        {
            // Deliberately NOT asserting which one it opens on. That is decided by the renderer and the console
            // size — half blocks get two pixels per row, so under about forty rows a knight and a bishop are the
            // same smudge and the game switches to letters — and a test host's console size is whatever the runner
            // happened to have. What IS invariant: exactly one of the two is showing, and "text" swaps them.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Chess);

            var opened = game.Screen;
            game.Type("text");
            var toggled = game.Screen;

            Assert.NotEqual(opened, toggled);
            Assert.True(IsLetterBoard(opened) != IsLetterBoard(toggled),
                "toggling did not swap between the picture and the letters:\n" + game.Describe());

            game.Type("text");
            Assert.Equal(IsLetterBoard(opened), IsLetterBoard(game.Screen));
        }

        /// <summary>Whether the screen is showing the character board rather than the rendered picture.</summary>
        private static bool IsLetterBoard(string screen) =>
            screen.Contains("a  b  c  d  e  f  g  h", StringComparison.Ordinal);

        /// <summary>
        ///     The name of a square the board still shows as face down, read straight off the screen — which is the
        ///     only place a screen test is entitled to look. The row number the panel draws down its own sunken edge
        ///     is what names the rank, and the square's offset along the row names the file.
        /// </summary>
        /// <param name="screen">The visible screen.</param>
        /// <returns>A square such as "C4", or null if the whole board is face up.</returns>
        private static string FirstHiddenSquareName(string screen)
        {
            foreach (var row in BoardRows(screen))
            {
                for (var x = 0; x < _minesweeperBoard.BoardWidth; x++)
                {
                    if (IsHidden(row, x))
                        return $"{(char) ('A' + x)}{row[_minesweeperBoard.BoardOriginColumn - 1]}";
                }
            }

            return null;
        }

        /// <summary>How many squares on a minesweeper board are still face down.</summary>
        /// <param name="screen">The visible screen.</param>
        /// <returns>The count.</returns>
        private static int CountHidden(string screen)
        {
            var hidden = 0;

            foreach (var row in BoardRows(screen))
            {
                for (var x = 0; x < _minesweeperBoard.BoardWidth; x++)
                {
                    if (IsHidden(row, x))
                        hidden++;
                }
            }

            return hidden;
        }

        /// <summary>
        ///     Whether a square is still face down, read off the panel: an untouched square carries the raised
        ///     left-hand bevel and an opened one carries a thin grid line in its place.
        /// </summary>
        /// <param name="row">One drawn row of squares.</param>
        /// <param name="x">Which square across.</param>
        /// <returns>True when it has not been opened.</returns>
        private static bool IsHidden(string row, int x)
        {
            var at = _minesweeperBoard.BoardOriginColumn + x*MinesweeperFace.TileWidth;
            return at < row.Length && row[at] == '▌';
        }

        /// <summary>
        ///     The rows of the panel that carry squares, found by the row number drawn down its side. Asked of the
        ///     panel's own geometry rather than of numbers written down here, so a test cannot disagree with the
        ///     thing it is reading.
        /// </summary>
        /// <param name="screen">The frame.</param>
        /// <returns>Each row of squares, in order.</returns>
        private static IEnumerable<string> BoardRows(string screen)
        {
            foreach (var line in screen.Replace("\r\n", "\n").Split('\n'))
            {
                if (line.Length > _minesweeperBoard.BoardOriginColumn && line[0] == '▌' &&
                    char.IsDigit(line[_minesweeperBoard.BoardOriginColumn - 1]))
                    yield return line;
            }
        }

        /// <summary>What the right-hand counter says, which is the clock.</summary>
        /// <param name="screen">The frame.</param>
        /// <returns>Seconds since the first square was opened, or -1 when the counter was not found.</returns>
        private static int Clock(string screen)
        {
            foreach (var line in screen.Replace("\r\n", "\n").Split('\n'))
            {
                if (!line.Contains(":)", StringComparison.Ordinal) &&
                    !line.Contains(":(", StringComparison.Ordinal) &&
                    !line.Contains("B)", StringComparison.Ordinal))
                    continue;

                var at = line.Length - _minesweeperBoard.BoardOriginColumn - 3;
                if (at >= 0 && int.TryParse(line.Substring(at, 3), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out var seconds))
                    return seconds;
            }

            return -1;
        }

        /// <summary>What the left-hand counter says, read off the row the face is drawn on.</summary>
        /// <param name="screen">The frame.</param>
        /// <returns>How many mines are still unaccounted for, or -1 when the counter was not found.</returns>
        private static int MinesLeft(string screen)
        {
            foreach (var line in screen.Replace("\r\n", "\n").Split('\n'))
            {
                if (!line.Contains(":)", StringComparison.Ordinal) &&
                    !line.Contains(":(", StringComparison.Ordinal) &&
                    !line.Contains("B)", StringComparison.Ordinal))
                    continue;

                var at = _minesweeperBoard.BoardOriginColumn;
                if (at + 3 <= line.Length && int.TryParse(line.Substring(at, 3), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out var left))
                    return left;
            }

            return -1;
        }

        [Fact]
        public void ChessCommandsWork()
        {
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Chess);

            game.Type("level 2");
            Assert.Contains("level 2", game.Screen, StringComparison.Ordinal);

            game.Type("flip");
            Assert.Contains("Black's side", game.Screen, StringComparison.Ordinal);

            game.Type("help");
            Assert.Contains("resign", game.Screen, StringComparison.Ordinal);
        }

    }
}
