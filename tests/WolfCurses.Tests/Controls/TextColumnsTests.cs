using WolfCurses.Window.Control;
using Xunit;

namespace WolfCurses.Tests.Controls
{
    /// <summary>
    ///     TextColumns lays blocks side by side. The whole reason it belongs in the library rather than in every
    ///     application is that it must measure visible width: a styled row is far longer than it is wide, and
    ///     padding it by character count shreds every column to its right.
    /// </summary>
    public class TextColumnsTests
    {
        private const char Esc = '';

        [Fact]
        public void TwoBlocksAreJoinedRowByRow()
        {
            var joined = TextColumns.Join("a\nbb\nccc", "1\n2\n3", 1);

            Assert.Equal("a   1\nbb  2\nccc 3", joined.Replace("\r\n", "\n"));
        }

        [Fact]
        public void TheLeftColumnIsPaddedToItsOwnWidestRow()
        {
            var joined = TextColumns.Join("x\nlonger", "1\n2", 2).Replace("\r\n", "\n");

            // Both rows put the right column at the same visible offset, which is the entire job.
            var rows = joined.Split('\n');
            Assert.Equal(rows[0].IndexOf('1'), rows[1].IndexOf('2'));
        }

        [Fact]
        public void ColumnsAlignOnVisibleWidth_NotStringLength()
        {
            // The test that fails if anyone "simplifies" this to PadRight. The styled row is 3 columns wide and 14
            // characters long; measured by length it would push its neighbour eleven columns out of true.
            var styled = $"{Esc}[31mred{Esc}[0m";
            Assert.Equal(3, WolfCurses.Graphics.AnsiText.VisibleLength(styled));
            Assert.True(styled.Length > 3);

            var joined = TextColumns.Join($"{styled}\nabc", "L\nR", 1).Replace("\r\n", "\n");
            var rows = joined.Split('\n');

            // Compared on the STRIPPED rows. Searching the raw row for the right column's text finds it inside the
            // escape instead - "ESC[31m" contains a '1' - which is the same class of mistake as measuring width
            // with Length, and is why the first version of this test failed.
            var styledOffset = WolfCurses.Graphics.AnsiText.StripEscapes(rows[0]).IndexOf('L');
            var plainOffset = rows[1].IndexOf('R');
            Assert.Equal(plainOffset, styledOffset);

            // And at the RIGHT absolute offset, not merely a consistent one. Measuring the column's width by
            // character length inflates it for every row equally, so the two columns stay aligned with each other
            // while the gutter silently becomes eleven columns wide - consistent, and wrong. The left column is 3
            // visible columns and the gap is 1, so the right column starts at 4 and nowhere else.
            Assert.Equal(4, styledOffset);
        }

        [Fact]
        public void RaggedHeightsAreFilledOut()
        {
            var joined = TextColumns.Join("a\nb\nc", "1", 1).Replace("\r\n", "\n");
            var rows = joined.Split('\n');

            Assert.Equal(3, rows.Length);
            Assert.Equal("a 1", rows[0]);
            Assert.Equal("b", rows[1]);
            Assert.Equal("c", rows[2]);
        }

        [Fact]
        public void NoTrailingSpacesArePaintedPastTheLastColumnWithContent()
        {
            // Invisible, but still cells the presenter writes and diffs, and on a tall layout they are most of the
            // frame.
            var joined = TextColumns.Join("aaaa\nbbbb", "1", 2).Replace("\r\n", "\n");

            foreach (var row in joined.Split('\n'))
                Assert.Equal(row.TrimEnd(), row);
        }

        [Fact]
        public void MoreThanTwoColumnsWork()
        {
            var joined = TextColumns.Join(1, "a", "b", "c").Replace("\r\n", "\n");

            Assert.Equal("a b c", joined);
        }

        [Fact]
        public void AnEmptyColumnIsZeroWideButStillTakesItsGutters()
        {
            // It is not dropped - the columns either side stay in order and both gutters are drawn - but it claims
            // no width, because there is no width it could claim. A caller who wants a slot to keep a fixed size
            // when it empties has to pad it themselves; nothing here can guess the number.
            var joined = TextColumns.Join(1, "a", string.Empty, "c").Replace("\r\n", "\n");

            Assert.Equal("a  c", joined);
        }

        [Fact]
        public void NothingAtAllIsEmpty()
        {
            Assert.Equal(string.Empty, TextColumns.Join(2));
            Assert.Equal(string.Empty, TextColumns.Join(2, null));
        }
    }
}
