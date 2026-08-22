using System;
using Xunit;

namespace WolfCurses.Tests
{
    /// <summary>
    ///     The media clock.
    ///     <para>
    ///         Driven by a hand-wound clock, so nothing here plays or waits for anything. The tests that earn the
    ///         type its place are the ones about the state a position has and a stopwatch has not: that a pause
    ///         keeps its place, that a seek does not start or stop playback, and that time owed while behind is
    ///         still owed - the opposite of what <see cref="IntervalTimer" /> does on purpose.
    ///     </para>
    /// </summary>
    public class PlaybackClockTests
    {
        /// <summary>A clock the test winds by hand, and the playback clock reading it.</summary>
        private static (PlaybackClock Clock, Action<double> Wind) Wound()
        {
            var now = TimeSpan.Zero;
            var clock = new PlaybackClock(() => now);

            return (clock, seconds => now += TimeSpan.FromSeconds(seconds));
        }

        [Fact]
        public void ANewClockIsStoppedAtTheBeginning()
        {
            var (clock, wind) = Wound();

            Assert.False(clock.IsRunning);
            Assert.Equal(TimeSpan.Zero, clock.Position);

            // And time passing does nothing at all until it is started.
            wind(10d);
            Assert.Equal(TimeSpan.Zero, clock.Position);
        }

        [Fact]
        public void PositionFollowsRealTimeWhileItRuns()
        {
            var (clock, wind) = Wound();

            clock.Start();
            wind(2.5d);

            Assert.Equal(TimeSpan.FromSeconds(2.5d), clock.Position);
        }

        [Fact]
        public void PositionMutatesNothing()
        {
            var (clock, wind) = Wound();

            clock.Start();
            wind(3d);

            // Asked twice with no time passing between, it must answer the same twice: the render method that
            // calls this runs about a thousand times a second.
            var first = clock.Position;
            Assert.Equal(first, clock.Position);
            Assert.Equal(first, clock.Position);
        }

        [Fact]
        public void PausingKeepsItsPlaceAndTimePassingDoesNotMoveIt()
        {
            var (clock, wind) = Wound();

            clock.Start();
            wind(4d);
            clock.Pause();

            wind(100d);

            Assert.False(clock.IsRunning);
            Assert.Equal(TimeSpan.FromSeconds(4d), clock.Position);

            clock.Resume();
            wind(1d);

            // The hundred seconds it was paused for are not owed. That is the one thing a pause has to get right.
            Assert.Equal(TimeSpan.FromSeconds(5d), clock.Position);
        }

        [Fact]
        public void PausingTwiceDoesNotBankTheTimeTwice()
        {
            var (clock, wind) = Wound();

            clock.Start();
            wind(4d);

            clock.Pause();
            clock.Pause();
            clock.Pause();

            Assert.Equal(TimeSpan.FromSeconds(4d), clock.Position);
        }

        [Fact]
        public void ResumingWhileAlreadyRunningChangesNothing()
        {
            var (clock, wind) = Wound();

            clock.Start();
            wind(4d);
            clock.Resume();
            wind(1d);

            Assert.Equal(TimeSpan.FromSeconds(5d), clock.Position);
        }

        [Fact]
        public void TimeOwedWhileNobodyWasLookingIsStillOwed()
        {
            var (clock, wind) = Wound();

            clock.Start();

            // The whole difference from IntervalTimer, which drops a late period on purpose. A film does not slow
            // down because the machine was busy; it is further along than it was.
            wind(10d);

            Assert.Equal(TimeSpan.FromSeconds(10d), clock.Position);
            Assert.Equal(300L, clock.FrameAt(30d));
        }

        [Fact]
        public void SeekingDoesNotStartOrStopPlayback()
        {
            var (clock, wind) = Wound();

            clock.Duration = TimeSpan.FromMinutes(10d);

            // Paused stays paused, which is what makes dragging a scrub bar work at all.
            clock.SeekTo(TimeSpan.FromSeconds(30d));
            Assert.False(clock.IsRunning);
            Assert.Equal(TimeSpan.FromSeconds(30d), clock.Position);

            wind(5d);
            Assert.Equal(TimeSpan.FromSeconds(30d), clock.Position);

            // And playing stays playing, without stuttering to a stop because somebody touched the bar.
            clock.Resume();
            clock.SeekTo(TimeSpan.FromSeconds(60d));
            Assert.True(clock.IsRunning);

            wind(2d);
            Assert.Equal(TimeSpan.FromSeconds(62d), clock.Position);
        }

        [Fact]
        public void SeekingIsClampedIntoTheMedia()
        {
            var (clock, _) = Wound();

            clock.Duration = TimeSpan.FromSeconds(90d);

            clock.SeekTo(TimeSpan.FromSeconds(-30d));
            Assert.Equal(TimeSpan.Zero, clock.Position);

            clock.SeekTo(TimeSpan.FromSeconds(1000d));
            Assert.Equal(TimeSpan.FromSeconds(90d), clock.Position);
        }

