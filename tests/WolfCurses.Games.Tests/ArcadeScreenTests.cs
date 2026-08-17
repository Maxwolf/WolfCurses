using System;
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
            game.ChooseMenuItem(1);
            Assert.Contains("Score", game.Screen, StringComparison.Ordinal);

            game.Escape();

            Assert.Contains("Which game?", game.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void SnakeAdvancesOnItsOwnClockAndSteers()
        {
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem(1);

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
            game.ChooseMenuItem(3);

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
            game.ChooseMenuItem(3);

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
            game.ChooseMenuItem(2);
            Assert.Contains("Mines 10 left", game.Screen, StringComparison.Ordinal);

            var hiddenBefore = CountHidden(game.Screen);
            game.Type("e5");

            // Counted rather than pattern-matched: a small cascade can leave a whole row of dots standing, so
            // "the all-hidden row is gone" is a test that fails on a legal board.
            Assert.True(CountHidden(game.Screen) < hiddenBefore,
                "opening a square revealed nothing:\n" + game.Describe());

            game.Type("f a1");
            Assert.Contains("Mines 9 left", game.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void MinesweeperRejectsNonsenseWithoutChangingTheBoard()
        {
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem(2);
            game.Type("e5");

            var before = game.Screen;
            game.Type("zz9");

            Assert.Contains("is not a square", game.Screen, StringComparison.Ordinal);
            Assert.Equal(BoardRowsOf(before), BoardRowsOf(game.Screen));
        }

        [Fact]
        public void ChessPlaysAMoveAndAnswersIt()
        {
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem(4);

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
            game.ChooseMenuItem(4);

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
            game.ChooseMenuItem(4);

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
            game.ChooseMenuItem(4);

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

        /// <summary>How many squares on a minesweeper board are still face down.</summary>
        private static int CountHidden(string screen)
        {
            var hidden = 0;
            foreach (var character in screen)
            {
                if (character == '·')
                    hidden++;
            }

            return hidden;
        }

        [Fact]
        public void ChessCommandsWork()
        {
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem(4);

            game.Type("level 2");
            Assert.Contains("level 2", game.Screen, StringComparison.Ordinal);

            game.Type("flip");
            Assert.Contains("Black's side", game.Screen, StringComparison.Ordinal);

            game.Type("help");
            Assert.Contains("resign", game.Screen, StringComparison.Ordinal);
        }

        /// <summary>The board rows of a minesweeper screen, so a test can say "the board did not change".</summary>
        private static string BoardRowsOf(string screen)
        {
            var rows = new System.Text.StringBuilder();
            foreach (var line in screen.Split('\n'))
            {
                var row = line.TrimEnd('\r');
                if (row.StartsWith("│ ", StringComparison.Ordinal))
                    rows.AppendLine(row);
            }

            return rows.ToString();
        }
    }
}
