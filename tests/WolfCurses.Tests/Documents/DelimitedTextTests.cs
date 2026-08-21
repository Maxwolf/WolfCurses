using System.Collections.Generic;
using WolfCurses.Documents;
using Xunit;

namespace WolfCurses.Tests.Documents
{
    /// <summary>
    ///     Reading and writing delimited text.
    ///     <para>
    ///         Every test here is a shape that <c>line.Split(',')</c> gets wrong, which is the whole reason the type
    ///         exists. The quoted-newline one is the important one: it is not a bug in a line splitter so much as
    ///         proof that splitting into lines first cannot be made to work at all.
    ///     </para>
    /// </summary>
    public class DelimitedTextTests
    {
        [Fact]
        public void ItReadsPlainRows()
        {
            var rows = DelimitedText.Read("a,b,c\nd,e,f");

            Assert.Equal(2, rows.Count);
            Assert.Equal(new[] {"a", "b", "c"}, rows[0]);
            Assert.Equal(new[] {"d", "e", "f"}, rows[1]);
        }

        [Fact]
        public void AQuotedFieldMayContainTheDelimiter()
        {
            var rows = DelimitedText.Read("\"Wolf, Max\",42");

            Assert.Single(rows);
            Assert.Equal(new[] {"Wolf, Max", "42"}, rows[0]);
        }

        [Fact]
        public void ADoubledQuoteInsideAQuotedFieldIsOneQuote()
        {
            var rows = DelimitedText.Read("\"she said \"\"no\"\"\",b");

            Assert.Equal(new[] {"she said \"no\"", "b"}, rows[0]);
        }

        [Fact]
        public void AQuotedFieldMayContainALineBreak()
        {
            // The shape that makes a record and a line different things. Two rows, not three.
            var rows = DelimitedText.Read("a,\"one\ntwo\"\nb,c");

            Assert.Equal(2, rows.Count);
            Assert.Equal(new[] {"a", "one\ntwo"}, rows[0]);
            Assert.Equal(new[] {"b", "c"}, rows[1]);
        }

        [Fact]
        public void EveryLineEndingSeparatesRowsAndCarriageReturnLineFeedIsOnlyOne()
        {
            Assert.Equal(3, DelimitedText.Read("a\r\nb\r\nc").Count);
            Assert.Equal(3, DelimitedText.Read("a\nb\nc").Count);
            Assert.Equal(3, DelimitedText.Read("a\rb\rc").Count);
        }

        [Fact]
        public void ATrailingLineBreakDoesNotInventARow()
        {
            Assert.Equal(2, DelimitedText.Read("a,b\nc,d\n").Count);
            Assert.Equal(2, DelimitedText.Read("a,b\r\nc,d\r\n").Count);
        }

        [Fact]
        public void NothingAtAllIsNoRowsRatherThanOneEmptyOne()
        {
            Assert.Empty(DelimitedText.Read(null));
            Assert.Empty(DelimitedText.Read(string.Empty));
        }

        [Fact]
        public void RaggedRowsStayRagged()
        {
            var rows = DelimitedText.Read("a,b,c\nd\ne,f");

            Assert.Equal(3, rows[0].Count);
            Assert.Single(rows[1]);
            Assert.Equal(2, rows[2].Count);
        }

        [Fact]
        public void EmptyFieldsAreKeptInPlace()
        {
            var rows = DelimitedText.Read(",b,,d,");

            Assert.Equal(new[] {string.Empty, "b", string.Empty, "d", string.Empty}, rows[0]);
        }

        [Fact]
        public void NothingIsTrimmed()
        {
            // A leading space may be data, and a parser that helpfully removes it cannot be told to stop.
            var rows = DelimitedText.Read("  a  ,b");

            Assert.Equal("  a  ", rows[0][0]);
        }

        [Fact]
        public void AQuoteInTheMiddleOfAnUnquotedFieldIsJustAQuote()
        {
            // Inches and minutes are written this way. Refusing the file would be worse than reading it plainly.
            var rows = DelimitedText.Read("6\" pipe,b");

            Assert.Equal(new[] {"6\" pipe", "b"}, rows[0]);
        }

        [Fact]
        public void AnUnterminatedQuotedFieldRunsToTheEndRatherThanThrowing()
        {
            var rows = DelimitedText.Read("a,\"never closed");

            Assert.Equal(new[] {"a", "never closed"}, rows[0]);
        }

        [Fact]
        public void AnyDelimiterWorksAndTheCommaThenMeansNothing()
        {
            var rows = DelimitedText.Read("a,b\tc", '\t');

            Assert.Equal(new[] {"a,b", "c"}, rows[0]);
        }

        [Fact]
        public void WritingQuotesOnlyWhatHasToBeQuoted()
        {
            var written = DelimitedText.Write(new[] {new[] {"plain", "has,comma", "has\"quote", "has\nbreak"}},
                DelimitedText.DefaultDelimiter, "\n");

            // Hand-written absolute. Quoting everything would also round-trip, and would also be wrong: a file
            // where every field is quoted is not what anything else produces.
            Assert.Equal("plain,\"has,comma\",\"has\"\"quote\",\"has\nbreak\"\n", written);
        }

        [Fact]
        public void WritingEndsWithALineBreak()
        {
            Assert.Equal("a,b\n", DelimitedText.Write(new[] {new[] {"a", "b"}}, DelimitedText.DefaultDelimiter, "\n"));
            Assert.Equal(string.Empty, DelimitedText.Write(new List<string[]>()));
        }

        [Fact]
        public void WhatIsWrittenReadsBackTheSame()
        {
            // The property that matters, over the awkward shapes rather than the easy ones. Including the row of
            // one empty field, which is why writing ends with a line break at all.
            var original = new[]
            {
                new[] {"Month", "Income", "Note"},
                new[] {"Jan", "18400", "Wolf, Max said \"fine\""},
                new[] {string.Empty},
                new[] {"Feb", "0", "one\ntwo"},
                new[] {"  spaced  ", string.Empty, "6\" pipe"}
            };

            var rows = DelimitedText.Read(DelimitedText.Write(original, DelimitedText.DefaultDelimiter, "\n"));

            Assert.Equal(original.Length, rows.Count);

            for (var row = 0; row < original.Length; row++)
                Assert.Equal(original[row], rows[row]);
        }
    }
}
