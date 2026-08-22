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
        ///     agree: the scene graph contributes one row above the form body (its spinner and the window's name,
        ///     which this app's <c>OnPreRender</c> ends), and the form itself opens with a blank line, a title, a
        ///     readout and a reaction line. Five, and the screen has to add the scene graph's one to its own four or
        ///     every grab samples a cell-row further down the canvas than the pointer was on.
        /// </summary>
        private const int PictureTopRow = 5;

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
        public void TheGrabRectangleStartsOnThePicturesOwnFirstRow()
        {
            using var game = Opened();

            // Walked to the top of the canvas first, because that is the only place a one row error in the hit
            // rectangle can be seen at all: anywhere else the row above and the row below are both penguin, and a
            // drag is measured from its own press, so the error cancels for every frame after the first one.
            for (var press = 0; press < 60; press++)
                game.Press(ConsoleKey.UpArrow);

            Settle(game);

            var top = TopRowOnScreen(game);

            // The two derivations of the same fact, made to agree out loud. Without this the test below would still
            // pass while measuring from a row nobody draws on.
            Assert.Equal(PictureTopRow, top);

            var start = Picture(game);

            // The reaction line, one row above the picture. Nothing of the canvas is drawn there, so a press on it
            // must take hold of nothing - and while the screen counted only its own rows it answered that row with
            // canvas row zero and dragged the penguin sitting there.
            Sweep(game, top - 1);
            Settle(game);

            Assert.Equal(start, Picture(game));

            // The other side of the same edge, so a rectangle pushed one row the other way fails here rather than
            // passing quietly.
            Sweep(game, top);
            Settle(game);

            Assert.True(start != Picture(game), "a press on the picture's own first row took hold of nothing");
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

        [Fact]
        public void ThePictureIsMeasuredWithoutTheBlankRowAndThePromptUnderIt()
        {
            // A test about this file's own measuring rather than about the screen, which is worth having because the
            // fault it pins was invisible rather than wrong-looking. PictureRows counted everything from the
            // picture's first row to the end of the frame, which swept in the blank line and the input prompt the
            // scene graph adds under the form, so it came out two rows over and PictureMiddleRow landed a row below
            // the real middle. At the headless twenty-four-row fallback both still sat on the picture, so every
            // assertion in this file went on passing and would have gone on passing on a taller console until the
            // press missed.
            using var game = Opened();

            var rows = game.Screen.Split('\n');
            var covered = PictureRows(game);

            // The regression guard: measuring to the end of the frame makes this an equality rather than a less-than.
            Assert.True(PictureTopRow + covered < rows.Length,
                "the picture was measured to the very end of the frame, so the prompt was counted as part of it");

            // Every row it claims is a drawn row, and the one after it is where the picture stops.
            for (var row = PictureTopRow; row < PictureTopRow + covered; row++)
                Assert.NotEqual(0, rows[row].TrimEnd('\r').Length);

            Assert.Equal(0, rows[PictureTopRow + covered].TrimEnd('\r').Length);

            // And the row the drag tests press on really is inside the picture.
            Assert.InRange(PictureMiddleRow(game), PictureTopRow, PictureTopRow + covered - 1);
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

        /// <summary>
        ///     Which row the picture is really drawn on, read off the frame rather than counted a second time here.
        ///     The reaction line is the last thing composed above it, so the row after it is the picture's own first.
        /// </summary>
        /// <param name="game">The running demo.</param>
        /// <returns>The screen row.</returns>
        private static int TopRowOnScreen(DrivenDemoApp game)
        {
            var rows = game.Screen.Split('\n');

            for (var row = 0; row < rows.Length; row++)
            {
                if (rows[row].Contains("ARROW KEYS", StringComparison.Ordinal))
                    return row + 1;
            }

            Assert.Fail("the screen never drew its reaction line:" + Environment.NewLine + game.Describe());
            return -1;
        }

        /// <summary>
        ///     Presses over the left penguin on one row and sweeps right with the button held, which is a whole drag.
        ///     The column is where that penguin is by construction rather than by measurement: it is built at canvas
        ///     x zero and nothing in this test moves it sideways before the sweep.
        /// </summary>
        /// <param name="game">The running demo.</param>
        /// <param name="row">The screen row to press and sweep along.</param>
        private static void Sweep(DrivenDemoApp game, int row)
        {
            Send(game, 1, row, MouseButtonEnum.Left, MouseEventKindEnum.Press);

            for (var column = 3; column <= 17; column += 2)
                Send(game, column, row, MouseButtonEnum.Left, MouseEventKindEnum.Move);

            Send(game, 17, row, MouseButtonEnum.Left, MouseEventKindEnum.Release);
        }

        /// <summary>
        ///     How many rows the picture covers, counted off the screen rather than assumed.
        ///     <para>
        ///         <b>It stops at the first blank row, because the picture is not the last thing in the frame.</b>
        ///         The form's text ends with the picture, and <c>SceneGraph</c> then adds a line break of its own and
        ///         the input prompt, so the frame runs picture, blank, prompt. Measuring this as "everything from the
        ///         picture's first row to the end of the frame" counted those last two as picture and came out two
        ///         rows over, which put <see cref="PictureMiddleRow" /> a row below the real middle. Both numbers
        ///         still landed on the picture at the headless twenty-four-row fallback, which is exactly why no test
        ///         failed and why this was worth fixing before a taller console made it matter.
        ///     </para>
        ///     <para>
        ///         A picture row can never be blank: a half-block row is one glyph repeated across the full width of
        ///         the picture whatever is drawn on it, which is the same property <see cref="Picture" /> relies on
        ///         from the other side.
        ///     </para>
        /// </summary>
        /// <param name="game">The running demo.</param>
        /// <returns>The row count.</returns>
        private static int PictureRows(DrivenDemoApp game)
        {
            var rows = game.Screen.Split('\n');
            var covered = 0;

            for (var row = PictureTopRow; row < rows.Length; row++)
            {
                if (rows[row].TrimEnd('\r').Length == 0)
                    break;

                covered++;
            }

            return covered;
        }

        /// <summary>The screen row halfway down the picture, where both penguins sit.</summary>
        /// <param name="game">The running demo.</param>
        /// <returns>The row.</returns>
        private static int PictureMiddleRow(DrivenDemoApp game)
        {
            return PictureTopRow + PictureRows(game)/2;
        }

        /// <summary>
        ///     The picture, escapes and all, which is what changes when a penguin moves. Read off the screen rather
        ///     than out of the form, so what is asserted is what a viewer would see.
        ///     <para>
        ///         <b>The raw frame, deliberately.</b> A half-block row is one glyph repeated for its whole width
        ///         and the entire picture is carried in the colours, so stripped of its escapes it reads the same
        ///         wherever the penguins are standing - an assertion made on the stripped rows compares nothing at
        ///         all and passes whatever the drag did.
        ///     </para>
        ///     <para>
        ///         <b>It stops where the picture stops</b>, rather than running to the end of the frame as it used
        ///         to. The blank row and the input prompt underneath are not the picture, and folding them in meant
        ///         this method answered "the picture changed" for anything that rewrote the prompt. Nothing does
        ///         today, which is the only reason it never lied.
        ///     </para>
        /// </summary>
        /// <param name="game">The running demo.</param>
        /// <returns>The picture's rows, joined.</returns>
        private static string Picture(DrivenDemoApp game)
        {
            // Counted off the stripped screen and applied to the raw one, which is sound because removing escape
            // sequences cannot remove a line break: the two split into the same number of rows.
            var rows = game.RawScreen.Split('\n');
            var last = Math.Min(rows.Length, PictureTopRow + PictureRows(game));
            var wanted = new StringBuilder();

            for (var row = PictureTopRow; row < last; row++)
                wanted.Append(rows[row].TrimEnd('\r')).Append('|');

            return wanted.ToString();
        }
    }
}
