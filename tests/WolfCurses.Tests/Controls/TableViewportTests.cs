using WolfCurses.Window.Control;
using Xunit;

namespace WolfCurses.Tests.Controls
{
    /// <summary>
    ///     The scroll window onto a table.
    ///     <para>
    ///         Deliberately tested with columns of <i>different</i> widths throughout. Every one of these sums is
    ///         right for the uniform case by accident, so a fixture where every column is eight wide would pass
    ///         against an implementation that multiplied instead of adding.
    ///     </para>
    /// </summary>
    public class TableViewportTests
    {
        /// <summary>Six columns adding to 44, none the same as its neighbour.</summary>
        private static readonly int[] _widths = {6, 12, 4, 10, 3, 9};

        [Fact]
        public void ItCountsTheWholeColumnsThatFit()
        {
            var viewport = new TableViewport(20, 5);

            // Six and twelve fit in twenty; four would make twenty-two.
            Assert.Equal(2, viewport.VisibleColumns(_widths));
        }

        [Fact]
        public void APartlyFittingColumnIsNotCounted()
        {
            var viewport = new TableViewport(17, 5);

            // Eighteen would be needed for the second column, so only the first is drawn and five cells stay blank.
            Assert.Equal(1, viewport.VisibleColumns(_widths));
        }

        [Fact]
        public void AColumnWiderThanTheWindowIsStillDrawn()
        {
            var viewport = new TableViewport(4, 5);

            // Nothing fits, and answering zero would render an empty screen with no cell to click on to escape it.
            Assert.Equal(1, viewport.VisibleColumns(_widths));
        }

        [Fact]
        public void AHitTestAndTheDrawnOffsetAgreeForEveryVisibleColumn()
        {
            var viewport = new TableViewport(30, 5);
            viewport.ScrollTo(0, 1);

            // Read off the object rather than restating the arithmetic: whichever cell a column was drawn in must
            // be the cell that hit-tests back to it, and so must its last cell.
            for (var i = 0; i < viewport.VisibleColumns(_widths); i++)
            {
                var column = viewport.FirstColumn + i;
                var offset = viewport.ColumnOffset(column, _widths);

                Assert.True(offset >= 0);
                Assert.Equal(column, viewport.ColumnAt(offset, _widths));
                Assert.Equal(column, viewport.ColumnAt(offset + _widths[column] - 1, _widths));
            }
        }

        [Fact]
        public void TheGroundPastTheLastDrawnColumnBelongsToNobody()
        {
            var viewport = new TableViewport(20, 5);

            var last = viewport.FirstColumn + viewport.VisibleColumns(_widths) - 1;
            var past = viewport.ColumnOffset(last, _widths) + _widths[last];

            // Rounding this to the nearest column would move the cursor somewhere nobody pointed at.
            Assert.Equal(-1, viewport.ColumnAt(past, _widths));
            Assert.Equal(-1, viewport.ColumnAt(-1, _widths));
        }

        [Fact]
        public void AColumnThatIsNotDrawnHasNoOffset()
        {
            var viewport = new TableViewport(20, 5);

            Assert.Equal(0, viewport.ColumnOffset(0, _widths));
            Assert.Equal(-1, viewport.ColumnOffset(4, _widths));
        }

        [Fact]
        public void RevealingAColumnCanCostMoreThanOneColumnOfScrolling()
        {
            var viewport = new TableViewport(20, 5);

            Assert.True(viewport.EnsureVisible(0, 3, _widths));

            // Column three is ten wide, so showing it needs twenty of the window: giving up only column zero would
            // leave twelve plus four plus ten, which is twenty-six. This is the sum a subtraction gets wrong.
            Assert.Equal(2, viewport.FirstColumn);
            Assert.True(viewport.ColumnOffset(3, _widths) >= 0);
        }

        [Fact]
        public void RevealingSomethingAlreadyOnScreenMovesNothing()
        {
            var viewport = new TableViewport(20, 5);

            Assert.False(viewport.EnsureVisible(0, 1, _widths));
            Assert.Equal(0, viewport.FirstColumn);
            Assert.Equal(0, viewport.FirstRow);
        }

        [Fact]
        public void RevealingAColumnToTheLeftScrollsStraightToIt()
        {
            var viewport = new TableViewport(20, 5);
            viewport.ScrollTo(0, 4);

            Assert.True(viewport.EnsureVisible(0, 1, _widths));
            Assert.Equal(1, viewport.FirstColumn);
        }

