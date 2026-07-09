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
        public void DrawProgressBar_ValueAboveMax_OverfillsBarPast100Percent()
        {
            // Documents current behavior: no clamping; the filled section exceeds barSize and the empty loop
            // simply never runs.
            Assert.Equal(new string(FILLED, 20) + " 200.00%", TextProgress.DrawProgressBar(200, 100, 10));
        }

        [Fact]
        public void DrawProgressBar_MaxValueZero_ThrowsDivideByZeroException()
        {
            // Documents current behavior: the decimal division value/maxValue is unguarded.
            Assert.Throws<DivideByZeroException>(() => TextProgress.DrawProgressBar(0, 0, 10));
        }
    }
}
