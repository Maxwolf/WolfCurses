using System;
using WolfCurses.Window.Control;
using Xunit;

namespace WolfCurses.Tests.Controls
{
    /// <summary>
    ///     Percentages are formatted with "N2"; TestCulture pins the invariant culture so the decimal separator is
    ///     always a dot.
    /// </summary>
    public class TextProgressTests
    {
        private const char FILLED = '█';
        private const char EMPTY = '▒';

        [Fact]
        public void DrawProgressBar_Zero_AllEmptyBlocks()
        {
            Assert.Equal(new string(EMPTY, 10) + " 0.00%", TextProgress.DrawProgressBar(0, 100, 10));
        }

        [Fact]
        public void DrawProgressBar_Half_HalfFilled()
        {
            Assert.Equal(new string(FILLED, 5) + new string(EMPTY, 5) + " 50.00%",
                TextProgress.DrawProgressBar(50, 100, 10));
        }

        [Fact]
        public void DrawProgressBar_Full_AllFilledBlocks()
        {
            Assert.Equal(new string(FILLED, 10) + " 100.00%", TextProgress.DrawProgressBar(100, 100, 10));
        }

        [Fact]
        public void DrawProgressBar_ValueAboveMax_ClampsToFullBar()
        {
            // Overshoot clamps to the maximum so the bar never exceeds its size or reports over 100%.
            Assert.Equal(new string(FILLED, 10) + " 100.00%", TextProgress.DrawProgressBar(200, 100, 10));
        }

        [Fact]
        public void DrawProgressBar_NegativeValue_ClampsToEmptyBar()
        {
            Assert.Equal(new string(EMPTY, 10) + " 0.00%", TextProgress.DrawProgressBar(-25, 100, 10));
        }

        [Fact]
        public void DrawProgressBar_FullWithAwkwardBarSize_FillsEveryCell()
        {
            // Regression: the old floor(perc / (1/barSize)) decimal math left one cell unfilled at exactly 100%
            // for bar sizes whose reciprocal is not exact, like 7.
            Assert.Equal(new string(FILLED, 7) + " 100.00%", TextProgress.DrawProgressBar(7, 7, 7));
        }

        [Fact]
        public void DrawProgressBar_MaxValueZero_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => TextProgress.DrawProgressBar(0, 0, 10));
        }

        [Fact]
        public void DrawProgressBar_BarSizeZero_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => TextProgress.DrawProgressBar(5, 10, 0));
        }
    }
}
