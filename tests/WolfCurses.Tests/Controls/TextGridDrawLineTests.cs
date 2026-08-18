using System;
using System.Collections.Generic;
using WolfCurses.Graphics;
using WolfCurses.Window.Control;
using Xunit;

namespace WolfCurses.Tests.Controls
{
    /// <summary>
    ///     <see cref="TextGrid.DrawLine" />, the cell counterpart of <see cref="PixelBuffer.DrawLine(int,int,int,int,Rgba32)" />.
    /// </summary>
    public class TextGridDrawLineTests
    {
        [Fact]
        public void BothEndsAreDrawn()
        {
            var grid = new TextGrid(9, 5);

            grid.DrawLine(2, 1, 6, 3, '*');

            Assert.Equal('*', grid.GlyphAt(2, 1));
            Assert.Equal('*', grid.GlyphAt(6, 3));
        }

        [Fact]
        public void AHorizontalLineFillsEveryCellBetweenItsEnds()
        {
            var grid = new TextGrid(10, 3);

            grid.DrawLine(2, 1, 7, 1, '-');

            for (var x = 0; x < 10; x++)
                Assert.Equal(x is >= 2 and <= 7 ? '-' : ' ', grid.GlyphAt(x, 1));
        }

        [Fact]
        public void AVerticalLineFillsEveryCellBetweenItsEnds()
        {
            var grid = new TextGrid(3, 10);

            grid.DrawLine(1, 8, 1, 2, '|');

            for (var y = 0; y < 10; y++)
                Assert.Equal(y is >= 2 and <= 8 ? '|' : ' ', grid.GlyphAt(1, y));
        }

        [Fact]
        public void ALineWithNoLengthIsStillAMark()
        {
            // The far end of a scene, where a whole object has shrunk into a single cell, relies on this.
            var grid = new TextGrid(5, 5);

            grid.DrawLine(2, 2, 2, 2, '#');

            Assert.Equal('#', grid.GlyphAt(2, 2));
        }

        [Fact]
        public void ALineIsClippedToTheGridRatherThanRefusedByIt()
        {
            var grid = new TextGrid(6, 6);

            grid.DrawLine(-4, 3, 9, 3, '-');

            for (var x = 0; x < 6; x++)
                Assert.Equal('-', grid.GlyphAt(x, 3));
        }

        [Fact]
        public void ALineThatMissesTheGridEntirelyDrawsNothing()
        {
            var grid = new TextGrid(6, 6);

            grid.DrawLine(-40, -40, -10, -10, '#');
            grid.DrawLine(60, 0, 90, 5, '#');
            grid.DrawLine(0, 60, 5, 90, '#');

            foreach (var row in grid.Render().Replace("\r\n", "\n").Split('\n'))
                Assert.Equal(new string(' ', 6), row);
        }

        [Fact]
        public void ALineDrawnEitherWayRoundDrawsTheSameCells()
        {
            // Rounding away from zero is what buys this. A line that changed shape depending on which end it was
            // started from would make an object flicker as it crossed the middle of the screen.
            var forward = new TextGrid(21, 11);
            var backward = new TextGrid(21, 11);

            forward.DrawLine(1, 1, 19, 9, '#');
            backward.DrawLine(19, 9, 1, 1, '#');

            Assert.Equal(forward.Render(), backward.Render());
        }

        [Fact]
        public void TheVisiblePartOfALineIsTheSameWhetherOrNotItRunsOffTheGrid()
        {
            // THE property that makes clipping the loop range instead of each cell sound. The position at each step
            // has to be a pure function of the step index, recomputed from the original endpoints - an incremental
            // error accumulator would start from wherever the loop was entered and draw a different line, so a shape
            // would change as it crossed an edge. Compared against a reference walk that starts from the true end.
            var grid = new TextGrid(40, 20);
            grid.DrawLine(-300, -140, 340, 180, '#');

            foreach (var (x, y) in ReferenceWalk(-300, -140, 340, 180))
            {
                if (x >= 0 && x < 40 && y >= 0 && y < 20)
                    Assert.Equal('#', grid.GlyphAt(x, y));
            }
        }

        [Fact]
        public void EnormousCoordinatesCostTheGridAndNotTheDistance()
        {
            // A vertex a hair in front of the near plane projects a very long way off the side of the screen, so this
            // is the ordinary case for anything drawing a projected scene rather than an exotic one.
            var grid = new TextGrid(80, 24);
            var clock = System.Diagnostics.Stopwatch.StartNew();

            for (var i = 0; i < 1000; i++)
                grid.DrawLine(-1_000_000, 12, 1_000_000, 13, '-');

            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(2), $"a thousand lines took {clock.Elapsed}");

            // Drawn, and drawn in the right place - a loop that ran the whole two million steps would also pass the
            // clock assertion on a fast enough machine, so the cells matter as much as the time.
            Assert.True(grid.GlyphAt(0, 12) == '-' || grid.GlyphAt(0, 13) == '-', "nothing landed in the first column");
            Assert.True(grid.GlyphAt(79, 12) == '-' || grid.GlyphAt(79, 13) == '-', "nothing landed in the last column");
        }

        [Fact]
        public void AnUnstyledLineEmitsNoEscapesAtAll()
        {
            // The byte-identity invariant the whole widget-colour feature rests on: nobody coloured this, so nothing
            // is emitted - not even a reset.
            var grid = new TextGrid(12, 4);

            grid.DrawLine(0, 0, 11, 3, '#');

            Assert.DoesNotContain('', grid.Render());
        }

        [Fact]
        public void AStyledLineIsColoured()
        {
            var grid = new TextGrid(12, 4) {ColorMode = AnsiColorModeEnum.Palette256};

            grid.DrawLine(0, 2, 11, 2, '-', new TextStyle(ConsoleColor.Green));

            Assert.Contains('', grid.Render());
            Assert.Equal(new TextStyle(ConsoleColor.Green), grid.StyleAt(5, 2));
        }

        [Fact]
        public void TheGridStaysARectangleWhateverIsDrawnOnIt()
        {
            var grid = new TextGrid(15, 6) {CellWidth = 2};

            grid.DrawLine(-50, -50, 60, 60, '#');

            foreach (var row in grid.Render().Replace("\r\n", "\n").Split('\n'))
                Assert.Equal(30, AnsiText.VisibleLength(row));
        }

        /// <summary>
        ///     Walks a line the slow, obvious way: every step from the true start, with no clipping anywhere.
        ///     <para>
        ///         It uses the same per-step arithmetic on purpose. What is being compared is not the rounding — that
        ///         would just be the implementation checking itself — but whether entering the loop part of the way
        ///         along changes the answer, which is the one thing clipping a loop range can get wrong.
        ///     </para>
        /// </summary>
        /// <param name="x0">Start column.</param>
        /// <param name="y0">Start row.</param>
        /// <param name="x1">End column.</param>
        /// <param name="y1">End row.</param>
        /// <returns>Every cell the line passes through.</returns>
        private static IEnumerable<(int X, int Y)> ReferenceWalk(int x0, int y0, int x1, int y1)
        {
            var dx = x1 - x0;
            var dy = y1 - y0;
            var steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
            var xMajor = Math.Abs(dx) >= Math.Abs(dy);

            for (var step = 0; step <= steps; step++)
            {
                var minor = (int) Math.Round((double) (xMajor ? dy : dx)*step/steps, MidpointRounding.AwayFromZero);
                var x = xMajor ? x0 + (dx > 0 ? step : -step) : x0 + minor;
                var y = xMajor ? y0 + minor : y0 + (dy > 0 ? step : -step);
                yield return (x, y);
            }
        }
    }
}
