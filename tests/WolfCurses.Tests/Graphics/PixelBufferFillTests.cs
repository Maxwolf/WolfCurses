using WolfCurses.Graphics;
using Xunit;

namespace WolfCurses.Tests.Graphics
{
    /// <summary>
    ///     Fill paints a rectangle and clips rather than throwing, which is what a compositing caller wants — and
    ///     what <c>GifDecoder</c>'s hand-written min/max loop was doing before it called this instead.
    /// </summary>
    public class PixelBufferFillTests
    {
        private static readonly Rgba32 _red = new(255, 0, 0, 255);
        private static readonly Rgba32 _blue = new(0, 0, 255, 128);

        [Fact]
        public void FillPaintsTheWholeBuffer()
        {
            var buffer = new PixelBuffer(4, 3);

            buffer.Fill(_red);

            for (var y = 0; y < 3; y++)
            for (var x = 0; x < 4; x++)
                Assert.Equal(_red, buffer.GetPixel(x, y));
        }

        [Fact]
        public void FillPaintsOnlyTheRectangleAsked()
        {
            var buffer = new PixelBuffer(5, 5);

            buffer.Fill(1, 1, 2, 3, _red);

            for (var y = 0; y < 5; y++)
            for (var x = 0; x < 5; x++)
            {
                var inside = x >= 1 && x < 3 && y >= 1 && y < 4;
                Assert.Equal(inside ? _red : default, buffer.GetPixel(x, y));
            }
        }

        [Fact]
        public void AlphaIsWrittenRatherThanBlended()
        {
            // Fill is a paint, not a composite - DrawImage is the one that blends. A caller painting a background
            // under a transparent sprite needs the background to actually be the colour it asked for.
            var buffer = new PixelBuffer(2, 2);
            buffer.Fill(_red);

            buffer.Fill(_blue);

            Assert.Equal(_blue, buffer.GetPixel(0, 0));
        }

        [Fact]
        public void ARectangleHangingOffTheEdgeIsClipped()
        {
            var buffer = new PixelBuffer(3, 3);

            buffer.Fill(-2, -2, 4, 4, _red);

            Assert.Equal(_red, buffer.GetPixel(0, 0));
            Assert.Equal(_red, buffer.GetPixel(1, 1));
            Assert.Equal(default, buffer.GetPixel(2, 2));
        }

        [Theory]
        [InlineData(10, 10, 2, 2)] // entirely past the right and bottom
        [InlineData(-9, -9, 2, 2)] // entirely before the left and top
        [InlineData(1, 1, 0, 5)] // no width
        [InlineData(1, 1, 5, -3)] // negative height
        public void ARectangleWithNothingInsideThePicturePaintsNothing(int x, int y, int width, int height)
        {
            var buffer = new PixelBuffer(4, 4);

            buffer.Fill(x, y, width, height, _red);

            for (var row = 0; row < 4; row++)
            for (var column = 0; column < 4; column++)
                Assert.Equal(default, buffer.GetPixel(column, row));
        }

        [Fact]
        public void FillMatchesWhatASetPixelLoopWouldHaveWritten()
        {
            // The equivalence that lets GifDecoder's loop be replaced without touching its differential tests.
            var byFill = new PixelBuffer(9, 7);
            var byLoop = new PixelBuffer(9, 7);

            byFill.Fill(2, 1, 5, 4, _red);

            var right = System.Math.Min(2 + 5, byLoop.Width);
            var bottom = System.Math.Min(1 + 4, byLoop.Height);
            for (var y = System.Math.Max(1, 0); y < bottom; y++)
            for (var x = System.Math.Max(2, 0); x < right; x++)
                byLoop.SetPixel(x, y, _red);

            Assert.Equal(byLoop.Data, byFill.Data);
        }
    }
}
