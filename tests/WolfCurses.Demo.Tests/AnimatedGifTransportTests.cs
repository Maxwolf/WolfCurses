using System;
using System.Threading;
using WolfCurses.Demo.Tests.Support;
using Xunit;

namespace WolfCurses.Demo.Tests
{
    /// <summary>
    ///     The GIF screen's transport, which is the demo's only runnable demonstration of <c>PlaybackClock</c> and
    ///     <c>Timeline</c> - the office media player needs ffmpeg, ffprobe and ffplay on PATH, so for most people
    ///     these two types had no demo at all.
    ///     <para>
    ///         <b>What is under test is the rule the clock exists for.</b> An <c>IntervalTimer</c> drops a late
    ///         period on purpose; a media position must never drop anything, because a frame's time is a fact about
    ///         the media rather than about how often somebody asked. So a screen that has not been ticked for a
    ///         while has to arrive at the frame due <i>now</i> and simply never show the ones in between.
    ///     </para>
    /// </summary>
    [Collection("DemoApp")]
    public class AnimatedGifTransportTests
    {
        [Fact]
        public void FallingBehindSkipsFramesRatherThanRunningSlow()
        {
            using var game = Playing();

            var first = FrameNumber(game);

            // A whole second of real time with nothing ticked at all, which is roughly thirty frames of this file.
            // A pacer would advance by ONE and the animation would run a thirtieth of its proper speed; a clock
            // lands on whatever is due and the rest are simply gone.
            Thread.Sleep(1000);
            game.Tick(2);

            var after = FrameNumber(game);
            var moved = Math.Abs(after - first);

            Assert.True(moved > 5,
                $"a second of real time moved the animation from frame {first} to {after}, which is a pacer's " +
                "behaviour rather than a clock's");
        }

        [Fact]
        public void SpacePausesAndTheFrameStopsMoving()
        {
            using var game = Playing();

            game.PressChar(' ', ConsoleKey.Spacebar);
            game.Tick(2);

            var held = FrameNumber(game);

            Thread.Sleep(400);
            game.Tick(4);

            Assert.Equal(held, FrameNumber(game));
            Assert.Contains("PAUSED", game.Screen, StringComparison.Ordinal);

            // And the space did not end up in the echoed prompt, which it would have done left alone: a space is
            // printable, so it reaches the input buffer as well as the key handler.
            Assert.Equal(string.Empty, game.App.InputManager.InputBuffer);
        }

        [Fact]
        public void SteppingAFrameHoldsThereRatherThanRunningOnAgain()
        {
            // Stepping is only useful on a screen whose job is to let you inspect one composited frame, and a step
            // that left the clock running would be overwritten before it could be looked at.
            using var game = Playing();

            game.Press(ConsoleKey.RightArrow);
            game.Tick(2);

            var stepped = FrameNumber(game);

            Thread.Sleep(400);
            game.Tick(4);

            Assert.Equal(stepped, FrameNumber(game));

            game.Press(ConsoleKey.LeftArrow);
            game.Tick(2);

            Assert.Equal(stepped - 1, FrameNumber(game));
        }

        [Fact]
        public void TheScrubBarSaysWhereItHasGotToAndHowLongTheFileIs()
        {
            // Timeline's own rule is that both ends are exact, so an unknown length draws no marker at all. A GIF
            // has a real length, which is the whole reason this screen can show a bar rather than a spinner.
            using var game = Playing();

            var screen = game.Screen;

            Assert.Contains("0:0", screen, StringComparison.Ordinal);
            Assert.Contains("frame ", screen, StringComparison.Ordinal);
        }

        /// <summary>Opens the GIF screen and waits out the load, which renders every frame up front.</summary>
        /// <returns>The running demo, playing.</returns>
        private static DrivenDemoApp Playing()
        {
            var game = new DrivenDemoApp();
            game.DismissSplash();
            game.ChooseMenuItem((int) DemoCommandsEnum.ShowAnimatedGif);

            // The load is spread across the tick loop a slice at a time, so it needs both ticks and real time.
            for (var attempt = 0; attempt < 400 && FrameNumber(game) < 0; attempt++)
            {
                game.Tick(4);
                Thread.Sleep(10);
            }

            Assert.True(FrameNumber(game) >= 0,
                "the GIF never finished loading:" + Environment.NewLine + game.Screen);

            return game;
        }

        /// <summary>
        ///     Which frame is showing, read off the screen's own readout rather than out of the form, or -1 while it
        ///     is still loading.
        /// </summary>
        /// <param name="game">The running demo.</param>
        /// <returns>The one-based frame number the screen reports.</returns>
        private static int FrameNumber(DrivenDemoApp game)
        {
            var screen = game.Screen;
            var at = screen.IndexOf("frame ", StringComparison.Ordinal);
            if (at < 0)
                return -1;

            at += "frame ".Length;
            var end = at;
            while (end < screen.Length && char.IsDigit(screen[end]))
                end++;

            return end > at ? int.Parse(screen[at..end], System.Globalization.CultureInfo.InvariantCulture) : -1;
        }
    }
}
