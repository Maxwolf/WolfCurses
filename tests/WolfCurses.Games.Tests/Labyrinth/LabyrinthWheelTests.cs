using System;
using System.Collections.Generic;
using WolfCurses.Games.Tests.Support;
using Xunit;

namespace WolfCurses.Games.Tests.Labyrinth
{
    /// <summary>
    ///     Looking around the maze with the wheel, driven through the arcade with nothing reaching into the form.
    ///     <para>
    ///         <b>The claim under test is the one that could have wrecked this game</b>: a camera the player can
    ///         move at will, on the one screen built around not being able to see. It does not, because the wheel
    ///         moves the window while the torch decides what is drawn - so the tests that matter here are the
    ///         negative ones, that nothing new is lit and that no step is taken.
    ///     </para>
    ///     <para>
    ///         Nothing sleeps, as in every other test of this screen: it redraws when something is pressed and at no
    ///         other time, so settling the queues is the whole of the wait.
    ///     </para>
    /// </summary>
    [Collection("GamesApp")]
    public class LabyrinthWheelTests
    {
        /// <summary>
        ///     How many rows of characters the maze occupies: 13 cells of maze plus the walls between and around
        ///     them. Named here because the two camera tests below are meaningless on a console tall enough to show
        ///     all of it at once - a test host with a real terminal attached gets a different view from one without,
        ///     which is exactly the assumption this arcade's tests have been caught making before.
        /// </summary>
        private const int MazeGridRows = 27;

        /// <summary>Turns the wheel by a number of notches; positive is away from the user, which is north.</summary>
        /// <param name="game">The running arcade.</param>
        /// <param name="notches">How far the wheel turned.</param>
        private static void Wheel(DrivenGamesApp game, int notches)
        {
            game.App.InputManager.SendMouseEvent(new MouseEvent(20, 10, MouseButtonEnum.None,
                kind: MouseEventKindEnum.Wheel, wheelDelta: notches));

            game.App.PumpInput();
        }

        [Fact]
        public void AWheelNotchLooksAroundWithoutTakingAStep()
        {
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Labyrinth);

            Assert.SkipUnless(ViewIsShorterThanTheMaze(game),
                "this console shows the whole maze at once, so there is nothing to look around at");

            // Compared on the box's own rows rather than on the whole frame, because the scene graph's status line
            // carries a spinner that advances on every tick - so "the screen changed" is true of any two frames and
            // says nothing at all about the camera.
            var before = ViewBlock(game);
            Assert.Contains("Steps 0 ", game.Screen, StringComparison.Ordinal);

            Wheel(game, -1);
            Wheel(game, -1);

