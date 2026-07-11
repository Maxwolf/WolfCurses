using System;
using System.Linq;
using WolfCurses.Tests.Support;
using WolfCurses.Window.Control;
using Xunit;

namespace WolfCurses.Tests.Controls
{
    /// <summary>
    ///     Pins how <see cref="LineGraph" /> plots a series onto its character grid: point placement (top = max,
    ///     bottom = min), connecting segments, the area fill, the axis/scale chrome, and the degenerate cases (empty,
    ///     flat, non-finite gaps).
    /// </summary>
    public class LineGraphTests
    {
        private static LineGraph Bare()
        {
            return new LineGraph
            {
                Width = 3,
                Height = 3,
                ShowAxis = false,
                ShowScale = false,
                Connected = false,
                Fill = false,
                PointChar = '*'
            };
        }

        [Fact]
        public void Render_RisingSeries_PlotsDiagonalFromBottomLeftToTopRight()
        {
            var expected =
                "  *" + Text.NL +
                " * " + Text.NL +
                "*  ";

            Assert.Equal(expected, Bare().Render(new double[] {0, 1, 2}));
        }

        [Fact]
        public void Render_Connected_FillsVerticalSegmentsBetweenPoints()
        {
            var graph = Bare();
            graph.Connected = true;

            var expected =
                "  *" + Text.NL +
                " **" + Text.NL +
                "** ";

            Assert.Equal(expected, graph.Render(new double[] {0, 1, 2}));
        }

        [Fact]
        public void Render_Fill_FillsAreaBeneathThePoints()
        {
            var graph = Bare();
            graph.Fill = true;
            graph.AreaChar = '.';

            var expected =
                "  *" + Text.NL +
                " *." + Text.NL +
                "*..";

            Assert.Equal(expected, graph.Render(new double[] {0, 1, 2}));
        }

        [Fact]
        public void Render_FlatSeries_PlotsOnTheMiddleRow()
        {
            var expected =
                "   " + Text.NL +
                "***" + Text.NL +
                "   ";

            Assert.Equal(expected, Bare().Render(new double[] {5, 5, 5}));
        }

        [Fact]
        public void Render_WithAxisAndScale_DrawsGutterAndBorders()
        {
            var graph = new LineGraph
            {
                Width = 3,
                Height = 3,
                ShowAxis = true,
                ShowScale = true,
                Connected = false,
                Fill = false,
                PointChar = '*'
            };

            var expected =
                "2│  *" + Text.NL +
                " │ * " + Text.NL +
                "0│*  " + Text.NL +
                " └───";

            Assert.Equal(expected, graph.Render(new double[] {0, 1, 2}));
        }

        [Fact]
        public void Render_Empty_DrawsBlankFramedPlotWithStableLayout()
        {
            var graph = new LineGraph {Width = 3, Height = 3, ShowAxis = true, ShowScale = true, PointChar = '*'};

            var lines = graph.Render(Array.Empty<double>()).Split(Text.NL);

            Assert.Equal(4, lines.Length); // Height rows + one axis row
            Assert.DoesNotContain('*', graph.Render(Array.Empty<double>()));
        }

        [Fact]
        public void Render_NonFiniteSample_LeavesAGapInTheLine()
        {
            var graph = new LineGraph
            {
                Width = 3,
                Height = 3,
                ShowAxis = false,
                ShowScale = false,
                Connected = true,
                Fill = false,
                Minimum = 0,
                Maximum = 2,
                PointChar = '*'
            };

            // Middle column is NaN: it plots nothing and breaks the connecting line so nothing spans the gap column.
            var lines = graph.Render(new[] {0d, double.NaN, 2d}).Split(Text.NL);

            Assert.Equal(3, lines.Length);
            Assert.All(lines, line => Assert.Equal(' ', line[1])); // the whole middle column stays blank
            Assert.Equal('*', lines[2][0]); // first point at bottom-left
            Assert.Equal('*', lines[0][2]); // last point at top-right
        }

        [Fact]
        public void Render_PlotArea_HasExpectedDimensions()
        {
            var graph = new LineGraph
            {
                Width = 20,
                Height = 8,
                ShowAxis = false,
                ShowScale = false
            };

            var lines = graph.Render(Enumerable.Range(0, 40).Select(i => (double) i).ToArray()).Split(Text.NL);

            Assert.Equal(8, lines.Length);
            Assert.All(lines, line => Assert.Equal(20, line.Length));
        }

        [Fact]
        public void Render_MaximumPinnedBelowAllData_KeepsScaleValidAndLabelsNonInverted()
        {
            // Regression: with only Maximum pinned and every value above it, the auto minimum (dataMin=120) exceeded
            // the pinned max (100), giving an inverted range that plotted on the middle row and printed reversed
            // gutter labels (top "100", bottom "120"). The window must reconcile to a valid, non-inverted scale.
            var graph = new LineGraph
            {
                Width = 5,
                Height = 5,
                ShowAxis = true,
                ShowScale = true,
                Maximum = 100,
                PointChar = '*'
            };

            var result = graph.Render(new double[] {120, 150, 130, 140, 160});
            var lines = result.Split(Text.NL);

            Assert.Equal(6, lines.Length); // 5 rows + axis
            Assert.DoesNotContain("120", result); // the out-of-window auto bound never leaks in as a label
            Assert.StartsWith("100", lines[0]); // top scale label
            Assert.StartsWith("100", lines[4]); // bottom scale label equals the top: a valid, non-inverted window
            Assert.Contains('*', result); // points are still plotted
        }

        [Fact]
        public void Render_BothBoundsPinnedInverted_AreSwappedIntoAValidWindow()
        {
            // Minimum > Maximum is treated as the caller giving the pins backwards, so they are swapped and the
            // series plots normally (0 at the bottom, 10 at the top).
            var graph = new LineGraph
            {
                Width = 3,
                Height = 3,
                ShowAxis = false,
                ShowScale = false,
                Minimum = 10,
                Maximum = 0,
                PointChar = '*'
            };

            var expected =
                "  *" + Text.NL +
                " **" + Text.NL +
                "** ";

            Assert.Equal(expected, graph.Render(new double[] {0, 5, 10}));
        }

        [Theory]
        [InlineData(0, 5)]
        [InlineData(5, 0)]
        public void Render_NonPositiveDimensions_Throw(int width, int height)
        {
            var graph = new LineGraph {Width = width, Height = height};

            Assert.Throws<ArgumentOutOfRangeException>(() => graph.Render(new double[] {1, 2, 3}));
        }
    }
}
