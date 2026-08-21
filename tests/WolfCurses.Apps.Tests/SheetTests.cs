using System.Linq;
using WolfCurses.Apps.Spreadsheet;
using Xunit;

namespace WolfCurses.Apps.Tests
{
    /// <summary>
    ///     The grid and the formulas, with no screen anywhere near them.
    ///     <para>
    ///         Assertions are absolute answers rather than "it produced a number", because almost every wrong
    ///         formula engine produces a number: the bugs in one are off-by-one ranges, precedence the wrong way
    ///         round, and empty cells counted when they should not be, all of which look like arithmetic working.
    ///     </para>
    /// </summary>
    public class SheetTests
    {
        /// <summary>A sheet with a small column of figures in B1 to B4 and a word above them.</summary>
        private static Sheet WithNumbers()
        {
            var sheet = new Sheet();

            sheet.SetText(0, 1, "Income");
            sheet.SetText(1, 1, "10");
            sheet.SetText(2, 1, "20");
            sheet.SetText(3, 1, "30");

            return sheet;
        }

        private static string Display(Sheet sheet, string cell)
        {
            Assert.True(CellAddress.TryParse(cell, out var address));
            return sheet.GetValue(address).Display();
        }

        private static string Evaluate(string formula)
        {
            var sheet = WithNumbers();
            sheet.SetText(9, 9, formula);

            return sheet.GetValue(9, 9).Display();
        }

        [Fact]
        public void ACellHoldsWhatWasTypedAndIsWorthSomethingElse()
        {
            var sheet = new Sheet();
            sheet.SetText(0, 0, "=1+1");

            // The whole distinction a spreadsheet is built on, and the reason saving gives back the formula.
            Assert.Equal("=1+1", sheet.GetText(0, 0));
            Assert.Equal("2", sheet.GetValue(0, 0).Display());
        }

        [Fact]
        public void NumbersAndTextAreToldApartByWhetherTheyParse()
        {
            var sheet = new Sheet();

            sheet.SetText(0, 0, "42");
            sheet.SetText(1, 0, "-3.5");
            sheet.SetText(2, 0, "Jan");
            sheet.SetText(3, 0, "2026-01-14");

            Assert.True(sheet.GetValue(0, 0).IsNumber);
            Assert.True(sheet.GetValue(1, 0).IsNumber);
            Assert.Equal(SheetValueKindEnum.Text, sheet.GetValue(2, 0).Kind);

            // A date is text here, which is honest: there is no date type, and quietly reading it as a subtraction
            // would be worse than leaving it alone.
            Assert.Equal(SheetValueKindEnum.Text, sheet.GetValue(3, 0).Kind);
        }

        [Fact]
        public void AnUntouchedCellIsEmptyRatherThanZero()
        {
            var sheet = new Sheet();

            Assert.True(sheet.GetValue(5, 5).IsEmpty);
            Assert.Equal(string.Empty, sheet.GetValue(5, 5).Display());

            // Empty and zero are different answers, which is what makes COUNT and AVERAGE mean anything.
            Assert.Equal("0", Evaluate("=COUNT(A1:A9)"));
        }

        [Fact]
        public void ArithmeticBindsTheWayArithmeticDoes()
        {
            Assert.Equal("7", Evaluate("=1+2*3"));
            Assert.Equal("9", Evaluate("=(1+2)*3"));
            Assert.Equal("-5", Evaluate("=-2-3"));
            Assert.Equal("2", Evaluate("=8/4"));

            // Powers go the other way about: two to the three to the two is two to the ninth, not sixty-four.
            Assert.Equal("512", Evaluate("=2^3^2"));
        }

        [Fact]
        public void ACellReferenceIsWorthWhateverThatCellIs()
        {
            Assert.Equal("20", Evaluate("=B3"));
            Assert.Equal("60", Evaluate("=B2+B3+B4"));
        }

        [Fact]
        public void ARangeAddsUpTheCellsItCovers()
        {
            Assert.Equal("60", Evaluate("=SUM(B2:B4)"));

            // Including the heading, which is text and is passed over rather than refused: a column almost always
            // has a word at the top of it and a total that would not add one up would be useless.
            Assert.Equal("60", Evaluate("=SUM(B1:B4)"));
        }

        [Fact]
        public void TheFunctionsGiveTheseExactAnswers()
        {
            Assert.Equal("20", Evaluate("=AVERAGE(B2:B4)"));
            Assert.Equal("10", Evaluate("=MIN(B1:B4)"));
            Assert.Equal("30", Evaluate("=MAX(B1:B4)"));

            // COUNT counts numbers and COUNTA counts anything at all, which is the difference the heading makes.
            Assert.Equal("3", Evaluate("=COUNT(B1:B4)"));
            Assert.Equal("4", Evaluate("=COUNTA(B1:B4)"));

            Assert.Equal("3.5", Evaluate("=ROUND(3.456, 1)"));
            Assert.Equal("3", Evaluate("=ROUND(3.456)"));
            Assert.Equal("4", Evaluate("=ABS(0-4)"));
            Assert.Equal("3", Evaluate("=INT(3.9)"));
            Assert.Equal("5", Evaluate("=SQRT(25)"));
        }

