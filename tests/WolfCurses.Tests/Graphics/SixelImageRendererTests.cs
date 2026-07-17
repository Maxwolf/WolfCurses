using System;
using System.Linq;
using WolfCurses.Graphics;
using Xunit;

namespace WolfCurses.Tests.Graphics
{
    /// <summary>
    ///     Pins the exact sixel output of <see cref="SixelImageRenderer" /> against hand-built synthetic images: the
    ///     DCS envelope and raster attributes, palette declaration on sixel's 0-100 color scale, the six-pixel band
    ///     encoding and its bit order, run-length compression, transparency, and the row accounting that lets the
    ///     presenter know how much of the screen the picture covers.
    /// </summary>
    public class SixelImageRendererTests
    {
        private const char ESC = (char) 27;

        /// <summary>The character encoding a band whose listed rows hold the color; row 0 is the top of the six.</summary>
        private static char Data(params int[] rows) => (char) (0x3F + rows.Aggregate(0, (m, r) => m | (1 << r)));

        /// <summary>A renderer with a 1x1-pixel cell, so one image pixel is one row/column and sizes stay readable.</summary>
        private static SixelImageRenderer Renderer(int maxColors = 256) => new(1, 1, maxColors);

        private static AnsiImageOptions Opts(int cols, int rows, Rgb24? background = null) => new()
        {
            MaxColumns = cols,
            MaxRows = rows,
            Fit = AnsiImageFitEnum.Stretch,
            BackgroundColor = background
        };

        private static PixelBuffer Solid(int width, int height, Rgba32 color)
        {
            var buffer = new PixelBuffer(width, height);
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                buffer.SetPixel(x, y, color);
            return buffer;
        }

        /// <summary>The payload row of a rendered picture, with its graphics marker stripped.</summary>
        private static string Payload(string rendered)
        {
            var first = rendered.Split('\n')[0].TrimEnd('\r');
            return first.Substring(1);
        }

        [Fact]
        public void Render_SingleRedPixel_EmitsCompleteSixelEnvelope()
        {
            var image = Solid(1, 1, new Rgba32(255, 0, 0, 255));
            var payload = Payload(Renderer().Render(image, Opts(1, 1)));

            // ESC P 0;1;0 q  -- P2=1 leaves undrawn pixels transparent
            // "1;1;1;1        -- raster: square pixels, 1x1 image
            // #0;2;100;0;0    -- palette entry 0 as RGB percentages
            // #0~             -- select it; '~' would be all six rows, but a 1px-tall image sets only row 0
            Assert.Equal($"{ESC}P0;1;0q\"1;1;1;1#0;2;100;0;0#0{Data(0)}{ESC}\\", payload);
        }

        [Fact]
        public void Render_ColorChannels_ScaledToSixelPercentRange()
        {
            // Sixel color components are percentages, not bytes: 255 -> 100, 128 -> 50 (rounded), 0 -> 0.
            var image = Solid(1, 1, new Rgba32(255, 128, 0, 255));
            var payload = Payload(Renderer().Render(image, Opts(1, 1)));
            Assert.Contains("#0;2;100;50;0", payload, StringComparison.Ordinal);
        }

        [Fact]
        public void Render_SixPixelColumn_PacksIntoOneCharacterWithTopPixelAsLowBit()
        {
            // A column of six pixels is exactly one band, so all six become a single data character with every bit
            // set. This pins the bit order: row 0 (top) is bit 0.
            var image = Solid(1, 6, new Rgba32(255, 0, 0, 255));
            var payload = Payload(Renderer().Render(image, Opts(1, 6)));
            Assert.Contains($"#0{Data(0, 1, 2, 3, 4, 5)}", payload, StringComparison.Ordinal);
        }

        [Fact]
        public void Render_TopPixelOnlyOfBand_SetsOnlyLowBit()
        {
            var image = new PixelBuffer(1, 6);
            image.SetPixel(0, 0, new Rgba32(255, 0, 0, 255));
            var payload = Payload(Renderer().Render(image, Opts(1, 6)));

            // Only the top of the six rows is opaque; the rest are transparent and take no part in any mask.
            Assert.Contains($"#0{Data(0)}", payload, StringComparison.Ordinal);
        }

        [Fact]
        public void Render_BottomPixelOfBand_SetsHighBit()
        {
            var image = new PixelBuffer(1, 6);
            image.SetPixel(0, 5, new Rgba32(255, 0, 0, 255));
            var payload = Payload(Renderer().Render(image, Opts(1, 6)));
            Assert.Contains($"#0{Data(5)}", payload, StringComparison.Ordinal);
        }

        [Fact]
        public void Render_LongRunOfIdenticalColumns_IsRunLengthCompressed()
        {
            var image = Solid(10, 1, new Rgba32(255, 0, 0, 255));
            var payload = Payload(Renderer().Render(image, Opts(10, 1)));
            Assert.Contains($"#0!10{Data(0)}", payload, StringComparison.Ordinal);
        }

        [Fact]
        public void Render_ShortRun_IsSpelledOutRatherThanCompressed()
        {
            // Three repeats cost the same spelled out as compressed ("!3c" is three characters too), so the encoder
            // does not reach for the escape until a run is long enough to actually save something.
            var image = Solid(3, 1, new Rgba32(255, 0, 0, 255));
            var payload = Payload(Renderer().Render(image, Opts(3, 1)));

            var expected = new string(Data(0), 3);
            Assert.Contains($"#0{expected}", payload, StringComparison.Ordinal);
            Assert.DoesNotContain("!", payload, StringComparison.Ordinal);
        }

