using System;
using System.Globalization;
using System.Text.RegularExpressions;
using WolfCurses.Apps.Tests.Support;
using Xunit;

namespace WolfCurses.Apps.Tests
{
    /// <summary>
    ///     The word processor as a person meets it: keys in, frames out.
    ///     <para>
    ///         Assertions are about the editor's own status line rather than about words in the document. That is
    ///         partly discipline (the sample files are fixtures, and a test that quotes one breaks when the fixture
    ///         is swapped) and partly the only honest option: what is being tested is where the caret is and what the
    ///         document did, which the status line states outright and the text does not.
    ///     </para>
    /// </summary>
    [Collection("AppsApp")]
    public class WordProcessorTests
    {
        private static DrivenAppsApp OpenEditor()
        {
            var suite = new DrivenAppsApp();
            suite.ChooseMenuItem((int) AppsCommandsEnum.WordProcessor);
            return suite;
        }

        /// <summary>The line count the status line reports, which is also proof a document was really loaded.</summary>
        private static int ReportedLineCount(string screen)
        {
            var match = Regex.Match(screen, @"(\d+) lines");
            Assert.True(match.Success, "the status line did not report a line count:\n" + screen);
            return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        [Fact]
        public void ItOpensOnTheDefaultDocumentWithTheCaretAtTheStart()
        {
            using var suite = OpenEditor();

            Assert.Contains("rfc1149.txt", suite.Screen, StringComparison.Ordinal);
            Assert.Contains("Ln 1, Col 1", suite.Screen, StringComparison.Ordinal);

            // A real file rather than the "could not open" path, which is the failure this would otherwise pass
            // straight through: an empty buffer is one line.
            Assert.True(ReportedLineCount(suite.Screen) > 100,
                "the sample document does not look like it was loaded:\n" + suite.Describe());
        }

        /// <summary>The caret position the status line reports, one-based exactly as it is shown.</summary>
        private static (int Line, int Column) ReportedCaret(string screen)
        {
            var match = Regex.Match(screen, @"Ln (\d+), Col (\d+)");
            Assert.True(match.Success, "the status line did not report a caret position:\n" + screen);

            return (int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
                int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture));
        }

        [Fact]
        public void TheArrowKeysMoveTheCaretAndTheStatusLineSaysWhereItIs()
        {
            // Asserted as movement rather than as a named cell, because the fixture's shape is not this test's
            // business: the RFC opens with blank lines, so RIGHT on line two steps to the next line instead of to
            // column two, and a test that named a coordinate would be asserting the sample file rather than the
            // editor. The property is that the caret goes down a line, and then strictly forward in reading order.
            using var suite = OpenEditor();
            Assert.Equal((1, 1), ReportedCaret(suite.Screen));

            suite.Press(ConsoleKey.DownArrow);
            Assert.Equal(2, ReportedCaret(suite.Screen).Line);

            var before = ReportedCaret(suite.Screen);
            suite.Press(ConsoleKey.RightArrow);
            var after = ReportedCaret(suite.Screen);

            Assert.True(after.Line > before.Line || (after.Line == before.Line && after.Column > before.Column),
                $"RIGHT moved the caret from {before} to {after}, which is not forwards");
        }

        [Fact]
        public void TheCaretNeverRunsOffTheStartOrTheEndOfTheDocument()
        {
            // Walking into either end is the ordinary case, not an edge case, and clamping there is what stops the
            // renderer being handed a position that does not exist.
            using var suite = OpenEditor();

            suite.Press(ConsoleKey.LeftArrow);
            suite.Press(ConsoleKey.UpArrow);
            Assert.Equal((1, 1), ReportedCaret(suite.Screen));

            suite.Press(ConsoleKey.End, ConsoleModifiers.Control);
            var end = ReportedCaret(suite.Screen);

            suite.Press(ConsoleKey.RightArrow);
            suite.Press(ConsoleKey.DownArrow);
            Assert.Equal(end, ReportedCaret(suite.Screen));
        }

