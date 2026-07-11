using WolfCurses.Window.Control;
using Xunit;

namespace WolfCurses.Tests.Controls
{
    /// <summary>
    ///     Pins how <see cref="Sparkline" /> maps a series onto its eight-glyph block ramp: automatic and pinned
    ///     ranges, the flat/single-value degenerate case, and non-finite handling.
    /// </summary>
    public class SparklineTests
    {
        [Fact]
        public void Render_ZeroToSeven_WalksTheWholeRamp()
        {
            var spark = new Sparkline();

            // Values 0..7 over the range 0..7 land on ramp levels 0..7 exactly.
            Assert.Equal(Sparkline.DefaultRamp, spark.Render(new double[] {0, 1, 2, 3, 4, 5, 6, 7}));
        }

        [Fact]
        public void Render_Empty_ReturnsEmptyString()
        {
            Assert.Equal(string.Empty, new Sparkline().Render(new double[0]));
        }

        [Fact]
        public void Render_Null_ReturnsEmptyString()
        {
            Assert.Equal(string.Empty, new Sparkline().Render(null));
        }

        [Fact]
        public void Render_SingleValue_IsLowestGlyph()
        {
            // One value (or any flat series) has no range, so it renders as the lowest glyph.
            Assert.Equal("▁", new Sparkline().Render(new double[] {42}));
        }

        [Fact]
        public void Render_FlatSeries_AllLowestGlyph()
        {
            Assert.Equal("▁▁▁", new Sparkline().Render(new double[] {3, 3, 3}));
        }

        [Fact]
        public void Render_PinnedRange_ScalesAgainstIt()
        {
            var spark = new Sparkline {Minimum = 0, Maximum = 10};

            // 5 of 0..10 is the midpoint: round(0.5 * 7) = 4 -> the fifth glyph.
            Assert.Equal("▅", spark.Render(new double[] {5}));
        }

        [Fact]
        public void Render_NonFiniteValues_DrawLowestAndAreIgnoredForRange()
        {
            // Range comes from the finite {0, 10}; the NaN in the middle draws as the lowest glyph.
            Assert.Equal("▁▁█", new Sparkline().Render(new[] {0d, double.NaN, 10d}));
        }

        [Fact]
        public void Render_EmptyRamp_FallsBackToDefault()
        {
            var spark = new Sparkline {Ramp = string.Empty};

            Assert.Equal(Sparkline.DefaultRamp, spark.Render(new double[] {0, 1, 2, 3, 4, 5, 6, 7}));
        }

        [Fact]
        public void Render_CustomRamp_IsHonored()
        {
            var spark = new Sparkline {Ramp = ".:#"};

            // Range 0..2 over a 3-glyph ramp: 0 -> '.', 1 -> ':', 2 -> '#'.
            Assert.Equal(".:#", spark.Render(new double[] {0, 1, 2}));
        }
    }
}
