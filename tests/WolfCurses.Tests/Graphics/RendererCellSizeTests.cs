using WolfCurses.Graphics;
using Xunit;

namespace WolfCurses.Tests.Graphics
{
    /// <summary>
    ///     How many image pixels a renderer puts in one character cell.
    ///     <para>
    ///         The number a streaming source needs: given a window this many columns by this many rows, produce
    ///         pixels at that size and the resample never happens. These are pinned because they are answered
    ///         through the interface's <i>defaults</i> for one implementation and through real properties for the
    ///         others, and nothing else in the suite would notice if a true-pixel renderer quietly started
    ///         answering the half-block numbers.
    ///     </para>
    /// </summary>
    public class RendererCellSizeTests
    {
        [Fact]
        public void HalfBlocksTakeTheDefaultsOfOneAcrossAndTwoDown()
        {
            IImageRenderer renderer = new HalfBlockImageRenderer();

            // Two down, because that renderer's whole trick is an upper and a lower half in each cell.
            Assert.Equal(1, renderer.CellPixelWidth);
            Assert.Equal(2, renderer.CellPixelHeight);
        }

        [Fact]
        public void TheTruePixelRenderersAnswerWithWhatTheyWereBuiltWith()
        {
            IImageRenderer sixel = new SixelImageRenderer();
            IImageRenderer kitty = new KittyImageRenderer();

            Assert.Equal(10, sixel.CellPixelWidth);
            Assert.Equal(20, sixel.CellPixelHeight);
            Assert.Equal(10, kitty.CellPixelWidth);
            Assert.Equal(20, kitty.CellPixelHeight);

            IImageRenderer narrow = new SixelImageRenderer(6, 13);

            Assert.Equal(6, narrow.CellPixelWidth);
            Assert.Equal(13, narrow.CellPixelHeight);
        }

        [Fact]
        public void ARendererThatSaysNothingStillAnswers()
        {
            // A third-party renderer written before these members existed compiles and gives the safe answer,
            // which is the whole point of them being default interface members.
            IImageRenderer plain = new SilentRenderer();

            Assert.Equal(1, plain.CellPixelWidth);
            Assert.Equal(2, plain.CellPixelHeight);
            Assert.False(plain.DrawsTruePixels);
            Assert.Equal(nameof(SilentRenderer), plain.Name);
        }

        [Fact]
        public void TheNumbersAreWhatASourceWouldScaleTo()
        {
            // The arithmetic the whole thing exists for, spelled out so a change to either default is visible as a
            // change to the size a caller would ask ffmpeg, a camera or a plotter for.
            IImageRenderer sixel = new SixelImageRenderer();
            IImageRenderer blocks = new HalfBlockImageRenderer();

            Assert.Equal((700, 360), Target(sixel, 70, 18));
            Assert.Equal((70, 36), Target(blocks, 70, 18));
        }

        /// <summary>How big a picture should arrive for a window of this many columns and rows.</summary>
        private static (int Width, int Height) Target(IImageRenderer renderer, int columns, int rows)
        {
            return (columns * renderer.CellPixelWidth, rows * renderer.CellPixelHeight);
        }

        /// <summary>A renderer that implements nothing it does not have to, which is the compatibility case.</summary>
        private sealed class SilentRenderer : IImageRenderer
        {
            public string Render(PixelBuffer image, AnsiImageOptions options)
            {
                return string.Empty;
            }
        }
    }
}
