using WolfCurses.Documents;
using Xunit;

namespace WolfCurses.Tests.Documents
{
    /// <summary>
    ///     Walking the words of a document. Small enough to look obvious, and the boundary conditions are exactly
    ///     the ones every caller would otherwise get wrong on its own.
    /// </summary>
    public class TextWordsTests
    {
        private static string WordAt(string line, int from)
        {
            return TextWords.TryNextWord(line, from, out var start, out var length)
                ? line.Substring(start, length)
                : null;
        }

        [Fact]
        public void ItFindsTheFirstWordAtOrAfterAColumn()
        {
            const string line = "  alpha beta  ";

            Assert.Equal("alpha", WordAt(line, 0));
            Assert.Equal("alpha", WordAt(line, 2));
            Assert.Equal("lpha", WordAt(line, 3));
            Assert.Equal("beta", WordAt(line, 7));
        }

        [Fact]
        public void ResumingFromTheStartOfAWordHandsBackTheSameWord()
        {
            // Documented so it cannot be mistaken for a bug, and pinned because the cost of getting it wrong is a
            // loop that never ends rather than an answer that is merely wrong. Callers resume at start + length.
            const string line = "alpha beta";

            Assert.True(TextWords.TryNextWord(line, 0, out var start, out var length));
            Assert.Equal("alpha", line.Substring(start, length));

            Assert.True(TextWords.TryNextWord(line, start, out var again, out _));
            Assert.Equal(start, again);

            Assert.True(TextWords.TryNextWord(line, start + length, out var next, out var nextLength));
            Assert.Equal("beta", line.Substring(next, nextLength));
        }

        [Fact]
        public void ALineWithNoWordsLeftAnswersFalseRatherThanAnEmptyWord()
        {
            Assert.False(TextWords.TryNextWord("alpha", 5, out var start, out var length));
            Assert.Equal(-1, start);
            Assert.Equal(0, length);

            Assert.False(TextWords.TryNextWord("   ", 0, out _, out _));
            Assert.False(TextWords.TryNextWord(string.Empty, 0, out _, out _));
            Assert.False(TextWords.TryNextWord(null, 0, out _, out _));
        }

        [Fact]
        public void APositionOutsideTheLineIsHandledRatherThanThrowing()
        {
            Assert.Equal("alpha", WordAt("alpha", -5));
            Assert.False(TextWords.TryNextWord("alpha", 99, out _, out _));
        }

        [Fact]
        public void AWordIsTheSameRunTheRestOfTheLibraryMeansByOne()
        {
            // Digits and the underscore are part of a word here because they are part of one to CTRL+arrow and to a
            // double-click. One rule, so a caller cannot be shown a different word from the one it can select.
            Assert.Equal("rfc1149", WordAt("see rfc1149 now", 4));
            Assert.Equal("some_name", WordAt("some_name", 0));
            Assert.Equal("don", WordAt("don't", 0));
        }

        [Fact]
        public void ACallerMayBringItsOwnIdeaOfAWord()
        {
            // Which is the seam a spell checker needs: an apostrophe is a boundary to a cursor and part of the word
            // to a dictionary, and both are right for what is asking.
            static bool WithApostrophes(char character)
            {
                return char.IsLetterOrDigit(character) || character == '\'';
            }

            Assert.True(TextWords.TryNextWord("don't stop", 0, out var start, out var length, WithApostrophes));
            Assert.Equal("don't", "don't stop".Substring(start, length));
        }

        [Fact]
        public void CountingWalksEveryLine()
        {
            var lines = new[] {"one two", string.Empty, "  three  ", "four-five"};

            // Two, none, one and then two: the hyphen is not a word character, so "four-five" counts as two.
            Assert.Equal(5, TextWords.Count(lines));
            Assert.Equal(0, TextWords.Count(new[] {string.Empty, "   "}));
            Assert.Equal(0, TextWords.Count(null));
        }
    }
}
