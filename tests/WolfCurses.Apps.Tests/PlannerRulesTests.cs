using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WolfCurses.Apps.Planner;
using WolfCurses.Documents;
using Xunit;

namespace WolfCurses.Apps.Tests
{
    /// <summary>
    ///     The planner's own logic, with no screen anywhere near it: what a date means, when the holidays fall, and
    ///     what a file holds.
    ///     <para>
    ///         The holidays are asserted against dates that can be checked against any almanac, because a computed
    ///         holiday that is a day out is still a plausible-looking date on a plausible-looking day of the week.
    ///     </para>
    /// </summary>
    public class PlannerRulesTests
    {
        [Fact]
        public void EasterFallsWhereEveryAlmanacSaysItDoes()
        {
            // Four years running, which is what catches an algorithm that is right for one and wrong for the
            // century, the leap year or the epact.
            Assert.Equal(new DateOnly(2024, 3, 31), Holidays.Easter(2024));
            Assert.Equal(new DateOnly(2025, 4, 20), Holidays.Easter(2025));
            Assert.Equal(new DateOnly(2026, 4, 5), Holidays.Easter(2026));
            Assert.Equal(new DateOnly(2027, 3, 28), Holidays.Easter(2027));
        }

        [Fact]
        public void EasterIsAlwaysASundayInMarchOrApril()
        {
            // Over two centuries, which is far more than any list of known dates could cover and catches a formula
            // that drifts rather than one that is simply wrong.
            for (var year = 1900; year <= 2100; year++)
            {
                var easter = Holidays.Easter(year);

                Assert.Equal(DayOfWeek.Sunday, easter.DayOfWeek);
                Assert.InRange(easter.Month, 3, 4);
            }
        }

        [Fact]
        public void TheNthWeekdayIsTheNthOneAndNotTheNthDay()
        {
            // Thanksgiving 2026: November begins on a Sunday, so the first Thursday is the fifth and the fourth is
            // the twenty-sixth.
            Assert.Equal(new DateOnly(2026, 11, 26), Holidays.NthWeekday(2026, 11, DayOfWeek.Thursday, 4));

            // A month starting on the very day being counted has its first one on the first.
            Assert.Equal(new DateOnly(2026, 11, 1), Holidays.NthWeekday(2026, 11, DayOfWeek.Sunday, 1));
        }

        [Fact]
        public void AskingForAWeekdayAMonthDoesNotHaveGivesTheLastOneItDoes()
        {
            // November 2026 has four Thursdays. Walking on regardless would report a date in December, which is a
            // real date on the right weekday and therefore looks entirely fine.
            var fifth = Holidays.NthWeekday(2026, 11, DayOfWeek.Thursday, 5);

            Assert.Equal(11, fifth.Month);
            Assert.Equal(new DateOnly(2026, 11, 26), fifth);
        }

        [Fact]
        public void TheLastWeekdayIsTheLastOne()
        {
            // Memorial Day 2026: May ends on a Sunday, so the last Monday is the twenty-fifth.
            Assert.Equal(new DateOnly(2026, 5, 25), Holidays.LastWeekday(2026, 5, DayOfWeek.Monday));

            // And where the month ends on the day being asked for, that is the answer.
            Assert.Equal(new DateOnly(2026, 5, 31), Holidays.LastWeekday(2026, 5, DayOfWeek.Sunday));
        }

        [Fact]
        public void EveryYearGetsTheSameHolidaysWorkedOutFresh()
        {
            var thisYear = Holidays.For(2026);
            var farFuture = Holidays.For(2099);

            Assert.Equal(thisYear.Count, farFuture.Count);

            // The point of computing them: a calendar paged seventy years on still knows when Easter is, where a
            // list of dates would have run out.
            Assert.Contains(farFuture, holiday => holiday.Title == "Easter Sunday");
            Assert.Contains(farFuture, holiday => holiday.Title == "Christmas Day");
        }

        [Fact]
        public void HolidaysComeBackInDateOrder()
        {
            var holidays = Holidays.For(2026);

            for (var i = 1; i < holidays.Count; i++)
            {
                var before = holidays[i - 1].Month * 100 + holidays[i - 1].Day;
                var after = holidays[i].Month * 100 + holidays[i].Day;

                Assert.True(before <= after, "the holidays came back out of order at " + holidays[i].Title);
            }
        }

