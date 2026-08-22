using System;
using WolfCurses.Graphics;
using WolfCurses.Window.Control;
using Xunit;

namespace WolfCurses.Tests.Controls
{
    /// <summary>
    ///     The bar down the side of anything bigger than its window. It is all arithmetic, and the arithmetic has two
    ///     classic off-by-ones in it: a thumb that rounds away to nothing on a long document, and a thumb that never
    ///     quite reaches the end of its track, which reads as the document having more in it than it does.
    /// </summary>
    public class ScrollBarTests
    {
        private static ScrollBar Bar(int length, int total, int visible, int position)
        {
            return new ScrollBar {Length = length, Total = total, Visible = visible, Position = position};
        }

        [Fact]
        public void TheBarIsExactlyAsLongAsItWasToldToBeWithArrowsAtItsEnds()
        {
            var cells = Bar(10, 100, 5, 0).Cells();

            Assert.Equal(10, cells.Length);
            Assert.Equal("↑", cells[0]);
            Assert.Equal("↓", cells[9]);
        }

        [Fact]
        public void AHorizontalBarPointsSideways()
        {
            var cells = new ScrollBar(horizontal: true) {Length = 6, Total = 50, Visible = 10}.Cells();

            Assert.Equal("←", cells[0]);
            Assert.Equal("→", cells[5]);
        }

        [Fact]
        public void AnUnstyledBarIsPlainTextWithNoEscapesAtAll()
        {
            // The same stance every control here takes: colour nothing and the output carries no escape sequences,
            // so a terminal that was told NO_COLOR gets exactly what it asked for.
            Assert.DoesNotContain('\x1b', Bar(12, 200, 10, 40).Render());
        }

        [Fact]
        public void AStyledBarPaintsItsThumbAndTrackDifferently()
        {
            var bar = Bar(12, 200, 10, 0);
            bar.ThumbStyle = new TextStyle(ConsoleColor.Black, ConsoleColor.Gray);
            bar.TrackStyle = new TextStyle(ConsoleColor.Gray, ConsoleColor.DarkBlue);
            bar.ColorMode = AnsiColorModeEnum.Palette256;

            Assert.Contains('\x1b', bar.Render());
        }

        [Fact]
        public void ALongDocumentStillHasAThumbYouCanSee()
        {
            // Ten cells of track showing ten lines of a hundred thousand rounds to zero unless it is floored at one,
            // and a scrollbar with no thumb in it looks broken rather than precise.
            var bar = Bar(12, 100_000, 10, 0);

            Assert.Equal(1, bar.ThumbLength);
        }

        [Fact]
        public void WhenEverythingFitsTheThumbFillsTheTrack()
        {
            var bar = Bar(12, 8, 20, 0);

            Assert.Equal(bar.TrackLength, bar.ThumbLength);
            Assert.Equal(0, bar.ThumbStart);
        }

        [Fact]
        public void TheThumbReachesTheEndOfTheTrackExactlyWhenTheLastItemIsOnScreen()
        {
            // Scaled against the furthest the window can start rather than against the item count. Getting this
            // wrong leaves a gap at the bottom that never closes however far you scroll.
            var bar = Bar(12, 100, 10, 90);

            Assert.Equal(bar.TrackLength - bar.ThumbLength, bar.ThumbStart);
        }

        [Fact]
        public void TheThumbIsAtTheTopWhenTheWindowIs()
        {
            Assert.Equal(0, Bar(12, 100, 10, 0).ThumbStart);
        }

        [Fact]
        public void TheThumbNeverLeavesItsTrackAtAnyPosition()
        {
            // Swept rather than sampled, because the failures here are at the ends and one cell wide.
            var bar = Bar(14, 250, 17, 0);

            for (var position = 0; position <= 250; position++)
            {
                bar.Position = position;

                Assert.InRange(bar.ThumbStart, 0, bar.TrackLength - bar.ThumbLength);
                Assert.InRange(bar.ThumbLength, 1, bar.TrackLength);
            }
        }

        [Fact]
        public void TheDrawnThumbIsWhereTheArithmeticSaysItIs()
        {
            // Read off the rendered cells rather than restated, so the drawing and the numbers cannot drift.
            var bar = Bar(12, 100, 10, 45);
            var cells = bar.Cells();

            var first = Array.IndexOf(cells, "█");
            var count = 0;
            foreach (var cell in cells)
            {
                if (cell == "█")
                    count++;
            }

            Assert.Equal(bar.ThumbStart + 1, first);
            Assert.Equal(bar.ThumbLength, count);
        }

        [Fact]
        public void PressingAnArrowStepsOneItem()
        {
            var bar = Bar(12, 100, 10, 40);

            Assert.Equal(39, bar.PositionForPress(0));
            Assert.Equal(41, bar.PositionForPress(11));
        }

