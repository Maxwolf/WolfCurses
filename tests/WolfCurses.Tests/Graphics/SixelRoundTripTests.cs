using System;
using WolfCurses.Graphics;
using WolfCurses.Tests.Support;
using Xunit;

namespace WolfCurses.Tests.Graphics
{
    /// <summary>
    ///     Encodes pictures with <see cref="SixelImageRenderer" /> and reads them back with <see cref="SixelDecoder" />,
    ///     checking the pixels survive the trip. Where the other sixel tests pin the exact characters emitted, these
    ///     prove those characters actually reconstruct the picture — catching whole-format mistakes (bands in the wrong
    ///     order, an inverted bit, a run-length off by one, one color's pass overwriting another's) that pinning cannot
    ///     see because it only ever compares against what the encoder was written to do.
    ///     <para>
    ///         Colors are drawn from black and full-intensity channels throughout: sixel states its color components as
    ///         percentages, so only values that land exactly on that 0-100 scale survive a round trip unchanged, and
    ///         anything else would fail on the format's own rounding rather than on a real defect.
    ///     </para>
    /// </summary>
    public class SixelRoundTripTests
    {
        private static readonly Rgba32 _red = new(255, 0, 0, 255);
        private static readonly Rgba32 _green = new(0, 255, 0, 255);
        private static readonly Rgba32 _blue = new(0, 0, 255, 255);
        private static readonly Rgba32 _white = new(255, 255, 255, 255);
        private static readonly Rgba32 _transparent = new(0, 0, 0, 0);

        /// <summary>A renderer whose cell is one pixel, so the picture is encoded at exactly the size given.</summary>
        private static SixelImageRenderer Renderer() => new(1, 1);

        /// <summary>Renders at exactly the image's own size, then decodes the payload back into pixels.</summary>
        private static PixelBuffer RoundTrip(PixelBuffer image)
        {
            var rendered = Renderer().Render(image, new AnsiImageOptions
            {
                MaxColumns = image.Width,
                MaxRows = image.Height,
                Fit = AnsiImageFitEnum.Stretch
            });

            var payload = rendered.Split('\n')[0].TrimEnd('\r').Substring(1);
            return SixelDecoder.Decode(payload);
        }

        private static void AssertSamePixels(PixelBuffer expected, PixelBuffer actual)
        {
            Assert.Equal(expected.Width, actual.Width);
            Assert.Equal(expected.Height, actual.Height);

            for (var y = 0; y < expected.Height; y++)
            {
                for (var x = 0; x < expected.Width; x++)
                {
                    var want = expected.GetPixel(x, y);
                    var got = actual.GetPixel(x, y);

                    // Anything below the alpha threshold is never drawn, so it must come back untouched (transparent)
                    // rather than as some color.
                    if (want.A < 128)
                    {
                        Assert.True(got.A == 0, $"Pixel ({x},{y}) should have been left undrawn but has alpha {got.A}.");
                        continue;
                    }

                    Assert.True(want.R == got.R && want.G == got.G && want.B == got.B && got.A == 255,
                        $"Pixel ({x},{y}) expected ({want.R},{want.G},{want.B}) but decoded ({got.R},{got.G},{got.B},{got.A}).");
                }
            }
        }

        private static PixelBuffer Build(int width, int height, Func<int, int, Rgba32> paint)
        {
            var buffer = new PixelBuffer(width, height);
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                buffer.SetPixel(x, y, paint(x, y));
            return buffer;
        }

        [Fact]
        public void RoundTrip_SinglePixel()
        {
            var image = Build(1, 1, (_, _) => _red);
            AssertSamePixels(image, RoundTrip(image));
        }

        [Fact]
        public void RoundTrip_VerticalStripes_PreservesColumnOrder()
        {
            var image = Build(4, 1, (x, _) => x % 2 == 0 ? _red : _blue);
            AssertSamePixels(image, RoundTrip(image));
        }

        [Fact]
        public void RoundTrip_HorizontalStripes_PreservesBitOrderWithinABand()
        {
            // Each row of the six-pixel band is a different color, which only reconstructs if the bit order is right.
            var image = Build(1, 6, (_, y) => y % 2 == 0 ? _red : _green);
            AssertSamePixels(image, RoundTrip(image));
        }

        [Fact]
        public void RoundTrip_TallerThanOneBand_StacksBandsInOrder()
        {
            // Twenty rows is four bands, the last one only partly full.
            var image = Build(3, 20, (_, y) => y < 10 ? _red : _blue);
            AssertSamePixels(image, RoundTrip(image));
        }

        [Fact]
        public void RoundTrip_HeightNotAMultipleOfSix_DoesNotInventPixels()
        {
            var image = Build(2, 7, (_, _) => _green);
            AssertSamePixels(image, RoundTrip(image));
        }

        [Fact]
        public void RoundTrip_FourColorsInOneBand_EachPassOverlaysWithoutErasingTheOthers()
        {
            var colors = new[] {_red, _green, _blue, _white};
            var image = Build(4, 4, (x, _) => colors[x]);
            AssertSamePixels(image, RoundTrip(image));
        }

        [Fact]
        public void RoundTrip_LongRuns_SurviveRunLengthCompression()
        {
            // Runs well past the compression threshold, and of differing lengths, to catch an off-by-one in the count.
            var image = Build(40, 6, (x, _) => x < 17 ? _red : _blue);
            AssertSamePixels(image, RoundTrip(image));
        }

        [Fact]
        public void RoundTrip_Transparency_LeavesHolesRatherThanFillingThem()
        {
            var image = Build(6, 6, (x, y) => (x + y) % 2 == 0 ? _red : _transparent);
            AssertSamePixels(image, RoundTrip(image));
        }

        [Fact]
        public void RoundTrip_TransparentGapInTheMiddleOfARun_SplitsItCorrectly()
        {
            var image = Build(9, 1, (x, _) => x == 4 ? _transparent : _white);
            AssertSamePixels(image, RoundTrip(image));
        }

        [Fact]
        public void RoundTrip_Checkerboard_PreservesEveryPixelPosition()
        {
            var image = Build(8, 8, (x, y) => (x + y) % 2 == 0 ? _white : _blue);
            AssertSamePixels(image, RoundTrip(image));
        }

        [Fact]
        public void RoundTrip_ColorsWithinThePaletteLimit_AreLossless()
        {
            // Eight distinct colors, far under the 256-entry limit, so median cut keeps every one exactly and the
            // picture must come back bit-identical.
            var palette = new[]
            {
                new Rgba32(0, 0, 0, 255), _red, _green, _blue,
                new Rgba32(255, 255, 0, 255), new Rgba32(255, 0, 255, 255),
                new Rgba32(0, 255, 255, 255), _white
            };

            var image = Build(8, 8, (x, y) => palette[(x + y) % palette.Length]);
            AssertSamePixels(image, RoundTrip(image));
        }
    }
}
