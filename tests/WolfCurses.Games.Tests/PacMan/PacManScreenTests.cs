using System;
using System.Collections.Generic;
using System.Threading;
using WolfCurses.Games.Tests.Support;
using Xunit;

namespace WolfCurses.Games.Tests.PacMan
{
    /// <summary>
    ///     Pac-Man driven through the real arcade — keys in, frames out.
    ///     <para>
    ///         These sleep, unlike the maze game's, because this one is paced by an <see cref="IntervalTimer" /> off
    ///         the system tick: real time has to pass for a step to fall due, and that is the point of the timer.
    ///     </para>
    /// </summary>
    [Collection("GamesApp")]
    public class PacManScreenTests
    {
        [Fact]
        public void ItOpensOnTheBoardWithItsScoreboard()
        {
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.PacMan);

            Assert.Contains("Score 0", game.Screen, StringComparison.Ordinal);
            Assert.Contains("Board 1", game.Screen, StringComparison.Ordinal);
            Assert.Contains("READY!", game.Screen, StringComparison.Ordinal);
            Assert.Contains("Pellets 0/", game.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void TheWallsAreDrawnAsConnectedLinesRatherThanBlocks()
        {
            // The whole reason BoxDrawing exists. Corners, tees and a crossing all have to be on screen, or the maze
            // is being drawn with one glyph and the junction table is doing nothing.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.PacMan);

            var screen = game.Screen;

            foreach (var glyph in new[] {'╔', '╗', '╚', '╝', '═', '║', '╦', '╩'})
                Assert.Contains(glyph, screen);

            // And no blocks, which is what this replaced.
            Assert.DoesNotContain('█', screen);
        }

        [Fact]
        public void TheOutsideWallDoesNotJoinToAnythingBeyondTheBoard()
        {
            // The bug that shipped for exactly one run: the maze reports "wall" for everywhere off the board, which
            // is right for walking and wrong for drawing, and it turned the whole border into tees pointing into
            // space. The top-left of the board is a corner or the fix came out.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.PacMan);

            var board = BoardRows(game);

            Assert.StartsWith("╔", board[0], StringComparison.Ordinal);
            Assert.Contains("╗", board[0], StringComparison.Ordinal);
            Assert.DoesNotContain('╩', board[0]);
        }

        [Fact]
        public void TheWholeScreenFitsAnEightyByTwentyFourTerminal()
        {
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.PacMan);

            var lines = new List<string>(game.Screen.Replace("\r\n", "\n").Split('\n'));

            Assert.InRange(lines.Count, 1, 23);
            foreach (var line in lines)
                Assert.InRange(line.TrimEnd('\r').Length, 0, 80);
        }

        [Fact]
        public void ThePanelSitsBesideTheBoardRatherThanUnderIt()
        {
            // TextColumns doing what it does in Tetris, on rows that are several hundred bytes of colour each - which
            // is why it measures visible width and PadRight would shred the panel diagonally down the screen.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.PacMan);

            var board = BoardRows(game);
            var withPanel = 0;

            foreach (var row in board)
            {
                var boardEnds = row.LastIndexOfAny(new[] {'║', '╝', '╗', '═', '╩', '╦'});
                if (boardEnds >= 0 && row.Length > boardEnds + 2)
                    withPanel++;
            }

            Assert.True(withPanel >= 4, $"only {withPanel} board rows have anything beside them");
            Assert.Contains("SCATTER", game.Screen, StringComparison.Ordinal);
            Assert.Contains("Blinky", game.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void TheGameRunsOnItsOwnClock()
        {
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.PacMan);

            var opening = game.Screen;

            // Real elapsed time has to pass, because a step is paced by an IntervalTimer rather than by counting
            // ticks - which is the whole point of the timer. One tick consumes at most one period, so this has to run
            // for more ticks than the opening pause is steps long, not merely for longer than it lasts.
            var started = false;
            for (var i = 0; i < 30 && !started; i++)
            {
                Thread.Sleep(130);
                game.Tick();
                started = !game.Screen.Contains("READY!", StringComparison.Ordinal);
            }

            Assert.True(started, "the board never came out of its opening pause");
            Assert.NotEqual(opening, game.Screen);
        }

        [Fact]
        public void SteeringMovesThePlayerAndEatsPellets()
        {
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.PacMan);

            game.Press(ConsoleKey.UpArrow);

            for (var i = 0; i < 12; i++)
            {
                Thread.Sleep(130);
                game.Tick();
            }

            Assert.DoesNotContain("Score 0 ", game.Screen, StringComparison.Ordinal);
            Assert.DoesNotContain("Pellets 0/", game.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void WasdSteersItTooAndStaysOutOfThePrompt()
        {
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.PacMan);

            foreach (var (character, key) in new[]
                     {('w', ConsoleKey.W), ('a', ConsoleKey.A), ('s', ConsoleKey.S), ('d', ConsoleKey.D)})
                game.PressChar(character, key);

            Assert.Equal(string.Empty, game.App.InputManager.InputBuffer);
        }

        [Fact]
        public void EscapeBacksOutToTheArcadeMenu()
        {
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.PacMan);
            Assert.Contains("Board 1", game.Screen, StringComparison.Ordinal);

            game.Escape();

            Assert.Contains("Which game?", game.Screen, StringComparison.Ordinal);
            Assert.Contains("Pac-Man best", game.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void TheArcadeMenuOffersIt()
        {
            using var game = new DrivenGamesApp();

            Assert.Contains("Pac-Man", game.Screen, StringComparison.Ordinal);
            Assert.Contains("Pac-Man best: 0", game.Screen, StringComparison.Ordinal);
        }

        /// <summary>The rows of the frame that carry the board, which is every row that starts with a wall glyph.</summary>
        private static List<string> BoardRows(DrivenGamesApp game)
        {
            var rows = new List<string>();
            foreach (var line in game.Screen.Replace("\r\n", "\n").Split('\n'))
            {
                var row = line.TrimEnd('\r');
                if (row.Length > 0 && (row[0] == '║' || row[0] == '╔' || row[0] == '╚' || row[0] == ' '))
                    rows.Add(row);
            }

            return rows;
        }
    }
}
