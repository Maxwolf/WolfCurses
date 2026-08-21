using WolfCurses.Graphics;
using Xunit;

namespace WolfCurses.Tests.Graphics
{
    /// <summary>
    ///     Fitting text to an exact number of visible columns.
    ///     <para>
    ///         The reason this is not <c>PadRight</c> plus <c>Substring</c> is entirely about styled text, so most
    ///         of what is asserted here carries colour. The two failures being guarded against are a padded row that
    ///         is too short because the escapes were counted as columns, and a trimmed row that has lost its reset
    ///         and leaves the colour switched on across everything drawn after it.
    ///     </para>
    /// </summary>
    public class AnsiTextFitTests
    {
        /// <summary>Red on, six letters, reset off. Six visible columns and fifteen characters.</summary>
        private const string Styled = "\u001B[31mabcdef\u001B[0m";

        [Fact]
        public void ShortTextIsPaddedToTheWidth()
        {
            Assert.Equal("ab   ", AnsiText.Fit("ab", 5));
            Assert.Equal("   ab", AnsiText.Fit("ab", 5, AnsiHorizontalAlignmentEnum.Right));
            Assert.Equal(" ab  ", AnsiText.Fit("ab", 5, AnsiHorizontalAlignmentEnum.Center));
        }

        [Fact]
        public void CentringPutsTheOddSpaceOnTheRight()
        {
            // Two spaces to place around one character. Stated as an absolute rather than "it is centred", because
            // both answers are centred and only one of them is stable from one call to the next.
            Assert.Equal(" a  ", AnsiText.Fit("a", 4, AnsiHorizontalAlignmentEnum.Center));
        }

        [Fact]
        public void LongTextIsTrimmedToTheWidth()
        {
            Assert.Equal("abc", AnsiText.Fit("abcdef", 3));
        }

        [Fact]
        public void TextAlreadyTheRightWidthIsReturnedUnchanged()
        {
            const string exact = "abcde";

            Assert.Same(exact, AnsiText.Fit(exact, 5));
            Assert.Same(Styled, AnsiText.Fit(Styled, 6));
        }

        [Fact]
        public void NullBecomesThatManySpaces()
        {
            Assert.Equal("    ", AnsiText.Fit(null, 4));
        }

        [Fact]
        public void NoWidthAtAllIsAnEmptyString()
        {
            Assert.Equal(string.Empty, AnsiText.Fit("abc", 0));
            Assert.Equal(string.Empty, AnsiText.Fit("abc", -3));
        }

        [Fact]
        public void StyledTextIsPaddedByWhatItLooksLikeRatherThanByItsLength()
        {
            var fitted = AnsiText.Fit(Styled, 9);

            // The absolute answer, hand-written. Asserting only that the visible length came out at nine would
            // pass for an implementation that threw the colour away.
            Assert.Equal("\u001B[31mabcdef\u001B[0m   ", fitted);
            Assert.Equal(9, AnsiText.VisibleLength(fitted));

            // What the naive version would have produced: fifteen characters is already past nine, so PadRight
            // adds nothing at all and the cell is six columns wide in a nine column slot.
            Assert.Equal(15, Styled.Length);
        }

        [Fact]
        public void TrimmingStyledTextKeepsTheEscapesThatSitPastTheCut()
        {
            var fitted = AnsiText.Fit(Styled, 3);

            // The reset is the point. Dropping it because it fell past the third visible column would leave the
            // terminal red for the rest of the screen.
            Assert.Equal("\u001B[31mabc\u001B[0m", fitted);
            Assert.Equal(3, AnsiText.VisibleLength(fitted));
        }

        [Fact]
        public void EveryFittedRowIsExactlyTheWidthAsked()
        {
            // The invariant the callers rely on, over a spread of inputs rather than one. A table whose cells are
            // each "about" the right width is a table with a ragged right edge.
            string[] samples = {string.Empty, "a", "abcdefghij", Styled, "\u001B[1m\u001B[4mx\u001B[0m", null};

            foreach (var sample in samples)
            {
                for (var width = 1; width <= 12; width++)
                {
                    Assert.Equal(width, AnsiText.VisibleLength(AnsiText.Fit(sample, width)));
                    Assert.Equal(width,
                        AnsiText.VisibleLength(AnsiText.Fit(sample, width, AnsiHorizontalAlignmentEnum.Right)));
                    Assert.Equal(width,
                        AnsiText.VisibleLength(AnsiText.Fit(sample, width, AnsiHorizontalAlignmentEnum.Center)));
                }
            }
        }

        [Fact]
        public void ItMeasuresTheWholeEscapeGrammarRatherThanOnlyColour()
        {
            // An OSC 8 hyperlink, whose URL is not in an SGR sequence at all. A fitter that only understood
            // "ESC [ ... m" would count the URL as visible columns and pad this to nothing.
            const string link = "\u001B]8;;https://bigmaxwolf.com\u0007go\u001B]8;;\u0007";

            Assert.Equal(2, AnsiText.VisibleLength(link));
            Assert.Equal(6, AnsiText.VisibleLength(AnsiText.Fit(link, 6)));
        }
    }
}