        [Fact]
        public void AnAnnualEntryHappensEveryYearAndADatedOneDoesNot()
        {
            var annual = new PlannerEvent(PlannerEvent.EveryYear, 5, 4, string.Empty, "Tail Appreciation Day");
            var once = new PlannerEvent(2026, 5, 4, string.Empty, "One off");

            Assert.True(annual.IsAnnual);
            Assert.True(annual.FallsOn(new DateOnly(2026, 5, 4)));
            Assert.True(annual.FallsOn(new DateOnly(2099, 5, 4)));
            Assert.False(annual.FallsOn(new DateOnly(2026, 5, 5)));

            Assert.False(once.IsAnnual);
            Assert.True(once.FallsOn(new DateOnly(2026, 5, 4)));
            Assert.False(once.FallsOn(new DateOnly(2027, 5, 4)));
        }

        [Fact]
        public void AnAnnualEntryOnALeapDayHappensOnlyInLeapYears()
        {
            var leapling = new PlannerEvent(PlannerEvent.EveryYear, 2, 29, string.Empty, "Birthday");

            Assert.True(leapling.FallsOn(new DateOnly(2024, 2, 29)));

            // There is no other day it could honestly be moved to, and quietly picking one would be the program
            // inventing an anniversary.
            Assert.False(leapling.FallsOn(new DateOnly(2025, 2, 28)));
            Assert.False(leapling.FallsOn(new DateOnly(2025, 3, 1)));
        }

        [Fact]
        public void ADateReadsBackTheWayItWasWritten()
        {
            Assert.Equal("05-04", new PlannerEvent(PlannerEvent.EveryYear, 5, 4, "", "x").DateText());
            Assert.Equal("2026-05-04", new PlannerEvent(2026, 5, 4, "", "x").DateText());

            Assert.True(PlannerEvent.TryParseDate("05-04", out var year, out var month, out var day));
            Assert.Equal(PlannerEvent.EveryYear, year);
            Assert.Equal(5, month);
            Assert.Equal(4, day);

            Assert.True(PlannerEvent.TryParseDate("2026-05-04", out year, out month, out day));
            Assert.Equal(2026, year);
        }

        [Fact]
        public void ThingsThatAreNotDatesAreRefused()
        {
            // The header row of the file is one of these, which is the neatest reason to be lenient about them.
            Assert.False(PlannerEvent.TryParseDate("Date", out _, out _, out _));
            Assert.False(PlannerEvent.TryParseDate(null, out _, out _, out _));
            Assert.False(PlannerEvent.TryParseDate("13-01", out _, out _, out _));
            Assert.False(PlannerEvent.TryParseDate("05", out _, out _, out _));
        }

        [Fact]
        public void ADayShowsItsHolidaysAndItsEntriesTogether()
        {
            var diary = new PlannerDiary();
            diary.Add(new PlannerEvent(2026, 12, 25, "10:00", "Open presents"));

            var day = diary.On(new DateOnly(2026, 12, 25));

            // The holiday leads, because it is what the day is rather than something on it.
            Assert.Equal(2, day.Count);
            Assert.Equal("Christmas Day", day[0].Title);
            Assert.Equal("Open presents", day[1].Title);
        }

        [Fact]
        public void AnAllDayEntrySortsBeforeATimedOneRatherThanAtMidnight()
        {
            var diary = new PlannerDiary();

            diary.Add(new PlannerEvent(2026, 3, 2, "09:00", "Nine"));
            diary.Add(new PlannerEvent(2026, 3, 2, string.Empty, "All day"));
            diary.Add(new PlannerEvent(2026, 3, 2, "07:00", "Seven"));

            var day = diary.On(new DateOnly(2026, 3, 2));

            // Something taking the whole day is not at midnight, and putting it there would say it was.
            Assert.Equal(new[] {"All day", "Seven", "Nine"}, day.Select(entry => entry.Title).ToArray());
        }

        [Fact]
        public void AHolidayCannotBePutIntoThePlanner()
        {
            var diary = new PlannerDiary();

            diary.Add(new PlannerEvent(2026, 1, 1, string.Empty, "Mine", PlannerEventKindEnum.Holiday));

            // Nothing would ever store it, so accepting it would produce an entry that vanished on save.
            Assert.Empty(diary.Events);
        }

        [Fact]
        public void AnEmptyTitleIsNotAnEntry()
        {
            var diary = new PlannerDiary();

            diary.Add(new PlannerEvent(2026, 1, 2, "09:00", "   "));

            Assert.Empty(diary.Events);
        }

        [Fact]
        public void ADayWithNothingOnItSaysSo()
        {
            var diary = new PlannerDiary();

            Assert.False(diary.HasAnythingOn(new DateOnly(2026, 3, 3)));
            Assert.True(diary.HasAnythingOn(new DateOnly(2026, 12, 25)));

            diary.Add(new PlannerEvent(2026, 3, 3, string.Empty, "Something"));
            Assert.True(diary.HasAnythingOn(new DateOnly(2026, 3, 3)));
        }