        [Fact]
        public void AverageDividesByHowManyNumbersThereWereNotHowManyCells()
        {
            // The range is four cells and only three are numbers. Dividing by four would give fifteen, which is a
            // perfectly plausible wrong answer.
            Assert.Equal("20", Evaluate("=AVERAGE(B1:B4)"));
        }

        [Fact]
        public void AFunctionTakesLooseArgumentsAndRangesAlike()
        {
            Assert.Equal("60", Evaluate("=SUM(B2, B3, B4)"));
            Assert.Equal("61", Evaluate("=SUM(B2:B3, B4, 1)"));
        }

        [Fact]
        public void EachSortOfMistakeSaysWhichSortItWas()
        {
            Assert.Equal("#NAME?", Evaluate("=FLIBBLE(1)"));
            Assert.Equal("#DIV/0!", Evaluate("=1/0"));
            Assert.Equal("#ERROR!", Evaluate("=1+"));
            Assert.Equal("#ERROR!", Evaluate("=(1+2"));

            // Trailing rubbish is refused rather than half understood, or this would quietly come to three.
            Assert.Equal("#ERROR!", Evaluate("=1+2)"));

            // Arithmetic on a word, which is stricter than a function over a range and deliberately so.
            Assert.Equal("#VALUE!", Evaluate("=B1+1"));

            // A range on its own is not a value; answering with its first cell would be a plausible wrong number.
            Assert.Equal("#VALUE!", Evaluate("=B2:B4"));

            // Off the edge of the grid.
            Assert.Equal("#REF!", Evaluate("=B9999"));
        }

        [Fact]
        public void AMistakeInOneCellSpreadsToTheTotalRatherThanBeingSkipped()
        {
            var sheet = WithNumbers();
            sheet.SetText(2, 1, "=1/0");

            // A total that quietly left out a broken cell would be a number that looks right and is not.
            sheet.SetText(9, 9, "=SUM(B2:B4)");
            Assert.Equal("#DIV/0!", sheet.GetValue(9, 9).Display());
        }

        [Fact]
        public void ACellThatNeedsItsOwnValueSaysSoInsteadOfHangingTheProgram()
        {
            var sheet = new Sheet();
            sheet.SetText(0, 0, "=A1+1");

            // Without the guard this recurses until the stack runs out, which takes the whole program down rather
            // than one cell. This test hangs or crashes the suite on a regression rather than failing politely.
            Assert.Equal("#CIRC!", sheet.GetValue(0, 0).Display());
        }

        [Fact]
        public void ALongerLoopIsCaughtTheSameWay()
        {
            var sheet = new Sheet();

            sheet.SetText(0, 0, "=B1");
            sheet.SetText(0, 1, "=C1");
            sheet.SetText(0, 2, "=A1");

            Assert.Equal("#CIRC!", sheet.GetValue(0, 0).Display());
        }

        [Fact]
        public void ChangingACellChangesWhatDependsOnIt()
        {
            var sheet = WithNumbers();
            sheet.SetText(9, 9, "=SUM(B2:B4)");

            Assert.Equal("60", sheet.GetValue(9, 9).Display());

            sheet.SetText(3, 1, "70");

            // The value was cached the first time it was asked for, so this is the test that the cache is thrown
            // away rather than kept.
            Assert.Equal("100", sheet.GetValue(9, 9).Display());
        }

        [Fact]
        public void WholeNumbersLoseTheirDecimalPointAndTheRestKeepTwoPlaces()
        {
            var sheet = new Sheet();

            sheet.SetText(0, 0, "=10/2");
            sheet.SetText(1, 0, "=10/4");
            sheet.SetText(2, 0, "=10/3");

            Assert.Equal("5", Display(sheet, "A1"));
            Assert.Equal("2.5", Display(sheet, "A2"));
            Assert.Equal("3.33", Display(sheet, "A3"));
        }

        [Fact]
        public void EmptyingACellReallyEmptiesIt()
        {
            var sheet = WithNumbers();

            sheet.SetText(1, 1, string.Empty);

            Assert.True(sheet.GetValue(1, 1).IsEmpty);
            Assert.Equal("50", Evaluate("=SUM(B3:B4)"));
        }

        [Fact]
        public void ItKnowsHowMuchOfTheGridIsUsed()
        {
            var sheet = new Sheet();

            sheet.SetText(4, 2, "x");

            Assert.Equal(5, sheet.UsedRowCount);
            Assert.Equal(3, sheet.UsedColumnCount);
        }