            // The player starts in the middle of a maze twice as tall as the window, so two notches south is
            // nowhere near the clamp and the view has to have moved.
            Assert.NotEqual(before, ViewBlock(game));
            Assert.Contains("Steps 0 ", game.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void LookingAroundLightsNothingTheTorchHasNotAlreadyLit()
        {
            // The whole of why this was safe to ship. Only cells the torch has lit are painted at all, so scrolling
            // over unwalked ground shows blank - there is nothing there to give away. Read off the explored figure
            // rather than off the drawn cells, because that number IS what has been lit, and it is the thing a pan
            // that quietly revealed the maze would move.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Labyrinth);

            // Wandered rather than marched, the same lesson the frame-size test in this folder records: pressing one
            // direction over and over walks into a wall and stops. It has to be a real walk, because the reading is
            // a whole percent of 325 cells - an opening cross of corridors can round down to zero, which would let
            // this pass while measuring nothing.
            var wander = 24680;
            for (var i = 0; i < 120; i++)
            {
                wander = wander*1103515245 + 12345;
                game.Press((wander >> 16 & 3) switch
                {
                    0 => ConsoleKey.UpArrow,
                    1 => ConsoleKey.DownArrow,
                    2 => ConsoleKey.LeftArrow,
                    _ => ConsoleKey.RightArrow
                });
            }

            // The gate is the reading itself rather than a step count: a walk long enough to light a whole percent
            // is what this needs, and how many steps that took is neither knowable in an unseeded maze nor the
            // thing being measured. A player who escapes partway through is fine too - the maze is then fully lit
            // and the pan below still must not move the number.
            var explored = Explored(game);
            var steps = Steps(game);
            Assert.True(explored > 0, "the torch lit almost nothing, so this proves nothing");

            for (var i = 0; i < 12; i++)
                Wheel(game, i%2 == 0 ? 1 : -1);

            for (var i = 0; i < 12; i++)
                Wheel(game, 1);

            Assert.Equal(explored, Explored(game));
            Assert.Equal(steps, Steps(game));
        }

        [Fact]
        public void TheCameraGoesBackOnThePlayerAtTheNextStep()
        {
            // A peek that had to be undone by hand would be a mode, and the one place a mode must not be is a screen
            // whose player cannot see themselves. Measured as the row the player is drawn on: the camera centres
            // them, so away from the edges that row is the same one on every centred frame and a different one on
            // any peeked frame.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Labyrinth);

            Assert.SkipUnless(ViewIsShorterThanTheMaze(game),
                "this console shows the whole maze at once, so the camera has nowhere to go");

            var centred = PlayerRow(game);
            Assert.True(centred >= 0, "the player is not on the screen at all");

            for (var i = 0; i < 6; i++)
                Wheel(game, 1);

            Assert.NotEqual(centred, PlayerRow(game));

            // Whichever way this press goes - a step or a wall - the camera is back, which is the point: trying to
            // walk ends the peek rather than succeeding at it.
            game.Press(ConsoleKey.UpArrow);

            Assert.Equal(centred, PlayerRow(game));
        }

        [Fact]
        public void TheWheelDoesNotResizeTheFrameAroundTheView()
        {
            // The rectangle-in, rectangle-out invariant, from the other side. Walking into a corner already proves
            // the box does not breathe; the wheel can put the window further off the maze than any walk can, since
            // it moves the camera without a player to keep it honest.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Labyrinth);

            var width = FrameWidth(game);
            var height = FrameHeight(game);

            for (var i = 0; i < 20; i++)
            {
                Wheel(game, 1);
                Assert.Equal(width, FrameWidth(game));
                Assert.Equal(height, FrameHeight(game));
            }

            for (var i = 0; i < 40; i++)
            {
                Wheel(game, -1);
                Assert.Equal(width, FrameWidth(game));
                Assert.Equal(height, FrameHeight(game));
            }
        }

        // ------------------------------------------------------------ helpers

        /// <summary>
        ///     Whether this console is small enough for the maze to hang off the view, which is the condition the
        ///     camera exists under. Read off the drawn box rather than recomputed from the console, so it is a
        ///     statement about the frame the test is actually looking at.
        /// </summary>
        private static bool ViewIsShorterThanTheMaze(DrivenGamesApp game)
        {
            var height = FrameHeight(game);
            return height > 0 && height - 2 < MazeGridRows;
        }

        /// <summary>
        ///     The rows of the box and nothing else, which is what any "did the view move" comparison has to be
        ///     about: the status line above it carries a spinner that advances on every tick, so two frames of a
        ///     screen nobody touched are never equal.
        /// </summary>
        private static string ViewBlock(DrivenGamesApp game)
        {
            var rows = Rows(game);
            var top = rows.FindIndex(line => line.Length > 0 && line[0] == '┌');
            var bottom = rows.FindIndex(line => line.Length > 0 && line[0] == '└');

            if (top < 0 || bottom < top)
                return string.Empty;

            return string.Join('\n', rows.GetRange(top, bottom - top + 1));
        }

        /// <summary>Which frame row the player is drawn on, or -1 when they are not on screen.</summary>
        private static int PlayerRow(DrivenGamesApp game)
        {
            var rows = Rows(game);
            for (var i = 0; i < rows.Count; i++)
            {
                if (rows[i].Contains('@', StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        /// <summary>What the heading says has been lit, as a percentage.</summary>
        private static int Explored(DrivenGamesApp game)
        {
            return Number(game, "Explored ");
        }

        /// <summary>What the heading says has been walked.</summary>
        private static int Steps(DrivenGamesApp game)
        {
            return Number(game, "Steps ");
        }

        /// <summary>The number following a label on the heading, or -1 when it is not there.</summary>
        private static int Number(DrivenGamesApp game, string label)
        {
            foreach (var line in Rows(game))
            {
                var at = line.IndexOf(label, StringComparison.Ordinal);
                if (at < 0)
                    continue;

                var digits = string.Empty;
                for (var i = at + label.Length; i < line.Length && char.IsDigit(line[i]); i++)
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