        [Fact]
        public void RemovingTakesItOutAndSaysWhetherItWasThere()
        {
            var diary = new PlannerDiary();
            var entry = new PlannerEvent(2026, 3, 3, string.Empty, "Something");

            diary.Add(entry);
            Assert.True(diary.Remove(entry));
            Assert.False(diary.Remove(entry));
            Assert.Empty(diary.Events);
        }

        [Fact]
        public void WhatIsWrittenReadsBackAsTheSameEntries()
        {
            var diary = new PlannerDiary();

            diary.Add(new PlannerEvent(PlannerEvent.EveryYear, 5, 4, string.Empty, "Tail Appreciation Day"));
            diary.Add(new PlannerEvent(2026, 8, 24, "20:00", "Regret, quietly"));

            var rows = new System.Collections.Generic.List<string[]> {new[] {"Date", "Time", "What"}};

            foreach (var entry in diary.Events)
                rows.Add(new[] {entry.DateText(), entry.Time, entry.Title});

            var read = PlannerLibrary.Parse(DelimitedText.Write(rows, ',', "\n"));

            Assert.Equal(2, read.Events.Count);
            Assert.True(read.Events[0].IsAnnual);
            Assert.Equal("Regret, quietly", read.Events[1].Title);
            Assert.Equal("20:00", read.Events[1].Time);

            // Freshly read means nothing has been changed yet.
            Assert.False(read.IsModified);
        }

        [Fact]
        public void RowsThatAreNotEntriesAreSkippedRatherThanRefused()
        {
            var read = PlannerLibrary.Parse("Date,Time,What\n05-04,,Real\nnonsense\n,,\n2026-01-01,,Also real\n");

            Assert.Equal(2, read.Events.Count);
        }

        [Fact]
        public void TheSamplePlannerLoadsAndIsAboutWhatItSaysItIs()
        {
            var diary = PlannerLibrary.TryLoad(PlannerLibrary.DefaultPlannerPath, out var error);

            Assert.True(diary != null,
                "the sample planner did not load from " + PlannerLibrary.DefaultPlannerPath + ": " + error);

            Assert.True(diary.Events.Count > 20,
                "the sample has only " + diary.Events.Count + " entries");

            // Some of it comes round every year, which is what stops the sample going stale.
            Assert.Contains(diary.Events, entry => entry.IsAnnual);
            Assert.Contains(diary.Events, entry => !entry.IsAnnual);

            // And the awkwardly quoted rows survived being read.
            Assert.Contains(diary.Events, entry => entry.Title.Contains("good boy", StringComparison.Ordinal));
            Assert.Contains(diary.Events, entry => entry.Title.Contains("1 city", StringComparison.Ordinal));
        }

        [Fact]
        public void TheSamplesAnnualEntriesTurnUpInAnyYearAtAll()
        {
            var diary = PlannerLibrary.TryLoad(PlannerLibrary.DefaultPlannerPath, out _);

            Assert.NotNull(diary);

            // The whole reason they are annual: somebody running this in 2040 still has something to look at.
            foreach (var year in new[] {2026, 2030, 2099})
                Assert.True(diary.HasAnythingOn(new DateOnly(year, 5, 4)),
                    "nothing on 4 May " + year);
        }

        [Fact]
        public void AFileThatCannotBeReadSaysSoRatherThanThrowing()
        {
            Assert.Null(PlannerLibrary.TryLoad(Path.Combine(PlannerLibrary.Folder, "no-such-file.csv"),
                out var error));

            Assert.False(string.IsNullOrEmpty(error));
            Assert.Null(PlannerLibrary.TryLoad(null, out _));
        }

        [Fact]
        public void TheListHoldsWhatFallsInsideItAndNothingBeforeTheDayItStartsOn()
        {
            var diary = new PlannerDiary();

            diary.Add(new PlannerEvent(2026, 8, 21, string.Empty, "The day before it starts"));
            diary.Add(new PlannerEvent(2026, 8, 24, "20:00", "Inside it"));
            diary.Add(new PlannerEvent(2026, 9, 30, string.Empty, "Long after it ends"));

            var from = new DateOnly(2026, 8, 23);
            var agenda = PlannerAgenda.Build(diary, from, 7);

            // Asserted about the range rather than about a count, since the holidays are computed and a week of
            // any particular year may or may not have one in it.
            foreach (var line in agenda)
                Assert.InRange(line.Date, from, from.AddDays(6));

            for (var i = 1; i < agenda.Count; i++)
                Assert.True(agenda[i - 1].Date <= agenda[i].Date, "the list came back out of date order");

            Assert.Contains(agenda, line => line.Entry.Title == "Inside it");
            Assert.DoesNotContain(agenda, line => line.Entry.Title == "The day before it starts");
            Assert.DoesNotContain(agenda, line => line.Entry.Title == "Long after it ends");
        }

