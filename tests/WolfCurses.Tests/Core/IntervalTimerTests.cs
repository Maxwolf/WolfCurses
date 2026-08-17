using System;
using Xunit;

namespace WolfCurses.Tests.Core
{
    /// <summary>
    ///     IntervalTimer paces anything that happens on real elapsed time. Every test here drives the internal
    ///     <see cref="Func{TimeSpan}" /> clock seam rather than sleeping, so the suite stays deterministic and fast —
    ///     a timer test that waits is a timer test that is flaky on a loaded build agent.
    /// </summary>
    public class IntervalTimerTests
    {
        /// <summary>A clock the test winds forward by hand.</summary>
        private sealed class FakeClock
        {
            public TimeSpan Now { get; private set; }

            public TimeSpan Read() => Now;

            public void Advance(int milliseconds) => Now += TimeSpan.FromMilliseconds(milliseconds);
        }

        [Fact]
        public void TryConsume_IsFalseUntilTheIntervalHasPassed()
        {
            var clock = new FakeClock();
            var timer = new IntervalTimer(TimeSpan.FromMilliseconds(33), clock.Read);

            Assert.False(timer.TryConsume());

            clock.Advance(32);
            Assert.False(timer.TryConsume());

            clock.Advance(1);
            Assert.True(timer.TryConsume());
        }

        [Fact]
        public void TryConsume_DropsTheOvershootInsteadOfBankingIt()
        {
            // The load-bearing test, and the type's entire opinion. A period that ran long starts the next one from
            // now, so the time it overran by is gone. Subtract-the-interval semantics would answer true twice more
            // here, which in a game is a sprite teleporting or a piece falling three rows for one gravity tick.
            var clock = new FakeClock();
            var timer = new IntervalTimer(TimeSpan.FromMilliseconds(33), clock.Read);

            clock.Advance(100);
            Assert.True(timer.TryConsume());

            Assert.False(timer.TryConsume());
            clock.Advance(32);
            Assert.False(timer.TryConsume());
        }

        [Fact]
        public void LastElapsed_IsTheWholePeriodIncludingOvershoot()
        {
            // What a caller moving something by a delta needs, and what used to have to be snapshotted into a local
            // before the restart could eat it.
            var clock = new FakeClock();
            var timer = new IntervalTimer(TimeSpan.FromMilliseconds(33), clock.Read);

            clock.Advance(50);
            Assert.True(timer.TryConsume());
            Assert.Equal(TimeSpan.FromMilliseconds(50), timer.LastElapsed);
        }

        [Fact]
        public void TotalElapsed_IsNeverResetByConsumingOrRestarting()
        {
            // The phase clock an animation is drawn against. Restarting it would make the animation jump, so nothing
            // resets it - which is what lets one timer be both the pacer and the phase source.
            var clock = new FakeClock();
            var timer = new IntervalTimer(TimeSpan.FromMilliseconds(10), clock.Read);

            clock.Advance(25);
            Assert.True(timer.TryConsume());
            timer.Restart();
            clock.Advance(5);

            Assert.Equal(TimeSpan.FromMilliseconds(30), timer.TotalElapsed);
            Assert.Equal(TimeSpan.FromMilliseconds(5), timer.Elapsed);
        }

        [Fact]
        public void TryConsume_WithAnExplicitInterval_IgnoresTheProperty()
        {
            // How a pace that is recomputed every step is expressed: gravity that speeds up with the level, or an
            // animation whose every frame declares its own delay.
            var clock = new FakeClock();
            var timer = new IntervalTimer(TimeSpan.FromSeconds(10), clock.Read);

            clock.Advance(40);
            Assert.True(timer.TryConsume(TimeSpan.FromMilliseconds(33)));
        }

        [Fact]
        public void IsDue_AnswersWithoutTakingThePeriod()
        {
            // Why the mutating one is not called Due(): a predicate-shaped name is what gets written inside a render
            // method, which the scene graph calls about a thousand times a second. This is the one safe to ask there.
            var clock = new FakeClock();
            var timer = new IntervalTimer(TimeSpan.FromMilliseconds(33), clock.Read);

            clock.Advance(40);
            Assert.True(timer.IsDue);
            Assert.True(timer.IsDue);
            Assert.Equal(TimeSpan.FromMilliseconds(40), timer.Elapsed);

            Assert.True(timer.TryConsume());
            Assert.False(timer.IsDue);
        }

        [Fact]
        public void Restart_StartsThePeriodOverWithoutTakingIt()
        {
            var clock = new FakeClock();
            var timer = new IntervalTimer(TimeSpan.FromMilliseconds(33), clock.Read);

            clock.Advance(50);
            Assert.True(timer.TryConsume());
            var lastElapsed = timer.LastElapsed;

            clock.Advance(30);
            timer.Restart();
            Assert.Equal(TimeSpan.Zero, timer.Elapsed);

            // Restart does not close a period, so the measurement of the last one that did close survives it.
            Assert.Equal(lastElapsed, timer.LastElapsed);

            clock.Advance(32);
            Assert.False(timer.TryConsume());
        }

        [Fact]
        public void Interval_IsSettableForAPaceThatChangesInOnePlace()
        {
            var clock = new FakeClock();
            var timer = new IntervalTimer(TimeSpan.FromMilliseconds(100), clock.Read);

            clock.Advance(50);
            Assert.False(timer.TryConsume());

            timer.Interval = TimeSpan.FromMilliseconds(40);
            Assert.True(timer.TryConsume());
        }

        [Fact]
        public void ANegativeInterval_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new IntervalTimer(TimeSpan.FromMilliseconds(-1)));
        }

        [Fact]
        public void TheRealClockRuns_WithoutHavingToWaitForIt()
        {
            // One test that does not use the seam, so the production constructor's Stopwatch is exercised at all.
            // Deliberately asserts only what needs no sleeping: a fresh timer is not yet due, and an interval of zero
            // always is (which is exactly why the constructor requires one to be stated).
            Assert.False(new IntervalTimer(TimeSpan.FromMinutes(1)).IsDue);
            Assert.True(new IntervalTimer(TimeSpan.Zero).TryConsume());
        }
    }
}
