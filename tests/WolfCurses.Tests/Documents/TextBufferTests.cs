using System;
using System.Linq;
using WolfCurses.Documents;
using Xunit;

namespace WolfCurses.Tests.Documents
{
    /// <summary>
    ///     The editable document model. What is pinned here is mostly the handful of things that separate a text
    ///     buffer that works from one that only looks like it does: the column vertical movement remembers, the line
    ///     ending a file keeps when it is saved back, and what a selection means when the caret is above its anchor
    ///     rather than below it.
    /// </summary>
    public class TextBufferTests
    {
        [Fact]
        public void SelectingARangeReplacesWhateverWasSelectedRatherThanStretchingIt()
        {
            // THE trap this method exists to avoid. MoveTo with an extend flag keeps whatever anchor is already
            // there, so selecting a search result that way would quietly stretch the previous selection over the
            // new one and every Find would select more of the document than the last.
            var buffer = TextBuffer.FromText("alpha beta gamma");

            buffer.Select(new TextPosition(0, 0), new TextPosition(0, 5));
            Assert.Equal("alpha", buffer.GetSelectedText(), StringComparer.Ordinal);

            buffer.Select(new TextPosition(0, 11), new TextPosition(0, 16));
            Assert.Equal("gamma", buffer.GetSelectedText(), StringComparer.Ordinal);
        }

        [Fact]
        public void SelectingLeavesTheCaretOnTheSecondPositionAndClampsBothInside()
        {
            var buffer = TextBuffer.FromText("one\ntwo");

            buffer.Select(new TextPosition(0, 1), new TextPosition(1, 2));

            Assert.Equal(new TextPosition(1, 2), buffer.Caret);
            Assert.True(buffer.HasSelection);

            buffer.Select(new TextPosition(-5, -5), new TextPosition(99, 99));

            Assert.Equal(buffer.EndPosition(), buffer.Caret);
            Assert.Equal(TextPosition.Start, buffer.SelectionStart);
        }

        [Fact]
        public void AnEmptyDocumentIsOneEmptyLineSoThereIsAlwaysSomewhereToBe()
        {
            // Not zero lines. Every method here indexes a line, and "no lines" would make the caret a position that
            // does not exist, which every caller would then have to special-case.
            var buffer = new TextBuffer();

            Assert.Equal(1, buffer.LineCount);
            Assert.Equal(string.Empty, buffer.GetLine(0));
            Assert.Equal(TextPosition.Start, buffer.Caret);
            Assert.False(buffer.HasSelection);
            Assert.False(buffer.IsModified);
        }

        [Theory]
        [InlineData("one\r\ntwo\r\nthree")]
        [InlineData("one\ntwo\nthree")]
        [InlineData("one\r\ntwo\r\n")]
        [InlineData("one\ntwo\n")]
        [InlineData("")]
        [InlineData("no line breaks at all")]
        public void LoadingAndSavingIsExactlyReversible(string text)
        {
            // The difference between an editor and a reformatter: open a file, save it untouched, get the same bytes.
            // A trailing newline is part of that, which is why a document ending in one has a final empty line.
            var buffer = TextBuffer.FromText(text);

            Assert.Equal(text, buffer.GetText());
        }

        [Fact]
        public void ATrailingNewlineIsAFinalEmptyLineRatherThanNothing()
        {
            var buffer = TextBuffer.FromText("one\ntwo\n");

            Assert.Equal(3, buffer.LineCount);
            Assert.Equal(string.Empty, buffer.GetLine(2));
        }

        [Theory]
        [InlineData("a\r\nb\r\nc", "\r\n")]
        [InlineData("a\nb\nc", "\n")]
        [InlineData("a\r\nb\r\nc\nd", "\r\n")] // one stray LF does not flip a CRLF document
        [InlineData("a\nb\nc\r\nd", "\n")] // and one stray CRLF does not flip an LF one
        public void TheLineEndingIsRememberedFromWhatDominatedTheFile(string text, string expected)
        {
            var buffer = TextBuffer.FromText(text);

            Assert.Equal(expected, buffer.NewLine);
        }

        [Fact]
        public void ADocumentWithNoLineBreaksTakesThePlatformEnding()
        {
            var buffer = TextBuffer.FromText("single line");

            Assert.Equal(Environment.NewLine, buffer.NewLine);
        }

        [Fact]
        public void VerticalMovementRemembersTheColumnItStartedIn()
        {
            // THE trap this type exists to get right. Walk down from column 8 through a short line and back up: a
            // buffer without a desired column comes to rest at column 2, having quietly forgotten where it was, and
            // vertical movement over ragged text stops being reversible.
            var buffer = TextBuffer.FromText("0123456789\nab\n0123456789");
            buffer.MoveTo(new TextPosition(0, 8));

            buffer.MoveDown();
            Assert.Equal(new TextPosition(1, 2), buffer.Caret); // clamped to the short line

            buffer.MoveDown();
            Assert.Equal(new TextPosition(2, 8), buffer.Caret); // and back out to where it started

            buffer.MoveUp();
            buffer.MoveUp();
            Assert.Equal(new TextPosition(0, 8), buffer.Caret);
        }

