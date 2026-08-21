using System;
using System.Text.RegularExpressions;
using WolfCurses.Apps.Tests.Support;
using WolfCurses.Graphics;
using Xunit;

namespace WolfCurses.Apps.Tests
{
    /// <summary>
    ///     The spreadsheet as a person meets it: keys in, frames out.
    ///     <para>
    ///         Where the cursor is comes from the entry line under the grid, which states it outright, rather than
    ///         from counting cells across a frame. That is the same discipline the editor's tests follow and for the
    ///         same reason: the layout is what several of these tests exist to protect, so nothing here may assume
    ///         it.
    ///     </para>
    /// </summary>
    [Collection("Suite")]
    public class SpreadsheetTests
    {
        /// <summary>The screen row the first row of cells is drawn on, and the columns cell A and B start at.</summary>
        private const int FirstGridRow = 4;

        private const int HeadingRow = 3;

        private const int ColumnAColumn = 6;

        private const int ColumnBColumn = 18;

        private static DrivenSuite OpenSpreadsheet()
        {
            var suite = new DrivenSuite();
            suite.ChooseMenuItem((int) OfficeCommandsEnum.Spreadsheet);

            return suite;
        }

        /// <summary>
        ///     Which cell the cursor is on, read off the entry line. Anchored to the start of the line, because the
        ///     sample's own instructions mention cell ranges and would otherwise match.
        /// </summary>
        private static string CursorCell(string screen)
        {
            var match = Regex.Match(screen, @"(?m)^ ([A-Z]+\d+): ");
            Assert.True(match.Success, "the entry line did not say which cell the cursor is on:\n" + screen);

            return match.Groups[1].Value;
        }

        /// <summary>What the entry line says is in the current cell, which is what was typed and not what it shows.</summary>
        private static string CellContents(string screen)
        {
            var match = Regex.Match(screen, @"(?m)^ [A-Z]+\d+: (.*)$");
            Assert.True(match.Success, "the entry line was not on the screen:\n" + screen);

            return match.Groups[1].Value.TrimEnd('\r', ' ');
        }

        /// <summary>
        ///     Jumps to a cell through the Go To dialog, the way a person would.
        ///     <para>
        ///         The backspaces are not padding. <c>TextInputDialog</c> pre-fills its buffer with the default
        ///         value, which here is the cell the cursor is already on, and that default is there to be accepted
        ///         with ENTER rather than typed over: typing without clearing it first appends, so asking for H2
        ///         from A1 submits "A1H2" and the dialog rightly refuses it.
        ///     </para>
        /// </summary>
        private static void GoToCell(DrivenSuite suite, string cell)
        {
            suite.Press(ConsoleKey.F8);

            for (var i = 0; i < 8; i++)
                suite.Press(ConsoleKey.Backspace);

            suite.Type(cell);
        }

        [Fact]
        public void ItOpensOnTheSampleSheetWithTheCursorAtTheTopLeft()
        {
            using var suite = OpenSpreadsheet();

            Assert.Contains("spreadsheet.csv", suite.Screen, StringComparison.Ordinal);
            Assert.Equal("A1", CursorCell(suite.Screen));
        }

        [Fact]
        public void TheColumnsAreLetteredAndTheRowsNumbered()
        {
            using var suite = OpenSpreadsheet();
            var rows = suite.Screen.Split('\n');

            // The headings are what tell somebody which cell a formula is talking about, so their absence is worth
            // catching outright rather than through a formula that then refers to nothing.
            Assert.Contains("A", rows[HeadingRow], StringComparison.Ordinal);
            Assert.Contains("B", rows[HeadingRow], StringComparison.Ordinal);
            Assert.Contains("1", rows[FirstGridRow], StringComparison.Ordinal);
        }

        [Fact]
        public void TheArrowKeysMoveTheCursor()
        {
            using var suite = OpenSpreadsheet();

            suite.Press(ConsoleKey.DownArrow);
            suite.Press(ConsoleKey.RightArrow);

            Assert.Equal("B2", CursorCell(suite.Screen));
        }

        [Fact]
        public void TheCursorStopsAtTheEdgesRatherThanWalkingOff()
        {
            using var suite = OpenSpreadsheet();

            suite.Press(ConsoleKey.UpArrow);
            suite.Press(ConsoleKey.LeftArrow);

            Assert.Equal("A1", CursorCell(suite.Screen));
        }

        [Fact]
        public void TypingIntoACellStoresItAndMovesDown()
        {
            using var suite = OpenSpreadsheet();

            // Somewhere the sample has nothing, so this is about what was typed rather than about the fixture.
            GoToCell(suite, "H2");

            suite.PressChar('4', ConsoleKey.D4);
            suite.PressChar('2', ConsoleKey.D2);
            suite.Press(ConsoleKey.Enter);

            Assert.Equal("H3", CursorCell(suite.Screen));

            suite.Press(ConsoleKey.UpArrow);
            Assert.Equal("42", CellContents(suite.Screen));
        }

