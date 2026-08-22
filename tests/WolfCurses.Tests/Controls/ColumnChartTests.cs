using System;
using WolfCurses.Graphics;
using WolfCurses.Window.Control;
using Xunit;

namespace WolfCurses.Tests.Controls
{
    /// <summary>
    ///     The vertical bar chart.
    ///     <para>
    ///         The two tests that earn it its place are at the ends of the scale: a value that is small but not
    ///         nothing must draw something, or a meter reads as switched off when it is merely quiet; and a value
    ///         at the top must fill its last row and not spill into the next. Everything else is that arithmetic
    ///         from a different angle.
    ///     </para>
    /// </summary>
    public class ColumnChartTests
    {
        /// <summary>A chart with a pinned scale, so the tests are about the drawing rather than about the scaling.</summary>
        private static ColumnChart Meter() => new() {Height = 4, Minimum = 0d, Maximum = 1d};

        /// <summary>One column of a rendered chart, top to bottom, with the escapes taken out.</summary>
        private static string ColumnOf(ColumnChart chart, double[] values, int index)
        {
            var rows = chart.Render(values);
            var column = new char[rows.Count];

            for (var row = 0; row < rows.Count; row++)
                column[row] = AnsiText.StripEscapes(rows[row])[index];

            return new string(column);
        }

        [Fact]
        public void ItDrawsAsManyRowsAsItWasToldTo()
        {
            var chart = Meter();

            Assert.Equal(4, chart.Render(new[] {0.5d}).Count);

            chart.Height = 9;
            Assert.Equal(9, chart.Render(new[] {0.5d}).Count);
        }

        [Fact]
        public void AValueTooSmallToRoundToACellStillDrawsSomething()
        {
            // The whole reason the floor exists. One percent of four rows of eight steps is 0.32 of a step, which
            // rounds to nothing, and a spectrum with a quiet channel would show a hole rather than a short bar.
            var column = ColumnOf(Meter(), new[] {0.01d}, 0);

            Assert.Equal("   ▁", column);
        }

        [Fact]
        public void OnlyTheBottomOfTheScaleDrawsNothingAtAll()
        {
            var column = ColumnOf(Meter(), new[] {0d}, 0);

            Assert.Equal("    ", column);
        }

        [Fact]
        public void AValueAtTheTopFillsItsLastRowAndDoesNotSpill()
        {
            var chart = Meter();
            var column = ColumnOf(chart, new[] {1d}, 0);

            Assert.Equal("████", column);
            Assert.Equal(4, chart.Render(new[] {1d}).Count);
        }

        [Fact]
        public void AValueAboveTheScaleIsClampedRatherThanDrawnTaller()
        {
            Assert.Equal("████", ColumnOf(Meter(), new[] {9d}, 0));
        }

        [Fact]
        public void HalfWayFillsHalfTheRowsExactly()
        {
            // Two of four rows, and the row at the boundary is a whole block rather than a partial one.
            Assert.Equal("  ██", ColumnOf(Meter(), new[] {0.5d}, 0));
        }

        [Fact]
        public void ThePartlyFilledRowUsesTheRampAndTheRestAreWhole()
        {
            // One whole row plus five eighths of the next, out of four rows of eight steps.
            var column = ColumnOf(Meter(), new[] {(8 + 5) / 32d}, 0);

            Assert.Equal(' ', column[0]);
            Assert.Equal(' ', column[1]);
            Assert.Equal('▅', column[2]);
            Assert.Equal('█', column[3]);
        }

        [Fact]
        public void EveryBarIsAsWideAsItWasToldAndTheGapsAreBetweenThem()
        {
            var chart = new ColumnChart {Height = 2, Minimum = 0d, Maximum = 1d, ColumnWidth = 3, Gap = 1};
            var row = AnsiText.StripEscapes(chart.Render(new[] {1d, 1d})[0]);

            Assert.Equal("███ ███", row);
            Assert.Equal(7, chart.WidthFor(2));
            Assert.Equal(3, chart.WidthFor(1));
            Assert.Equal(0, chart.WidthFor(0));
        }

        [Fact]
        public void EveryRowIsTheSameWidthWhateverTheValues()
        {
            var chart = Meter();
            var rows = chart.Render(new[] {0d, 0.3d, 1d, 0.01d});

            foreach (var row in rows)
                Assert.Equal(4, AnsiText.VisibleLength(row));
        }