        [Fact]
        public void AHorizontalMoveReAimsTheRememberedColumn()
        {
            // The other half of the rule: the moment you move sideways on purpose, that is the column you meant.
            var buffer = TextBuffer.FromText("0123456789\nab\n0123456789");
            buffer.MoveTo(new TextPosition(0, 8));
            buffer.MoveLeft();

            buffer.MoveDown();
            buffer.MoveDown();

            Assert.Equal(new TextPosition(2, 7), buffer.Caret);
        }

        [Fact]
        public void AnEditReAimsTheRememberedColumnToo()
        {
            var buffer = TextBuffer.FromText("0123456789\nab\n0123456789");
            buffer.MoveTo(new TextPosition(0, 8));
            buffer.Insert('X'); // caret is now at column 9

            buffer.MoveDown();
            buffer.MoveDown();

            Assert.Equal(new TextPosition(2, 9), buffer.Caret);
        }

        [Fact]
        public void MovingLeftOffALineStepsOntoTheEndOfThePreviousOne()
        {
            var buffer = TextBuffer.FromText("abc\ndef");
            buffer.MoveTo(new TextPosition(1, 0));

            buffer.MoveLeft();

            Assert.Equal(new TextPosition(0, 3), buffer.Caret);
        }

        [Fact]
        public void MovingRightOffALineStepsOntoTheStartOfTheNextOne()
        {
            var buffer = TextBuffer.FromText("abc\ndef");
            buffer.MoveTo(new TextPosition(0, 3));

            buffer.MoveRight();

            Assert.Equal(new TextPosition(1, 0), buffer.Caret);
        }

        [Fact]
        public void MovementStopsAtTheEndsRatherThanWrappingRound()
        {
            var buffer = TextBuffer.FromText("abc");

            buffer.MoveLeft();
            Assert.Equal(TextPosition.Start, buffer.Caret);

            buffer.MoveToEnd();
            buffer.MoveRight();
            Assert.Equal(new TextPosition(0, 3), buffer.Caret);
        }

        [Fact]
        public void ASelectionReadsInDocumentOrderWhicheverEndTheCaretIsOn()
        {
            // Dragging upward puts the anchor after the caret. Everything downstream (delete, copy, highlight) wants
            // the pair in reading order, so the buffer sorts them rather than every caller remembering to.
            var buffer = TextBuffer.FromText("hello world");
            buffer.MoveTo(new TextPosition(0, 8));
            buffer.MoveTo(new TextPosition(0, 3), true);

            Assert.True(buffer.HasSelection);
            Assert.Equal(new TextPosition(0, 3), buffer.SelectionStart);
            Assert.Equal(new TextPosition(0, 8), buffer.SelectionEnd);
            Assert.Equal("lo wo", buffer.GetSelectedText());
        }

        [Fact]
        public void AMultiLineSelectionJoinsWithTheDocumentsOwnLineEnding()
        {
            var buffer = TextBuffer.FromText("one\r\ntwo\r\nthree");
            buffer.MoveTo(new TextPosition(0, 1));
            buffer.MoveTo(new TextPosition(2, 2), true);

            Assert.Equal("ne\r\ntwo\r\nth", buffer.GetSelectedText());
        }

        [Fact]
        public void AnAnchorOnTheCaretIsNotASelection()
        {
            // Shift-arrow back to where you started leaves an anchor equal to the caret; that is an empty selection,
            // which must read as no selection or every "is anything selected" test in a caller goes wrong.
            var buffer = TextBuffer.FromText("hello");
            buffer.MoveTo(new TextPosition(0, 2));
            buffer.MoveRight(true);
            buffer.MoveLeft(true);

            Assert.False(buffer.HasSelection);
            Assert.Equal(string.Empty, buffer.GetSelectedText());
        }

        [Fact]
        public void TypingOverASelectionReplacesIt()
        {
            var buffer = TextBuffer.FromText("hello world");
            buffer.MoveTo(new TextPosition(0, 6));
            buffer.MoveToLineEnd(true);

            buffer.Insert("there");

            Assert.Equal("hello there", buffer.GetText());
            Assert.False(buffer.HasSelection);
            Assert.Equal(new TextPosition(0, 11), buffer.Caret);
        }

