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
