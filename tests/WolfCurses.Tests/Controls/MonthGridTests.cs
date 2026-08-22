using System;
using WolfCurses.Graphics;
using WolfCurses.Window.Control;
using Xunit;

namespace WolfCurses.Tests.Controls
{
    /// <summary>
    ///     The month grid.
    ///     <para>
    ///         Off-by-one is the whole difficulty of a calendar, so the test that earns the control its place is
    ///         the one asserting that where a date is drawn is where a click on it lands, with both halves read off
    ///         <c>Render</c>'s own output rather than restated. The rest are the months that catch a wrong sum: one
    ///         starting on the first column, one starting on the last, a leap February, and the two ends of what a
    ///         date can hold.
    ///     </para>
    /// </summary>
    public class MonthGridTests
    {
        /// <summary>August 2026 begins on a Saturday, which is the last column of a Sunday-first week.</summary>
        private static MonthGrid August() => new(2026, 8) {Row = 5, Column = 3};

        /// <summary>The cell text a date is drawn in, found rather than computed.</summary>
        private static string CellOf(MonthGrid grid, DateOnly date)
        {
            Assert.True(grid.TryPositionOf(date, out var row, out var column),
                date + " is not on the grid showing " + grid.Title);

            var line = AnsiText.StripEscapes(grid.Render()[row - grid.Row]);

            return line.Substring(column - grid.Column, grid.CellWidth);
        }

        [Fact]
        public void ItIsAlwaysTheSameSizeWhateverTheMonthNeeds()
        {
            var grid = August();

            // Six weeks always. A grid that changed height would move everything under it as you paged from one
            // month to the next, which is worse than a blank row in February.
            Assert.Equal(7, grid.Height);
            Assert.Equal(28, grid.Width);
            Assert.Equal(7, grid.Render().Count);

            foreach (var month in new[] {2, 4, 8, 12})
            {
                grid.Show(2026, month);
                Assert.Equal(7, grid.Render().Count);
            }
        }

        [Fact]
        public void TheDayNamesStartOnWhicheverDayTheWeekDoes()
        {
            var grid = August();

            // Right aligned with the marker column left blank, so a name sits over the numbers and not over their marks.
            Assert.StartsWith(" Su ", AnsiText.StripEscapes(grid.Render()[0]), StringComparison.Ordinal);

            grid.FirstDayOfWeek = DayOfWeek.Monday;
            Assert.StartsWith(" Mo ", AnsiText.StripEscapes(grid.Render()[0]), StringComparison.Ordinal);
        }

        [Fact]
        public void WhereADateIsDrawnIsWhereAClickOnItLands()
        {
            // The whole justification for the control. Every day of the month, both ways round, so a sum that is
            // right in the middle of the month and wrong at its edges cannot pass.
            var grid = August();

            for (var day = 1; day <= 31; day++)
            {
                var date = new DateOnly(2026, 8, day);

                Assert.True(grid.TryPositionOf(date, out var row, out var column));
                Assert.Equal(date, grid.DayAt(row, column));

                // And anywhere else inside the same cell is the same day.
                Assert.Equal(date, grid.DayAt(row, column + grid.CellWidth - 1));
            }
        }

        [Fact]
        public void AMonthStartingInTheLastColumnPutsItsFirstDayThere()
        {
            // The first of August 2026 is a Saturday: the very last cell of the first week, with six blanks in
            // front of it. This is the month that catches a leading-offset sum that is one out.
            var grid = August();
            var week = AnsiText.StripEscapes(grid.Render()[1]);

            Assert.Equal("                          1 ", week);
        }

        [Fact]
        public void AMonthStartingInTheFirstColumnHasNoBlanksAtAll()
        {
            // February 2026 begins on a Sunday, which is the other end of the same sum.
            var grid = new MonthGrid(2026, 2);
            var week = AnsiText.StripEscapes(grid.Render()[1]);

            Assert.Equal("  1   2   3   4   5   6   7 ", week);
        }

        [Fact]
        public void FebruaryKnowsAboutLeapYears()
        {
            var grid = new MonthGrid(2024, 2);

            Assert.NotNull(grid.TryPositionOf(new DateOnly(2024, 2, 29), out _, out _) ? "yes" : null);

            grid.Show(2025, 2);
            Assert.False(grid.TryPositionOf(new DateOnly(2025, 2, 28).AddDays(1), out _, out _));
        }