        [Fact]
        public void PastingTextWithNewlinesInItSplicesInWholeLines()
        {
            var buffer = TextBuffer.FromText("start end");
            buffer.MoveTo(new TextPosition(0, 6));

            buffer.Insert("one\ntwo\nthree ");

            // One line, plus the two line breaks in the pasted text, is three lines.
            Assert.Equal(3, buffer.LineCount);
            Assert.Equal("start one", buffer.GetLine(0));
            Assert.Equal("two", buffer.GetLine(1));
            Assert.Equal("three end", buffer.GetLine(2));
            Assert.Equal(new TextPosition(2, 6), buffer.Caret);
        }

        [Fact]
        public void EnterSplitsTheLineAtTheCaret()
        {
            var buffer = TextBuffer.FromText("abcdef");
            buffer.MoveTo(new TextPosition(0, 3));

            buffer.InsertNewLine();

            Assert.Equal(2, buffer.LineCount);
            Assert.Equal("abc", buffer.GetLine(0));
            Assert.Equal("def", buffer.GetLine(1));
            Assert.Equal(new TextPosition(1, 0), buffer.Caret);
        }

        [Fact]
        public void BackspaceAtALineStartJoinsOntoThePreviousLine()
        {
            var buffer = TextBuffer.FromText("abc\ndef");
            buffer.MoveTo(new TextPosition(1, 0));

            buffer.Backspace();

            Assert.Equal(1, buffer.LineCount);
            Assert.Equal("abcdef", buffer.GetLine(0));
            Assert.Equal(new TextPosition(0, 3), buffer.Caret);
        }

        [Fact]
        public void DeleteAtALineEndPullsTheNextLineUp()
        {
            var buffer = TextBuffer.FromText("abc\ndef");
            buffer.MoveTo(new TextPosition(0, 3));

            buffer.Delete();

            Assert.Equal(1, buffer.LineCount);
            Assert.Equal("abcdef", buffer.GetLine(0));
            Assert.Equal(new TextPosition(0, 3), buffer.Caret);
        }

        [Fact]
        public void BackspaceAndDeleteAtTheDocumentEndsDoNothing()
        {
            var buffer = TextBuffer.FromText("abc");

            buffer.MoveToStart();
            buffer.Backspace();
            buffer.MoveToEnd();
            buffer.Delete();

            Assert.Equal("abc", buffer.GetText());
        }

        [Fact]
        public void DeletingASelectionLeavesTheCaretWhereItBegan()
        {
            var buffer = TextBuffer.FromText("one\ntwo\nthree");
            buffer.MoveTo(new TextPosition(0, 1));
            buffer.MoveTo(new TextPosition(2, 2), true);

            buffer.DeleteSelection();

            Assert.Equal("oree", buffer.GetText());
            Assert.Equal(new TextPosition(0, 1), buffer.Caret);
            Assert.False(buffer.HasSelection);
        }

        [Fact]
        public void DoubleClickingAWordSelectsExactlyThatWord()
        {
            var buffer = TextBuffer.FromText("the quick brown fox");

            buffer.SelectWordAt(new TextPosition(0, 12)); // inside "brown"

            Assert.Equal("brown", buffer.GetSelectedText());
            Assert.Equal(new TextPosition(0, 10), buffer.SelectionStart);
            Assert.Equal(new TextPosition(0, 15), buffer.SelectionEnd);
        }

        [Fact]
        public void DoubleClickingASeparatorTakesOneCharacterRatherThanTheWhitespaceRun()
        {
            // Swallowing the run would mean double-clicking a gap selects the layout, which is a surprising amount
            // of document to delete by accident.
            var buffer = TextBuffer.FromText("a      b");

            buffer.SelectWordAt(new TextPosition(0, 3));

            Assert.Equal(" ", buffer.GetSelectedText());
        }

        [Fact]
        public void DoubleClickingPastTheEndOfALineTakesTheLastWordOnIt()
        {
            // Clicking in the empty space to the right of a line is the ordinary case, not an edge case: the caret
            // clamps to the line end, and a word must still be found there.
            var buffer = TextBuffer.FromText("hello world");

            buffer.SelectWordAt(new TextPosition(0, 40));

            Assert.Equal("world", buffer.GetSelectedText());
        }

        [Fact]
        public void DoubleClickingAnEmptyLineSelectsNothingAndDoesNotThrow()
        {
            var buffer = TextBuffer.FromText("one\n\ntwo");

            buffer.SelectWordAt(new TextPosition(1, 0));

            Assert.False(buffer.HasSelection);
            Assert.Equal(new TextPosition(1, 0), buffer.Caret);
        }

