using System;
using System.Text;
using System.Threading;
using WolfCurses.Demo.Tests.Support;
using Xunit;

namespace WolfCurses.Demo.Tests
{
    /// <summary>
    ///     Dragging a penguin, which is the demo's demonstration of <see cref="MouseEventKindEnum.Move" /> and
    ///     <see cref="MouseEventKindEnum.Release" />.
    ///     <para>
    ///         A test host has no mouse and never will, so nothing here pretends to test whether a terminal reports
    ///         one. What it tests is everything <i>after</i> that: that a move carrying a button moves the sprite it
    ///         took hold of, that a release ends it, and - the one that matters most - that a bare hover with no
    ///         button held moves nothing at all. <c>InputManager.SendMouseEvent</c> is public precisely so a host,
    ///         or a test, can feed events that did not come from a console.
    ///     </para>
    ///     <para>
    ///         <b>The sleeps are not padding.</b> The screen composes on an <c>IntervalTimer</c> at 33 ms off the
    ///         system tick, so spinning ticks with no real time between them produces no frame, no measured picture
    ///         and therefore nothing that can be hit-tested. That is what the first version of this file failed on.
    ///     </para>
    /// </summary>
    [Collection("DemoApp")]
    public class SpriteDragTests
    {
        /// <summary>
        ///     Where the picture starts, derived here independently of the screen's own counting so the two have to
        ///     agree: the library contributes one un-terminated line above the form body, and the form opens with a
        ///     blank line, a title, a readout and a reaction line.
        /// </summary>
        private const int PictureTopRow = 4;

        [Fact]
        public void DraggingMovesThePenguinAndAHoverDoesNot()
        {
            using var game = Opened();

            var middle = PictureMiddleRow(game);
            var start = Picture(game);

            // A move with NO button is a hover. It must reach the form and change nothing, or a penguin would
            // follow the pointer around without anybody having picked it up.
            Send(game, 20, middle, MouseButtonEnum.None, MouseEventKindEnum.Move);
            Settle(game);

            Assert.Equal(start, Picture(game));

            // The driven penguin starts at canvas x 0, so it is under the left-hand end of the picture's middle
            // row. Press there, then sweep right with the button still down.
            Send(game, 1, middle, MouseButtonEnum.Left, MouseEventKindEnum.Press);
            for (var column = 3; column <= 30; column += 3)
                Send(game, column, middle, MouseButtonEnum.Left, MouseEventKindEnum.Move);

            Settle(game);
            var dragged = Picture(game);

            Assert.True(start != dragged, "the penguin did not move when it was dragged");

            // The release ends it, so further moves with nothing held change nothing. Without a release the sprite
            // stays attached to the pointer for ever, which is the failure this kind exists to prevent.
            Send(game, 30, middle, MouseButtonEnum.Left, MouseEventKindEnum.Release);
            Settle(game);
            var afterRelease = Picture(game);

            for (var column = 33; column <= 50; column += 3)
                Send(game, column, middle, MouseButtonEnum.None, MouseEventKindEnum.Move);

            Settle(game);

            Assert.Equal(afterRelease, Picture(game));
        }

        [Fact]
        public void PointingAboveThePictureTakesHoldOfNothing()
        {
            // Dropped rather than clamped, the same rule the arcade's own click maps follow: a press on the title
            // line must not grab whatever happens to be at the top of the canvas.
            using var game = Opened();

            var start = Picture(game);

            Send(game, 2, 1, MouseButtonEnum.Left, MouseEventKindEnum.Press);
            for (var column = 4; column <= 30; column += 3)
                Send(game, column, 1, MouseButtonEnum.Left, MouseEventKindEnum.Move);

            Settle(game);

            Assert.Equal(start, Picture(game));
        }

        [Fact]
        public void TheScreenAsksForPointerReportingAndHandsItBackWhenItCloses()
        {
            // Motion is one event for every cell the pointer crosses, so it is asked for by the screen that wants
            // it rather than switched on for the whole demo. The handing back is the half that is easy to leave out
            // and impossible to see: nothing would look wrong, every later demo would simply be paying for a flood
            // none of them read.
            using var game = new DrivenDemoApp();
            game.DismissSplash();

            Assert.False(game.App.InputManager.ReportsMouseMotion);

            game.ChooseMenuItem((int) DemoCommandsEnum.SpriteTestCollision);
            Assert.True(game.App.InputManager.ReportsMouseMotion);

            game.Press(ConsoleKey.Escape);

            Assert.False(game.App.InputManager.ReportsMouseMotion);
        }

        /// <summary>Opens the collision screen and lets it draw its first picture.</summary>
        /// <returns>The running demo, on that screen.</returns>
        private static DrivenDemoApp Opened()
        {
            var game = new DrivenDemoApp();
            game.DismissSplash();
            game.ChooseMenuItem((int) DemoCommandsEnum.SpriteTestCollision);
            Settle(game);

            Assert.True(PictureRows(game) > 2, "the picture never appeared:" + Environment.NewLine + game.Screen);
            return game;
        }

        /// <summary>Lets a paced frame fall due and draws it.</summary>
        /// <param name="game">The running demo.</param>
        private static void Settle(DrivenDemoApp game)
        {
            Thread.Sleep(45);
            game.Tick(2);
        }

        /// <summary>Feeds one mouse event through the same public door a host reading the console would use.</summary>
        private static void Send(DrivenDemoApp game, int column, int row, MouseButtonEnum button,
            MouseEventKindEnum kind)
        {
            game.App.InputManager.SendMouseEvent(new MouseEvent(column, row, button, kind: kind));
            game.App.PumpInput();
        }

        /// <summary>How many rows the picture covers, measured off the screen rather than assumed.</summary>
        /// <param name="game">The running demo.</param>
        /// <returns>The row count.</returns>
        private static int PictureRows(DrivenDemoApp game)
        {
            return Math.Max(0, game.Screen.Split('\n').Length - PictureTopRow);
        }

        /// <summary>The screen row halfway down the picture, where both penguins sit.</summary>
        /// <param name="game">The running demo.</param>
        /// <returns>The row.</returns>
        private static int PictureMiddleRow(DrivenDemoApp game)
        {
            return PictureTopRow + PictureRows(game)/2;
        }

        /// <summary>
        ///     The picture, stripped, which is what changes when a penguin moves. Read off the screen rather than
        ///     out of the form, so what is asserted is what a viewer would see.
        /// </summary>
        /// <param name="game">The running demo.</param>
        /// <returns>The picture's rows, joined.</returns>
        private static string Picture(DrivenDemoApp game)
        {
            var rows = game.Screen.Split('\n');
            var wanted = new StringBuilder();

            for (var row = PictureTopRow; row < rows.Length; row++)
                wanted.Append(rows[row].TrimEnd('\r')).Append('|');

            return wanted.ToString();
        }
    }
}
