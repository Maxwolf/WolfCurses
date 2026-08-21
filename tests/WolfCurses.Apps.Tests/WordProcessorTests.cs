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

            // Measured from the left edge of the field rather than of the screen, since the document sits inside a
            // frame now and its first column is one past the border. Anchoring to the border keeps this about the
            // tab arithmetic instead of about how much chrome happens to be drawn.
            var row = FirstRowContaining(suite.Screen, 'Z');
            var fieldStart = row.IndexOf('│', StringComparison.Ordinal) + 1;
            Assert.True(fieldStart > 0, "the document field's left edge was not drawn:\n" + row);

            Assert.Equal(8, row.IndexOf('Z', StringComparison.Ordinal) - fieldStart);
        }

        /// <summary>Which rendered row a character first appears on, for asserting that something moved.</summary>
        private static int IndexOfRowContaining(string screen, char character)
        {
            var rows = screen.Split('\n');
            for (var i = 0; i < rows.Length; i++)
            {
                if (rows[i].IndexOf(character) >= 0)
                    return i;
            }

            Assert.Fail($"no rendered row contained '{character}':\n{screen}");
            return -1;
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
        public void TheScreenIsLaidOutLikeTheDosEditor()
        {
            // Menu bar across the top with Help at the far right, and the file name notched into the top of a frame
            // around the field.
            using var suite = OpenEditor();
            var rows = suite.Screen.Split('\n');

            var bar = Array.Find(rows, row => row.Contains("File", StringComparison.Ordinal));
            Assert.NotNull(bar);
            Assert.Contains("Edit", bar, StringComparison.Ordinal);
            Assert.Contains("Search", bar, StringComparison.Ordinal);
            Assert.Contains("Options", bar, StringComparison.Ordinal);

            // Help is laid from the right edge, so it comes after every left-hand title on the row.
            Assert.True(bar.IndexOf("Help", StringComparison.Ordinal) >
                        bar.IndexOf("Options", StringComparison.Ordinal),
                "Help was not at the right-hand end of the bar: " + bar);

            var top = Array.Find(rows, row => row.Contains('┌', StringComparison.Ordinal));
            Assert.NotNull(top);
            Assert.Contains("rfc1149.txt", top, StringComparison.Ordinal);
        }

        [Fact]
        public void TheFieldHasAScrollBarShowingHowFarThroughTheDocumentItIs()
        {
            using var suite = OpenEditor();
            Assert.Contains('↑', suite.Screen);
            Assert.Contains('↓', suite.Screen);

            // Compared by row INDEX rather than by row text: the thumb really does move, but both ends of this
            // document are blank lines, so the two rows read identically and comparing their contents would pass
            // whether the thumb moved or not.
            var top = IndexOfRowContaining(suite.Screen, '█');

            suite.Press(ConsoleKey.End, ConsoleModifiers.Control);
            var bottom = IndexOfRowContaining(suite.Screen, '█');

            Assert.True(bottom > top, $"the scrollbar thumb did not move down: row {top} then row {bottom}");
        }

        [Fact]
        public void TheEditorStartsBelowTheSceneGraphsOwnHeaderRow()
        {
            // SceneGraph appends a window's text straight onto its status row with no separator, so a screen that
            // does not begin with a newline gets its first line printed on the end of "[ - ] - Window(1): ...".
            // That row cannot be replaced, so the whole editor has to sit under it, and every mouse row offset
            // counts from there.
            using var suite = OpenEditor();
            var rows = suite.Screen.Split('\n');

            Assert.Contains("WolfCurses Apps", rows[0], StringComparison.Ordinal);
            Assert.DoesNotContain("File", rows[0], StringComparison.Ordinal);

            // Row 1 is the menu bar, which is what MenuBar.BarRow is told and what a click is measured against.
            Assert.Contains("File", rows[1], StringComparison.Ordinal);
        }

        [Fact]
        public void F10OpensTheMenusWhereAltMayNotBeDelivered()
        {
            // ALT is not reliably reported as a modifier: terminals swallow it, send an escape prefix, or hand it to
            // the window manager. F10 is the traditional way in for exactly that reason and arrives everywhere.
            using var suite = OpenEditor();

            suite.Press(ConsoleKey.F10);
            Assert.Contains("New", suite.Screen, StringComparison.Ordinal);

            suite.Press(ConsoleKey.F10);
            Assert.DoesNotContain("New", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void ClickingAMenuTitleOpensItAndClickingAnEntryRunsIt()
        {
            using var suite = OpenEditor();
            var bar = suite.Screen.Split('\n')[1];

            suite.Click(1, bar.IndexOf("Options", StringComparison.Ordinal));
            Assert.Contains("Tab width 4", suite.Screen, StringComparison.Ordinal);

            // The panel's first entry is two rows below the bar: one for the panel's own top border.
            var rows = suite.Screen.Split('\n');
            var entryRow = Array.FindIndex(rows, row => row.Contains("Tab width 4", StringComparison.Ordinal));
            suite.Click(entryRow, rows[entryRow].IndexOf("Tab width 4", StringComparison.Ordinal));

            Assert.Contains("Tab width is now 4", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void ClickingInTheDocumentPutsTheCaretThere()
        {
            // Types its own line rather than clicking into the sample, whose shape is not this test's business: the
            // RFC's fifth line is blank, and a click past the end of a blank line correctly clamps to column one,
            // which would look like the hit test being broken.
            using var suite = OpenEditor();
            suite.Press(ConsoleKey.F10);
            suite.Press(ConsoleKey.Enter); // File > New
            Assert.Contains("Untitled", suite.Screen, StringComparison.Ordinal);

            foreach (var character in "abcdefghij")
                suite.PressChar(character, ConsoleKey.A);

            // Row 3 is the document's first line: the scene graph's header, this screen's leading newline, the menu
            // bar and the frame's top edge come first. Column 1 is just inside the frame's left edge.
            suite.Click(3, 1 + 3);

            Assert.Equal((1, 4), ReportedCaret(suite.Screen));
        }

        [Fact]
        public void ClickingPastTheEndOfAShortLineLandsAtItsEnd()
        {
            // The ordinary case, not an edge case: most of a document is shorter than the window is wide.
            using var suite = OpenEditor();
            suite.Press(ConsoleKey.F10);
            suite.Press(ConsoleKey.Enter);

            foreach (var character in "abc")
                suite.PressChar(character, ConsoleKey.A);

            suite.Click(3, 1 + 40);

            Assert.Equal((1, 4), ReportedCaret(suite.Screen));
        }

        [Fact]
        public void ClickingOutsideTheFieldLeavesTheCaretAlone()
        {
            using var suite = OpenEditor();
            var before = ReportedCaret(suite.Screen);

            suite.Click(0, 5); // the scene graph's own header row
            suite.Click(2, 5); // the frame's top edge

            Assert.Equal(before, ReportedCaret(suite.Screen));
        }

        /// <summary>
        ///     Which screen column the scrollbar is drawn in, found by its own glyphs rather than by measuring a
        ///     row: a row stripped of escapes still ends in a carriage return, so its length is two past the last
        ///     visible cell and anything derived from it clicks into empty space.
        /// </summary>
        private static int ScrollBarColumn(string screen)
        {
            foreach (var row in screen.Split('\n'))
            {
                var at = row.IndexOfAny(new[] {'█', '░', '↑', '↓'});
                if (at >= 0)
                    return at;
            }

            Assert.Fail("no scrollbar was drawn");
            return -1;
        }

        /// <summary>Which rendered row a phrase first appears on.</summary>
        private static int RowOf(string screen, string phrase)
        {
            var rows = screen.Split('\n');
            for (var i = 0; i < rows.Length; i++)
            {
                if (rows[i].Contains(phrase, StringComparison.Ordinal))
                    return i;
            }

            Assert.Fail($"no rendered row contained \"{phrase}\":\n{screen}");
            return -1;
        }

        [Fact]
        public void OpeningAMenuDoesNotMoveTheEditor()
        {
            // The reported bug. A panel drawn as extra rows shoves the whole field down the screen every time a menu
            // opens; drawn over the field, nothing below it moves at all.
            using var suite = OpenEditor();

            var titleBefore = RowOf(suite.Screen, "rfc1149.txt");
            var statusBefore = RowOf(suite.Screen, "lines");

            suite.Press(ConsoleKey.F10);

            Assert.Equal(titleBefore, RowOf(suite.Screen, "rfc1149.txt"));
            Assert.Equal(statusBefore, RowOf(suite.Screen, "lines"));
        }

        [Fact]
        public void AnOpenMenuIsDrawnOverTheDocumentRatherThanInsteadOfIt()
        {
            // The panel is narrow and the field is wide, so the text to the right of it must survive. Blanking whole
            // rows would keep the layout still and hide most of the page, which is not the fix.
            using var suite = OpenEditor();
            suite.Press(ConsoleKey.DownArrow);
            suite.Press(ConsoleKey.DownArrow);

            var withoutMenu = suite.Screen.Split('\n');
            suite.Press(ConsoleKey.F10);
            var withMenu = suite.Screen.Split('\n');

            // The row the panel's first entry lands on still ends the same way it did, because only the columns the
            // panel covers were replaced.
            var entryRow = RowOf(suite.Screen, "New");
            Assert.True(withoutMenu[entryRow].Length > 20, "the covered row was too short to prove anything");
            Assert.Equal(withoutMenu[entryRow][^12..], withMenu[entryRow][^12..]);
        }

        [Fact]
        public void OpenAndSaveCanBeChosenNow()
        {
            // They were drawn but disabled while there was nothing behind them, which reads as the menu being
            // broken rather than as the feature being unfinished.
            using var suite = OpenEditor();
            suite.Press(ConsoleKey.F10);

            suite.Press(ConsoleKey.DownArrow);
            Assert.Contains("Open...", suite.Screen, StringComparison.Ordinal);

            // The highlight really lands on it, which a disabled entry never allows.
            suite.Press(ConsoleKey.Enter);
            Assert.DoesNotContain("Ln 1, Col 1   115 lines", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void ClickingTheScrollBarArrowMovesThroughTheDocument()
        {
            using var suite = OpenEditor();
            var before = RowOf(suite.Screen, "█");

            var column = ScrollBarColumn(suite.Screen);

            // The track below the thumb pages down, so a few presses move a long way through the document.
            for (var press = 0; press < 3; press++)
                suite.Click(3 + 12, column);

            Assert.True(RowOf(suite.Screen, "█") > before, "the thumb did not move down");

            // And the caret stays exactly where it was. Scrolling the view is not moving the cursor, which is the
            // distinction the resize housekeeping used to lose: revealing the caret on every tick dragged the
            // document straight back and made the scrollbar look like it did nothing.
            Assert.Equal((1, 1), ReportedCaret(suite.Screen));
        }

        [Fact]
        public void DraggingAcrossTheDocumentSweepsASelection()
        {
            // The whole point of the release and move events. A press drops the anchor, the moves drag the other
            // end behind the pointer, and the release lets go; none of that is expressible in presses alone.
            using var suite = OpenEditor();
            suite.Press(ConsoleKey.F10);
            suite.Press(ConsoleKey.Enter); // File > New, so the line is this test's own

            foreach (var character in "abcdefghij")
                suite.PressChar(character, ConsoleKey.A);

            suite.Click(3, 1 + 2);
            Assert.DoesNotContain("selected", suite.Screen, StringComparison.Ordinal);

            suite.MoveMouse(3, 1 + 7, MouseButtonEnum.Left);

            Assert.Contains("5 selected", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void LettingGoEndsTheSweepSoLaterMovementDoesNotKeepSelecting()
        {
            // A drag that never ends is the failure mode of building this out of presses: without a release the
            // selection keeps growing every time the pointer passes over the window.
            using var suite = OpenEditor();
            suite.Press(ConsoleKey.F10);
            suite.Press(ConsoleKey.Enter);

            foreach (var character in "abcdefghij")
                suite.PressChar(character, ConsoleKey.A);

            suite.Drag(3, 1 + 1, 3, 1 + 4);
            var afterDrag = suite.Screen;

            suite.MoveMouse(3, 1 + 9, MouseButtonEnum.Left);

            Assert.Contains("3 selected", afterDrag, StringComparison.Ordinal);
            Assert.Contains("3 selected", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void AHoverMovesThePointerWithoutSelectingAnything()
        {
            using var suite = OpenEditor();

            suite.MoveMouse(5, 10);

            Assert.DoesNotContain("selected", suite.Screen, StringComparison.Ordinal);
            Assert.Equal((1, 1), ReportedCaret(suite.Screen));
        }

        [Fact]
        public void DraggingTheScrollBarThumbMovesThroughTheDocument()
        {
            using var suite = OpenEditor();
            var top = IndexOfRowContaining(suite.Screen, '█');

            // Take hold of the thumb where it is, carry it most of the way down, and let go.
            var column = ScrollBarColumn(suite.Screen);

            suite.Click(top, column);
            suite.MoveMouse(3 + 12, column, MouseButtonEnum.Left);
            suite.ReleaseMouse(3 + 12, column);

            Assert.True(IndexOfRowContaining(suite.Screen, '█') > top, "the thumb did not follow the drag");
            Assert.Equal((1, 1), ReportedCaret(suite.Screen));
        }

        [Fact]
        public void AnOpenMenuHangsDirectlyFromTheBarRatherThanFloatingBelowIt()
        {
            // Row 1 is the bar and row 2 is the frame's top edge, so the panel's first choice belongs on row 3. An
            // earlier version started the panel a row lower and left the frame's border showing between the menu
            // and its own title, which read as the menu floating loose.
            using var suite = OpenEditor();

            suite.Press(ConsoleKey.F10);

            Assert.Equal(3, RowOf(suite.Screen, "New"));
        }

        [Fact]
        public void TheWheelScrollsTheDocumentAndLeavesTheCaretAlone()
        {
            // What a wheel means everywhere: you are looking somewhere else, not typing somewhere else.
            // Asserted on the text rather than on the scrollbar thumb: two notches is six lines of a hundred and
            // fifteen, which rounds to the same thumb cell. The thumb is a summary, and a summary is the wrong
            // thing to measure a small movement with.
            using var suite = OpenEditor();
            var before = suite.ScreenBelowStatusLine;

            suite.Wheel(10, 20, -2);

            Assert.NotEqual(before, suite.ScreenBelowStatusLine);
            Assert.Equal((1, 1), ReportedCaret(suite.Screen));
        }

        [Fact]
        public void TheWheelScrollsBackUpAndStopsAtTheTop()
        {
            using var suite = OpenEditor();

            suite.Wheel(10, 20, -5);
            var scrolled = IndexOfRowContaining(suite.Screen, '█');

            suite.Wheel(10, 20, 5);
            Assert.True(IndexOfRowContaining(suite.Screen, '█') < scrolled, "the wheel did not scroll back up");

            // And winding it further up parks at the start rather than running off into empty space.
            suite.Wheel(10, 20, 50);
            Assert.Equal(3 + 1, IndexOfRowContaining(suite.Screen, '█'));
        }

        [Fact]
        public void TheWheelNeverFiresWhateverAClickWouldHaveDone()
        {
            // The reason the wheel is its own kind rather than a button: a wheel record carries a button bit, so
            // anything treating it as a press acts on every scroll. Here that would move the caret.
            using var suite = OpenEditor();

            suite.Wheel(3 + 4, 1 + 6, -1);
            suite.Wheel(3 + 4, 1 + 6, 1);

            Assert.Equal((1, 1), ReportedCaret(suite.Screen));
            Assert.DoesNotContain("selected", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void EscapeShutsAnOpenMenuRatherThanLeavingTheEditor()
        {
            // The hand-off: AppsWindow claims ESC for every application, but asks the application first, so a menu
            // that is open is what gets dismissed. Without it, opening a menu and pressing ESC drops you out of the
            // program entirely.
            using var suite = OpenEditor();

            suite.Press(ConsoleKey.F, ConsoleModifiers.Alt);
            Assert.Contains("New", suite.Screen, StringComparison.Ordinal);

            suite.Escape();

            Assert.DoesNotContain("Which application?", suite.Screen, StringComparison.Ordinal);
            Assert.Contains("rfc1149.txt", suite.Screen, StringComparison.Ordinal);

            // And with nothing open it leaves, exactly as before.
            suite.Escape();
            Assert.Contains("Which application?", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void WhileAMenuIsOpenTypingDoesNotReachTheDocument()
        {
            using var suite = OpenEditor();
            suite.Press(ConsoleKey.F, ConsoleModifiers.Alt);

            suite.PressChar('X', ConsoleKey.X);

            Assert.DoesNotContain("rfc1149.txt *", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void AMenuItemRunsWhenItIsChosen()
        {
            // Options carries the two entries that do something without needing a file dialog, so this proves the
            // whole path: open, walk, choose, act.
            using var suite = OpenEditor();

            suite.Press(ConsoleKey.O, ConsoleModifiers.Alt);
            suite.Press(ConsoleKey.DownArrow);
            suite.Press(ConsoleKey.Enter);

            Assert.Contains("Tab width is now 8", suite.Screen, StringComparison.Ordinal);
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

        /// <summary>
        ///     A fresh empty document with known text in it. Sample files are fixtures and a test that quotes one
        ///     breaks when the fixture is swapped, so anything asserting about characters types its own.
        /// </summary>
        /// <param name="text">What to type into it.</param>
        /// <returns>The running suite.</returns>
        private static DrivenAppsApp EditorWithText(string text)
        {
            var suite = OpenEditor();

            // File > New through the menu rather than by reaching into the form, so the shortest way to a known
            // document is also a test that the menu still runs things.
            suite.Press(ConsoleKey.F, ConsoleModifiers.Alt);
            suite.Press(ConsoleKey.Enter);

            foreach (var character in text)
                suite.PressChar(character, ConsoleKey.NoName);

            return suite;
        }

        /// <summary>Selects the line the caret is on, which is the shortest honest way to have something selected.</summary>
        /// <param name="suite">The running suite.</param>
        private static void SelectTheLine(DrivenAppsApp suite)
        {
            suite.Press(ConsoleKey.Home);
            suite.Press(ConsoleKey.End, ConsoleModifiers.Shift);
        }

        [Fact]
        public void CuttingTakesTheSelectionAwayAndPastingBringsItBack()
        {
            using var suite = EditorWithText("hello");
            SelectTheLine(suite);

            suite.Press(ConsoleKey.X, ConsoleModifiers.Control);

            Assert.Contains("Cut 5 characters", suite.Screen, StringComparison.Ordinal);
            Assert.DoesNotContain("hello", suite.Screen, StringComparison.Ordinal);

            suite.Press(ConsoleKey.V, ConsoleModifiers.Control);

            Assert.Contains("Pasted 5 characters", suite.Screen, StringComparison.Ordinal);
            Assert.Contains("hello", suite.Screen, StringComparison.Ordinal);
            Assert.Contains("Ln 1, Col 6", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void CopyingLeavesTheDocumentAloneAndSaysSoOutLoud()
        {
            // The one edit with nothing to show for itself: without the status line a copy and a key that did
            // nothing at all look exactly alike, which is why it reports rather than staying silent.
            using var suite = EditorWithText("hello");
            SelectTheLine(suite);

            suite.Press(ConsoleKey.Insert, ConsoleModifiers.Control);

            Assert.Contains("Copied 5 characters", suite.Screen, StringComparison.Ordinal);
            Assert.Contains("hello", suite.Screen, StringComparison.Ordinal);

            suite.Press(ConsoleKey.End);
            suite.Press(ConsoleKey.V, ConsoleModifiers.Control);

            Assert.Contains("hellohello", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void PastingReplacesWhateverWasSelected()
        {
            using var suite = EditorWithText("hello");
            SelectTheLine(suite);
            suite.Press(ConsoleKey.Insert, ConsoleModifiers.Control);

            // The selection is still standing, so this pastes over itself: one copy of the word and not two.
            suite.Press(ConsoleKey.V, ConsoleModifiers.Control);

            Assert.Contains("hello", suite.Screen, StringComparison.Ordinal);
            Assert.DoesNotContain("hellohello", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void TheEditorsOwnDosClipboardKeysWorkAsWell()
        {
            // SHIFT+DEL and SHIFT+INS are what the editor this imitates used, and they cost nothing to keep: both
            // keys were otherwise unspoken for, and a person who learned them in 1991 still has them.
            using var suite = EditorWithText("abc");
            SelectTheLine(suite);

            suite.Press(ConsoleKey.Delete, ConsoleModifiers.Shift);
            Assert.Contains("Cut 3 characters", suite.Screen, StringComparison.Ordinal);

            suite.Press(ConsoleKey.Insert, ConsoleModifiers.Shift);
            Assert.Contains("Pasted 3 characters", suite.Screen, StringComparison.Ordinal);
            Assert.Contains("abc", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void AnUnshiftedDeleteStillDeletesRatherThanQuietlyCutting()
        {
            // Not about the order of those two cases, which the compiler settles on its own: putting plain DEL
            // ahead of the shifted one is CS8120 rather than a quiet bug. What this pins is that DEL reaches
            // Delete and not Cut, asserted by what the clipboard turns out to hold rather than by the absence of
            // a message, since "no message" is also what an unhandled key looks like.
            using var suite = EditorWithText("abc");
            suite.Press(ConsoleKey.Home);
            suite.Press(ConsoleKey.Delete);

            Assert.DoesNotContain("abc", suite.Screen, StringComparison.Ordinal);
            Assert.Contains("bc", suite.Screen, StringComparison.Ordinal);

            suite.Press(ConsoleKey.V, ConsoleModifiers.Control);
            Assert.DoesNotContain("Pasted", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void AMultipleLineCutIsCountedInLinesRatherThanCharacters()
        {
            using var suite = EditorWithText("a");
            suite.Press(ConsoleKey.Enter);
            suite.PressChar('b', ConsoleKey.NoName);

            suite.Press(ConsoleKey.A, ConsoleModifiers.Control);
            suite.Press(ConsoleKey.X, ConsoleModifiers.Control);

            Assert.Contains("Cut 2 lines", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void TheEditMenuOpensPastEverythingThatMeansNothingYet()
        {
            // Nothing selected and nothing on the clipboard, so Cut, Copy, Paste and Clear are all dead and the
            // cursor lands on the first entry that is not: Select All. Choosing it selects the document, which is
            // what proves the highlight really was down there rather than sitting on a dead line.
            using var suite = EditorWithText("hello");

            suite.Press(ConsoleKey.E, ConsoleModifiers.Alt);
            suite.Press(ConsoleKey.Enter);

            Assert.Contains("5 selected", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void TheEditMenuOpensOnCutOnceSomethingIsSelected()
        {
            // Same menu, same keys, a different answer, and nothing told it a selection had appeared. That is what
            // the entries declaring their own enablement buys.
            using var suite = EditorWithText("hello");
            SelectTheLine(suite);

            suite.Press(ConsoleKey.E, ConsoleModifiers.Alt);
            suite.Press(ConsoleKey.Enter);

            Assert.Contains("Cut 5 characters", suite.Screen, StringComparison.Ordinal);
            Assert.DoesNotContain("hello", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void TheClipboardOutlivesTheEditorThatFilledIt()
        {
            // Which is the entire reason it lives on the window's data rather than inside the form. A copy taken in
            // one application has to survive that application being closed before it can land in another, and the
            // window's data object is the only thing in the suite that outlives both.
            using var suite = EditorWithText("wolf");
            SelectTheLine(suite);
            suite.Press(ConsoleKey.Insert, ConsoleModifiers.Control);

            suite.Escape();
            suite.ChooseMenuItem((int) AppsCommandsEnum.WordProcessor);

            // A new form on a new document: the typed text is long gone.
            Assert.DoesNotContain("wolf", suite.Screen, StringComparison.Ordinal);

            suite.Press(ConsoleKey.V, ConsoleModifiers.Control);
            Assert.Contains("Pasted 4 characters", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void FindSelectsTheFirstMatchAndFindNextMovesOnToTheNextOne()
        {
            // The pair of them together, because the interesting half is not that a search can match: it is that
            // pressing the key again does not land on the same match forever.
            using var suite = EditorWithText("one two one");

            suite.Press(ConsoleKey.F, ConsoleModifiers.Control);
            suite.Type("one");

            Assert.Contains("3 selected", suite.Screen, StringComparison.Ordinal);
            Assert.Contains("Ln 1, Col 4", suite.Screen, StringComparison.Ordinal);

            suite.Press(ConsoleKey.F3);

            Assert.Contains("3 selected", suite.Screen, StringComparison.Ordinal);
            Assert.Contains("Ln 1, Col 12", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void FindNextComesRoundTheEndRatherThanStopping()
        {
            using var suite = EditorWithText("one two one");

            suite.Press(ConsoleKey.F, ConsoleModifiers.Control);
            suite.Type("one");
            suite.Press(ConsoleKey.F3);
            suite.Press(ConsoleKey.F3);

            // Back on the first one. Without wrapping this would say it could not be found, on a document that
            // visibly contains it twice.
            Assert.Contains("Ln 1, Col 4", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void FindPreviousWalksTheOtherWay()
        {
            using var suite = EditorWithText("one two one");

            suite.Press(ConsoleKey.F, ConsoleModifiers.Control);
            suite.Type("one");
            suite.Press(ConsoleKey.F3, ConsoleModifiers.Shift);

            // Nothing lies before the first match, so it wraps to the last.
            Assert.Contains("Ln 1, Col 12", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void ASearchThatMatchesNothingSaysSoAndLeavesTheCaretAlone()
        {
            using var suite = EditorWithText("one two one");
            suite.Press(ConsoleKey.Home);

            suite.Press(ConsoleKey.F, ConsoleModifiers.Control);
            suite.Type("zzz");

            Assert.Contains("Cannot find", suite.Screen, StringComparison.Ordinal);
            Assert.Contains("Ln 1, Col 1", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void ChangeAllReplacesEveryOccurrenceAndCountsThem()
        {
            using var suite = EditorWithText("one two one");

            suite.Press(ConsoleKey.H, ConsoleModifiers.Control);
            suite.Type("one");
            suite.Type("ONE");

            Assert.Contains("Changed 2 occurrences", suite.Screen, StringComparison.Ordinal);
            Assert.Contains("ONE two ONE", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void ChangeAllDoesNotFindWhatItJustWroteAndSoTerminates()
        {
            // Changing something into a longer version of itself is where a naive Replace All runs until the
            // machine gives out. It resumes past what it wrote, so this finishes with two changes and not more.
            using var suite = EditorWithText("aa");

            suite.Press(ConsoleKey.H, ConsoleModifiers.Control);
            suite.Type("a");
            suite.Type("aa");

            Assert.Contains("Changed 2 occurrences", suite.Screen, StringComparison.Ordinal);
            Assert.Contains("aaaa", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void TheOptionsMenuMarksWhichTabWidthIsInForceAndTheMarkMoves()
        {
            // What the check mark is for. Two entries offering a choice with no sign of which one is in force is a
            // menu you have to change the setting to read.
            using var suite = EditorWithText("x");

            suite.Press(ConsoleKey.O, ConsoleModifiers.Alt);

            Assert.Contains("\u221A Tab width 8", suite.Screen, StringComparison.Ordinal);
            Assert.DoesNotContain("\u221A Tab width 4", suite.Screen, StringComparison.Ordinal);

            // A menu opens on its FIRST entry rather than on the ticked one, which is what every menu does and is
            // worth knowing here: the highlight is already sitting on Tab width 4, so ENTER alone chooses it.
            suite.Press(ConsoleKey.Enter);
            suite.Press(ConsoleKey.O, ConsoleModifiers.Alt);

            Assert.Contains("\u221A Tab width 4", suite.Screen, StringComparison.Ordinal);
            Assert.DoesNotContain("\u221A Tab width 8", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void SavingWithF2AsksWhereToPutADocumentThatHasNeverBeenSaved()
        {
            // F2 has been printed beside Save in the File menu since the menu existed, with nothing answering it.
            // Asserted against an untitled document so the test opens a dialog rather than writing to the disk.
            using var suite = EditorWithText("x");

            suite.Press(ConsoleKey.F2);

            Assert.Contains("ENTER opens the highlighted", suite.Screen, StringComparison.Ordinal);
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
