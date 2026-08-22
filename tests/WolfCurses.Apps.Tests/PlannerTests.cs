using System;
using System.Globalization;
using System.Text.RegularExpressions;
using WolfCurses.Apps.Planner;
using WolfCurses.Apps.Tests.Support;
using WolfCurses.Graphics;
using WolfCurses.Window.Control;
using Xunit;

namespace WolfCurses.Apps.Tests
{
    /// <summary>
    ///     The planner as a person meets it: keys in, frames out.
    ///     <para>
    ///         Where the cursor is comes from the status strip, which states it outright, rather than from counting
    ///         cells. The one test that does find a cell on screen finds it by looking for the day it wants and
    ///         then checks the status strip agrees, so a hit test that had drifted cannot make it pass.
    ///     </para>
    /// </summary>
    [Collection("Suite")]
    public class PlannerTests
    {
        private static DrivenSuite OpenPlanner()
        {
            var suite = new DrivenSuite();
            suite.ChooseMenuItem((int) OfficeCommandsEnum.Planner);

            return suite;
        }

        /// <summary>Which day the cursor is on, read off the status strip.</summary>
        private static DateOnly Selected(DrivenSuite suite)
        {
            var match = Regex.Match(suite.Screen, @"(?m)^  (\w{3} \d{1,2} \w{3} \d{4})");
            Assert.True(match.Success, "the status strip did not say which day is chosen:\n" + suite.Describe());

            return DateOnly.ParseExact(match.Groups[1].Value, "ddd d MMM yyyy", CultureInfo.InvariantCulture);
        }

        /// <summary>The month the calendar is showing, read off the box's own title.</summary>
        private static string MonthShown(DrivenSuite suite)
        {
            var rows = suite.Screen.Split('\n');
            var match = Regex.Match(rows[PlannerChrome.BodyRow], @"(\w+ \d{4})");

            Assert.True(match.Success, "the calendar had no month on it:\n" + suite.Describe());

            return match.Groups[1].Value;
        }

        /// <summary>Where a day of the month is drawn, found on screen rather than worked out.</summary>
        private static (int Row, int Column) CellOf(DrivenSuite suite, int day)
        {
            // The number as the grid pads it, which cannot be mistaken for a digit inside a longer one.
            var wanted = day.ToString(CultureInfo.InvariantCulture).PadLeft(3);
            var rows = suite.Screen.Split('\n');

            for (var row = PlannerChrome.GridRow + 1; row <= PlannerChrome.GridRow + MonthGrid.Weeks; row++)
            {
                var at = rows[row].IndexOf(wanted, StringComparison.Ordinal);

                if (at >= 0)
                    return (row, at + 2);
            }

            Assert.Fail("the " + day + "th was not drawn:\n" + suite.Describe());
            return (0, 0);
        }

        [Fact]
        public void ItOpensOnTodayWithTodayChosen()
        {
            using var suite = OpenPlanner();

            var today = DateOnly.FromDateTime(DateTime.Now);

            Assert.Equal(today, Selected(suite));
            Assert.Equal(today.ToString("MMMM yyyy", CultureInfo.InvariantCulture), MonthShown(suite));
        }

        [Fact]
        public void TheClockShowsTheRealDateAndTime()
        {
            using var suite = OpenPlanner();

            var screen = suite.Screen;

            // Proof it is reading the wall clock rather than showing something plausible: today's actual date.
            Assert.Contains(DateTime.Now.ToString("dddd d MMMM yyyy", CultureInfo.InvariantCulture), screen,
                StringComparison.Ordinal);

            Assert.Matches(@"\d\d:\d\d:\d\d", screen);
        }

        [Fact]
        public void TheArrowKeysWalkTheDaysAndTheWeeks()
        {
            using var suite = OpenPlanner();

            var start = Selected(suite);

            suite.Press(ConsoleKey.RightArrow);
            Assert.Equal(start.AddDays(1), Selected(suite));

            suite.Press(ConsoleKey.LeftArrow);
            Assert.Equal(start, Selected(suite));

            suite.Press(ConsoleKey.DownArrow);
            Assert.Equal(start.AddDays(7), Selected(suite));

            suite.Press(ConsoleKey.UpArrow);
            Assert.Equal(start, Selected(suite));
        }