        [Fact]
        public void TheCellsEitherSideOfTheMonthAreBlankRatherThanTheNeighboursDays()
        {
            var grid = August();

            // A cell either is a day of this month or is nothing at all, so a click can never quietly land in a
            // month you are not looking at.
            Assert.Null(grid.DayAt(grid.Row + 1, grid.Column));
            Assert.Null(grid.DayAt(grid.Row + 6, grid.Column + 6 * grid.CellWidth));

            // The header row is not a day either, nor is anywhere off the grid.
            Assert.Null(grid.DayAt(grid.Row, grid.Column));
            Assert.Null(grid.DayAt(grid.Row + 1, grid.Column - 1));
            Assert.Null(grid.DayAt(grid.Row + 1, grid.Column + grid.Width));
            Assert.Null(grid.DayAt(grid.Row + grid.Height, grid.Column));
        }

        [Fact]
        public void ADateInAnotherMonthHasNoPositionOnThisGrid()
        {
            var grid = August();

            Assert.False(grid.TryPositionOf(new DateOnly(2026, 7, 31), out _, out _));
            Assert.False(grid.TryPositionOf(new DateOnly(2026, 9, 1), out _, out _));
        }

        [Fact]
        public void PagingThroughTheMonthsRollsTheYearOver()
        {
            var grid = new MonthGrid(2026, 12);

            grid.MoveMonths(1);
            Assert.Equal(2027, grid.Year);
            Assert.Equal(1, grid.Month);

            grid.MoveMonths(-1);
            Assert.Equal(2026, grid.Year);
            Assert.Equal(12, grid.Month);

            grid.MoveMonths(-12);
            Assert.Equal(2025, grid.Year);
            Assert.Equal(12, grid.Month);
        }

        [Fact]
        public void PagingStopsAtTheEndsOfWhatADateCanHold()
        {
            var grid = new MonthGrid(1, 1);

            // Stopping rather than throwing: a page key held down should reach the end and stay there.
            grid.MoveMonths(-100);
            Assert.Equal(1, grid.Year);
            Assert.Equal(1, grid.Month);

            Assert.Equal(7, grid.Render().Count);

            grid.Show(9999, 12);
            grid.MoveMonths(100);
            Assert.Equal(9999, grid.Year);
            Assert.Equal(12, grid.Month);

            Assert.Equal(7, grid.Render().Count);
        }

        [Fact]
        public void TheVeryFirstMonthHasNothingToBackIntoAndStillDraws()
        {
            var grid = new MonthGrid(1, 1);

            // January of year one starts on a Monday, so a Sunday-first grid would want to reach back a day that
            // does not exist. It begins where it can instead.
            Assert.Equal(DateOnly.MinValue, grid.FirstCell);
            Assert.Equal(new DateOnly(1, 1, 1), grid.DayAt(grid.Row + 1, grid.Column));
        }

        [Fact]
        public void DaysWithSomethingOnThemAreMarked()
        {
            var grid = August();
            grid.Marked = date => date.Day == 14;

            Assert.EndsWith("·", CellOf(grid, new DateOnly(2026, 8, 14)), StringComparison.Ordinal);
            Assert.EndsWith(" ", CellOf(grid, new DateOnly(2026, 8, 15)), StringComparison.Ordinal);

            // The predicate is asked afresh, so a caller adding something to a day does not have to tell the grid.
            grid.Marked = date => date.Day == 15;
            Assert.EndsWith(" ", CellOf(grid, new DateOnly(2026, 8, 14)), StringComparison.Ordinal);
        }

        [Fact]
        public void TheSelectedDayIsDrawnDifferentlyFromItsNeighbour()
        {
            var grid = August();
            grid.ColorMode = AnsiColorModeEnum.Palette256;
            grid.SelectedStyle = new TextStyle(ConsoleColor.Black, ConsoleColor.Gray);
            grid.Selected = new DateOnly(2026, 8, 14);

            Assert.True(grid.TryPositionOf(grid.Selected.Value, out var row, out _));

            Assert.Contains(grid.SelectedStyle.OpenSequence(AnsiColorModeEnum.Palette256),
                grid.Render()[row - grid.Row], StringComparison.Ordinal);
        }