        [Fact]
        public void SeekingByAnAmountGoesBothWays()
        {
            var (clock, _) = Wound();

            clock.Duration = TimeSpan.FromSeconds(90d);
            clock.SeekTo(TimeSpan.FromSeconds(40d));

            clock.Seek(TimeSpan.FromSeconds(10d));
            Assert.Equal(TimeSpan.FromSeconds(50d), clock.Position);

            clock.Seek(TimeSpan.FromSeconds(-20d));
            Assert.Equal(TimeSpan.FromSeconds(30d), clock.Position);
        }

        [Fact]
        public void PositionNeverRunsPastAKnownDuration()
        {
            var (clock, wind) = Wound();

            clock.Duration = TimeSpan.FromSeconds(5d);
            clock.Start();
            wind(60d);

            Assert.Equal(TimeSpan.FromSeconds(5d), clock.Position);
            Assert.True(clock.HasEnded);
            Assert.Equal(1d, clock.Progress);
        }

        [Fact]
        public void AnUnknownDurationIsARealStateRatherThanZero()
        {
            var (clock, wind) = Wound();

            clock.Start();
            wind(600d);

            // A stream has no end and no fraction of one. Answering anything else would put a progress bar at a
            // position that is a guess, which is worse than a bar that never moves.
            Assert.Equal(TimeSpan.FromSeconds(600d), clock.Position);
            Assert.False(clock.HasEnded);
            Assert.Equal(0d, clock.Progress);

            // And a seek is not clamped at a top that is not known to be there.
            clock.SeekTo(TimeSpan.FromHours(3d));
            Assert.Equal(TimeSpan.FromHours(3d), clock.Position);
        }

        [Fact]
        public void ProgressIsTheFractionOfAKnownDuration()
        {
            var (clock, wind) = Wound();

            clock.Duration = TimeSpan.FromSeconds(200d);
            clock.Start();
            wind(50d);

            Assert.Equal(0.25d, clock.Progress, 6);
        }

        [Fact]
        public void StoppingRewindsAndPausing()
        {
            var (clock, wind) = Wound();

            clock.Start();
            wind(30d);
            clock.Stop();

            Assert.False(clock.IsRunning);
            Assert.Equal(TimeSpan.Zero, clock.Position);

            wind(10d);
            Assert.Equal(TimeSpan.Zero, clock.Position);
        }

        [Fact]
        public void StartingAgainGoesBackToTheBeginning()
        {
            var (clock, wind) = Wound();

            clock.Start();
            wind(30d);
            clock.Start();

            Assert.True(clock.IsRunning);
            Assert.Equal(TimeSpan.Zero, clock.Position);
        }

        [Fact]
        public void FrameAtFloorsSoAFrameIsNeverShownBeforeItsOwnMoment()
        {
            var (clock, wind) = Wound();

            clock.Start();

            // Just short of frame one at thirty a second.
            wind(1d / 30d - 0.0001d);
            Assert.Equal(0L, clock.FrameAt(30d));

            wind(0.0002d);
            Assert.Equal(1L, clock.FrameAt(30d));
        }

        [Fact]
        public void FrameAtAndFrameTimeAreInverses()
        {
            var (clock, _) = Wound();

            foreach (var fps in new[] {23.976d, 24d, 25d, 29.97d, 30d, 60d})
            {
                for (var frame = 0L; frame < 200L; frame += 37L)
                {
                    clock.SeekTo(PlaybackClock.FrameTime(frame, fps));

                    Assert.Equal(frame, clock.FrameAt(fps));
                }
            }
        }

        [Fact]
        public void AFrameRateOfNothingAnswersZeroRatherThanDividing()
        {
            var (clock, wind) = Wound();

            clock.Start();
            wind(10d);

            Assert.Equal(0L, clock.FrameAt(0d));
            Assert.Equal(0L, clock.FrameAt(-30d));
            Assert.Equal(TimeSpan.Zero, PlaybackClock.FrameTime(100L, 0d));
            Assert.Equal(TimeSpan.Zero, PlaybackClock.FrameTime(-5L, 30d));
        }

        [Fact]
        public void CatchingUpDropsExactlyTheFramesThatAreLate()
        {
            var (clock, wind) = Wound();

            clock.Start();

            var shown = 0L;
            var drawn = 0;

            // The loop the type exists for. Ten seconds pass between two looks at a thirty-a-second stream, so the
            // frame on screen has to become 300 without three hundred separate draws.
            wind(10d);

            while (shown < clock.FrameAt(30d))
            {
                shown++;
                drawn++;
            }

            Assert.Equal(300L, shown);
            Assert.Equal(300, drawn);

            // And with no time passing, nothing further is due.
            Assert.Equal(shown, clock.FrameAt(30d));
        }
    }
}
