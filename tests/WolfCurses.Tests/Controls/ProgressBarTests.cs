using System;
using WolfCurses.Window.Control;
using Xunit;

namespace WolfCurses.Tests.Controls
{
    /// <summary>
    ///     The <see cref="ProgressBar" /> is a pure string producer; these pin its glyphs, clamping, and the
    ///     integer-percentage suffix (formatted with the invariant culture the module initializer pins).
    /// </summary>
    public class ProgressBarTests
    {
        private const char FILLED = '█';
        private const char EMPTY = '░';

        [Fact]
        public void Render_Half_NoBrackets_FillsHalfAndShowsPercent()
        {
            var bar = new ProgressBar {Width = 10, ShowBrackets = false};

            Assert.Equal(new string(FILLED, 5) + new string(EMPTY, 5) + "  50%", bar.Render(0.5));
        }

        [Fact]
        public void Render_Full_AllFilled()
        {
            var bar = new ProgressBar {Width = 10, ShowBrackets = false};

            Assert.Equal(new string(FILLED, 10) + " 100%", bar.Render(1.0));
        }

        [Fact]
        public void Render_Zero_AllEmptyAndZeroPercent()
        {
            var bar = new ProgressBar {Width = 10, ShowBrackets = false};

            Assert.Equal(new string(EMPTY, 10) + "   0%", bar.Render(0.0));
        }

        [Fact]
        public void Render_Brackets_WrapTheBar()
        {
            var bar = new ProgressBar {Width = 4, ShowBrackets = true};

            Assert.Equal("[" + new string(FILLED, 2) + new string(EMPTY, 2) + "]  50%", bar.Render(0.5));
        }

        [Fact]
        public void Render_Label_PrependedWithSpace()
        {
            var bar = new ProgressBar {Width = 4, ShowBrackets = false, ShowPercentage = false, Label = "Load"};

            Assert.Equal("Load " + new string(FILLED, 2) + new string(EMPTY, 2), bar.Render(0.5));
        }

        [Fact]
        public void Render_ValueOverMaximum_ClampsToFull()
        {
            var bar = new ProgressBar {Width = 8, ShowBrackets = false};

            Assert.Equal(new string(FILLED, 8) + " 100%", bar.Render(150, 100));
        }

        [Fact]
        public void Render_NegativeFraction_ClampsToEmpty()
        {
            var bar = new ProgressBar {Width = 8, ShowBrackets = false};

            Assert.Equal(new string(EMPTY, 8) + "   0%", bar.Render(-0.5));
        }

        [Theory]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NegativeInfinity)]
        public void Render_NonFiniteFraction_TreatedAsEmpty(double fraction)
        {
            var bar = new ProgressBar {Width = 6, ShowBrackets = false};

            Assert.Equal(new string(EMPTY, 6) + "   0%", bar.Render(fraction));
        }

        [Fact]
        public void Render_MaximumZero_RendersEmptyInsteadOfDividingByZero()
        {
            var bar = new ProgressBar {Width = 6, ShowBrackets = false};

            Assert.Equal(new string(EMPTY, 6) + "   0%", bar.Render(5, 0));
        }

        [Fact]
        public void Render_WidthLessThanOne_Throws()
        {
            var bar = new ProgressBar {Width = 0};

            Assert.Throws<ArgumentOutOfRangeException>(() => bar.Render(0.5));
        }

        [Fact]
        public void Render_NearlyFull_DoesNotShowAFullBarBeforeHundredPercent()
        {
            // Regression: 0.95 * 10 rounds to 10 (a full bar) even though the label reads 95%. The bar must reserve
            // its last cell until the percentage actually reaches 100.
            var bar = new ProgressBar {Width = 10, ShowBrackets = false};

            Assert.Equal(new string(FILLED, 9) + new string(EMPTY, 1) + "  95%", bar.Render(0.95));
        }

        [Fact]
        public void Render_RoundsUpToHundredPercent_ShowsAFullBar()
        {
            // Once the percentage rounds to 100 the bar is completely full, so the two agree.
            var bar = new ProgressBar {Width = 10, ShowBrackets = false};

            Assert.Equal(new string(FILLED, 10) + " 100%", bar.Render(0.999));
        }

        [Fact]
        public void Render_TinyNonZero_ShowsAtLeastOneCell()
        {
            // A percentage that reads above 0 must not draw a completely empty bar.
            var bar = new ProgressBar {Width = 10, ShowBrackets = false};

            Assert.Equal(new string(FILLED, 1) + new string(EMPTY, 9) + "   2%", bar.Render(0.02));
        }

        [Fact]
        public void Render_RoundsDownToZeroPercent_ShowsAnEmptyBar()
        {
            var bar = new ProgressBar {Width = 10, ShowBrackets = false};

            Assert.Equal(new string(EMPTY, 10) + "   0%", bar.Render(0.004));
        }

        [Fact]
        public void Render_CustomGlyphs_AreHonored()
        {
            var bar = new ProgressBar
            {
                Width = 4,
                ShowBrackets = false,
                ShowPercentage = false,
                FilledChar = '#',
                EmptyChar = '-'
            };

            Assert.Equal("##--", bar.Render(0.5));
        }
    }
}