        [Fact]
        public void BackspaceReachesTheEditorAtAll()
        {
            // The single most important test in this file. ENTER and BACKSPACE are input-buffer control in this
            // library and reach no key handler; IForm.EditsText is what redirects them here, and without it a
            // backspace in a text editor does nothing whatsoever.
            using var suite = OpenEditor();

            suite.PressChar('X', ConsoleKey.X);
            Assert.Contains("Ln 1, Col 2", suite.Screen, StringComparison.Ordinal);

            suite.Press(ConsoleKey.Backspace);
            Assert.Contains("Ln 1, Col 1", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void ATabMovesTheCaretToTheNextStopRatherThanOneColumn()
        {
            // End to end proof that the document and the screen agree about where the caret is. A tab is one
            // character to the buffer and eight columns to the terminal, and the status line reports the column a
            // person can see, so this is 9 rather than 2.
            using var suite = OpenEditor();
            Assert.Equal((1, 1), ReportedCaret(suite.Screen));

            suite.Press(ConsoleKey.Tab);
            Assert.Equal((1, 9), ReportedCaret(suite.Screen));

            suite.Press(ConsoleKey.Tab);
            Assert.Equal((1, 17), ReportedCaret(suite.Screen));
        }

        [Fact]
        public void BackspaceRemovesAWholeTabInOnePress()
        {
            // The other half of a tab being one character: rubbing it out is one press, not eight.
            using var suite = OpenEditor();
            suite.Press(ConsoleKey.Tab);
            Assert.Equal((1, 9), ReportedCaret(suite.Screen));

            suite.Press(ConsoleKey.Backspace);

            Assert.Equal((1, 1), ReportedCaret(suite.Screen));
        }

        [Fact]
        public void TextAfterATabIsDrawnAtTheColumnTheCaretReports()
        {
            // The failure this guards against is the renderer expanding the line but leaving the highlight where the
            // character index said, which puts the block cursor several columns off on any indented line. Typing a
            // letter after a tab must draw it exactly where the status line claims the caret is.
            using var suite = OpenEditor();
            suite.Press(ConsoleKey.Tab);
            suite.PressChar('Z', ConsoleKey.Z);

            var caret = ReportedCaret(suite.Screen);
            Assert.Equal(10, caret.Column);

            var row = FirstRowContaining(suite.Screen, 'Z');
            Assert.Equal(8, row.IndexOf('Z', StringComparison.Ordinal));
        }

        /// <summary>The first rendered row containing a character, for asserting where something was drawn.</summary>
        private static string FirstRowContaining(string screen, char character)
        {
            foreach (var row in screen.Split('\n'))
            {
                if (row.IndexOf(character) >= 0)
                    return row.TrimEnd('\r');
            }

            Assert.Fail($"no rendered row contained '{character}':\n{screen}");
            return null;
        }

        [Fact]
        public void TypingMarksTheDocumentModified()
        {
            using var suite = OpenEditor();
            Assert.DoesNotContain("rfc1149.txt *", suite.Screen, StringComparison.Ordinal);

            suite.PressChar('X', ConsoleKey.X);

            Assert.Contains("rfc1149.txt *", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void EnterSplitsTheLineRatherThanSubmittingACommand()
        {
            using var suite = OpenEditor();
            var before = ReportedLineCount(suite.Screen);

            suite.Press(ConsoleKey.Enter);

            Assert.Equal(before + 1, ReportedLineCount(suite.Screen));
            Assert.Contains("Ln 2, Col 1", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void ShiftAndAnArrowSelectsAndTheStatusSaysHowMuch()
        {
            using var suite = OpenEditor();
            Assert.DoesNotContain("selected", suite.Screen, StringComparison.Ordinal);

            suite.Press(ConsoleKey.RightArrow, ConsoleModifiers.Shift);
            suite.Press(ConsoleKey.RightArrow, ConsoleModifiers.Shift);

            Assert.Contains("2 selected", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void PageDownScrollsTheDocumentRatherThanOnlyMovingTheCaret()
        {
            using var suite = OpenEditor();
            var firstScreen = suite.ScreenBelowStatusLine;

            suite.Press(ConsoleKey.PageDown);

            Assert.NotEqual(firstScreen, suite.ScreenBelowStatusLine);
            Assert.DoesNotContain("Ln 1, Col 1", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void ControlEndReachesTheBottomOfALongDocumentInOneStep()
        {
            using var suite = OpenEditor();
            var lines = ReportedLineCount(suite.Screen);

            suite.Press(ConsoleKey.End, ConsoleModifiers.Control);

            Assert.Contains($"Ln {lines},", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void TypingDoesNotLeakIntoThePromptUnderneath()
        {
            // InputFillsBuffer is false, so the characters go into the document and nowhere else. Without it the
            // prompt fills up with a copy of whatever is being written.
            using var suite = OpenEditor();

            suite.PressChar('h', ConsoleKey.H);
            suite.PressChar('i', ConsoleKey.I);

            Assert.DoesNotContain("ESC returns to the menu: hi", suite.Screen, StringComparison.Ordinal);
            Assert.Equal(string.Empty, suite.App.InputManager.InputBuffer);
        }

        [Fact]
        public void EscapeReturnsToTheMenuWithoutTheEditorHandlingIt()
        {
            // One override on AppsWindow backs every application out, which is why this form has no ESC handling of
            // its own at all.
            using var suite = OpenEditor();

            suite.Escape();

            Assert.Contains("Which application?", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void ReopeningTheEditorStartsFromTheFileAgain()
        {
            // A form is created fresh each time it is set, so an edited-then-abandoned document does not come back.
            using var suite = OpenEditor();
            suite.PressChar('X', ConsoleKey.X);
            Assert.Contains("rfc1149.txt *", suite.Screen, StringComparison.Ordinal);

            suite.Escape();
            suite.ChooseMenuItem((int) AppsCommandsEnum.WordProcessor);

            Assert.DoesNotContain("rfc1149.txt *", suite.Screen, StringComparison.Ordinal);
            Assert.Contains("Ln 1, Col 1", suite.Screen, StringComparison.Ordinal);
        }
    }
}
