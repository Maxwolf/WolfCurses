using System;
using System.Linq;
using System.Text.RegularExpressions;
using WolfCurses.Tests.Support;
using WolfCurses.Window.Menu;
using Xunit;

namespace WolfCurses.Tests.Windows
{
    /// <summary>
    ///     Covers <see cref="MenuLayout" />: a menu that fits is composed one item per line exactly as before, and a
    ///     menu too tall for the console reflows column-major into just enough columns to fit so the input prompt
    ///     beneath it is not pushed off the bottom and clipped.
    /// </summary>
    public class MenuColumnLayoutTests
    {
        /// <summary>Inverse-video highlight escapes are environment-dependent; the "&gt; " marker is the contract.</summary>
        private static string StripSgr(string text)
        {
            return Regex.Replace(text, @"\x1b\[[0-9;]*m", string.Empty);
        }

        private static string[] Menu(int count)
        {
            return Enumerable.Range(1, count).Select(i => $"{i}. Item {i}").ToArray();
        }

        [Fact]
        public void ComputeColumnCount_FittingMenu_StaysSingleColumn()
        {
            Assert.Equal(1, MenuLayout.ComputeColumnCount(itemCount: 10, availableRows: 20, totalWidth: 80));
        }

        [Fact]
        public void ComputeColumnCount_ShortMenu_NeverReflowsEvenOnACrampedConsole()
        {
            // A handful of items stays single-column however few rows are reported — which is also what keeps small
            // menus (and the pinned MenuHighlightTests) byte-identical when a headless host reports a tiny height.
            Assert.Equal(1, MenuLayout.ComputeColumnCount(itemCount: 3, availableRows: 1, totalWidth: 80));
        }

        [Fact]
        public void ComputeColumnCount_TallMenu_SplitsIntoJustEnoughColumns()
        {
            Assert.Equal(2, MenuLayout.ComputeColumnCount(21, 19, 80)); // 21 into 19 rows needs two columns
            Assert.Equal(3, MenuLayout.ComputeColumnCount(40, 19, 80)); // 40 needs three
        }

        [Fact]
        public void ComputeColumnCount_NarrowConsole_CapsColumnsToWhatTheWidthCanHold()
        {
            // A very tall list cannot make columns narrower than is readable; a 30-wide console holds two, not more.
            Assert.Equal(2, MenuLayout.ComputeColumnCount(100, 5, 30));
        }

        [Fact]
        public void Compose_WhenItFits_IsByteIdenticalToTheSingleColumnMenu()
        {
            var composed = MenuLayout.Compose(Menu(3), highlightedIndex: -1, availableRows: 20, totalWidth: 80);

            Assert.Equal("  1. Item 1" + Text.NL + "  2. Item 2" + Text.NL + "  3. Item 3" + Text.NL, composed);
            Assert.DoesNotContain('\x1b', composed);
        }

        [Fact]
        public void Compose_SingleColumnWithHighlight_MarksOnlyTheChosenRow()
        {
            var stripped = StripSgr(MenuLayout.Compose(Menu(3), highlightedIndex: 1, availableRows: 20, totalWidth: 80));

            Assert.Equal("  1. Item 1" + Text.NL + "> 2. Item 2" + Text.NL + "  3. Item 3" + Text.NL, stripped);
        }

        [Fact]
        public void Compose_OverflowingMenu_ReflowsColumnMajorIntoFewerRows()
        {
            var composed = MenuLayout.Compose(Menu(21), highlightedIndex: -1, availableRows: 19, totalWidth: 80);
            var lines = composed.Split(Text.NL, StringSplitOptions.RemoveEmptyEntries);

            // Two columns of eleven: the whole menu is eleven physical rows instead of twenty-one.
            Assert.Equal(11, lines.Length);

            // Column-major means item 1 heads the first column and item 12 heads the second, side by side on row one.
            Assert.StartsWith("  1. Item 1", StripSgr(lines[0]));
            Assert.Contains("12. Item 12", StripSgr(lines[0]));

            // Every item is present.
            for (var i = 1; i <= 21; i++)
                Assert.Contains($"{i}. Item {i}", composed);
        }

        [Fact]
        public void Compose_OverflowingMenuWithHighlight_MarksOnlyTheChosenCell()
        {
            // Index 12 is item 13, the top of the second column.
            var stripped = StripSgr(MenuLayout.Compose(Menu(21), highlightedIndex: 12, availableRows: 19, totalWidth: 80));

            Assert.Contains("> 13. Item 13", stripped);
            Assert.DoesNotContain("> 1. Item 1", stripped); // item 1 is not the highlighted one
            Assert.Single(Regex.Matches(stripped, "> ")); // exactly one cursor in the whole grid
        }

        [Fact]
        public void Compose_NarrowColumns_TruncateWithAnEllipsisAndNeverOverflowTheWidth()
        {
            var rows = Enumerable.Range(1, 20)
                .Select(i => $"{i}. A very long menu description that does not fit").ToArray();

            var composed = MenuLayout.Compose(rows, highlightedIndex: -1, availableRows: 8, totalWidth: 60);

            Assert.Contains("…", composed);
            foreach (var line in composed.Split(Text.NL, StringSplitOptions.RemoveEmptyEntries))
                Assert.True(StripSgr(line).Length <= 60, $"physical row wider than the console: '{StripSgr(line)}'");
        }