        [Fact]
        public void Render_TwoColorsInOneBand_SeparatedByCarriageReturnNotAfterLast()
        {
            var image = new PixelBuffer(2, 1);
            image.SetPixel(0, 0, new Rgba32(255, 0, 0, 255));
            image.SetPixel(1, 0, new Rgba32(0, 0, 255, 255));
            var payload = Payload(Renderer().Render(image, Opts(2, 1)));

            // Each color is a separate pass over the band, rejoined at the left margin with '$'. The band is walked
            // twice: red at column 0 then blue at column 1 (its leading blank column emitted as an empty mask).
            Assert.Contains("$", payload, StringComparison.Ordinal);

            // A trailing '$' after the final color is known to upset some terminals, so the last pass must not have one.
            var body = payload.Substring(payload.IndexOf("#0;2;", StringComparison.Ordinal));
            Assert.DoesNotContain($"${ESC}\\", body, StringComparison.Ordinal);
        }

        [Fact]
        public void Render_MultipleBands_SeparatedByGraphicsNewlineNotAfterLast()
        {
            // Seven rows is two bands (six plus one), so exactly one band separator.
            var image = Solid(1, 7, new Rgba32(255, 0, 0, 255));
            var payload = Payload(Renderer().Render(image, Opts(1, 7)));

            Assert.Equal(1, payload.Count(c => c == '-'));
            Assert.DoesNotContain($"-{ESC}\\", payload, StringComparison.Ordinal);
        }

        [Fact]
        public void Render_FullyTransparentImage_DeclaresNoColorsAndDrawsNothing()
        {
            var image = new PixelBuffer(4, 4); // A fresh buffer is transparent black.
            var payload = Payload(Renderer().Render(image, Opts(4, 4)));

            Assert.StartsWith($"{ESC}P0;1;0q\"1;1;4;4", payload, StringComparison.Ordinal);
            Assert.DoesNotContain("#", payload, StringComparison.Ordinal);
        }

        [Fact]
        public void Render_TransparentPixels_AreOmittedSoTerminalShowsThrough()
        {
            // Left column opaque, right column transparent: only the left contributes to the mask, and the trailing
            // empty column is dropped entirely rather than emitted as a blank mask.
            var image = new PixelBuffer(2, 1);
            image.SetPixel(0, 0, new Rgba32(255, 0, 0, 255));
            var payload = Payload(Renderer().Render(image, Opts(2, 1)));

            Assert.Contains($"#0{Data(0)}{ESC}\\", payload, StringComparison.Ordinal);
        }

        [Fact]
        public void Render_TransparentPixelsWithBackgroundColor_AreCompositedAndDrawn()
        {
            var image = new PixelBuffer(2, 1);
            image.SetPixel(0, 0, new Rgba32(255, 0, 0, 255));
            var payload = Payload(Renderer().Render(image, Opts(2, 1, new Rgb24(0, 0, 255))));

            // With a backdrop nothing is see-through, so both columns are drawn: red over blue, and blue where the
            // image was transparent. Two colors means two passes.
            Assert.Contains("#0;2;", payload, StringComparison.Ordinal);
            Assert.Contains("#1;2;", payload, StringComparison.Ordinal);
            Assert.Contains("$", payload, StringComparison.Ordinal);
        }

        [Fact]
        public void Render_MoreColorsThanPaletteAllows_IsReducedToTheLimit()
        {
            // A gradient of 64 distinct colors squeezed into a 4-entry palette.
            var image = new PixelBuffer(64, 1);
            for (var x = 0; x < 64; x++)
                image.SetPixel(x, 0, new Rgba32((byte) (x * 4), 0, 0, 255));

            var payload = Payload(Renderer(4).Render(image, Opts(64, 1)));

            var declared = System.Text.RegularExpressions.Regex.Matches(payload, @"#(\d+);2;").Count;
            Assert.Equal(4, declared);
        }

        [Fact]
        public void Render_PictureTallerThanOneRow_AccountsForCoveredRowsWithPlaceholders()
        {
            // A 20-pixel-tall picture on a 20-pixel-tall cell covers exactly one row; on a 10-pixel cell, two.
            var image = Solid(10, 20, new Rgba32(255, 0, 0, 255));
            var rendered = new SixelImageRenderer(10, 10).Render(image, new AnsiImageOptions
            {
                MaxColumns = 1,
                MaxRows = 2,
                Fit = AnsiImageFitEnum.Stretch
            });

            var lines = rendered.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
            Assert.Equal(2, lines.Length);
            Assert.StartsWith(AnsiGraphics.RowPlaceholder, lines[0], StringComparison.Ordinal);
            Assert.True(lines[0].Length > 1, "The first line must carry the payload, not just the marker.");
            Assert.Equal(AnsiGraphics.RowPlaceholder, lines[1]);
        }

        [Fact]
        public void Render_NullImage_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => Renderer().Render(null));
        }

        [Theory]
        [InlineData(0, 20, 256)]
        [InlineData(10, 0, 256)]
        [InlineData(10, 20, 0)]
        [InlineData(10, 20, 257)]
        public void Constructor_RejectsUnusableConfiguration(int cellWidth, int cellHeight, int maxColors)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SixelImageRenderer(cellWidth, cellHeight, maxColors));
        }
    }
}
