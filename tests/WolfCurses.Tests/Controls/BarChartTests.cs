using System;
using WolfCurses.Tests.Support;
using WolfCurses.Window.Control;
using Xunit;

namespace WolfCurses.Tests.Controls
{
    /// <summary>
    ///     Pins <see cref="BarChart" /> layout: labels padded to a common width, bars scaled to the largest value, the
    ///     optional track and value columns, and how negative/non-finite values are handled.
    /// </summary>
    public class BarChartTests
    {
        [Fact]
        public void Render_ScalesBarsToLargestValue_AndShowsValues()
        {
            var chart = new BarChart {Width = 10};

            var expected =
                "A │ █████ 5" + Text.NL +
                "B │ ██████████ 10";

            Assert.Equal(expected, chart.Render(new[]
            {
                new BarChartValue("A", 5),
                new BarChartValue("B", 10)
            }));
        }

        [Fact]
        public void Render_PadsLabelsToCommonWidth()
        {
            var chart = new BarChart {Width = 4, ShowValues = false};

            var expected =
                "Wood │ ████" + Text.NL +
                "Iron │ ██";

            Assert.Equal(expected, chart.Render(new[]
            {
                new BarChartValue("Wood", 12),
                new BarChartValue("Iron", 6)
            }));
        }

        [Fact]
        public void Render_ShowTrack_PadsBarsToWidth()
        {
            var chart = new BarChart {Width = 10, ShowTrack = true, ShowValues = false};

            var expected =
                "A │ █████░░░░░" + Text.NL +
                "B │ ██████████";

            Assert.Equal(expected, chart.Render(new[]
            {
                new BarChartValue("A", 5),
                new BarChartValue("B", 10)
            }));
        }

        [Fact]
        public void Render_NegativeValue_DrawsEmptyBarButShowsTheNumber()
        {
            var chart = new BarChart {Width = 10};

            var expected =
                "A │ ██████████ 10" + Text.NL +
                "B │  -3";

            Assert.Equal(expected, chart.Render(new[]
            {
                new BarChartValue("A", 10),
                new BarChartValue("B", -3)
            }));
        }

        [Fact]
        public void Render_Empty_ReturnsEmptyString()
        {
            Assert.Equal(string.Empty, new BarChart().Render(new BarChartValue[0]));
        }

        [Fact]
        public void Render_Null_ReturnsEmptyString()
        {
            Assert.Equal(string.Empty, new BarChart().Render(null));
        }

        [Fact]
        public void Render_AllZero_DrawsNoBars()
        {
            var chart = new BarChart {Width = 10, ShowValues = false};

            var expected =
                "A │ " + Text.NL +
                "B │ ";

            Assert.Equal(expected, chart.Render(new[]
            {
                new BarChartValue("A", 0),
                new BarChartValue("B", 0)
            }));
        }

        [Fact]
        public void Render_WidthLessThanOne_Throws()
        {
            var chart = new BarChart {Width = 0};

            Assert.Throws<ArgumentOutOfRangeException>(() => chart.Render(new[] {new BarChartValue("A", 1)}));
        }

        [Fact]
        public void Render_NonFiniteValue_TreatedAsZero()
        {
            var chart = new BarChart {Width = 10, ShowValues = false};

            var expected =
                "A │ ██████████" + Text.NL +
                "B │ ";

            Assert.Equal(expected, chart.Render(new[]
            {
                new BarChartValue("A", 10),
                new BarChartValue("B", double.NaN)
            }));
        }
    }
}
