using WolfCurses.Tests.Support;
using WolfCurses.Window.Control;
using Xunit;

namespace WolfCurses.Tests.Controls
{
    /// <summary>
    ///     Pins <see cref="Box" /> layout: the border styles, title placement and alignment, interior padding, minimum
    ///     width, multi-line content, and ANSI-aware column measurement.
    /// </summary>
    public class BoxTests
    {
        [Fact]
        public void Render_Default_WrapsContentInASingleBorder()
        {
            var expected =
                "┌──┐" + Text.NL +
                "│Hi│" + Text.NL +
                "└──┘";

            Assert.Equal(expected, new Box().Render("Hi"));
        }

        [Fact]
        public void Render_Padding_AddsInteriorRowsAndColumns()
        {
            var expected =
                "┌────┐" + Text.NL +
                "│    │" + Text.NL +
                "│ Hi │" + Text.NL +
                "│    │" + Text.NL +
                "└────┘";

            Assert.Equal(expected, new Box {Padding = 1}.Render("Hi"));
        }

        [Fact]
        public void Render_TitleLeft_SitsNearTheLeftCorner()
        {
            var expected =
                "┌─ T ─┐" + Text.NL +
                "│Hello│" + Text.NL +
                "└─────┘";

            Assert.Equal(expected, new Box {Title = "T"}.Render("Hello"));
        }

        [Fact]
        public void Render_TitleCentered_IsBalanced()
        {
            var expected =
                "┌── T ──┐" + Text.NL +
                "│x      │" + Text.NL +
                "└───────┘";

            Assert.Equal(expected,
                new Box {Title = "T", TitleAlignment = BoxAlignmentEnum.Center, MinimumWidth = 7}.Render("x"));
        }

        [Fact]
        public void Render_TitleRight_SitsNearTheRightCorner()
        {
            var expected =
                "┌─── T ─┐" + Text.NL +
                "│x      │" + Text.NL +
                "└───────┘";

            Assert.Equal(expected,
                new Box {Title = "T", TitleAlignment = BoxAlignmentEnum.Right, MinimumWidth = 7}.Render("x"));
        }

        [Fact]
        public void Render_Multiline_PadsEachRowToTheWidestLine()
        {
            var expected =
                "┌───┐" + Text.NL +
                "│ab │" + Text.NL +
                "│cde│" + Text.NL +
                "└───┘";

            Assert.Equal(expected, new Box().Render("ab" + Text.NL + "cde"));
        }

        [Fact]
        public void Render_AsciiBorder_UsesPlainCharacters()
        {
            var expected =
                "+--+" + Text.NL +
                "|Hi|" + Text.NL +
                "+--+";

            Assert.Equal(expected, new Box {Border = BoxBorderEnum.Ascii}.Render("Hi"));
        }

        [Fact]
        public void Render_NoBorder_PadsContentIntoARectangle()
        {
            var expected =
                "ab " + Text.NL +
                "cde";

            Assert.Equal(expected, new Box {Border = BoxBorderEnum.None}.Render("ab" + Text.NL + "cde"));
        }

        [Fact]
        public void Render_Null_ProducesAnEmptyFramedBox()
        {
            var expected =
                "┌┐" + Text.NL +
                "││" + Text.NL +
                "└┘";

            Assert.Equal(expected, new Box().Render(null));
        }

        [Fact]
        public void Render_MinimumWidth_GrowsAShortBox()
        {
            var expected =
                "┌──────────┐" + Text.NL +
                "│hi        │" + Text.NL +
                "└──────────┘";

            Assert.Equal(expected, new Box {MinimumWidth = 10}.Render("hi"));
        }

        [Fact]
        public void Render_AnsiColoredTitle_KeepsTheFrameAligned()
        {
            // Regression: the title was measured by raw length, so ANSI escapes in it over-grew the box and left
            // the top border shorter than the sides. It must be measured by visible width like the content.
            const string title = "\x1b[31mRED\x1b[0m";
            var expected =
                "┌ " + title + " ┐" + Text.NL +
                "│hello│" + Text.NL +
                "└─────┘";

            Assert.Equal(expected, new Box {Title = title}.Render("hello"));
        }

        [Fact]
        public void Render_MultilineTitle_StaysOnOneBorderLine()
        {
            // Regression: a newline in the title broke the top border into two physical lines. Newlines collapse to
            // spaces so the frame stays intact.
            var expected =
                "┌ a b ┐" + Text.NL +
                "│x    │" + Text.NL +
                "└─────┘";

            Assert.Equal(expected, new Box {Title = "a\nb"}.Render("x"));
        }

        [Fact]
        public void Render_AnsiColoredContent_IsMeasuredByVisibleWidth()
        {
            // "Hi" is 2 visible columns despite the color escapes, so it aligns against the 3-wide plain line.
            const string colored = "\x1b[31mHi\x1b[0m";
            var expected =
                "┌───┐" + Text.NL +
                "│" + colored + " │" + Text.NL +
                "│XYZ│" + Text.NL +
                "└───┘";

            Assert.Equal(expected, new Box().Render(colored + Text.NL + "XYZ"));
        }
    }
}
