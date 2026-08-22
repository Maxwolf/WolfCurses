using System;
using WolfCurses.Graphics;
using WolfCurses.Window.Control;
using Xunit;

namespace WolfCurses.Tests.Controls
{
    /// <summary>
    ///     The scrub bar.
    ///     <para>
    ///         The test that earns it its place is that where the playhead is drawn is where a click on it seeks
    ///         to, read off <c>Render</c>'s own output rather than restated here. The second is the off-by-one at
    ///         the right-hand end: divide by the width rather than one less and the last second of a film is a
    ///         place the pointer cannot reach, which is invisible everywhere except exactly there.
    ///     </para>
    /// </summary>
    public class TimelineTests
    {
        /// <summary>A two-minute bar drawn on a known row and column.</summary>
        private static Timeline Bar() => new()
        {
            Width = 30,
            Row = 7,
            Column = 4,
            Duration = TimeSpan.FromMinutes(2d)
        };

        /// <summary>The rendered row with the escapes taken out.</summary>
        private static string Plain(Timeline bar) => AnsiText.StripEscapes(bar.Render());

        [Fact]
        public void ItIsExactlyAsWideAsItWasToldToBe()
        {
            var bar = Bar();

            Assert.Equal(30, AnsiText.VisibleLength(bar.Render()));

            bar.Position = TimeSpan.FromSeconds(61d);
            Assert.Equal(30, AnsiText.VisibleLength(bar.Render()));

            bar.ShowTimes = false;
            Assert.Equal(30, AnsiText.VisibleLength(bar.Render()));
        }

        [Fact]
        public void BothEndsAreExact()
        {
            var bar = Bar();

            // The first cell is the beginning and the LAST cell is the whole duration. Divide by the width rather
            // than one less and the end is never reachable, in either direction.
            Assert.Equal(TimeSpan.Zero, bar.TimeAt(bar.Row, bar.BarColumn));
            Assert.Equal(bar.Duration, bar.TimeAt(bar.Row, bar.BarColumn + bar.BarWidth - 1));

            bar.Position = TimeSpan.Zero;
            Assert.Equal(bar.BarColumn, bar.MarkerColumn());

            bar.Position = bar.Duration;
            Assert.Equal(bar.BarColumn + bar.BarWidth - 1, bar.MarkerColumn());
        }

        [Fact]
        public void WhereThePlayheadIsDrawnIsWhereAClickOnItSeeksTo()
        {
            var bar = Bar();

            // Both halves read off the control: the column it says the marker is in must be the column the marker
            // was drawn in, and clicking there must come back with roughly the position it was drawn for.
            foreach (var seconds in new[] {0d, 7d, 33d, 60d, 91d, 120d})
            {
                bar.Position = TimeSpan.FromSeconds(seconds);

                var column = bar.MarkerColumn();
                var drawn = Plain(bar)[column - bar.Column];

                Assert.Equal(bar.MarkerChar, drawn);

                var landed = bar.TimeAt(bar.Row, column);
                Assert.NotNull(landed);

                // Within half a cell, which is all a bar this wide can say.
                var cell = bar.Duration.TotalSeconds / (bar.BarWidth - 1);
                Assert.True(Math.Abs(landed.Value.TotalSeconds - seconds) <= cell,
                    seconds + "s was drawn at a column that seeks to " + landed.Value.TotalSeconds + "s");
            }
        }

        [Fact]
        public void ThePartAlreadyPlayedIsDrawnDifferentlyFromThePartToCome()
        {
            var bar = Bar();
            bar.Position = TimeSpan.FromMinutes(1d);

            var row = Plain(bar);
            var marker = bar.MarkerColumn() - bar.Column;

            Assert.Equal(bar.FilledChar, row[marker - 1]);
            Assert.Equal(bar.MarkerChar, row[marker]);
            Assert.Equal(bar.TrackChar, row[marker + 1]);
        }

