using WolfCurses.Graphics;
using Xunit;

namespace WolfCurses.Tests.Graphics
{
    /// <summary>
    ///     Covers the <see cref="AnsiImage" /> compositing facade — overlaying one (possibly transparent) image on top
    ///     of another so both are visible — and its immutability guarantees.
    /// </summary>
    public class AnsiImageCompositingTests
    {
        private static AnsiImage Solid(int width, int height, Rgba32 color)
        {
            var buffer = new PixelBuffer(width, height);
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                buffer.SetPixel(x, y, color);
            return AnsiImage.FromPixels(buffer);
        }

        [Fact]
        public void Overlay_CentersForegroundOverBackground()
        {
            var background = Solid(4, 4, new Rgba32(255, 0, 0, 255)); // red
            var foreground = Solid(2, 2, new Rgba32(0, 0, 255, 255)); // blue, opaque

            var result = background.Overlay(foreground); // centered => covers the middle 2x2 block

            Assert.Equal(4, result.Width);
            Assert.Equal(4, result.Height);
            Assert.Equal(255, result.Pixels.GetPixel(0, 0).R); // corner still red
            Assert.Equal(255, result.Pixels.GetPixel(2, 2).B); // center now blue
            Assert.Equal(0, result.Pixels.GetPixel(2, 2).R);
        }

        [Fact]
        public void Overlay_DoesNotMutateEitherOriginal()
        {
            var background = Solid(2, 2, new Rgba32(255, 0, 0, 255));
            var foreground = Solid(2, 2, new Rgba32(0, 0, 255, 255));

            _ = background.Overlay(foreground, 0, 0);

            Assert.Equal(255, background.Pixels.GetPixel(0, 0).R); // background untouched
            Assert.Equal(255, foreground.Pixels.GetPixel(0, 0).B); // foreground untouched
        }

        [Fact]
        public void Overlay_TransparentForeground_ShowsBackgroundThrough()
        {
            var background = Solid(2, 2, new Rgba32(10, 20, 30, 255));
            var foreground = Solid(2, 2, new Rgba32(0, 0, 255, 0)); // fully transparent

            var result = background.Overlay(foreground, 0, 0);

            var pixel = result.Pixels.GetPixel(0, 0);
            Assert.Equal(10, pixel.R);
            Assert.Equal(20, pixel.G);
            Assert.Equal(30, pixel.B);
        }

        [Fact]
        public void Composite_Static_MatchesInstanceOverlay()
        {
            var background = Solid(3, 3, new Rgba32(0, 0, 0, 255));
            var foreground = Solid(1, 1, new Rgba32(255, 255, 255, 255));

            var viaStatic = AnsiImage.Composite(background, foreground);
            var viaInstance = background.Overlay(foreground);

            Assert.Equal(viaInstance.Pixels.Data, viaStatic.Pixels.Data);
        }

        [Fact]
        public void Resize_ChangesPixelDimensions()
        {
            var image = Solid(4, 4, new Rgba32(1, 2, 3, 255));

            var smaller = image.Resize(2, 2);

            Assert.Equal(2, smaller.Width);
            Assert.Equal(2, smaller.Height);
        }
    }
}
