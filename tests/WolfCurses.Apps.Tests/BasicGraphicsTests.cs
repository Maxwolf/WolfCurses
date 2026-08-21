using System;
using System.Linq;
using WolfCurses.Apps.Basic;
using WolfCurses.Apps.Tests.Support;
using Xunit;

namespace WolfCurses.Apps.Tests
{
    /// <summary>
    ///     The drawing statements, checked at two levels: that the interpreter passes the right numbers along, and
    ///     that a real screen puts the right pixels down when it gets them.
    /// </summary>
    public class BasicGraphicsTests
    {
        private static RecordingBasicHost Asked(string source)
        {
            return RecordingBasicHost.Run(source);
        }

        /// <summary>A screen with a program already drawn on it, so the pixels can be asked about.</summary>
        private static BasicScreen Drawn(string source)
        {
            var screen = new BasicScreen(80, 25);
            BasicProgram.Compile(source).Run(new BasicRuntime(screen, 1));

            return screen;
        }

        [Fact]
        public void ScreenSetsTheCoordinateSpaceTheProgramMeans()
        {
            // What PSET (319, 199) means depends entirely on which SCREEN was asked for, so the mode has to set the
            // size rather than the screen guessing at one.
            var screen = Drawn("SCREEN 13");
            Assert.True(screen.IsGraphics);
            Assert.Equal(320, screen.ScreenWidth);
            Assert.Equal(200, screen.ScreenHeight);

            var bigger = Drawn("SCREEN 9");
            Assert.Equal(640, bigger.ScreenWidth);
            Assert.Equal(350, bigger.ScreenHeight);
        }

        [Fact]
        public void ScreenZeroGoesBackToText()
        {
            Assert.False(Drawn("SCREEN 13\nSCREEN 0").IsGraphics);
        }

        [Fact]
        public void PsetPutsOnePixelDownInTheColourItWasGiven()
        {
            var screen = Drawn("SCREEN 13\nPSET (10, 20), 4");

            Assert.Equal(4, screen.ColorAt(10, 20));
            Assert.Equal(0, screen.ColorAt(11, 20));
        }

        [Fact]
        public void AStatementWithNoColourUsesWhateverColorLastSet()
        {
            // Passing zero instead would silently draw everything in black, which is why a missing colour travels
            // as -1 and the screen decides.
            var screen = Drawn("SCREEN 13\nCOLOR 10\nPSET (5, 5)");

            Assert.Equal(10, screen.ColorAt(5, 5));
        }

        [Fact]
        public void LineDrawsBetweenTheTwoPointsItWasGiven()
        {
            var screen = Drawn("SCREEN 13\nLINE (0, 0)-(10, 0), 15");

            Assert.Equal(15, screen.ColorAt(0, 0));
            Assert.Equal(15, screen.ColorAt(5, 0));
            Assert.Equal(15, screen.ColorAt(10, 0));
            Assert.Equal(0, screen.ColorAt(11, 0));
        }

        [Fact]
        public void BDrawsTheOutlineOfABoxAndBfFillsIt()
        {
            var outline = Drawn("SCREEN 13\nLINE (2, 2)-(8, 6), 12, B");

            Assert.Equal(12, outline.ColorAt(2, 2));
            Assert.Equal(12, outline.ColorAt(8, 6));
            Assert.Equal(12, outline.ColorAt(5, 2));

            // The middle is untouched, which is the whole difference between B and BF.
            Assert.Equal(0, outline.ColorAt(5, 4));

            var filled = Drawn("SCREEN 13\nLINE (2, 2)-(8, 6), 12, BF");
            Assert.Equal(12, filled.ColorAt(5, 4));
            Assert.Equal(12, filled.ColorAt(2, 2));
            Assert.Equal(12, filled.ColorAt(8, 6));
        }

        [Fact]
        public void ALineWithNoFirstPointCarriesOnFromTheLastOne()
        {
            // How a program draws a path without repeating a coordinate on every line.
            var screen = Drawn("SCREEN 13\nPSET (0, 0), 1\nLINE -(10, 0), 14");

            Assert.Equal(14, screen.ColorAt(5, 0));
            Assert.Equal(14, screen.ColorAt(10, 0));
        }

        [Fact]
        public void CircleDrawsAnOutlineRatherThanADisc()
        {
            var screen = Drawn("SCREEN 13\nCIRCLE (50, 50), 20, 11");

            // The four points on the axes are on it.
            Assert.Equal(11, screen.ColorAt(70, 50));
            Assert.Equal(11, screen.ColorAt(30, 50));
            Assert.Equal(11, screen.ColorAt(50, 70));
            Assert.Equal(11, screen.ColorAt(50, 30));

            // And the middle is not, which is what makes it CIRCLE and not a filled disc.
            Assert.Equal(0, screen.ColorAt(50, 50));
        }

