using System;
using WolfCurses.Games.Tests.Support;
using Xunit;

namespace WolfCurses.Games.Tests
{
    /// <summary>
    ///     What a steer in Snake is allowed to change, which is nothing at all until the snake steps.
    ///     <para>
    ///         <b>The screen is the whole of the claim.</b> SnakeDialog used to compose the entire board again from
    ///         inside its key handler, on the stated grounds that the heading in the status line moves at once. It
    ///         does not: SnakeBoard.Steer records a pending direction, and SnakeBoard.Direction is the direction last
    ///         actually TRAVELLED, so every frame that handler built was byte-identical to the one already showing.
    ///         That is what let the compose be deleted outright rather than moved behind a dirty flag, and this is
    ///         the test that says so. Should a readout ever show the pending direction, it fails here first, and the
    ///         answer then is ChessDialog's: a dirty flag consumed in OnTick, never a compose in an input handler.
    ///     </para>
    ///     <para>
    ///         <b>The keys are sent before a single pump on purpose.</b> InputManager drains the console buffer with
    ///         a while loop and spends every queued key inside one tick, so a held arrow arrives as a burst of eight
    ///         or ten presses with no time between them. That burst is the shape that made a per-press compose
    ///         expensive rather than merely pointless, and it is also what keeps this sample short: the step is paced
    ///         on real elapsed time, so a sample that dawdled would have the snake move underneath it.
    ///     </para>
    /// </summary>
    [Collection("GamesApp")]
    public class SnakeSteerTests
    {
        [Fact]
        public void SteeringChangesNothingOnScreenUntilTheSnakeSteps()
        {
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Snake);

            var before = BelowTheSpinner(game.Screen);
            Assert.Contains("Heading Right", before, StringComparison.Ordinal);

            // Down is a legal turn out of Right, so the steer is accepted and recorded. Sent straight at the input
            // manager rather than through the driver's Press, which pumps after each one: the point of the burst is
            // that all ten reach the form inside a single tick, exactly as a held key does.
            var arrow = new ConsoleKeyInfo((char) 0, ConsoleKey.DownArrow, false, false, false);
            for (var press = 0; press < 10; press++)
                game.App.InputManager.SendConsoleKey(arrow);

            game.App.PumpInput();

            Assert.Equal(before, BelowTheSpinner(game.Screen));

            // The other half, or the assertion above would pass just as happily against a screen that had stopped
            // drawing altogether. Real time has to pass, because the step is paced by an IntervalTimer rather than
            // by counting ticks.
            for (var wait = 0; wait < 6; wait++)
            {
                if (!string.Equals(before, BelowTheSpinner(game.Screen), StringComparison.Ordinal))
                    break;

                System.Threading.Thread.Sleep(150);
                game.Tick();
            }

            Assert.Contains("Heading Down", game.Screen, StringComparison.Ordinal);
            Assert.NotEqual(before, BelowTheSpinner(game.Screen));
        }

        /// <summary>
        ///     Everything under the scene graph's first row, which is the only part of a frame that can be asked
        ///     whether it changed: that row carries a spinner advancing on every tick, so two frames taken a tick
        ///     apart never match each other whatever the window did or did not do.
        /// </summary>
        /// <param name="frame">A whole frame as the driver read it.</param>
        /// <returns>The frame without its status row.</returns>
        private static string BelowTheSpinner(string frame)
        {
            var firstBreak = frame.IndexOf('\n');
            return firstBreak < 0 ? frame : frame.Substring(firstBreak + 1);
        }
    }
}
