using System;
using WolfCurses.Graphics;
using Xunit;

namespace WolfCurses.Tests.Graphics
{
    /// <summary>
    ///     Exercises the decoder-agnostic pixel container and, most importantly, its area-averaging resize — including
    ///     the premultiplied-alpha behavior that stops transparent pixels from tinting their opaque neighbours.
    /// </summary>
    public class PixelBufferTests
    {
        [Fact]
        public void Constructor_WrongDataLength_Throws()
        {
            Assert.Throws<ArgumentException>(() => new PixelBuffer(2, 2, new byte[8]));
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 0)]
        [InlineData(-1, 4)]
        public void Constructor_NonPositiveDimensions_Throws(int width, int height)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PixelBuffer(width, height, new byte[16]));
        }

        [Fact]
        public void SetPixel_ThenGetPixel_RoundTrips()
        {
            var buffer = new PixelBuffer(2, 2);
            var color = new Rgba32(10, 20, 30, 40);
            buffer.SetPixel(1, 1, color);

            var read = buffer.GetPixel(1, 1);
            Assert.Equal(10, read.R);
            Assert.Equal(20, read.G);
            Assert.Equal(30, read.B);
            Assert.Equal(40, read.A);
        }

        [Fact]
        public void GetPixel_OutOfBounds_Throws()
        {
            var buffer = new PixelBuffer(2, 2);
            Assert.Throws<ArgumentOutOfRangeException>(() => buffer.GetPixel(2, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => buffer.GetPixel(0, 2));
        }

        [Fact]
        public void Resize_SameDimensions_ReturnsIndependentCopy()
        {
            var buffer = new PixelBuffer(1, 1);
            buffer.SetPixel(0, 0, new Rgba32(1, 2, 3, 4));

            var resized = buffer.Resize(1, 1);

            Assert.NotSame(buffer.Data, resized.Data);
            Assert.Equal(buffer.Data, resized.Data);
        }

        [Fact]
        public void Resize_SolidColorDownscale_PreservesColor()
        {
            // A 4x4 block of one opaque color must average down to exactly that color.
            var buffer = new PixelBuffer(4, 4);
            for (var y = 0; y < 4; y++)
            for (var x = 0; x < 4; x++)
                buffer.SetPixel(x, y, new Rgba32(60, 120, 180, 255));

            var small = buffer.Resize(1, 1);
            var pixel = small.GetPixel(0, 0);

            Assert.Equal(60, pixel.R);
            Assert.Equal(120, pixel.G);
            Assert.Equal(180, pixel.B);
            Assert.Equal(255, pixel.A);
        }

        [Fact]
        public void Resize_TransparentNeighbour_DoesNotBleedColorButAveragesAlpha()
        {
            // Left pixel opaque red, right pixel fully transparent blue. Averaging to a single pixel must keep the hue
            // pure red (the transparent blue contributes no color) while the alpha averages to half.
            var buffer = new PixelBuffer(2, 1);
            buffer.SetPixel(0, 0, new Rgba32(255, 0, 0, 255));
            buffer.SetPixel(1, 0, new Rgba32(0, 0, 255, 0));

            var merged = buffer.Resize(1, 1).GetPixel(0, 0);

            Assert.Equal(255, merged.R);
            Assert.Equal(0, merged.G);
            Assert.Equal(0, merged.B); // no blue bleed from the transparent pixel
            Assert.Equal(128, merged.A); // (255 + 0) / 2, rounded away from zero
        }

        [Fact]
        public void Resize_FullyTransparentRegion_StaysTransparent()
        {
            var buffer = new PixelBuffer(2, 2); // all zero => transparent black
            var merged = buffer.Resize(1, 1).GetPixel(0, 0);
            Assert.Equal(0, merged.A);
        }

        [Fact]
        public void Resize_Upscale_ProducesRequestedSize()
        {
            var buffer = new PixelBuffer(1, 1);
            buffer.SetPixel(0, 0, new Rgba32(200, 100, 50, 255));

            var big = buffer.Resize(3, 2);

            Assert.Equal(3, big.Width);
            Assert.Equal(2, big.Height);
            // Upscaling a solid pixel keeps the color everywhere.
            Assert.Equal(200, big.GetPixel(2, 1).R);
            Assert.Equal(100, big.GetPixel(2, 1).G);
        }

        [Fact]
        public void Constructor_DimensionsOverflowByteCount_ThrowsInsteadOfWrapping()
        {
            // 40000 * 40000 * 4 = 6.4e9 overflows a 32-bit byte count; it must be rejected, not silently wrapped into
            // a small, plausible-looking length that would later crash or corrupt memory.
            Assert.Throws<ArgumentOutOfRangeException>(() => new PixelBuffer(40000, 40000));
        }

        [Fact]
        public void Resize_DimensionsOverflowByteCount_ThrowsInsteadOfWrapping()
        {
            var buffer = new PixelBuffer(2, 2);
            Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Resize(40000, 40000));
        }

        [Fact]
        public void Crop_ExtractsRequestedRegion()
        {
            // Fill each pixel with a color encoding its (x,y) so we can verify the exact region copied.
            var buffer = new PixelBuffer(4, 4);
            for (var y = 0; y < 4; y++)
            for (var x = 0; x < 4; x++)
                buffer.SetPixel(x, y, new Rgba32((byte) (x * 10), (byte) (y * 10), 0, 255));

            var region = buffer.Crop(1, 2, 2, 2);

            Assert.Equal(2, region.Width);
            Assert.Equal(2, region.Height);
            Assert.Equal(10, region.GetPixel(0, 0).R); // source x=1
            Assert.Equal(20, region.GetPixel(0, 0).G); // source y=2
            Assert.Equal(20, region.GetPixel(1, 1).R); // source x=2
            Assert.Equal(30, region.GetPixel(1, 1).G); // source y=3
        }

        [Fact]
        public void Crop_OutsideBounds_Throws()
        {
            var buffer = new PixelBuffer(4, 4);
            Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Crop(3, 0, 2, 2));
        }

        [Fact]
        public void DrawImage_OpaqueOverlay_ReplacesPixels()
        {
            var background = new PixelBuffer(2, 2);
            for (var y = 0; y < 2; y++)
            for (var x = 0; x < 2; x++)
                background.SetPixel(x, y, new Rgba32(255, 0, 0, 255));

            var overlay = new PixelBuffer(1, 1);
            overlay.SetPixel(0, 0, new Rgba32(0, 0, 255, 255));

            background.DrawImage(overlay, 0, 0);

            Assert.Equal(0, background.GetPixel(0, 0).R);
            Assert.Equal(255, background.GetPixel(0, 0).B); // replaced with blue
            Assert.Equal(255, background.GetPixel(1, 1).R); // untouched red
        }

        [Fact]
        public void DrawImage_FullyTransparentOverlay_LeavesBackgroundUntouched()
        {
            var background = new PixelBuffer(1, 1);
            background.SetPixel(0, 0, new Rgba32(10, 20, 30, 255));

            var overlay = new PixelBuffer(1, 1);
            overlay.SetPixel(0, 0, new Rgba32(0, 0, 255, 0)); // transparent

            background.DrawImage(overlay, 0, 0);

            var pixel = background.GetPixel(0, 0);
            Assert.Equal(10, pixel.R);
            Assert.Equal(20, pixel.G);
            Assert.Equal(30, pixel.B);
            Assert.Equal(255, pixel.A);
        }

        [Fact]
        public void DrawImage_SemiTransparentOverOpaque_Blends()
        {
            var background = new PixelBuffer(1, 1);
            background.SetPixel(0, 0, new Rgba32(0, 0, 0, 255)); // black

            var overlay = new PixelBuffer(1, 1);
            overlay.SetPixel(0, 0, new Rgba32(255, 255, 255, 128)); // 50% white

            background.DrawImage(overlay, 0, 0);

            var pixel = background.GetPixel(0, 0);
            Assert.Equal(128, pixel.R); // halfway between black and white
            Assert.Equal(255, pixel.A); // stays fully opaque
        }

        [Fact]
        public void DrawImage_OverTransparentBackground_KeepsOverlayTranslucency()
        {
            // Compositing a translucent overlay onto a transparent background must keep the overlay's own translucency
            // (so you can still see through it / stack another image under it) rather than forcing it opaque.
            var background = new PixelBuffer(1, 1); // transparent black
            var overlay = new PixelBuffer(1, 1);
            overlay.SetPixel(0, 0, new Rgba32(100, 100, 100, 128));

            background.DrawImage(overlay, 0, 0);

            var pixel = background.GetPixel(0, 0);
            Assert.Equal(100, pixel.R);
            Assert.Equal(128, pixel.A); // still half transparent
        }

        [Fact]
        public void DrawImage_NegativeOffset_ClipsWithoutThrowing()
        {
            var background = new PixelBuffer(2, 2);
            var overlay = new PixelBuffer(2, 2);
            for (var y = 0; y < 2; y++)
            for (var x = 0; x < 2; x++)
                overlay.SetPixel(x, y, new Rgba32(0, 0, 255, 255));

            // Overlay shifted up-left so only its bottom-right pixel lands on the background at (0,0).
            background.DrawImage(overlay, -1, -1);

            Assert.Equal(255, background.GetPixel(0, 0).B);
            Assert.Equal(0, background.GetPixel(1, 1).A); // still untouched/transparent
        }
    }
}
