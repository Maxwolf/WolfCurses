using System;
using Xunit;

namespace WolfCurses.Tests.Core
{
    /// <summary>
    ///     What a <see cref="MouseEvent" /> is worth as a value, which stopped being obvious the moment it grew
    ///     kinds.
    ///     <para>
    ///         <b>Equality once compared only the press fields</b>, so a release was equal to the press that
    ///         preceded it and a wheel notch was equal to the same notch turned the other way. Nothing inside the
    ///         library compares two of these - the queue keeps every event rather than deduplicating, which is
    ///         deliberate and documented - so the fault could sit there indefinitely without a single test going
    ///         red. It is a caller writing "only redraw when something changed" who would have found it, in the
    ///         form of a screen that would not update.
    ///     </para>
    /// </summary>
    public class MouseEventValueTests
    {
        [Fact]
        public void APressAndAReleaseAtTheSameCellAreNotTheSameEvent()
        {
            var press = new MouseEvent(10, 5, MouseButtonEnum.Left);
            var release = new MouseEvent(10, 5, MouseButtonEnum.Left, kind: MouseEventKindEnum.Release);

            Assert.NotEqual(press, release);
            Assert.True(press != release);
        }

        [Fact]
        public void AHoverAndAPressAtTheSameCellAreNotTheSameEvent()
        {
            // The pair a "did anything change?" gate compares most often, since a hover arrives for every cell the
            // pointer crosses and a press arrives on top of one of them.
            var hover = new MouseEvent(3, 4, MouseButtonEnum.None, kind: MouseEventKindEnum.Move);
            var press = new MouseEvent(3, 4, MouseButtonEnum.None);

            Assert.NotEqual(hover, press);
        }

        [Fact]
        public void TheWheelTurnedEachWayGivesTwoDifferentEvents()
        {
            // Both are MouseButtonEnum.None at the same cell and both are Wheel, so the delta is the only thing
            // telling them apart - which is exactly why it has to be compared.
            var up = new MouseEvent(1, 1, MouseButtonEnum.None, kind: MouseEventKindEnum.Wheel, wheelDelta: 1);
            var down = new MouseEvent(1, 1, MouseButtonEnum.None, kind: MouseEventKindEnum.Wheel, wheelDelta: -1);

            Assert.NotEqual(up, down);
        }

        [Fact]
        public void TwoNotchesIsNotOneNotch()
        {
            var one = new MouseEvent(1, 1, MouseButtonEnum.None, kind: MouseEventKindEnum.Wheel, wheelDelta: 1);
            var two = new MouseEvent(1, 1, MouseButtonEnum.None, kind: MouseEventKindEnum.Wheel, wheelDelta: 2);

            Assert.NotEqual(one, two);
        }

        [Fact]
        public void TheSameEventTwiceIsEqualAndHashesTheSame()
        {
            // The other half: making the comparison stricter must not make a genuine repeat look different, or a
            // gate built on it would redraw on every event instead of none of them.
            var first = new MouseEvent(7, 2, MouseButtonEnum.Right, ConsoleModifiers.Shift,
                MouseEventKindEnum.Move);
            var second = new MouseEvent(7, 2, MouseButtonEnum.Right, ConsoleModifiers.Shift,
                MouseEventKindEnum.Move);

            Assert.Equal(first, second);
            Assert.True(first == second);
            Assert.Equal(first.GetHashCode(), second.GetHashCode());
        }

        [Fact]
        public void EventsThatDifferOnlyByKindHashDifferently()
        {
            // Not required for correctness - unequal values may share a hash - but a type whose whole difference is
            // one field wants that field in the hash, or a dictionary of them degenerates to a linear scan.
            var press = new MouseEvent(9, 9, MouseButtonEnum.Left);
            var release = new MouseEvent(9, 9, MouseButtonEnum.Left, kind: MouseEventKindEnum.Release);

            Assert.NotEqual(press.GetHashCode(), release.GetHashCode());
        }

        [Fact]
        public void TheDescriptionSaysWhatHappenedAndNotOnlyWhichButton()
        {
            // This is a diagnostic string and it is read in exactly the situation where nothing else is available:
            // somebody printing events to work out why a terminal is not reporting what they expected. One that
            // cannot tell a press from a release is no help at all in that situation.
            Assert.Contains("Press", new MouseEvent(2, 3, MouseButtonEnum.Left).ToString(),
                StringComparison.Ordinal);
            Assert.Contains("Release",
                new MouseEvent(2, 3, MouseButtonEnum.Left, kind: MouseEventKindEnum.Release).ToString(),
                StringComparison.Ordinal);
        }

        [Fact]
        public void AWheelDescriptionSaysWhichWayAndHowFar()
        {
            // The wheel names its delta instead of its button, because its button is always None and the delta is
            // the whole content of the event.
            var text = new MouseEvent(2, 3, MouseButtonEnum.None, kind: MouseEventKindEnum.Wheel, wheelDelta: -2)
                .ToString();

            Assert.Contains("Wheel", text, StringComparison.Ordinal);
            Assert.Contains("-2", text, StringComparison.Ordinal);
            Assert.DoesNotContain("None", text, StringComparison.Ordinal);
        }
    }
}
