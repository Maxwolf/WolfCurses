using System;
using Xunit;

namespace WolfCurses.Tests
{
    /// <summary>
    ///     <see cref="HeldAxis" />, driven against a clock the test moves by hand so nothing ever sleeps.
    /// </summary>
    public class HeldAxisTests
    {
        private static readonly TimeSpan _release = TimeSpan.FromMilliseconds(180);

        [Fact]
        public void AFreshAxisIsNotHeld()
        {
            var axis = Build(out _);

            Assert.Equal(0, axis.Direction);
            Assert.False(axis.IsHeld);
            Assert.Equal(TimeSpan.Zero, axis.HeldFor);
        }

        [Fact]
        public void PressingSaysWhichWay()
        {
            var axis = Build(out _);

            axis.Press(-1);
            Assert.Equal(-1, axis.Direction);

            axis.Press(1);
            Assert.Equal(1, axis.Direction);
        }

        [Fact]
        public void AnyNegativeIsMinusOneAndAnyPositiveIsPlusOne()
        {
            var axis = Build(out _);

            axis.Press(-97);
            Assert.Equal(-1, axis.Direction);

            axis.Press(97);
            Assert.Equal(1, axis.Direction);
        }

        [Fact]
        public void SilenceLongerThanTheReleaseDelayIsTheKeyUpEventThatNeverArrives()
        {
            var axis = Build(out var clock);
            axis.Press(1);

            clock.Advance(TimeSpan.FromMilliseconds(179));
            Assert.Equal(1, axis.Direction);

            clock.Advance(TimeSpan.FromMilliseconds(2));
            Assert.Equal(0, axis.Direction);
            Assert.False(axis.IsHeld);
        }

        [Fact]
        public void RepeatsFromAHeldKeyKeepItHeldForever()
        {
            var axis = Build(out var clock);

            // A key repeating at about thirty a second, held for ten seconds.
            for (var i = 0; i < 300; i++)
            {
                axis.Press(1);
                clock.Advance(TimeSpan.FromMilliseconds(33));
                Assert.Equal(1, axis.Direction);
            }
        }

        [Fact]
        public void ReadingTheDirectionChangesNothing()
        {
            // It sits in render paths that run about a thousand times a second, so a property that quietly consumed
            // state would be the IntervalTimer.TryConsume trap all over again.
            var axis = Build(out var clock);
            axis.Press(1);

            for (var i = 0; i < 1000; i++)
                Assert.Equal(1, axis.Direction);

            clock.Advance(TimeSpan.FromMilliseconds(100));
            Assert.Equal(1, axis.Direction);
            Assert.Equal(TimeSpan.FromMilliseconds(100), axis.HeldFor);
        }

        [Fact]
        public void HeldForGrowsWhileTheAxisIsHeld()
        {
            var axis = Build(out var clock);
            axis.Press(1);

            clock.Advance(TimeSpan.FromMilliseconds(100));
            axis.Press(1);
            clock.Advance(TimeSpan.FromMilliseconds(100));

            // Two hundred milliseconds since it started moving, even though the last press was a hundred ago.
            Assert.Equal(TimeSpan.FromMilliseconds(200), axis.HeldFor);
        }

        [Fact]
        public void HeldForStartsAgainAfterTheAxisHasBeenLetGo()
        {
            // THE bug this type was extracted for. Written by hand, the "were we standing still?" test gets asked
            // after the new direction has been assigned - by which point something always is - so it never fires,
            // the start stamp stays at zero and a speed ramp built on it is pinned to full speed forever. Here that
            // shows up as the second press reporting a held time measured from the first one.
            var axis = Build(out var clock);

            axis.Press(1);
            clock.Advance(TimeSpan.FromSeconds(5));
            Assert.Equal(0, axis.Direction);

            axis.Press(1);
            clock.Advance(TimeSpan.FromMilliseconds(40));

            Assert.Equal(TimeSpan.FromMilliseconds(40), axis.HeldFor);
        }

        [Fact]
        public void ReversingDoesNotStartTheHeldTimeAgain()
        {
            // The axis never came to rest, so a player sweeping one way and then the other keeps the speed they had
            // built up rather than being dropped to a crawl in the middle of a movement.
            var axis = Build(out var clock);

            axis.Press(1);
            clock.Advance(TimeSpan.FromMilliseconds(100));
            axis.Press(-1);
            clock.Advance(TimeSpan.FromMilliseconds(50));

            Assert.Equal(-1, axis.Direction);
            Assert.Equal(TimeSpan.FromMilliseconds(150), axis.HeldFor);
        }

        [Fact]
        public void AnAxisThatIsNotHeldHasNoHeldTime()
        {
            var axis = Build(out var clock);
            axis.Press(1);
            clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(TimeSpan.Zero, axis.HeldFor);
        }

        [Fact]
        public void ReleasingLetsGoAtOnceRatherThanWaitingOutTheDelay()
        {
            var axis = Build(out _);
            axis.Press(1);

            axis.Release();

            Assert.Equal(0, axis.Direction);
            Assert.Equal(TimeSpan.Zero, axis.HeldFor);
        }

        [Fact]
        public void PressingZeroIsTheSameAsLettingGo()
        {
            var axis = Build(out _);
            axis.Press(1);

            axis.Press(0);

            Assert.Equal(0, axis.Direction);
        }

        [Fact]
        public void AModalDialogLeavesTheAxisCorrectlyReleased()
        {
            // The reason it keeps its own clock and nothing ever resets it: a form that stops ticking while a dialog
            // is up comes back having heard nothing for however long the dialog was, which is exactly what "let go"
            // looks like. Restarting the clock instead would resume in whatever direction was last held.
            var axis = Build(out var clock);
            axis.Press(1);

            clock.Advance(TimeSpan.FromSeconds(30));

            Assert.Equal(0, axis.Direction);
        }

        [Fact]
        public void ADelayThatIsNotPositiveIsRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HeldAxis(TimeSpan.Zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HeldAxis(TimeSpan.FromMilliseconds(-1)));
        }

        [Fact]
        public void TheDefaultDelayIsLongerThanAKeyRepeatAndShorterThanADeliberateTap()
        {
            // The number is a judgement call, but the two bounds around it are not: shorter than a key-repeat
            // interval and a held key stutters, longer than a deliberate double-tap and the controls feel sticky.
            Assert.InRange(HeldAxis.DefaultReleaseAfter, TimeSpan.FromMilliseconds(60), TimeSpan.FromMilliseconds(400));
            Assert.Equal(HeldAxis.DefaultReleaseAfter, new HeldAxis().ReleaseAfter);
        }

        private static HeldAxis Build(out FakeClock clock)
        {
            var fake = new FakeClock();
            clock = fake;
            return new HeldAxis(_release, () => fake.Now);
        }

        private sealed class FakeClock
        {
            public TimeSpan Now { get; private set; }

            public void Advance(TimeSpan by)
            {
                Now += by;
            }
        }
    }
}