        [Fact]
        public void RevealingAColumnWiderThanTheWindowLandsOnIt()
        {
            var viewport = new TableViewport(8, 5);

            Assert.True(viewport.EnsureVisible(0, 1, _widths));

            // Twelve will never fit in eight; parking on it is as close as there is, and the loop has to stop.
            Assert.Equal(1, viewport.FirstColumn);
        }

        [Fact]
        public void RevealingARowScrollsTheLeastItCan()
        {
            var viewport = new TableViewport(40, 5);

            Assert.True(viewport.EnsureVisible(7, 0, _widths));
            Assert.Equal(3, viewport.FirstRow);

            Assert.True(viewport.EnsureVisible(1, 0, _widths));
            Assert.Equal(1, viewport.FirstRow);
        }

        [Fact]
        public void TheFurthestRightIsTheLastScreenfulRatherThanTheLastColumn()
        {
            var viewport = new TableViewport(20, 5);
            viewport.ScrollTo(0, 99);
            viewport.ClampToTable(50, _widths);

            // Three, nine and ten come to twenty-two, so column four cannot be the origin; three, nine is twelve.
            // Stopping at the last column instead would show one nine-wide column beside eleven empty cells.
            Assert.Equal(4, viewport.FirstColumn);
            Assert.Equal(2, viewport.VisibleColumns(_widths));
        }

        [Fact]
        public void TheFurthestDownIsTheLastScreenfulRatherThanTheLastRow()
        {
            var viewport = new TableViewport(40, 5);
            viewport.ScrollTo(999, 0);
            viewport.ClampToTable(50, _widths);

            Assert.Equal(45, viewport.FirstRow);
        }

        [Fact]
        public void ATableSmallerThanTheWindowStaysAtTheTopLeft()
        {
            var viewport = new TableViewport(80, 20);
            viewport.ScrollTo(5, 5);
            viewport.ClampToTable(3, _widths);

            Assert.Equal(0, viewport.FirstRow);
            Assert.Equal(0, viewport.FirstColumn);
        }

        [Fact]
        public void ScrollingNeverGoesAboveOrLeftOfTheStart()
        {
            var viewport = new TableViewport(40, 5);
            viewport.ScrollBy(-4, -4);

            Assert.Equal(0, viewport.FirstRow);
            Assert.Equal(0, viewport.FirstColumn);
        }

        [Fact]
        public void ANoSizedWindowBecomesOneCell()
        {
            // A headless host really does report a console size of zero, and none of the arithmetic means anything
            // at that size.
            var viewport = new TableViewport(0, 0);

            Assert.Equal(1, viewport.Width);
            Assert.Equal(1, viewport.Rows);
        }

        [Fact]
        public void RowsTranslateBothWays()
        {
            var viewport = new TableViewport(40, 5);
            viewport.ScrollTo(10, 0);

            Assert.Equal(12, viewport.RowAt(2));
            Assert.Equal(15, viewport.LastRowExclusive);

            Assert.True(viewport.TryRowToScreen(12, out var screenRow));
            Assert.Equal(2, screenRow);

            // The false return is the useful half: a row scrolled away has no line to be drawn on.
            Assert.False(viewport.TryRowToScreen(9, out _));
            Assert.False(viewport.TryRowToScreen(15, out _));
        }

        [Fact]
        public void AColumnWithNoWidthStillOccupiesACell()
        {
            // Zero would make two columns share an offset, so a hit test could not tell them apart, and it would
            // let EnsureVisible give up ground forever without the target ever arriving.
            int[] widths = {0, 0, 0, 0};
            var viewport = new TableViewport(3, 5);

            Assert.Equal(3, viewport.VisibleColumns(widths));
            Assert.Equal(0, viewport.ColumnAt(0, widths));
            Assert.Equal(1, viewport.ColumnAt(1, widths));

            Assert.True(viewport.EnsureVisible(0, 3, widths));
            Assert.Equal(1, viewport.FirstColumn);
        }

        [Fact]
        public void ATableWithNoColumnsAnswersEmptily()
        {
            var viewport = new TableViewport(40, 5);
            int[] none = { };

            Assert.Equal(0, viewport.VisibleColumns(none));
            Assert.Equal(0, viewport.VisibleColumns(null));
            Assert.Equal(-1, viewport.ColumnAt(0, none));
            Assert.Equal(0, viewport.LastColumnOrigin(none));
        }
    }
}