        [Fact]
        public void WithNoScalePinnedItFitsTheValuesItWasGiven()
        {
            var chart = new ColumnChart {Height = 2};
            var rows = chart.Render(new[] {10d, 20d});

            // The lowest value is the bottom of the scale, so it draws nothing, and the highest fills.
            Assert.Equal("  ", AnsiText.StripEscapes(rows[0]).Substring(0, 1) + AnsiText.StripEscapes(rows[1])[0]);
            Assert.Equal("██", AnsiText.StripEscapes(rows[0])[1].ToString() + AnsiText.StripEscapes(rows[1])[1]);
        }

        [Fact]
        public void APeakIsDrawnAboveItsBarAndNotWhereTheBarAlreadyIs()
        {
            var chart = Meter();
            var rows = chart.Render(new[] {0.25d}, new[] {1d});

            var column = new char[rows.Count];
            for (var row = 0; row < rows.Count; row++)
                column[row] = AnsiText.StripEscapes(rows[row])[0];

            // The bar is one row; the peak marks the fourth.
            Assert.Equal('▔', column[0]);
            Assert.Equal(' ', column[1]);
            Assert.Equal(' ', column[2]);
            Assert.Equal('█', column[3]);
        }

        [Fact]
        public void APeakNoHigherThanItsBarIsNotDrawnAtAll()
        {
            var chart = Meter();
            var withPeak = chart.Render(new[] {1d}, new[] {1d});
            var without = chart.Render(new[] {1d});

            Assert.Equal(without, withPeak);
        }

        [Fact]
        public void APeakAboveEveryBarStillFitsOnAnUnpinnedScale()
        {
            // Scaled to the bars alone, a peak higher than all of them would sit off the top of the chart.
            var chart = new ColumnChart {Height = 4};
            var rows = chart.Render(new[] {1d, 2d}, new[] {8d, 8d});

            foreach (var row in rows)
                Assert.Equal(2, AnsiText.StripEscapes(row).Length);

            Assert.Contains('▔', AnsiText.StripEscapes(rows[0]));
        }

        [Fact]
        public void FewerPeaksThanBarsSimplyMarksFewerBars()
        {
            var chart = Meter();
            var rows = chart.Render(new[] {0.25d, 0.25d}, new[] {1d});

            var top = AnsiText.StripEscapes(rows[0]);

            Assert.Equal('▔', top[0]);
            Assert.Equal(' ', top[1]);
        }

        [Fact]
        public void AChartNobodyColouredEmitsNothingAtAll()
        {
            var chart = Meter();

            foreach (var row in chart.Render(new[] {0d, 0.5d, 1d}))
                Assert.DoesNotContain('\x1b', row);
        }

        [Fact]
        public void NoValuesAtAllDrawsBlankRowsRatherThanThrowing()
        {
            var chart = Meter();

            Assert.Equal(4, chart.Render(null).Count);
            Assert.Equal(4, chart.Render(Array.Empty<double>()).Count);
            Assert.All(chart.Render(null), row => Assert.Equal(string.Empty, row));
        }

        [Fact]
        public void EveryValueTheSameDrawsSomethingRatherThanDividingByZero()
        {
            var chart = new ColumnChart {Height = 3};
            var rows = chart.Render(new[] {5d, 5d, 5d});

            Assert.Equal(3, rows.Count);

            foreach (var row in rows)
                Assert.Equal(3, AnsiText.StripEscapes(row).Length);
        }

        [Fact]
        public void ARampOfOneGlyphGivesWholeRowsOnly()
        {
            var chart = new ColumnChart {Height = 4, Minimum = 0d, Maximum = 1d, Ramp = "#"};

            // No sub-row resolution, so every drawn cell is the one glyph there is.
            Assert.Equal("  ##", ColumnOf(chart, new[] {0.5d}, 0));
            Assert.Equal("   #", ColumnOf(chart, new[] {0.01d}, 0));
        }

        [Fact]
        public void ANotANumberIsSkippedRatherThanScalingEverythingToNothing()
        {
            var chart = new ColumnChart {Height = 2};
            var rows = chart.Render(new[] {double.NaN, 1d, 2d});

            Assert.Equal(2, rows.Count);
            Assert.Equal(' ', AnsiText.StripEscapes(rows[0])[0]);
            Assert.Equal('█', AnsiText.StripEscapes(rows[0])[2]);
        }
    }
}