        [Fact]
        public void AColumnWidthIsClampedToSomethingReadable()
        {
            var sheet = new Sheet();

            sheet.SetColumnWidth(0, 1);
            Assert.Equal(Sheet.MinimumColumnWidth, sheet.GetColumnWidth(0));

            sheet.SetColumnWidth(0, 9999);
            Assert.Equal(Sheet.MaximumColumnWidth, sheet.GetColumnWidth(0));
        }

        [Fact]
        public void MergingReplacesWhateverItOverlaps()
        {
            var sheet = new Sheet();

            sheet.Merge(0, 0, 4);
            sheet.Merge(0, 2, 3);

            // One merge covering a cell and never two, or the drawing and the hit test could disagree about which
            // one a cell belongs to.
            Assert.Single(sheet.Merges);
            Assert.Equal(2, sheet.Merges[0].FirstColumn);
            Assert.Equal(4, sheet.Merges[0].LastColumn);
        }

        [Fact]
        public void AMergeOfOneColumnIsNoMergeAtAll()
        {
            var sheet = new Sheet();

            sheet.Merge(0, 0, 4);
            sheet.Merge(0, 0, 1);

            Assert.Empty(sheet.Merges);
        }

        [Fact]
        public void AMergeCanBeUndoneFromAnyCellItCovers()
        {
            var sheet = new Sheet();
            sheet.Merge(3, 1, 4);

            Assert.NotNull(sheet.MergeAt(3, 3));
            Assert.Null(sheet.MergeAt(4, 3));

            Assert.True(sheet.Unmerge(3, 3));
            Assert.False(sheet.Unmerge(3, 3));
            Assert.Null(sheet.MergeAt(3, 1));
        }

        [Fact]
        public void ColumnLettersCarryTheWayLettersHaveTo()
        {
            Assert.Equal("A", CellAddress.ColumnName(0));
            Assert.Equal("Z", CellAddress.ColumnName(25));

            // The one everybody gets wrong: there is no letter meaning nothing, so the carry has to borrow one or
            // the column after Z comes out as BA.
            Assert.Equal("AA", CellAddress.ColumnName(26));
            Assert.Equal("AB", CellAddress.ColumnName(27));
            Assert.Equal("BA", CellAddress.ColumnName(52));
        }

        [Fact]
        public void EveryColumnNameReadsBackAsTheColumnItNames()
        {
            // The round trip over a spread rather than one example, which is what catches a carry that is right at
            // the boundary and wrong just past it.
            for (var column = 0; column < 800; column++)
            {
                Assert.True(CellAddress.TryParse(CellAddress.ColumnName(column) + "1", out var address));
                Assert.Equal(column, address.Column);
            }
        }

        [Fact]
        public void AnAddressIsReadTheWayAPersonWritesIt()
        {
            Assert.True(CellAddress.TryParse("b7", out var lower));
            Assert.Equal(new CellAddress(6, 1), lower);

            Assert.True(CellAddress.TryParse("  AA10  ", out var spaced));
            Assert.Equal(new CellAddress(9, 26), spaced);

            Assert.Equal("B7", new CellAddress(6, 1).ToString());
        }

        [Fact]
        public void ThingsThatAreNotAddressesAreRefused()
        {
            Assert.False(CellAddress.TryParse(null, out _));
            Assert.False(CellAddress.TryParse("7", out _));
            Assert.False(CellAddress.TryParse("B", out _));
            Assert.False(CellAddress.TryParse("B0", out _));
            Assert.False(CellAddress.TryParse("B-1", out _));
        }

        [Fact]
        public void ARangeSortsItsCornersHoweverItWasMade()
        {
            // Dragging upwards and leftwards puts the moving end before the fixed one, which is the commonest way
            // a range is made and the reason this normalizes at all.
            var dragged = new CellRange(new CellAddress(9, 5), new CellAddress(2, 1));

            Assert.Equal(2, dragged.FirstRow);
            Assert.Equal(9, dragged.LastRow);
            Assert.Equal(1, dragged.FirstColumn);
            Assert.Equal(5, dragged.LastColumn);
            Assert.Equal("B3:F10", dragged.ToString());
        }

        [Fact]
        public void ARangeOfOneCellIsWrittenAsOneCell()
        {
            Assert.Equal("B3", new CellRange(new CellAddress(2, 1)).ToString());
            Assert.Equal(1, new CellRange(new CellAddress(2, 1)).CellCount);
        }

        [Fact]
        public void ARangeReadsAlongEachRowBeforeMovingDown()
        {
            var range = new CellRange(new CellAddress(0, 0), new CellAddress(1, 1));

            Assert.Equal(new[] {"A1", "B1", "A2", "B2"}, range.Cells().Select(cell => cell.ToString()).ToArray());
        }
    }
}
