using System;
using System.Collections.Generic;
using WolfCurses.Games.Tests.Support;
using Xunit;

namespace WolfCurses.Games.Tests.Labyrinth
{
    /// <summary>
    ///     The maze driven through the real arcade — keys in, frames out — with nothing reaching into the form.
    ///     <para>
    ///         <b>Nothing here sleeps</b>, unlike every other real-time screen test in this project. This is the one
    ///         game in the arcade with no clock: it redraws when a key is pressed and at no other time, so
    ///         <c>PumpInput</c> settling the queues is the whole of the wait.
    ///     </para>
    /// </summary>
    [Collection("GamesApp")]
    public class LabyrinthScreenTests
    {
        [Fact]
        public void TheMazeOpensWithItsHeadingAndAFrameAroundIt()
        {
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Labyrinth);

            Assert.Contains("Labyrinth", game.Screen, StringComparison.Ordinal);
            Assert.Contains("Steps 0", game.Screen, StringComparison.Ordinal);
            Assert.Contains("Explored", game.Screen, StringComparison.Ordinal);
            Assert.Contains("Exit ", game.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void TheWholeScreenFitsAnEightyByTwentyFourTerminal()
        {
            // The lesson the tetris well is sized by, and the reason the reserved-row count in the dialog is a named
            // constant. Overshoot it and the input prompt falls off the bottom, where the player cannot see what they
            // are typing - which is the bug that put MenuLayout in the library.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Labyrinth);

            var lines = Rows(game);

            // ConsolePresenter clips the frame to one row short of the console, which the headless host reports as 24.
            Assert.InRange(lines.Count, 1, 23);
            foreach (var line in lines)
                Assert.InRange(line.Length, 0, 80);
        }

        [Fact]
        public void TheViewIsAWindowOntoSomethingBiggerThanItself()
        {
            // The claim the whole game rests on. A 25x13 maze is 51x27 characters, 102 columns wide at two columns a
            // cell; if the frame on screen were that wide, the maze would fit and there would be nothing to explore.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Labyrinth);

            var frame = FrameWidth(game);

            Assert.InRange(frame, 20, 100);
        }

        [Fact]
        public void TheFrameIsTheSameSizeInACornerAsItIsInTheMiddle()
        {
            // The rectangle-in, rectangle-out invariant, end to end. Walking toward an edge pushes most of the view
            // off the maze; a renderer that stopped at the edge instead of padding would shrink the box every time,
            // and the whole screen would shuffle sideways as the player moved.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Labyrinth);

            var width = FrameWidth(game);
            var height = FrameHeight(game);

            // Wandered rather than marched. Pressing one direction twenty times walks into a wall and stops - a real
            // run of that showed the player taking ONE step in a hundred and twenty presses, which would have made
            // this test pass without the camera ever moving. The maze is unseeded, so the walk cannot be scripted
            // either; a deterministic pseudorandom sequence of directions is what actually gets the player about.
            var wander = 12345;
            for (var i = 0; i < 200; i++)
            {
                wander = wander*1103515245 + 12345;
                game.Press((wander >> 16 & 3) switch
                {
                    0 => ConsoleKey.UpArrow,
                    1 => ConsoleKey.DownArrow,
                    2 => ConsoleKey.LeftArrow,
                    _ => ConsoleKey.RightArrow
                });

                Assert.Equal(width, FrameWidth(game));
                Assert.Equal(height, FrameHeight(game));
            }

            // And the walk has to have happened, or everything above is a statement about a screen nobody touched.
            Assert.True(StepsOnScreen(game) >= 20, $"the player only took {StepsOnScreen(game)} steps");
        }

        [Fact]
        public void AnArrowKeyWalksTheMaze()
        {
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Labyrinth);

            // Which way is open depends on the maze, so all four are tried - and every one of them is a legal thing
            // to press, so this also covers walking into a wall costing nothing.
            foreach (var key in new[] {ConsoleKey.UpArrow, ConsoleKey.DownArrow, ConsoleKey.LeftArrow, ConsoleKey.RightArrow})
                game.Press(key);

            Assert.DoesNotContain("Steps 0 ", game.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void WasdSteersItTooAndStaysOutOfThePrompt()
        {
            // InputFillsBuffer => false. Without it the letters pile up in the echoed prompt while the player walks.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Labyrinth);

            foreach (var (character, key) in new[]
                     {('w', ConsoleKey.W), ('a', ConsoleKey.A), ('s', ConsoleKey.S), ('d', ConsoleKey.D)})
                game.PressChar(character, key);

            Assert.Equal(string.Empty, game.App.InputManager.InputBuffer);
            Assert.DoesNotContain("Steps 0 ", game.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void RDealsAFreshMazeAndPutsTheStepsBack()
        {
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Labyrinth);

            foreach (var key in new[] {ConsoleKey.UpArrow, ConsoleKey.DownArrow, ConsoleKey.LeftArrow, ConsoleKey.RightArrow})
                game.Press(key);

            Assert.DoesNotContain("Steps 0 ", game.Screen, StringComparison.Ordinal);

            game.PressChar('r', ConsoleKey.R);

            Assert.Contains("Steps 0 ", game.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void EscapeBacksOutToTheArcadeMenu()
        {
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Labyrinth);
            Assert.Contains("Explored", game.Screen, StringComparison.Ordinal);

            game.Escape();

            Assert.Contains("Which game?", game.Screen, StringComparison.Ordinal);
            Assert.Contains("Mazes escaped", game.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void EnterQuitsWithoutTypingAnything()
        {
            // ENTER never reaches OnKeyPressed - the input manager consumes it as buffer control - so it arrives as
            // an empty line at OnInputBufferReturned, which is the only reason a game with no typing can be quit
            // with the return key.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Labyrinth);

            game.Type(string.Empty);

            Assert.Contains("Which game?", game.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void TheArcadeMenuOffersIt()
        {
            using var game = new DrivenGamesApp();

            Assert.Contains("Labyrinth", game.Screen, StringComparison.Ordinal);
            Assert.Contains("Mazes escaped: 0", game.Screen, StringComparison.Ordinal);
        }

        // ------------------------------------------------------------ helpers

        /// <summary>How many steps the heading says the player has taken.</summary>
        private static int StepsOnScreen(DrivenGamesApp game)
        {
            foreach (var line in Rows(game))
            {
                var at = line.IndexOf("Steps ", StringComparison.Ordinal);
                if (at < 0)
                    continue;

                var digits = string.Empty;
                for (var i = at + 6; i < line.Length && char.IsDigit(line[i]); i++)
                    digits += line[i];

                return digits.Length == 0 ? -1 : int.Parse(digits, System.Globalization.CultureInfo.InvariantCulture);
            }

            return -1;
        }

        private static List<string> Rows(DrivenGamesApp game)
        {
            var rows = new List<string>();
            foreach (var line in game.Screen.Replace("\r\n", "\n").Split('\n'))
                rows.Add(line.TrimEnd('\r'));

            return rows;
        }

        /// <summary>How wide the box around the view is, read off the frame rather than recomputed.</summary>
        private static int FrameWidth(DrivenGamesApp game)
        {
            foreach (var line in Rows(game))
            {
                if (line.Length > 0 && line[0] == '┌')
                    return line.Length;
            }

            return -1;
        }

        /// <summary>How many rows the box occupies, top and bottom border included.</summary>
        private static int FrameHeight(DrivenGamesApp game)
        {
            var rows = Rows(game);
            var top = rows.FindIndex(line => line.Length > 0 && line[0] == '┌');
            var bottom = rows.FindIndex(line => line.Length > 0 && line[0] == '└');

            return top < 0 || bottom < 0 ? -1 : bottom - top + 1;
        }
    }
}
