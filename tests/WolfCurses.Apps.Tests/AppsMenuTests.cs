using System;
using WolfCurses.Apps.Tests.Support;
using Xunit;

namespace WolfCurses.Apps.Tests
{
    /// <summary>
    ///     The suite as a person meets it: keys go in, frames come out, and the assertions are about what is on
    ///     screen. Nothing here reaches inside a form.
    ///     <para>
    ///         There are no applications yet, so what is pinned is the scaffolding every one of them will hang off:
    ///         the menu comes up on its own, the enum is what a test names a choice by, ESC is harmless with nothing
    ///         to back out of, and Quit really does close the simulation rather than merely saying so.
    ///     </para>
    /// </summary>
    [Collection("Suite")]
    public class AppsMenuTests
    {
        [Fact]
        public void TheMenuComesUpByItselfAndAsksWhichApplication()
        {
            using var suite = new DrivenSuite();

            Assert.Contains("WolfCurses Apps", suite.Screen, StringComparison.Ordinal);
            Assert.Contains("Small office applications", suite.Screen, StringComparison.Ordinal);

            // Set at window creation rather than only on the way back from an application, which is the difference
            // between this being here on the first frame and a driver waiting out its whole timeout for it.
            Assert.Contains("Which application?", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void EveryChoiceOnTheEnumIsOnTheMenu()
        {
            // Applications are added by putting a value on the enum and one AddCommand line beside it, so this is
            // the test that fails when somebody does one and forgets the other.
            using var suite = new DrivenSuite();

            foreach (AppsCommandsEnum choice in Enum.GetValues<AppsCommandsEnum>())
            {
                var number = ((int) choice).ToString(System.Globalization.CultureInfo.InvariantCulture);
                Assert.Contains($"{number}. ", suite.Screen, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void EscapeWithNothingOpenIsHarmless()
        {
            // ESC backs out of an application, and with none open it must do nothing at all rather than close the
            // suite or clear the menu. Asserted below the status line because the scene graph's spinner advances on
            // every tick, so the whole frame legitimately differs from one tick to the next.
            using var suite = new DrivenSuite();
            var before = suite.ScreenBelowStatusLine;

            suite.Escape();

            Assert.NotNull(AppsSimulationApp.Instance);
            Assert.Equal(before, suite.ScreenBelowStatusLine);
        }

        [Fact]
        public void ChoosingQuitClosesTheSimulation()
        {
            // The host loop watches Instance and exits when it goes null, so this is the whole of "the program
            // ended" as far as anything but the console can see.
            using var suite = new DrivenSuite();

            suite.ChooseMenuItem((int) AppsCommandsEnum.Quit);

            Assert.Null(AppsSimulationApp.Instance);
        }

        [Fact]
        public void AnUntouchedMenuCarriesNoEscapeSequences()
        {
            // The library's compatibility stance, visible from the application side: the arrow-key highlight is
            // hidden until an arrow key summons it, so a menu nobody has touched renders as plain text.
            using var suite = new DrivenSuite();

            Assert.DoesNotContain('\x1b', suite.RawScreen);
        }

        [Fact]
        public void AnArrowKeySummonsTheHighlightOntoTheFirstChoice()
        {
            using var suite = new DrivenSuite();

            suite.Press(ConsoleKey.DownArrow);

            Assert.Contains("> 1. ", suite.Screen, StringComparison.Ordinal);
        }
    }
}
