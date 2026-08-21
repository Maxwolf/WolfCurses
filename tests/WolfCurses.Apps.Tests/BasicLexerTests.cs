using System;
using System.Linq;
using WolfCurses.Apps.Basic;
using Xunit;

namespace WolfCurses.Apps.Tests
{
    /// <summary>
    ///     Turning BASIC source into tokens. Pure text in, a list out, so none of this needs an application around
    ///     it and all of it runs in parallel.
    /// </summary>
    public class BasicLexerTests
    {
        private static string[] Words(string source)
        {
            return BasicLexer.Tokenize(source)
                .Where(token => token.Kind == BasicTokenKindEnum.Word)
                .Select(token => token.Text)
                .ToArray();
        }

        private static BasicToken[] Meaningful(string source)
        {
            return BasicLexer.Tokenize(source)
                .Where(token => token.Kind is not (BasicTokenKindEnum.EndOfLine or BasicTokenKindEnum.EndOfFile))
                .ToArray();
        }

        [Fact]
        public void WordsComeBackUppercasedSoNothingLaterHasToGuessAtCulture()
        {
            Assert.Equal(new[] {"PRINT", "HELLO"}, Words("print hello"));
            Assert.Equal(new[] {"PRINT", "HELLO"}, Words("PrInT HeLlO"));
        }

        [Fact]
        public void AColonStaysASymbolSoThatALabelIsStillTellable()
        {
            // It separates statements like a line break does, but folding it into one would make "name:" and a
            // statement that is a bare word indistinguishable, and CLS on its own line would parse as a label.
            var tokens = BasicLexer.Tokenize("A = 1 : B = 2");

            Assert.Contains(tokens, token => token.IsSymbol(":"));
            Assert.Equal(1, tokens.Count(token => token.Kind == BasicTokenKindEnum.EndOfLine));
        }

        [Fact]
        public void BothKindsOfCommentAreDroppedButTheirLineBreakSurvives()
        {
            // Dropping the line break as well would silently renumber the program, and the line number is the one
            // thing a BASIC user navigates by.
            Assert.Empty(Meaningful("' just a comment"));
            Assert.Empty(Meaningful("REM just a comment"));

            var tokens = BasicLexer.Tokenize("' comment\nPRINT 1");
            var print = tokens.First(token => token.Kind == BasicTokenKindEnum.Word);

            Assert.Equal("PRINT", print.Text, StringComparer.Ordinal);
            Assert.Equal(2, print.Line);
        }

        [Fact]
        public void ACommentAfterCodeOnlyEatsTheRestOfItsOwnLine()
        {
            var words = Words("PRINT 1 ' say one\nPRINT 2");

            Assert.Equal(new[] {"PRINT", "PRINT"}, words);
        }

        [Fact]
        public void EveryTokenRemembersWhichLineItCameFrom()
        {
            // The whole reason a token is a type rather than a string: an error has to name a line, because that is
            // what the user is looking at in the editor.
            var tokens = BasicLexer.Tokenize("PRINT 1\n\nPRINT 2");
            var words = tokens.Where(token => token.Kind == BasicTokenKindEnum.Word).ToArray();

            Assert.Equal(1, words[0].Line);
            Assert.Equal(3, words[1].Line);
        }

        [Fact]
        public void AStringKeepsItsOwnCharactersExactly()
        {
            var tokens = Meaningful("\"Hello World\"");

            Assert.Single(tokens);
            Assert.Equal(BasicTokenKindEnum.String, tokens[0].Kind);
            Assert.Equal("Hello World", tokens[0].Text, StringComparer.Ordinal);
        }

        [Fact]
        public void AnUnterminatedStringStopsAtTheLineBreakRatherThanEatingTheProgram()
        {
            // The program is being typed in the editor next door, so refusing to read the rest of the file over one
            // missing quote would report the mistake in entirely the wrong place.
            var tokens = BasicLexer.Tokenize("PRINT \"oops\nPRINT 2");
            var words = tokens.Where(token => token.Kind == BasicTokenKindEnum.Word).ToArray();

            Assert.Equal(new[] {"PRINT", "PRINT"}, words.Select(token => token.Text).ToArray());
            Assert.Equal(2, words[1].Line);
        }

        [Theory]
        [InlineData("1", 1d)]
        [InlineData("42", 42d)]
        [InlineData("3.5", 3.5d)]
        [InlineData(".5", 0.5d)]
        [InlineData("1E3", 1000d)]
        [InlineData("1.5E-2", 0.015d)]
        [InlineData("2D2", 200d)]
        public void NumbersAreReadInEveryShapeBasicWritesThem(string source, double expected)
        {
            var tokens = Meaningful(source);

            Assert.Single(tokens);
            Assert.Equal(BasicTokenKindEnum.Number, tokens[0].Kind);
            Assert.Equal(expected, tokens[0].Number, 10);
        }

        [Theory]
        [InlineData("&HFF", 255d)]
        [InlineData("&H10", 16d)]
        [InlineData("&O17", 15d)]
        public void HexadecimalAndOctalAreReadBecauseRealProgramsUseThemForColours(string source, double expected)
        {
            var tokens = Meaningful(source);

            Assert.Equal(expected, tokens[0].Number, 10);
        }

        [Fact]
        public void ATypeSuffixIsDroppedFromANumberButTheDollarStaysOnAName()
        {
            // A$ and A really are two different variables, so losing the dollar would merge them; the numeric
            // suffixes select a precision this interpreter deliberately does not have, so they go.
            Assert.Equal(new[] {"A$"}, Words("A$"));
            Assert.Equal(new[] {"COUNT"}, Words("COUNT%"));
            Assert.Equal(new[] {"TOTAL"}, Words("TOTAL#"));

            var number = Meaningful("100%")[0];
            Assert.Equal(100d, number.Number, 10);
        }

        [Fact]
        public void TwoCharacterOperatorsBeatTheirHalves()
        {
            var symbols = Meaningful("A <= B >= C <> D")
                .Where(token => token.Kind == BasicTokenKindEnum.Symbol)
                .Select(token => token.Text)
                .ToArray();

            Assert.Equal(new[] {"<=", ">=", "<>"}, symbols);
        }

        [Fact]
        public void TheReversedSpellingsOfThoseOperatorsAreNormalised()
        {
            // Plenty of old listings write =< and =>, and normalising here means nothing above has to know.
            var symbols = Meaningful("A =< B => C")
                .Where(token => token.Kind == BasicTokenKindEnum.Symbol)
                .Select(token => token.Text)
                .ToArray();

            Assert.Equal(new[] {"<=", ">="}, symbols);
        }

        [Fact]
        public void EmptySourceIsAProgramWithNothingInIt()
        {
            var tokens = BasicLexer.Tokenize(string.Empty);

            Assert.Equal(BasicTokenKindEnum.EndOfFile, tokens[tokens.Count - 1].Kind);
            Assert.Empty(Meaningful(string.Empty));
            Assert.Empty(Meaningful(null));
        }
    }
}
