using System;
using WolfCurses.Documents;
using Xunit;

namespace WolfCurses.Tests.Documents
{
    /// <summary>
    ///     Finding text in a document. Most of these are about <i>where a search starts</i> rather than about
    ///     whether it can match, because that is where a Find Next either works or repeats itself forever.
    /// </summary>
    public class TextSearchTests
    {
        private static string[] Lines(params string[] lines)
        {
            return lines;
        }

        [Fact]
        public void ForwardFindsTheFirstMatchAtOrAfterWhereItWasToldToStart()
        {
            var lines = Lines("one two one");

            Assert.Equal(new TextPosition(0, 0), TextSearch.Next(lines, "one", TextPosition.Start));

            // From inside the first match, the first one is behind it and the second is the answer.
            Assert.Equal(new TextPosition(0, 8), TextSearch.Next(lines, "one", new TextPosition(0, 1)));
        }

        [Fact]
        public void ForwardFromTheEndOfAMatchIsWhatStopsFindNextRepeatingItself()
        {
            // THE trap. A caller that searches from the caret while the caret sits on a match finds that same match
            // every time, and Find Next appears to do nothing at all. The rule that makes it work is that forward
            // means "at or after", so the caller resumes from the far end of what it just found.
            var lines = Lines("aa bb aa");

            var first = TextSearch.Next(lines, "aa", TextPosition.Start);
            Assert.Equal(new TextPosition(0, 0), first);

            var afterFirst = new TextPosition(first.Value.Line, first.Value.Column + 2);
            Assert.Equal(new TextPosition(0, 6), TextSearch.Next(lines, "aa", afterFirst));
        }

        [Fact]
        public void BackwardMeansStrictlyBeforeSoItIsTheMirrorOfForward()
        {
            // Asymmetric on purpose. Backward from the START of the current match has to skip it, where forward
            // from its END has already passed it; the two rules together are what make the pair of keys work.
            var lines = Lines("aa bb aa");

            Assert.Equal(new TextPosition(0, 0),
                TextSearch.Next(lines, "aa", new TextPosition(0, 6), backwards: true));

            // From the very start there is nothing before it, so it wraps to the last one instead.
            Assert.Equal(new TextPosition(0, 6),
                TextSearch.Next(lines, "aa", TextPosition.Start, backwards: true));
        }

        [Fact]
        public void ASingleOccurrenceIsFoundAgainAfterWrappingRatherThanLost()
        {
            // The other half of Find Next. Having passed the only match, wrapping has to come back round to it, or
            // pressing the key twice on a document with one hit says it cannot be found.
            var lines = Lines("first", "needle", "last");

            var hit = TextSearch.Next(lines, "needle", new TextPosition(2, 0));

            Assert.Equal(new TextPosition(1, 0), hit);
        }

        [Fact]
        public void WithoutWrappingItStopsAtTheEndInsteadOfComingRound()
        {
            var lines = Lines("needle", "nothing");

            Assert.Null(TextSearch.Next(lines, "needle", new TextPosition(1, 0), wrap: false));
            Assert.Equal(new TextPosition(0, 0), TextSearch.Next(lines, "needle", new TextPosition(1, 0)));
        }

        [Fact]
        public void ANeedleThatIsNotThereReturnsNullRatherThanSearchingForever()
        {
            // Wrapping is one extra pass and not a loop. If this ever regresses it hangs the suite rather than
            // failing it, which is the honest way round for a test about termination.
            var lines = Lines("aaa", "bbb", "ccc");

            Assert.Null(TextSearch.Next(lines, "zzz", new TextPosition(1, 1)));
            Assert.Null(TextSearch.Next(lines, "zzz", new TextPosition(1, 1), backwards: true));
        }

        [Fact]
        public void CaseIsIgnoredUntilItIsAskedFor()
        {
            var lines = Lines("The Wolf");

            Assert.Equal(new TextPosition(0, 4), TextSearch.Next(lines, "wolf", TextPosition.Start));
            Assert.Null(TextSearch.Next(lines, "wolf", TextPosition.Start, matchCase: true));
            Assert.Equal(new TextPosition(0, 4), TextSearch.Next(lines, "Wolf", TextPosition.Start, matchCase: true));
        }

        [Fact]
        public void WholeWordRefusesAMatchWithAWordCharacterAgainstEitherEnd()
        {
            var lines = Lines("cat concat cats cat.");

            // Every "cat" in the line except the two standing on their own is glued to something.
            Assert.Equal(new TextPosition(0, 0), TextSearch.Next(lines, "cat", TextPosition.Start, wholeWord: true));
            Assert.Equal(new TextPosition(0, 16),
                TextSearch.Next(lines, "cat", new TextPosition(0, 1), wholeWord: true));

            // A full stop is not a word character, so the last one counts and the run inside "concat" does not.
            Assert.Equal(4, TextSearch.Count(lines, "cat"));
            Assert.Equal(2, TextSearch.Count(lines, "cat", wholeWord: true));
        }

        [Fact]
        public void AWholeWordSearchStillLooksPastAGluedMatchOnTheSameLine()
        {
            // The bug a naive whole-word check has: reject the match and stop, rather than reject it and carry on
            // from the next column. Here the only real match sits after one that has to be refused.
            var lines = Lines("concat cat");

            Assert.Equal(new TextPosition(0, 7), TextSearch.Next(lines, "cat", TextPosition.Start, wholeWord: true));
        }

        [Fact]
        public void ANeedleSpanningALineBreakFindsNothing()
        {
            // Documented rather than supported. Nothing here joins lines, and a Find box is not where anybody asks
            // for that; saying so is better than a search that silently never matches for a reason nobody can see.
            var lines = Lines("one", "two");

            Assert.Null(TextSearch.Next(lines, "one\ntwo", TextPosition.Start));
        }

        [Fact]
        public void AnEmptyNeedleFindsNothingRatherThanEverything()
        {
            var lines = Lines("anything");

            Assert.Null(TextSearch.Next(lines, string.Empty, TextPosition.Start));
            Assert.Null(TextSearch.Next(lines, null, TextPosition.Start));
            Assert.Equal(0, TextSearch.Count(lines, string.Empty));
        }

        [Fact]
        public void CountingDoesNotCountOverlaps()
        {
            // Because replacing cannot produce them: a Replace All walks past what it just wrote, so a count that
            // included overlaps would promise more changes than the change could ever make.
            var lines = Lines("aaaa");

            Assert.Equal(2, TextSearch.Count(lines, "aa"));
        }

        [Fact]
        public void AStartingPositionOutsideTheDocumentIsPulledInsideIt()
        {
            // A caller holding a position from before an edit should get an answer rather than an exception.
            var lines = Lines("one", "two");

            Assert.Equal(new TextPosition(0, 0), TextSearch.Next(lines, "one", new TextPosition(99, 99)));
            Assert.Null(TextSearch.Next(Array.Empty<string>(), "one", TextPosition.Start));
            Assert.Null(TextSearch.Next(null, "one", TextPosition.Start));
        }

        [Fact]
        public void MatchesAreFoundAcrossLinesInTheDirectionOfTravel()
        {
            var lines = Lines("alpha", "beta", "alpha");

            Assert.Equal(new TextPosition(2, 0), TextSearch.Next(lines, "alpha", new TextPosition(1, 0)));
            Assert.Equal(new TextPosition(0, 0),
                TextSearch.Next(lines, "alpha", new TextPosition(1, 0), backwards: true));
        }
    }
}
