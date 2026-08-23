using System;
using System.Text.RegularExpressions;
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

            // The frame number rather than the word, since the frame RATE beside it also says "frame" and this
            // would pass on that alone - see FrameNumber for what that cost.
            Assert.True(FrameNumber(game) > 0, "the screen did not say which frame it was on:" + game.Describe());
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
        ///     Which frame is showing, read off the screen's own readout rather than out of the form, or -1 when
        ///     the animation is not playing yet.
        ///     <para>
        ///         <b>Two things on this screen say "frame" and only one of them is this one.</b> Searching for the
        ///         word found the frame <i>rate</i> first - <c>FrameCounter.Describe()</c> writes
        ///         "31.2 fps | 1.21 ms/frame" the moment it has a sample to report, and it sits to the left of the
        ///         frame number on the same line - so the next character was a space rather than a digit and the
        ///         answer came back as -1. It reads as the animation having stopped dead, and it only happens once
        ///         the counter has something to say: fast machine, no problem for a whole second, then no problem
        ///         ever again on a slow one. The first macOS runner this suite ever ran on found it.
        ///     </para>
        ///     <para>
        ///         The pattern also tells the playing readout from the loading one, which counts up
        ///         "frame 7 / 91" with spaces as it renders each frame up front. Matching that was the other half
        ///         of the same bug: a test could start measuring playback before playback had begun.
        ///     </para>
        /// </summary>
        /// <param name="game">The running demo.</param>
        /// <returns>The one-based frame number the screen reports, or -1 when it is not playing.</returns>
        private static int FrameNumber(DrivenDemoApp game)
        {
            var match = Regex.Match(game.Screen, @"frame (\d+)/(\d+)");

            return match.Success
                ? int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)
                : -1;
        }
    }
}
