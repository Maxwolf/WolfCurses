using System;
using WolfCurses.Graphics;
using WolfCurses.Tests.Support;
using Xunit;

namespace WolfCurses.Tests.Graphics
{
    /// <summary>
    ///     Pins the sixel renderer's enlargement path: when the fit makes the picture bigger on both axes, the encoder
    ///     never materializes the enlarged buffer — it builds the palette from the source pixels and stretches runs
    ///     arithmetically while encoding, which is what took a moving sprite scene from ~200ms a frame to ~17ms. These
    ///     tests prove the arithmetic stretch paints exactly what a nearest-neighbour enlargement would have, that
    ///     transparency and background flattening survive the shortcut, and that shrinking still goes the old way
    ///     (area-averaging is the point of a downscale and must not be traded for speed).
    ///     <para>
    ///         Decoding uses the same strict <see cref="SixelDecoder" /> as the round-trip tests, and colors stay on
    ///         values that survive sixel's 0-100 percent scale, for the same reason given there.
    ///     </para>
    /// </summary>
    public class SixelUpscaleTests
    {
        private static readonly Rgba32 _red = new(255, 0, 0, 255);
        private static readonly Rgba32 _green = new(0, 255, 0, 255);
        private static readonly Rgba32 _blue = new(0, 0, 255, 255);
        private static readonly Rgba32 _white = new(255, 255, 255, 255);
        private static readonly Rgba32 _transparent = new(0, 0, 0, 0);

        /// <summary>Renders with one-pixel cells at exactly the given output size, then decodes the payload.</summary>
        private static PixelBuffer RenderAt(PixelBuffer image, int outWidth, int outHeight,
            Rgb24? background = null)
        {
            var rendered = new SixelImageRenderer(1, 1).Render(image, new AnsiImageOptions
            {
                MaxColumns = outWidth,
                MaxRows = outHeight,
                Fit = AnsiImageFitEnum.Stretch,
                BackgroundColor = background
            });

            var payload = rendered.Split('\n')[0].TrimEnd('\r').Substring(1);
            return SixelDecoder.Decode(payload);
        }

        private static PixelBuffer Build(int width, int height, Func<int, int, Rgba32> paint)
        {
            var buffer = new PixelBuffer(width, height);
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                buffer.SetPixel(x, y, paint(x, y));
            return buffer;
        }

        private static void AssertPixel(PixelBuffer image, int x, int y, Rgba32 want)
        {
            var got = image.GetPixel(x, y);
            Assert.True(want.R == got.R && want.G == got.G && want.B == got.B && want.A == got.A,
                $"Pixel ({x},{y}) expected ({want.R},{want.G},{want.B},{want.A}) " +
                $"but decoded ({got.R},{got.G},{got.B},{got.A}).");
        }

        [Fact]
        public void Upscale_ReplicatesEachSourcePixelAsABlock()
        {
            // A 2x2 of four distinct colors tripled to 6x6: nearest-neighbour means each source pixel becomes a 3x3
            // block, with no blending anywhere — four colors in, exactly four colors out, in the right places.
            var image = Build(2, 2, (x, y) => y == 0 ? (x == 0 ? _red : _green) : (x == 0 ? _blue : _white));
            var decoded = RenderAt(image, 6, 6);

            Assert.Equal(6, decoded.Width);
            Assert.Equal(6, decoded.Height);

            for (var y = 0; y < 6; y++)
            for (var x = 0; x < 6; x++)
                AssertPixel(decoded, x, y, image.GetPixel(x / 3, y / 3));
        }

        [Fact]
        public void Upscale_ByANonIntegerFactor_SplitsColumnsAtPixelCenters()
        {
            // Three columns into eight cannot divide evenly. Center-based mapping reads source column
            // (2*dx+1)*3/16 for output column dx, which worked out on paper is 0,0,0,1,1,2,2,2 — blocks of three,
            // two and three. The uneven middle is the point: arithmetic run-stretching has to reproduce exactly
            // these group widths, not an idealized even split.
            var image = Build(3, 1, (x, _) => x == 0 ? _red : x == 1 ? _green : _blue);
            var decoded = RenderAt(image, 8, 1);

            for (var dx = 0; dx < 8; dx++)
            {
                var sx = (2 * dx + 1) * 3 / 16;
                AssertPixel(decoded, dx, 0, image.GetPixel(sx, 0));
            }
        }

        [Fact]
        public void Upscale_LeavesTransparentSourcePixelsUndrawn()
        {
            // The middle third of the picture is transparent; enlarged four-fold it must stay a hole (alpha zero all
            // the way through), not become a stretched smear of some neighbour's color.
            var image = Build(3, 1, (x, _) => x == 0 ? _red : x == 1 ? _transparent : _blue);
            var decoded = RenderAt(image, 12, 4);

            for (var y = 0; y < 4; y++)
            {
                for (var x = 0; x < 4; x++)
                    AssertPixel(decoded, x, y, _red);
                for (var x = 4; x < 8; x++)
                    Assert.Equal(0, decoded.GetPixel(x, y).A);
                for (var x = 8; x < 12; x++)
                    AssertPixel(decoded, x, y, _blue);
            }
        }

        [Fact]
        public void Upscale_FlattensOntoTheBackgroundColorFirst()
        {
            // With a background color the flatten happens at source resolution before the stretch — a pointwise
            // operation commutes with nearest-neighbour enlargement, so this is exact, and the transparent half must
            // come out as solid background rather than as a hole.
            var image = Build(2, 1, (x, _) => x == 0 ? _transparent : _blue);
            var decoded = RenderAt(image, 6, 3, new Rgb24(255, 0, 0));

            for (var y = 0; y < 3; y++)
            {
                for (var x = 0; x < 3; x++)
                    AssertPixel(decoded, x, y, _red);
                for (var x = 3; x < 6; x++)
                    AssertPixel(decoded, x, y, _blue);
            }
        }

        [Fact]
        public void Upscale_ClaimsTheRowsTheEnlargedPictureActuallyCovers()
        {
            // Real cell geometry: a 30x30 image into a 9x6-cell budget at 10x20 pixels per cell fits (Contain) at
            // 90x90 pixels, which is five 20-pixel rows. The payload must claim exactly those five rows — one payload
            // line plus four placeholders — and state the enlarged size, not the source size, in its raster
            // attributes, or the presenter's row accounting and the terminal's idea of the picture disagree.
            var image = Build(30, 30, (_, _) => _green);
            var rendered = new SixelImageRenderer().Render(image, new AnsiImageOptions
            {
                MaxColumns = 9,
                MaxRows = 6
            });

            var lines = rendered.Split(Environment.NewLine);
            Assert.Equal(5, lines.Length);

            var decoded = SixelDecoder.Decode(lines[0].Substring(1));
            Assert.Equal(90, decoded.Width);
            Assert.Equal(90, decoded.Height);
        }

        [Fact]
        public void Downscale_StillAveragesRatherThanPickingNearestPixels()
        {
            // Shrinking takes the legacy resize-first path, where a red and a blue pixel squeezed into one average to
            // purple. Nearest-neighbour would pick one of them whole — so purple here is proof the enlargement
            // shortcut did not swallow the downscale case.
            var image = Build(2, 1, (x, _) => x == 0 ? _red : _blue);
            var decoded = RenderAt(image, 1, 1);

            AssertPixel(decoded, 0, 0, new Rgba32(128, 0, 128, 255));
        }
    }
}