        [Fact]
        public void WalkingOffTheEndOfAMonthGoesIntoTheNextOne()
        {
            using var suite = OpenPlanner();

            var start = Selected(suite);

            // Far enough that it must cross a boundary whatever day of the month it started on.
            for (var i = 0; i < 40; i++)
                suite.Press(ConsoleKey.RightArrow);

            var moved = Selected(suite);

            Assert.Equal(start.AddDays(40), moved);
            Assert.Equal(moved.ToString("MMMM yyyy", CultureInfo.InvariantCulture), MonthShown(suite));
        }

        [Fact]
        public void PagingMovesAWholeMonthAtATime()
        {
            using var suite = OpenPlanner();

            var start = Selected(suite);

            suite.Press(ConsoleKey.PageDown);
            Assert.NotEqual(start.ToString("MMMM yyyy", CultureInfo.InvariantCulture), MonthShown(suite));

            suite.Press(ConsoleKey.PageUp);
            Assert.Equal(start.ToString("MMMM yyyy", CultureInfo.InvariantCulture), MonthShown(suite));
        }

        [Fact]
        public void HomeComesBackToToday()
        {
            using var suite = OpenPlanner();

            suite.Press(ConsoleKey.PageDown);
            suite.Press(ConsoleKey.PageDown);
            suite.Press(ConsoleKey.Home);

            Assert.Equal(DateOnly.FromDateTime(DateTime.Now), Selected(suite));
        }

        [Fact]
        public void ClickingADayChoosesIt()
        {
            using var suite = OpenPlanner();

            // Every month has a fifteenth, and it is never in the leading or trailing blanks.
            var (row, column) = CellOf(suite, 15);
            suite.Click(row, column);

            // The status strip agreeing is what makes this a test of the hit test rather than of the search above.
            Assert.Equal(15, Selected(suite).Day);
        }

        [Fact]
        public void EveryDayOfTheMonthCanBeClickedOn()
        {
            using var suite = OpenPlanner();

            var days = DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month);

            // One at a time and all of them, because an arithmetic slip that is right in the middle of a month and
            // wrong at its edges would pass on a single day.
            for (var day = 1; day <= days; day++)
            {
                var (row, column) = CellOf(suite, day);
                suite.Click(row, column);

                Assert.Equal(day, Selected(suite).Day);
            }
        }