        [Fact]
        public void AFormulaTypedIntoACellIsWorkedOut()
        {
            using var suite = OpenSpreadsheet();

            GoToCell(suite, "H2");

            foreach (var character in "=6*7")
                suite.PressChar(character, ConsoleKey.NoName);

            suite.Press(ConsoleKey.Enter);
            suite.Press(ConsoleKey.UpArrow);

            // The cell holds the formula and shows the answer, which is the whole distinction.
            Assert.Equal("=6*7", CellContents(suite.Screen));
            Assert.Contains("42", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void EscapeWhileTypingAbandonsTheEditAndStaysInTheApplication()
        {
            using var suite = OpenSpreadsheet();

            GoToCell(suite, "H2");

            suite.PressChar('9', ConsoleKey.D9);
            suite.Escape();

            // Still in the spreadsheet: ESC was spent on the edit rather than on leaving.
            Assert.Contains("spreadsheet.csv", suite.Screen, StringComparison.Ordinal);
            Assert.Equal(string.Empty, CellContents(suite.Screen));
        }

        [Fact]
        public void EscapeWithNothingOpenReturnsToTheSuiteMenu()
        {
            using var suite = OpenSpreadsheet();

            suite.Escape();

            Assert.Contains("Which application?", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void EditingKeepsWhatIsAlreadyThereAndTypingReplacesIt()
        {
            using var suite = OpenSpreadsheet();

            GoToCell(suite, "H2");

            suite.PressChar('1', ConsoleKey.D1);
            suite.PressChar('2', ConsoleKey.D2);
            suite.Press(ConsoleKey.Enter);
            suite.Press(ConsoleKey.UpArrow);

            // F2 keeps it, so a digit lands on the end.
            suite.Press(ConsoleKey.F2);
            suite.PressChar('3', ConsoleKey.D3);
            suite.Press(ConsoleKey.Enter);
            suite.Press(ConsoleKey.UpArrow);

            Assert.Equal("123", CellContents(suite.Screen));

            // Typing straight into the cell replaces it, which is the difference F2 exists for.
            suite.PressChar('7', ConsoleKey.D7);
            suite.Press(ConsoleKey.Enter);
            suite.Press(ConsoleKey.UpArrow);

            Assert.Equal("7", CellContents(suite.Screen));
        }

        [Fact]
        public void ShiftWithTheArrowsSweepsARangeAndABareArrowClearsIt()
        {
            using var suite = OpenSpreadsheet();

            suite.Press(ConsoleKey.DownArrow, ConsoleModifiers.Shift);
            suite.Press(ConsoleKey.RightArrow, ConsoleModifiers.Shift);

            Assert.Contains("2x2 selected", suite.Screen, StringComparison.Ordinal);

            suite.Press(ConsoleKey.DownArrow);
            Assert.DoesNotContain("selected", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void PageDownShowsRowsThatWereNotOnScreenBefore()
        {
            using var suite = OpenSpreadsheet();

            var before = suite.ScreenBelowStatusLine;
            suite.Press(ConsoleKey.PageDown);

            Assert.NotEqual(before, suite.ScreenBelowStatusLine);
            Assert.NotEqual("A1", CursorCell(suite.Screen));
        }

        [Fact]
        public void TheWheelScrollsTheViewAndLeavesTheCursorAlone()
        {
            using var suite = OpenSpreadsheet();

            var before = suite.ScreenBelowStatusLine;
            suite.Wheel(FirstGridRow, ColumnAColumn, -3);

            // Looking somewhere else is not typing somewhere else, which is what a wheel means everywhere.
            Assert.NotEqual(before, suite.ScreenBelowStatusLine);
            Assert.Equal("A1", CursorCell(suite.Screen));
        }

        [Fact]
        public void ClickingACellPutsTheCursorInIt()
        {
            using var suite = OpenSpreadsheet();

            suite.Click(FirstGridRow + 2, ColumnBColumn);

            Assert.Equal("B3", CursorCell(suite.Screen));
        }

        [Fact]
        public void DraggingSweepsARectangle()
        {
            using var suite = OpenSpreadsheet();

            suite.Drag(FirstGridRow, ColumnAColumn, FirstGridRow + 3, ColumnBColumn);

            // Four rows and two columns, which is what the corners say and is not expressible with presses alone.
            Assert.Contains("4x2 selected", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void ClickingAColumnLetterSelectsThatWholeColumn()
        {
            using var suite = OpenSpreadsheet();

            suite.Click(HeadingRow, ColumnBColumn);

            Assert.Contains("Selected column B", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void AChartCanBeDrawnOfTheSelectionAndAnyKeyPutsTheGridBack()
        {
            using var suite = OpenSpreadsheet();

            suite.Click(HeadingRow, ColumnBColumn);
            suite.Press(ConsoleKey.F6);

            Assert.Contains("Bar chart", suite.Screen, StringComparison.Ordinal);

            suite.Press(ConsoleKey.Spacebar);
            Assert.Contains("spreadsheet.csv", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void ALineGraphIsDrawnOfTheSameSelection()
        {
            using var suite = OpenSpreadsheet();

            suite.Click(HeadingRow, ColumnBColumn);
            suite.Press(ConsoleKey.F7);

            Assert.Contains("Line graph", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void EscapeShutsAChartRatherThanLeavingTheApplication()
        {
            using var suite = OpenSpreadsheet();

            suite.Press(ConsoleKey.F6);
            suite.Escape();

            Assert.Contains("spreadsheet.csv", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void OpeningAMenuDrawsItOverTheGridWithoutMovingAnything()
        {
            using var suite = OpenSpreadsheet();

            var rowsBefore = suite.Screen.Split('\n').Length;
            suite.Press(ConsoleKey.F10);

            var screen = suite.Screen;

            Assert.Contains("Save As...", screen, StringComparison.Ordinal);

            // Drawn over rather than appended: a panel that added rows would shove the sheet down the screen every
            // time a menu opened, which is the tell that a screen is being stacked rather than composited.
            Assert.Equal(rowsBefore, screen.Split('\n').Length);

            // And the row the panel covers is still exactly as wide as every other row.
            Assert.Equal("A1", CursorCell(screen));
        }

        [Fact]
        public void EscapeShutsAnOpenMenuRatherThanLeavingTheApplication()
        {
            using var suite = OpenSpreadsheet();

            suite.Press(ConsoleKey.F10);
            suite.Escape();

            Assert.DoesNotContain("Save As...", suite.Screen, StringComparison.Ordinal);
            Assert.Contains("spreadsheet.csv", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void WalkingOffTheRightHandEdgeScrollsTheColumnsAlong()
        {
            using var suite = OpenSpreadsheet();

            // Far enough right that the first columns have gone, which puts the cursor beyond the sheet's own data
            // and drags the merged banners at the top through the same slicing arithmetic.
            for (var i = 0; i < 12; i++)
                suite.Press(ConsoleKey.RightArrow);

            Assert.Equal("M1", CursorCell(suite.Screen));

            // Column A cannot still be the first heading if anything scrolled at all.
            var headings = suite.Screen.Split('\n')[HeadingRow];
            Assert.DoesNotContain(" A ", headings, StringComparison.Ordinal);

            AssertFits(suite.RawScreen);
        }

        [Fact]
        public void TheDataMenuWritesATotalUnderTheSelection()
        {
            using var suite = OpenSpreadsheet();

            GoToCell(suite, "H2");

            suite.PressChar('2', ConsoleKey.D2);
            suite.Press(ConsoleKey.Enter);
            suite.PressChar('3', ConsoleKey.D3);
            suite.Press(ConsoleKey.Enter);

            GoToCell(suite, "H2");
            suite.Press(ConsoleKey.DownArrow, ConsoleModifiers.Shift);

            // ALT and the menu's own letter, then down one entry to Total Selection.
            suite.Press(ConsoleKey.D, ConsoleModifiers.Alt);
            suite.Press(ConsoleKey.DownArrow);
            suite.Press(ConsoleKey.Enter);

            // It writes the formula rather than the answer, and leaves the cursor on it.
            Assert.Equal("H4", CursorCell(suite.Screen));
            Assert.Equal("=SUM(H2:H3)", CellContents(suite.Screen));
            Assert.Contains("5", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void TheWholeScreenFitsEightyByTwentyFour()
        {
            using var suite = OpenSpreadsheet();

            AssertFits(suite.RawScreen);

            // And with a menu open, which is the case that adds rows if anything is going to.
            suite.Press(ConsoleKey.F10);
            AssertFits(suite.RawScreen);
        }

        [Fact]
        public void EvenAChartFitsEightyByTwentyFour()
        {
            using var suite = OpenSpreadsheet();

            suite.Click(HeadingRow, ColumnBColumn);
            suite.Press(ConsoleKey.F6);

            AssertFits(suite.RawScreen);
        }

        /// <summary>Checks a frame against the suite's floor, measuring columns rather than characters.</summary>
        /// <param name="raw">The frame exactly as the terminal would receive it.</param>
        private static void AssertFits(string raw)
        {
            var rows = raw.Split('\n');

            Assert.True(rows.Length <= 24, "the screen is " + rows.Length + " rows, which is more than 24");

            foreach (var row in rows)
            {
                // Measured with the escape-aware walk, since a styled row is several times longer than it is wide.
                var width = AnsiText.VisibleLength(row.TrimEnd('\r'));

                Assert.True(width <= 80, "this row is " + width + " columns wide:\n" + row.TrimEnd('\r'));
            }
        }
    }
}
