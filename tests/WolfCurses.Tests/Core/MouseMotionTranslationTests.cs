using WolfCurses.Core;
using Xunit;

namespace WolfCurses.Tests.Core
{
    /// <summary>
    ///     The half of mouse support that can be tested on a machine with no console: turning a Windows console
    ///     record into an event of the right kind. Whether the terminal reports a click at all, whether QuickEdit ate
    ///     it, and whether ConPTY forwards it are the other half and need a human.
    ///     <para>
    ///         The three kinds come out of two bit sets and one flag, and the interesting cases are the ones where
    ///         those disagree: a move that carries a held button is a drag, a move that carries none is a hover, and
    ///         a record where nothing at all changed is not an event.
    ///     </para>
    /// </summary>
    public class MouseMotionTranslationTests
    {
        private const uint Moved = 0x0001;
        private const uint Wheeled = 0x0004;
        private const uint Left = 0x0001;
        private const uint Right = 0x0002;

        private static bool Translate(uint now, uint before, uint flags, out MouseEvent mouse)
        {
            return WindowsConsoleInput.TryTranslateMouse(10, 5, 0, now, before, 0, flags, out mouse);
        }

        [Fact]
        public void AButtonGoingDownIsAPress()
        {
            Assert.True(Translate(Left, 0, 0, out var mouse));

            Assert.Equal(MouseEventKindEnum.Press, mouse.Kind);
            Assert.Equal(MouseButtonEnum.Left, mouse.Button);
            Assert.Equal(10, mouse.Column);
            Assert.Equal(5, mouse.Row);
        }

        [Fact]
        public void AButtonComingBackUpIsARelease()
        {
            // The event the library never used to report, and the one a drag has to have or it never ends.
            Assert.True(Translate(0, Left, 0, out var mouse));

            Assert.Equal(MouseEventKindEnum.Release, mouse.Kind);
            Assert.Equal(MouseButtonEnum.Left, mouse.Button);
        }

        [Fact]
        public void MovingWithNothingHeldIsAHover()
        {
            Assert.True(Translate(0, 0, Moved, out var mouse));

            Assert.Equal(MouseEventKindEnum.Move, mouse.Kind);
            Assert.Equal(MouseButtonEnum.None, mouse.Button);
        }

        [Fact]
        public void MovingWithAButtonHeldIsADragAndSaysWhichButton()
        {
            // This is the whole reason a move carries a button at all: without it a drag and a hover are the same
            // event and nothing downstream can tell a sweep from a pointer wandering across the screen.
            Assert.True(Translate(Left, Left, Moved, out var mouse));

            Assert.Equal(MouseEventKindEnum.Move, mouse.Kind);
            Assert.Equal(MouseButtonEnum.Left, mouse.Button);
        }

        [Fact]
        public void ARecordWhereNothingChangedAndNothingMovedIsNotAnEvent()
        {
            // Reported as a move it would make a screen that draws a pointer redraw on every stray record.
            Assert.False(Translate(Left, Left, 0, out _));
            Assert.False(Translate(0, 0, 0, out _));
        }

        [Fact]
        public void AWheelNotchIsItsOwnKindAndNeverAButton()
        {
            // The hazard that kept the wheel out of this library for so long: a wheel record arrives with a button
            // bit set in the low word, so anything treating it as a press fires on a scroll. Reported as its own
            // kind with no button, nothing that handles a press can be reached by one.
            Assert.True(Translate(Notches(1), 0, Wheeled, out var mouse));

            Assert.Equal(MouseEventKindEnum.Wheel, mouse.Kind);
            Assert.Equal(MouseButtonEnum.None, mouse.Button);
            Assert.Equal(1, mouse.WheelDelta);
        }

        [Fact]
        public void TheWheelReportsWhichWayItTurned()
        {
            Assert.True(Translate(Notches(-1), 0, Wheeled, out var down));
            Assert.Equal(-1, down.WheelDelta);

            Assert.True(Translate(Notches(3), 0, Wheeled, out var fast));
            Assert.Equal(3, fast.WheelDelta);
        }

        [Fact]
        public void TheWheelIsCountedInNotchesRatherThanRawUnits()
        {
            // Windows counts in 120ths of a notch. Dividing here rather than in every caller is the difference
            // between one constant and the same constant repeated everywhere.
            Assert.True(Translate(Notches(1), 0, Wheeled, out var mouse));

            Assert.Equal(1, mouse.WheelDelta);
            Assert.NotEqual(120, mouse.WheelDelta);
        }

        [Fact]
        public void AWheelRecordCarryingNoNotchesIsNotAnEvent()
        {
            Assert.False(Translate(0, 0, Wheeled, out _));
        }

        [Fact]
        public void TheSidewaysWheelIsStillRefused()
        {
            // Reported in the same field as the vertical one, a horizontal flick would scroll a document up and
            // down, which is worse than it doing nothing at all.
            Assert.False(Translate(Notches(1), 0, 0x0008, out _));
        }

        /// <summary>A wheel record's notch count, which rides in the high word of the button field.</summary>
        private static uint Notches(int count)
        {
            return unchecked((uint) (count * 120 << 16));
        }

        [Fact]
        public void OnlyOneButtonIsReportedWhenSeveralChangeAtOnce()
        {
            Assert.True(Translate(Left | Right, 0, 0, out var mouse));

            Assert.Equal(MouseButtonEnum.Left, mouse.Button);
        }

        [Fact]
        public void ReleasingOneOfTwoHeldButtonsReportsTheOneThatWentUp()
        {
            Assert.True(Translate(Left, Left | Right, 0, out var mouse));

            Assert.Equal(MouseEventKindEnum.Release, mouse.Kind);
            Assert.Equal(MouseButtonEnum.Right, mouse.Button);
        }

        [Fact]
        public void TheRowIsStillWindowRelativeForEveryKind()
        {
            // Windows reports screen-buffer coordinates, so a scrolled console puts every event N rows off unless
            // WindowTop is subtracted. The kinds added here must not have found a way round that.
            Assert.True(WindowsConsoleInput.TryTranslateMouse(3, 40, 30, Left, 0, 0, 0, out var press));
            Assert.Equal(10, press.Row);

            Assert.True(WindowsConsoleInput.TryTranslateMouse(3, 40, 30, 0, Left, 0, 0, out var release));
            Assert.Equal(10, release.Row);

            Assert.True(WindowsConsoleInput.TryTranslateMouse(3, 40, 30, 0, 0, 0, Moved, out var move));
            Assert.Equal(10, move.Row);
        }

        [Fact]
        public void AnEventAboveTheWindowIsRefusedRatherThanReportedAtANegativeRow()
        {
            Assert.False(WindowsConsoleInput.TryTranslateMouse(3, 2, 30, Left, 0, 0, 0, out _));
        }

        [Fact]
        public void ThePressOnlyEntryPointStillAnswersForPressesAndNothingElse()
        {
            // Kept because the old behaviour is pinned elsewhere and because "was this a click" is still the
            // question most callers are asking. It delegates, so the two cannot disagree about coordinates.
            Assert.True(WindowsConsoleInput.TryTranslateMousePress(10, 5, 0, Left, 0, 0, 0, out var press));
            Assert.Equal(MouseEventKindEnum.Press, press.Kind);

            Assert.False(WindowsConsoleInput.TryTranslateMousePress(10, 5, 0, 0, Left, 0, 0, out _));
            Assert.False(WindowsConsoleInput.TryTranslateMousePress(10, 5, 0, 0, 0, 0, Moved, out _));
        }
    }
}