        [Fact]
        public void TheFirstThingComingUpIsNotSteppedOver()
        {
            var agenda = SampleAgenda();

            // The cursor is on a day the list holds no line for, which is the ordinary case: most days have
            // nothing on them. Stepping from "not in the list" as though it were the first line is what steps
            // over the first thing coming up, and it hides itself whenever that day has a second entry, because
            // both lines then say the same date.
            Assert.Equal(new DateOnly(2026, 8, 24), PlannerAgenda.Step(agenda, new DateOnly(2026, 8, 23), 1));
        }

        [Fact]
        public void ADayWithSeveralThingsOnItIsSteppedOverRatherThanThrough()
        {
            var agenda = SampleAgenda();

            // The twenty-sixth has two lines and is one day, so one press leaves it in either direction.
            Assert.Equal(new DateOnly(2026, 8, 28), PlannerAgenda.Step(agenda, new DateOnly(2026, 8, 26), 1));
            Assert.Equal(new DateOnly(2026, 8, 24), PlannerAgenda.Step(agenda, new DateOnly(2026, 8, 26), -1));
        }

        [Fact]
        public void BothEndsOfTheListAreWalls()
        {
            var agenda = SampleAgenda();

            // There is nothing above the day the list starts from, so backing off the top stays put rather than
            // jumping forward on to the first entry, which is what an index clamped at zero does.
            Assert.Null(PlannerAgenda.Step(agenda, new DateOnly(2026, 8, 23), -1));
            Assert.Null(PlannerAgenda.Step(agenda, new DateOnly(2026, 8, 24), -1));

            Assert.Null(PlannerAgenda.Step(agenda, new DateOnly(2026, 8, 28), 1));
            Assert.Null(PlannerAgenda.Step(agenda, new DateOnly(2026, 12, 25), 1));

            Assert.Null(PlannerAgenda.Step(Array.Empty<PlannerEntryLine>(), new DateOnly(2026, 8, 23), 1));
            Assert.Null(PlannerAgenda.Step(Array.Empty<PlannerEntryLine>(), new DateOnly(2026, 8, 23), -1));
        }

        [Fact]
        public void SteppingDownAndBackUpWalksEveryDayTheListHoldsInOrder()
        {
            var agenda = SampleAgenda();
            var days = new List<DateOnly>();
            var at = new DateOnly(2026, 8, 23);

            while (true)
            {
                var next = PlannerAgenda.Step(agenda, at, 1);

                if (next == null)
                    break;

                at = next.Value;
                days.Add(at);
            }

            Assert.Equal(
                new[] {new DateOnly(2026, 8, 24), new DateOnly(2026, 8, 26), new DateOnly(2026, 8, 28)},
                days);

            var back = new List<DateOnly>();

            while (true)
            {
                var previous = PlannerAgenda.Step(agenda, at, -1);

                if (previous == null)
                    break;

                at = previous.Value;
                back.Add(at);
            }

            back.Reverse();

            // Everything the walk down visited except its last stop, which is where the walk back began.
            Assert.Equal(days.GetRange(0, days.Count - 1), back);
        }

        /// <summary>
        ///     A list with a quiet day in front of it, one entry on the twenty-fourth, two on the twenty-sixth and
        ///     one on the twenty-eighth. Built by hand rather than out of a diary, so no computed holiday can turn
        ///     up in the middle of it and no assertion here is about what year it is.
        /// </summary>
        /// <returns>The lines.</returns>
        private static IReadOnlyList<PlannerEntryLine> SampleAgenda()
        {
            return new[]
            {
                Line(2026, 8, 24, "Eat a certain billionaire"),
                Line(2026, 8, 26, "Return library books"),
                Line(2026, 8, 26, "Savour every bite"),
                Line(2026, 8, 28, "Grow forty feet for the afternoon")
            };
        }

        /// <summary>One line of a list.</summary>
        /// <param name="year">The year it falls in.</param>
        /// <param name="month">The month it falls in.</param>
        /// <param name="day">The day it falls on.</param>
        /// <param name="title">What it is.</param>
        /// <returns>The line.</returns>
        private static PlannerEntryLine Line(int year, int month, int day, string title)
        {
            var date = new DateOnly(year, month, day);

            return new PlannerEntryLine(date, new PlannerEvent(date, string.Empty, title));
        }
    }
}