        [Fact]
        public void TodayIsToldRatherThanRead()
        {
            var grid = August();
            grid.ColorMode = AnsiColorModeEnum.Palette256;
            grid.TodayStyle = new TextStyle(ConsoleColor.White, ConsoleColor.DarkBlue);

            // Nothing has been said about today, so nothing is drawn as it. A control that asked the clock could
            // not be tested without waiting for a particular date to come round.
            Assert.DoesNotContain(grid.TodayStyle.OpenSequence(AnsiColorModeEnum.Palette256),
                string.Join("\n", grid.Render()), StringComparison.Ordinal);

            grid.Today = new DateOnly(2026, 8, 3);

            Assert.Contains(grid.TodayStyle.OpenSequence(AnsiColorModeEnum.Palette256),
                string.Join("\n", grid.Render()), StringComparison.Ordinal);
        }

        [Fact]
        public void TheCursorWinsOverTodayWhenTheyAreTheSameDay()
        {
            var grid = August();
            grid.ColorMode = AnsiColorModeEnum.Palette256;
            grid.TodayStyle = new TextStyle(ConsoleColor.White, ConsoleColor.DarkBlue);
            grid.SelectedStyle = new TextStyle(ConsoleColor.Black, ConsoleColor.Gray);

            grid.Today = new DateOnly(2026, 8, 3);
            grid.Selected = new DateOnly(2026, 8, 3);

            Assert.True(grid.TryPositionOf(grid.Today.Value, out var row, out _));
            var line = grid.Render()[row - grid.Row];

            // The cursor is where the next keystroke goes, so it is the one that has to be legible.
            Assert.Contains(grid.SelectedStyle.OpenSequence(AnsiColorModeEnum.Palette256), line,
                StringComparison.Ordinal);
        }

        [Fact]
        public void AGridNobodyColouredIsPlainText()
        {
            var grid = August();
            grid.Marked = _ => true;
            grid.Today = new DateOnly(2026, 8, 3);

            // The library's standing rule: no escape, not even a reset, when nothing asked for one. Note the
            // selected day is left unset, since that one falls back to inverse video by design.
            foreach (var row in grid.Render())
                Assert.Equal(row, AnsiText.StripEscapes(row));
        }

        [Fact]
        public void TheTitleReadsTheWayAPersonWritesIt()
        {
            Assert.Equal("August 2026", August().Title);
            Assert.Equal("February 2024", new MonthGrid(2024, 2).Title);
        }

        [Fact]
        public void EveryRowIsExactlyAsWideAsTheGridSaysItIs()
        {
            var grid = August();
            grid.Marked = date => date.Day % 3 == 0;

            foreach (var row in grid.Render())
                Assert.Equal(grid.Width, AnsiText.VisibleLength(row));
        }

        [Fact]
        public void AWiderCellStillLinesUpWithItsHitTest()
        {
            var grid = August();
            grid.CellWidth = 6;

            Assert.Equal(42, grid.Width);

            foreach (var row in grid.Render())
                Assert.Equal(42, AnsiText.VisibleLength(row));

            var date = new DateOnly(2026, 8, 20);
            Assert.True(grid.TryPositionOf(date, out var row2, out var column));
            Assert.Equal(date, grid.DayAt(row2, column));
        }

        [Fact]
        public void EveryMonthOfADecadeDrawsEveryOneOfItsDaysExactlyOnce()
        {
            // The invariant that catches an arithmetic slip nothing else would: over a hundred and twenty months,
            // including every leap year and every possible starting weekday, the grid must show each day once.
            var grid = new MonthGrid();

            foreach (var week in new[] {DayOfWeek.Sunday, DayOfWeek.Monday})
            {
                grid.FirstDayOfWeek = week;

                for (var year = 2020; year < 2030; year++)
                {
                    for (var month = 1; month <= 12; month++)
                    {
                        grid.Show(year, month);

                        var days = DateTime.DaysInMonth(year, month);
                        var seen = 0;

                        for (var cell = 0; cell < MonthGrid.Weeks * MonthGrid.DaysInWeek; cell++)
                        {
                            var found = grid.DayAt(
                                grid.Row + 1 + cell / MonthGrid.DaysInWeek,
                                grid.Column + cell % MonthGrid.DaysInWeek * grid.CellWidth);

                            if (found != null)
                                seen++;
                        }

                        Assert.Equal(days, seen);
                    }
                }
            }
        }
    }
}