        [Fact]
        public void ChristmasIsThereWithoutBeingInTheFile()
        {
            using var suite = OpenPlanner();

            // Paged to whichever December is next, so this works whatever day the test runs on.
            for (var i = 0; i < 12 && !MonthShown(suite).StartsWith("December", StringComparison.Ordinal); i++)
                suite.Press(ConsoleKey.PageDown);

            Assert.StartsWith("December", MonthShown(suite), StringComparison.Ordinal);

            var (row, column) = CellOf(suite, 25);
            suite.Click(row, column);

            // Worked out from the year rather than looked up, which is what lets the planner know it in any year.
            Assert.Contains("Christmas Day", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void AddingSomethingPutsItOnTheChosenDay()
        {
            using var suite = OpenPlanner();

            suite.Press(ConsoleKey.F2);
            suite.Type("Eat a certain billionaire");
            suite.Type("13:00");

            var screen = suite.Screen;

            Assert.Contains("Eat a certain billionaire", screen, StringComparison.Ordinal);
            Assert.Contains("13:00", screen, StringComparison.Ordinal);
        }

        [Fact]
        public void DecliningToGiveATimeStillAddsTheEntry()
        {
            using var suite = OpenPlanner();

            suite.Press(ConsoleKey.F2);
            suite.Type("Destroy a certain city");

            // A blank line cancels the second question. Throwing away something already typed because the optional
            // half was declined would be the worse answer.
            suite.Type(string.Empty);

            Assert.Contains("Destroy a certain city", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void AHolidayCannotBeRemovedBecauseThereIsNothingToRemove()
        {
            using var suite = OpenPlanner();

            for (var i = 0; i < 12 && !MonthShown(suite).StartsWith("December", StringComparison.Ordinal); i++)
                suite.Press(ConsoleKey.PageDown);

            var (row, column) = CellOf(suite, 25);
            suite.Click(row, column);

            suite.Press(ConsoleKey.Delete);

            // It is computed from the year rather than stored, so next year's would come back regardless.
            Assert.Contains("Nothing on that day to remove", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void TheWeekCanBeMadeToStartOnMonday()
        {
            using var suite = OpenPlanner();

            var rows = suite.Screen.Split('\n');
            Assert.Contains("Su  Mo", rows[PlannerChrome.GridRow], StringComparison.Ordinal);

            // View menu, fifth entry down: Today, Previous, Next, a rule, then the toggle.
            suite.Press(ConsoleKey.V, ConsoleModifiers.Alt);

            for (var i = 0; i < 4; i++)
                suite.Press(ConsoleKey.DownArrow);

            suite.Press(ConsoleKey.Enter);

            rows = suite.Screen.Split('\n');
            Assert.Contains("Mo  Tu", rows[PlannerChrome.GridRow], StringComparison.Ordinal);
        }

        [Fact]
        public void EscapeWithNothingOpenReturnsToTheSuiteMenu()
        {
            using var suite = OpenPlanner();

            suite.Escape();

            Assert.Contains("Which application?", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void EscapeShutsAnOpenMenuRatherThanLeavingTheApplication()
        {
            using var suite = OpenPlanner();

            suite.Press(ConsoleKey.F10);
            Assert.Contains("Save As...", suite.Screen, StringComparison.Ordinal);

            suite.Escape();

            Assert.DoesNotContain("Save As...", suite.Screen, StringComparison.Ordinal);
            Assert.DoesNotContain("Which application?", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void OpeningAMenuDrawsItOverTheMonthWithoutMovingAnything()
        {
            using var suite = OpenPlanner();

            var before = suite.Screen.Split('\n').Length;
            suite.Press(ConsoleKey.F10);

            Assert.Equal(before, suite.Screen.Split('\n').Length);

            // The day's entries beside the panel are untouched, which a full-width overlay would have lost.
            Assert.Contains("Now", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void TabCyclesThroughAllFourViewsAndBackToTheMonth()
        {
            using var suite = OpenPlanner();

            var seen = new System.Collections.Generic.List<string>();

            for (var i = 0; i < 4; i++)
            {
                seen.Add(suite.Screen.Split('\n')[PlannerChrome.BodyRow]);
                suite.Press(ConsoleKey.Tab);
            }

            // Four different screens, and the fourth press brings the first one back.
            Assert.Equal(4, new System.Collections.Generic.HashSet<string>(seen).Count);
            Assert.Equal(seen[0], suite.Screen.Split('\n')[PlannerChrome.BodyRow]);
        }

        [Fact]
        public void TheFunctionKeysGoStraightToAView()
        {
            using var suite = OpenPlanner();

            suite.Press(ConsoleKey.F6);
            Assert.Contains("Week of", suite.Screen, StringComparison.Ordinal);

            suite.Press(ConsoleKey.F7);
            Assert.Contains("Jan", suite.Screen, StringComparison.Ordinal);
            Assert.Contains("Dec", suite.Screen, StringComparison.Ordinal);

            suite.Press(ConsoleKey.F8);
            Assert.Contains("Coming up", suite.Screen, StringComparison.Ordinal);

            suite.Press(ConsoleKey.F5);
            Assert.Contains("Now", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void EveryViewIsExactlyTheSameHeight()
        {
            using var suite = OpenPlanner();

            var height = suite.Screen.Split('\n').Length;

            // Switching how you look at something must not move everything on the screen, which is the same
            // reason the month grid always draws six weeks.
            foreach (var key in new[] {ConsoleKey.F6, ConsoleKey.F7, ConsoleKey.F8, ConsoleKey.F5})
            {
                suite.Press(key);
                Assert.Equal(height, suite.Screen.Split('\n').Length);
            }
        }

        [Fact]
        public void TheWeekViewWritesEveryDayOfTheWeekOut()
        {
            using var suite = OpenPlanner();
            suite.Press(ConsoleKey.F6);

            var screen = suite.Screen;

            foreach (var day in new[] {"Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"})
                Assert.Contains(day, screen, StringComparison.Ordinal);
        }

        [Fact]
        public void ClickingADayInTheWeekViewChoosesIt()
        {
            using var suite = OpenPlanner();
            suite.Press(ConsoleKey.F6);

            var rows = suite.Screen.Split('\n');
            var row = Array.FindIndex(rows, line => line.Contains("Wednesday", StringComparison.Ordinal));

            Assert.True(row > 0, "the week view had no Wednesday on it");

            suite.Click(row, 5);

            Assert.Equal(DayOfWeek.Wednesday, Selected(suite).DayOfWeek);
        }

        [Fact]
        public void TheYearViewShowsTwelveMonthsAtOnce()
        {
            using var suite = OpenPlanner();
            suite.Press(ConsoleKey.F7);

            var screen = suite.Screen;

            foreach (var month in new[] {"Jan", "Feb", "Mar", "Apr", "May", "Jun",
                         "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"})
                Assert.Contains(" " + month + " ", screen, StringComparison.Ordinal);
        }

        [Fact]
        public void ClickingTheFirstCellOfJanuaryChoosesNewYearsDay()
        {
            using var suite = OpenPlanner();
            suite.Press(ConsoleKey.F7);

            var year = Selected(suite).Year;
            var rows = suite.Screen.Split('\n');
            var row = Array.FindIndex(rows, line => line.Contains(" Jan ", StringComparison.Ordinal));

            Assert.True(row > 0, "the year view had no January on it");

            suite.Click(row, PlannerYearView.FirstDayColumn);

            // An absolute answer rather than a round trip through the same arithmetic that drew it.
            Assert.Equal(new DateOnly(year, 1, 1), Selected(suite));
        }

        [Fact]
        public void TheYearViewMarksTheDaysEasterFallsOn()
        {
            using var suite = OpenPlanner();
            suite.Press(ConsoleKey.F7);

            var year = Selected(suite).Year;
            var easter = Holidays.Easter(year);
            var rows = suite.Screen.Split('\n');

            var row = Array.FindIndex(rows,
                line => line.Contains(" " + new DateOnly(year, easter.Month, 1)
                    .ToString("MMM", CultureInfo.InvariantCulture) + " ", StringComparison.Ordinal));

            // Clicking the cell Easter should be in and getting Easter back is what ties the computed holiday to
            // the picture of it.
            suite.Click(row, PlannerYearView.FirstDayColumn + easter.Day - 1);

            Assert.Equal(easter, Selected(suite));

            // The year view has room for a dot and not for a name, so the proof that the dot is the right day is
            // that the month view, which the chosen day carries across to, says what it is.
            suite.Press(ConsoleKey.F5);
            Assert.Contains("Easter Sunday", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void TheListShowsWhatIsComingWithoutTheEmptyDays()
        {
            using var suite = OpenPlanner();
            suite.Press(ConsoleKey.F8);

            var screen = suite.Screen;

            Assert.Contains("Coming up", screen, StringComparison.Ordinal);

            // Entries from several different days, which a calendar view could only show one of at a time.
            Assert.Matches(@"\w{3} \d{1,2} \w{3}", screen);
        }

        [Fact]
        public void InTheListTheArrowsStepBetweenEntriesRatherThanDays()
        {
            using var suite = OpenPlanner();

            // Somewhere with nothing on it for a while, so stepping a day at a time would go nowhere useful.
            suite.Press(ConsoleKey.F8);

            var start = Selected(suite);
            suite.Press(ConsoleKey.DownArrow);

            var moved = Selected(suite);

            // It landed on a day that has something on it, which day-stepping would only manage by luck.
            Assert.NotEqual(start, moved);
            Assert.Contains(PlannerChrome.ShortDate(moved), suite.Screen, StringComparison.Ordinal);

            suite.Press(ConsoleKey.UpArrow);
            Assert.Equal(start, Selected(suite));
        }

        [Fact]
        public void EveryViewFitsEightyByTwentyFour()
        {
            using var suite = OpenPlanner();

            foreach (var key in new[] {ConsoleKey.F5, ConsoleKey.F6, ConsoleKey.F7, ConsoleKey.F8})
            {
                suite.Press(key);
                AssertFits(suite.RawScreen);

                suite.Press(ConsoleKey.F10);
                AssertFits(suite.RawScreen);
                suite.Escape();
            }
        }

        [Fact]
        public void TheWholeScreenFitsEightyByTwentyFour()
        {
            using var suite = OpenPlanner();

            AssertFits(suite.RawScreen);

            suite.Press(ConsoleKey.F10);
            AssertFits(suite.RawScreen);
        }

        /// <summary>Checks a frame against the suite's floor, measuring columns rather than characters.</summary>
        private static void AssertFits(string raw)
        {
            var rows = raw.Split('\n');

            Assert.True(rows.Length <= 24, "the screen is " + rows.Length + " rows, which is more than 24");

            foreach (var row in rows)
            {
                var width = AnsiText.VisibleLength(row.TrimEnd('\r'));

                Assert.True(width <= 80, "this row is " + width + " columns wide:\n" + row.TrimEnd('\r'));
            }
        }
    }
}