        [Fact]
        public void PressingTheTrackJumpsAWindowful()
        {
            var bar = Bar(12, 100, 10, 40);

            Assert.Equal(30, bar.PositionForPress(1));
            Assert.Equal(50, bar.PositionForPress(10));
        }

        [Fact]
        public void PressingTheThumbItselfDoesNothing()
        {
            // Dragging it needs pointer motion, which this library does not report, so the honest answer to a press
            // on the thumb is that there is nothing to do rather than a jump the user did not ask for.
            var bar = Bar(12, 100, 10, 45);

            Assert.Equal(-1, bar.PositionForPress(bar.ThumbStart + 1));
        }

        [Fact]
        public void PressesNeverScrollPastEitherEnd()
        {
            var atTop = Bar(12, 100, 10, 0);
            Assert.Equal(0, atTop.PositionForPress(0));

            var atBottom = Bar(12, 100, 10, 90);
            Assert.Equal(90, atBottom.PositionForPress(11));
        }

        [Fact]
        public void APressOutsideTheBarIsNotTheBarsBusiness()
        {
            var bar = Bar(12, 100, 10, 40);

            Assert.Equal(-1, bar.PositionForPress(-1));
            Assert.Equal(-1, bar.PositionForPress(12));
        }

        [Fact]
        public void ABarWithNothingInItDoesNotThrow()
        {
            var bar = Bar(3, 0, 0, 0);

            Assert.Equal(3, bar.Cells().Length);
            Assert.Equal(-1, bar.PositionForPress(1));
        }

        [Fact]
        public void ACellAppendedToARowKeepsTheRowsWidthHonest()
        {
            // The bug this pins shipped and reached CI. A styled cell is a dozen characters wide and one COLUMN
            // wide, so appending Cells() output into a row as run text - whose length is its width by contract -
            // left the row believing it was eleven columns wider than it was.
            var bar = Bar(6, 100, 10, 0);
            bar.TrackStyle = new TextStyle(ConsoleColor.Gray);
            bar.ThumbStyle = new TextStyle(ConsoleColor.White);
            bar.ArrowStyle = new TextStyle(ConsoleColor.DarkGray);
            bar.ColorMode = AnsiColorModeEnum.Palette256;

            var row = new TextRow {ColorMode = AnsiColorModeEnum.Palette256}.Append("ab", TextStyle.None);
            bar.AppendCellTo(row, 3);

            Assert.Equal(3, row.Width);
            Assert.Equal(3, AnsiText.VisibleLength(row.Render()));
        }

        [Fact]
        public void SlicingARowThatHasABarInItNeverCutsAnEscapeInHalf()
        {
            // The failure mode in full: something drawing a panel over a row renders the columns either side of it,
            // the cut lands inside the scrollbar's escape sequence, and the row ends with a lone ESC. A bare ESC
            // swallows whatever follows, which in a composed frame is the newline - so rows run together and the
            // screen silently loses lines. Invisible without colour, which is why it passed on the machine it was
            // written on.
            var bar = Bar(6, 100, 10, 0);
            bar.TrackStyle = new TextStyle(ConsoleColor.Gray);
            bar.ThumbStyle = new TextStyle(ConsoleColor.White);
            bar.ArrowStyle = new TextStyle(ConsoleColor.DarkGray);
            bar.ColorMode = AnsiColorModeEnum.Palette256;

            var row = new TextRow {ColorMode = AnsiColorModeEnum.Palette256}.Append("abcd", TextStyle.None);
            bar.AppendCellTo(row, 2);

            // Every prefix of the row, which is what an overlay asks for. None of them may end mid-sequence, and a
            // dangling ESC is exactly what "ends mid-sequence" looks like.
            for (var columns = 0; columns <= row.Width; columns++)
            {
                var drawn = row.Render(0, columns);

                Assert.Equal(columns, AnsiText.VisibleLength(drawn));
                Assert.False(drawn.Length > 0 && drawn[drawn.Length - 1] == (char) 27,
                    "column " + columns + " ended on a bare ESC, which swallows whatever follows it");
            }
        }

        [Fact]
        public void AnAppendedCellDrawsTheSameGlyphTheStyledCellWouldHave()
        {
            // The two ways of asking must never disagree about what belongs in a cell, which is why they share one
            // private decision rather than each working it out.
            var bar = Bar(8, 100, 20, 40);
            var cells = bar.Cells();

            for (var i = 0; i < cells.Length; i++)
            {
                var row = new TextRow();
                bar.AppendCellTo(row, i);

                Assert.Equal(cells[i], row.Render());
            }
        }

    }
}