        [Fact]
        public void PaintFloodsAnAreaAndStopsAtItsBorder()
        {
            const string program = "SCREEN 13\nLINE (10, 10)-(30, 25), 15, B\nPAINT (20, 17), 2, 15";

            var screen = Drawn(program);

            Assert.Equal(2, screen.ColorAt(20, 17));
            Assert.Equal(2, screen.ColorAt(11, 11));
            Assert.Equal(15, screen.ColorAt(10, 10));

            // Outside the box is untouched, which is the half that says the border really stopped it.
            Assert.Equal(0, screen.ColorAt(5, 5));
            Assert.Equal(0, screen.ColorAt(40, 30));
        }

        [Fact]
        public void PaintOnAnAlreadyFilledAreaStopsRatherThanRunningForever()
        {
            // A border left out means "stop at the fill colour", which is also what makes the flood terminate once
            // an area is done.
            var screen = Drawn("SCREEN 13\nLINE (0, 0)-(20, 20), 3, BF\nPAINT (10, 10), 3");

            Assert.Equal(3, screen.ColorAt(10, 10));
        }

        [Fact]
        public void DrawingOffTheEdgeIsClippedRatherThanRefused()
        {
            // What the machines did: the part that is off the screen is lost and the program carries on.
            var screen = Drawn("SCREEN 13\nPSET (-5, -5), 4\nPSET (999, 999), 4\nLINE (-20, 10)-(999, 10), 9");

            Assert.Equal(9, screen.ColorAt(0, 10));
            Assert.Equal(9, screen.ColorAt(319, 10));
        }

        [Fact]
        public void ClearingWipesThePictureAsWellAsTheText()
        {
            var screen = Drawn("SCREEN 13\nLINE (0, 0)-(50, 50), 15, BF\nCLS");

            Assert.Equal(0, screen.ColorAt(25, 25));
        }

        [Fact]
        public void TheInterpreterPassesTheNumbersAlongUnchanged()
        {
            var host = Asked("SCREEN 9\nPSET (1, 2), 3\nCIRCLE (4, 5), 6, 7\nPAINT (8, 9), 10, 11");

            Assert.Equal(9, host.ScreenMode);
            Assert.Contains("PSET 1,2,3", host.Drawing, StringComparer.Ordinal);
            Assert.Contains("CIRCLE 4,5,6,7", host.Drawing, StringComparer.Ordinal);
            Assert.Contains("PAINT 8,9,10,11", host.Drawing, StringComparer.Ordinal);
        }

        [Fact]
        public void CoordinatesMayBeWorkedOutRatherThanWrittenDown()
        {
            var host = Asked("X = 10\nPSET (X * 2, X + 1), 5");

            Assert.Contains("PSET 20,11,5", host.Drawing, StringComparer.Ordinal);
        }

        [Fact]
        public void ACoordinateIsTruncatedRatherThanRounded()
        {
            // Which is what BASIC does, and is visible on anything that steps by a fraction: rounding would make a
            // slow diagonal wobble between two rows.
            var host = Asked("PSET (1.9, 2.9), 1");

            Assert.Contains("PSET 1,2,1", host.Drawing, StringComparer.Ordinal);
        }

        [Fact]
        public void APictureIsRenderedRatherThanTheTextGridWhileInAGraphicsMode()
        {
            var screen = Drawn("SCREEN 13\nLINE (0, 0)-(319, 199), 15, BF");

            Assert.NotEqual(new string(' ', 80), screen.Render(40, 10).Split('\n')[0]);
        }

        [Fact]
        public void AScreenModeNobodyHasIsRefused()
        {
            // Asked of a real screen rather than the recording one, which accepts any mode it is handed: the
            // refusal is the screen's own judgement and there is nothing to test in a host that just writes it down.
            var error = Assert.Throws<BasicError>(() => Drawn("SCREEN 99"));

            Assert.Contains("Unsupported screen mode", error.Reason, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("SCREEN 13\nLINE (1,1)(2,2)", "Expected -")]
        [InlineData("SCREEN 13\nPSET 1, 2", "Expected a coordinate in brackets")]
        [InlineData("SCREEN 13\nCIRCLE (1, 1)", "Expected a radius")]
        public void MistakesAreReportedRatherThanIgnored(string program, string expected)
        {
            var error = RecordingBasicHost.Fails(program);

            Assert.Contains(expected, error.Reason, StringComparison.Ordinal);
        }
    }
}
