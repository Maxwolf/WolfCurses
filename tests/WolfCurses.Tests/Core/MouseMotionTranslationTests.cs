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
        public void AWheelNotchIsStillRefusedOutright()
        {
            // A wheel record arrives with a button bit set and would read as a click. That was true before motion
            // existed and is the reason MouseButtonEnum has no wheel member; adding kinds must not reopen it.
            Assert.False(Translate(Left, 0, Wheeled, out _));
            Assert.False(Translate(0, 0, Wheeled | Moved, out _));
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