        [Fact]
        public void RowsPerColumn_BalancesTheItemsAcrossTheColumns()
        {
            // Every column but the last is this full; 21 into two columns is eleven and ten, not eleven and eleven.
            Assert.Equal(11, MenuLayout.RowsPerColumn(21, 2));
            Assert.Equal(7, MenuLayout.RowsPerColumn(21, 3));
            Assert.Equal(21, MenuLayout.RowsPerColumn(21, 1));
        }

        [Fact]
        public void StepColumn_MovesOneColumnAcrossKeepingTheRow()
        {
            // 21 items in two columns: the left holds indices 0-10, the right 11-20.
            Assert.Equal(11, MenuLayout.StepColumn(0, 21, 2, 1)); // top of the left column, across to the top of the right
            Assert.Equal(14, MenuLayout.StepColumn(3, 21, 2, 1)); // row three stays row three
            Assert.Equal(3, MenuLayout.StepColumn(14, 21, 2, -1)); // and back again
        }

        [Fact]
        public void StepColumn_IntoAColumnTooShortForThatRow_LandsOnItsBottomItem()
        {
            // The right column has ten items where the left has eleven, so there is no row ten to land on. The row is
            // where the highlight happens to be, not a requirement, so the step takes the nearest cell rather than
            // refusing to move.
            Assert.Equal(20, MenuLayout.StepColumn(10, 21, 2, 1));

            // Coming back is honestly asymmetric: index 20 really is on row nine, so Left goes to row nine's item.
            Assert.Equal(9, MenuLayout.StepColumn(20, 21, 2, -1));
        }

        [Fact]
        public void StepColumn_AtTheOuterEdges_IsAWallRatherThanWrappingRound()
        {
            // Deliberately unlike a single vertical step, which wraps: sideways wrapping would throw the highlight
            // the full width of the screen and move the number being read by a whole column at once.
            Assert.Equal(0, MenuLayout.StepColumn(0, 21, 2, -1)); // leftmost column, Left
            Assert.Equal(5, MenuLayout.StepColumn(5, 21, 2, -1));
            Assert.Equal(11, MenuLayout.StepColumn(11, 21, 2, 1)); // rightmost column, Right
            Assert.Equal(20, MenuLayout.StepColumn(20, 21, 2, 1));
        }

        [Fact]
        public void StepColumn_WhileTheMenuIsOneColumn_NeverMoves()
        {
            // Which is what keeps Left and Right as inert on a menu that fits its console as they always were.
            for (var index = 0; index < 5; index++)
            {
                Assert.Equal(index, MenuLayout.StepColumn(index, 5, 1, 1));
                Assert.Equal(index, MenuLayout.StepColumn(index, 5, 1, -1));
            }
        }

        [Fact]
        public void StepColumn_LandsWhereComposeDrewTheCellBeside()
        {
            // The reason RowsPerColumn is shared rather than written out twice: the grid the arrow keys move through
            // has to be the grid that was drawn. This reads both off Compose's own output instead of restating the
            // arithmetic, so a change that moved one and not the other fails here.
            const int count = 21;
            const int columns = 2;
            var rows = Menu(count);

            for (var index = 0; index < count; index++)
            {
                var next = MenuLayout.StepColumn(index, count, columns, 1);
                if (next == index)
                {
                    Assert.True(index >= 11, $"index {index} is in the left column and had somewhere to go");
                    continue;
                }

                var from = Cursor(rows, index);
                var to = Cursor(rows, next);

                Assert.True(to.Offset > from.Offset,
                    $"stepping right from {index} to {next} did not move across the screen");
                Assert.True(to.Line <= from.Line,
                    $"stepping right from {index} to {next} moved down the screen, to row {to.Line} from {from.Line}");
            }
        }

        /// <summary>
        ///     Where <see cref="MenuLayout.Compose" /> puts the highlight cursor for a given index: which physical
        ///     line it lands on and how many visible columns in. Read off the composed text rather than computed, so
        ///     assertions about the grid are about the drawn one.
        /// </summary>
        private static (int Line, int Offset) Cursor(string[] rows, int highlightedIndex)
        {
            // availableRows of 11 puts 21 items into exactly the two columns these tests are written against.
            var composed = MenuLayout.Compose(rows, highlightedIndex, availableRows: 11, totalWidth: 80);
            var lines = composed.Split('\n');

            for (var line = 0; line < lines.Length; line++)
            {
                var offset = StripSgr(lines[line]).IndexOf("> ", StringComparison.Ordinal);
                if (offset >= 0)
                    return (line, offset);
            }

            Assert.Fail($"no highlight cursor in the composed menu for index {highlightedIndex}");
            return (-1, -1);
        }

        [Theory]
        [InlineData("abc", 5, "abc  ")]
        [InlineData("abc", 3, "abc")]
        [InlineData("abcdef", 4, "abc…")]
        [InlineData("abc", 1, "…")]
        [InlineData("abc", 0, "")]
        public void Fit_PadsOrTruncatesToExactWidth(string text, int width, string expected)
        {
            Assert.Equal(expected, MenuLayout.Fit(text, width));
        }
    }
}
