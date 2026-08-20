using WolfCurses.Documents;
using Xunit;

namespace WolfCurses.Tests.Documents
{
    /// <summary>
    ///     The translation between where a character is stored and where it is drawn. Every one of these is a line
    ///     somebody would otherwise write inline in a render method, and the first of them is the mistake almost
    ///     everybody makes.
    /// </summary>
    public class TabStopsTests
    {
        [Fact]
        public void ATabAdvancesToTheNextStopRatherThanAddingAFixedNumberOfSpaces()
        {
            // THE trap. Replacing each tab with eight spaces gives "a" plus eight, and every table drawn that way
            // is misaligned by one column from the second row onwards. A tab in column one advances to column
            // eight, which is seven spaces, not eight.
            Assert.Equal("a       b", TabStops.Expand("a\tb", 8));
            Assert.Equal("        b", TabStops.Expand("\tb", 8));
            Assert.Equal("ab      c", TabStops.Expand("ab\tc", 8));
            Assert.Equal("abcdefg h", TabStops.Expand("abcdefg\th", 8));
        }

        [Fact]
        public void ATabSittingExactlyOnAStopAdvancesAWholeStop()
        {
            // The off-by-one that hides: at column eight the next stop is sixteen, so this is a full eight spaces
            // rather than zero. A "distance to the next multiple" written with the wrong modulo gives an empty tab.
            Assert.Equal("abcdefgh        i", TabStops.Expand("abcdefgh\ti", 8));
        }

        [Fact]
        public void ConsecutiveTabsEachAdvanceToTheirOwnStop()
        {
            Assert.Equal("                ", TabStops.Expand("\t\t", 8));
            Assert.Equal("a               b", TabStops.Expand("a\t\tb", 8));
        }

        [Theory]
        [InlineData(4)]
        [InlineData(2)]
        [InlineData(1)]
        public void TheStopIntervalIsHonoured(int width)
        {
            Assert.Equal(width, TabStops.Expand("\t", width).Length);
        }

        [Fact]
        public void AWidthBelowOneIsTreatedAsOneRatherThanDividingByZero()
        {
            Assert.Equal(" ", TabStops.Expand("\t", 0));
            Assert.Equal(" ", TabStops.Expand("\t", -4));
        }

        [Fact]
        public void ALineWithNoTabsIsHandedStraightBack()
        {
            // The common case by far, and it should cost a scan rather than an allocation.
            const string line = "nothing to expand here";

            Assert.Same(line, TabStops.Expand(line));
        }

        [Fact]
        public void NullAndEmptyLinesAreTolerated()
        {
            Assert.Null(TabStops.Expand(null));
            Assert.Equal(string.Empty, TabStops.Expand(string.Empty));
            Assert.Equal(0, TabStops.DisplayWidth(null));
            Assert.Equal(0, TabStops.DisplayWidth(string.Empty));
        }

        [Fact]
        public void ADisplayColumnIsWhereTheExpandedLinePutsThatCharacter()
        {
            // Asserted against Expand rather than restated, so the two cannot drift: whatever the expansion does,
            // the column arithmetic has to agree with it for every character in the line.
            const string line = "ab\tcd\te";
            var expanded = TabStops.Expand(line, 8);

            for (var index = 0; index < line.Length; index++)
            {
                var column = TabStops.ToDisplayColumn(line, index, 8);
                Assert.Equal(TabStops.Expand(line.Substring(0, index), 8).Length, column);
            }

            Assert.Equal(expanded.Length, TabStops.ToDisplayColumn(line, line.Length, 8));
        }

        [Fact]
        public void DisplayWidthIsTheLengthOfTheDrawnLine()
        {
            const string line = "\tindented\tand\ttabbed";

            Assert.Equal(TabStops.Expand(line, 8).Length, TabStops.DisplayWidth(line, 8));
        }

        [Fact]
        public void ClickingAnywhereInsideATabLandsOnTheTabItself()
        {
            // "a" then a tab covering columns 1 to 7. Clicking any of those columns means the tab, so a following
            // BACKSPACE removes the indent in one press rather than doing nothing because the caret was left
            // between two characters that are not there.
            const string line = "a\tb";

            for (var column = 1; column <= 7; column++)
                Assert.Equal(1, TabStops.ToDocumentColumn(line, column, 8));

            Assert.Equal(0, TabStops.ToDocumentColumn(line, 0, 8));
            Assert.Equal(2, TabStops.ToDocumentColumn(line, 8, 8));
        }

        [Fact]
        public void ClickingPastTheEndOfALineLandsAfterItsLastCharacter()
        {
            const string line = "a\tb";

            Assert.Equal(line.Length, TabStops.ToDocumentColumn(line, 9, 8));
            Assert.Equal(line.Length, TabStops.ToDocumentColumn(line, 500, 8));
        }

        [Fact]
        public void ANegativeColumnLandsAtTheStart()
        {
            Assert.Equal(0, TabStops.ToDocumentColumn("a\tb", -5, 8));
            Assert.Equal(0, TabStops.ToDisplayColumn("a\tb", -5, 8));
        }

        [Fact]
        public void EveryCharacterSurvivesTheRoundTripThroughItsDisplayColumn()
        {
            // The property that matters for a mouse: click where a character is drawn and you get that character
            // back. Asserted over every position of a line that is mostly tabs, which is where it would break.
            const string line = "\ta\t\tbc\td";

            for (var index = 0; index <= line.Length; index++)
            {
                var column = TabStops.ToDisplayColumn(line, index, 8);
                Assert.Equal(index, TabStops.ToDocumentColumn(line, column, 8));
            }
        }

        [Fact]
        public void PastTheEndOfALineOneColumnIsOneCharacter()
        {
            // The caret may sit beyond the stored text, and out there it moves a cell at a time: there is nothing
            // to expand, so the two coordinate spaces line back up.
            const string line = "a\tb";
            var width = TabStops.DisplayWidth(line, 8);

            Assert.Equal(width + 1, TabStops.ToDisplayColumn(line, line.Length + 1, 8));
            Assert.Equal(width + 3, TabStops.ToDisplayColumn(line, line.Length + 3, 8));
        }

        [Fact]
        public void ALineOfPlainTextMapsOneToOneInBothDirections()
        {
            // With no tabs the two coordinate spaces are the same, which is why every screen that has never seen a
            // tab behaves identically whether or not it goes through this type.
            const string line = "plain text, no tabs";

            for (var index = 0; index <= line.Length; index++)
            {
                Assert.Equal(index, TabStops.ToDisplayColumn(line, index, 8));
                Assert.Equal(index, TabStops.ToDocumentColumn(line, index, 8));
            }
        }
    }
}