        [Fact]
        public void ClickingAnywhereThatIsNotTheBarSeeksNowhere()
        {
            var bar = Bar();

            Assert.Null(bar.TimeAt(bar.Row + 1, bar.BarColumn));
            Assert.Null(bar.TimeAt(bar.Row - 1, bar.BarColumn));
            Assert.Null(bar.TimeAt(bar.Row, bar.BarColumn - 1));
            Assert.Null(bar.TimeAt(bar.Row, bar.BarColumn + bar.BarWidth));
        }

        [Fact]
        public void TheTimesTakeTheirSpaceOutOfTheBarRatherThanOutOfTheScreen()
        {
            var bar = Bar();

            var withTimes = bar.BarWidth;
            bar.ShowTimes = false;

            Assert.Equal(30, bar.BarWidth);
            Assert.True(withTimes < 30, "the times must come out of the bar's own width");
            Assert.Equal(bar.Column, bar.BarColumn);
        }

        [Fact]
        public void TheBarDoesNotMoveAsTheElapsedTimeGrowsADigit()
        {
            var bar = Bar();

            bar.Position = TimeSpan.FromSeconds(9d);
            var narrow = bar.BarColumn;
            var narrowWidth = bar.BarWidth;

            bar.Position = TimeSpan.FromSeconds(119d);

            // Measured from the longer of the two labels, so "0:09" and "1:59" occupy the same room and the bar
            // does not shuffle sideways once a second.
            Assert.Equal(narrow, bar.BarColumn);
            Assert.Equal(narrowWidth, bar.BarWidth);
        }

        [Fact]
        public void AnUnknownLengthDrawsATrackWithNoPlayheadAndSeeksNowhere()
        {
            var bar = new Timeline {Width = 30, Row = 7, Column = 4};

            var row = Plain(bar);

            Assert.Equal(30, row.Length);
            Assert.DoesNotContain(bar.MarkerChar, row);
            Assert.DoesNotContain(bar.FilledChar, row);
            Assert.Equal(-1, bar.MarkerColumn());
            Assert.Null(bar.TimeAt(bar.Row, bar.BarColumn));
        }

        [Fact]
        public void ABarNobodyColouredEmitsNothingAtAll()
        {
            var bar = Bar();
            bar.Position = TimeSpan.FromSeconds(45d);

            Assert.DoesNotContain('\x1b', bar.Render());
        }

        [Fact]
        public void APositionPastTheEndIsClampedRatherThanDrawnOffTheBar()
        {
            var bar = Bar();
            bar.Position = TimeSpan.FromHours(3d);

            Assert.Equal(bar.BarColumn + bar.BarWidth - 1, bar.MarkerColumn());
            Assert.Equal(30, AnsiText.VisibleLength(bar.Render()));
        }

        [Fact]
        public void AOneCellBarStillHasSomewhereToPutThePlayhead()
        {
            var bar = new Timeline {Width = 1, ShowTimes = false, Duration = TimeSpan.FromMinutes(2d)};

            Assert.Equal(1, bar.BarWidth);
            Assert.Equal(0, bar.MarkerColumn());
            Assert.Equal(TimeSpan.Zero, bar.TimeAt(0, 0));
        }

        [Fact]
        public void MinutesUnderAnHourAndHoursOverIt()
        {
            // A two-minute song reading 0:02:13 is noise; an hour-long film reading 73:41 is arithmetic.
            Assert.Equal("0:00", Timeline.Format(TimeSpan.Zero));
            Assert.Equal("2:13", Timeline.Format(TimeSpan.FromSeconds(133d)));
            Assert.Equal("59:59", Timeline.Format(TimeSpan.FromSeconds(3599d)));
            Assert.Equal("1:00:00", Timeline.Format(TimeSpan.FromSeconds(3600d)));
            Assert.Equal("1:13:41", Timeline.Format(TimeSpan.FromSeconds(4421d)));
            Assert.Equal("0:00", Timeline.Format(TimeSpan.FromSeconds(-30d)));
        }

        [Fact]
        public void TheTimesAreWhatItSaysTheyAre()
        {
            var bar = Bar();
            bar.Position = TimeSpan.FromSeconds(75d);

            var row = Plain(bar);

            Assert.StartsWith("1:15", row, StringComparison.Ordinal);
            Assert.EndsWith("2:00", row, StringComparison.Ordinal);
        }
    }
}