        [Fact]
        public void SelectingALineTakesItsLineBreakSoDeletingItClosesTheGap()
        {
            // Stopping at the line's end would leave an empty line behind, and "select line, delete" is exactly the
            // operation where that shows up as the feature not working.
            var buffer = TextBuffer.FromText("one\ntwo\nthree");

            buffer.SelectLine(1);
            buffer.DeleteSelection();

            Assert.Equal("one\nthree", buffer.GetText());
        }

        [Fact]
        public void SelectingTheLastLineHasNoLineBreakToTake()
        {
            var buffer = TextBuffer.FromText("one\ntwo");

            buffer.SelectLine(1);

            Assert.Equal("two", buffer.GetSelectedText());
        }

        [Fact]
        public void WordMovementWalksWordToWordRatherThanStallingOnTheGaps()
        {
            var buffer = TextBuffer.FromText("the quick  brown");

            buffer.MoveWordRight();
            Assert.Equal(new TextPosition(0, 4), buffer.Caret); // start of "quick"

            buffer.MoveWordRight();
            Assert.Equal(new TextPosition(0, 11), buffer.Caret); // start of "brown", past two spaces

            buffer.MoveWordLeft();
            Assert.Equal(new TextPosition(0, 4), buffer.Caret);

            buffer.MoveWordLeft();
            Assert.Equal(new TextPosition(0, 0), buffer.Caret);
        }

        [Fact]
        public void WordMovementCrossesLinesAtTheEnds()
        {
            var buffer = TextBuffer.FromText("abc\ndef");

            buffer.MoveTo(new TextPosition(0, 3));
            buffer.MoveWordRight();
            Assert.Equal(new TextPosition(1, 0), buffer.Caret);

            buffer.MoveWordLeft();
            Assert.Equal(new TextPosition(0, 3), buffer.Caret);
        }

        [Fact]
        public void SelectAllCoversTheDocumentAndLeavesTheCaretAtItsEnd()
        {
            var buffer = TextBuffer.FromText("one\ntwo");

            buffer.SelectAll();

            Assert.Equal("one\ntwo", buffer.GetSelectedText());
            Assert.Equal(buffer.EndPosition(), buffer.Caret);
        }

        [Fact]
        public void ThePositionsAGivenAreAlwaysPulledInsideTheDocument()
        {
            var buffer = TextBuffer.FromText("ab\ncdef");

            Assert.Equal(new TextPosition(1, 4), buffer.Clamp(new TextPosition(99, 99)));
            Assert.Equal(TextPosition.Start, buffer.Clamp(new TextPosition(-5, -5)));
            Assert.Equal(new TextPosition(0, 2), buffer.Clamp(new TextPosition(0, 40)));
        }

        [Fact]
        public void ModifiedTracksEditsAndNotMovement()
        {
            var buffer = TextBuffer.FromText("hello");
            Assert.False(buffer.IsModified);

            buffer.MoveToEnd();
            buffer.SelectAll();
            Assert.False(buffer.IsModified);

            buffer.Insert('!');
            Assert.True(buffer.IsModified);

            buffer.MarkSaved();
            Assert.False(buffer.IsModified);
        }

        [Fact]
        public void ReloadingClearsEverythingIncludingTheModifiedFlag()
        {
            var buffer = TextBuffer.FromText("hello");
            buffer.SelectAll();
            buffer.Insert("changed");

            buffer.SetText("fresh\ncontent");

            Assert.Equal(TextPosition.Start, buffer.Caret);
            Assert.False(buffer.HasSelection);
            Assert.False(buffer.IsModified);
            Assert.Equal("fresh\ncontent", buffer.GetText());
        }

        [Fact]
        public void InsertingNothingIsNotAnEdit()
        {
            var buffer = TextBuffer.FromText("hello");

            buffer.Insert(string.Empty);
            buffer.Insert((string) null);

            Assert.False(buffer.IsModified);
            Assert.Equal("hello", buffer.GetText());
        }

        [Fact]
        public void ALargeDocumentIsNotQuadraticToBuildOrRead()
        {
            // The shipped sample is 4,550 lines and a person will open bigger ones. This is not a benchmark, it is a
            // guard against the accidentally quadratic: at 20,000 lines an O(n^2) load takes long enough that a
            // generous wall-clock bound catches it while staying far away from flaking on a slow machine.
            var text = string.Join("\n", Enumerable.Range(0, 20_000).Select(i => $"line {i} of the document"));

            var clock = System.Diagnostics.Stopwatch.StartNew();
            var buffer = TextBuffer.FromText(text);
            var roundTripped = buffer.GetText();
            var elapsed = clock.Elapsed;

            Assert.Equal(20_000, buffer.LineCount);
            Assert.Equal(text, roundTripped);
            Assert.True(elapsed < TimeSpan.FromSeconds(5),
                $"loading and saving 20,000 lines took {elapsed.TotalSeconds:F1}s, which suggests something quadratic");
        }
    }
}
