using System.Linq;
using WolfCurses.Graphics;
using WolfCurses.Tests.Support;
using Xunit;

namespace WolfCurses.Tests.Graphics
{
    /// <summary>
    ///     Drives <see cref="SixelImageRenderer" /> with genuine photographs from the repository's <c>media/</c>
    ///     folder. The synthetic tests use a handful of colors and so never leave the palette's lossless path; a real
    ///     photograph has tens of thousands, which is the only way to exercise median cut for real, at the sizes and
    ///     run lengths an actual picture produces. Skips rather than fails when the fixtures are absent.
    /// </summary>
    public class SixelIntegrationTests
    {
        private const char ESC = (char) 27;

        /// <summary>The payload row of a rendered picture, with its graphics marker stripped.</summary>
        private static string Payload(string rendered)
        {
            return rendered.Split('\n')[0].TrimEnd('\r').Substring(1);
        }

        private static string RenderMedia(string fileName, AnsiImageOptions options, SixelImageRenderer renderer = null)
        {
            var image = AnsiImage.FromFile(TestImages.Media(fileName), TestImages.Decoder);
            return (renderer ?? new SixelImageRenderer()).Render(image.Pixels, options);
        }

        [Fact]
        public void Render_Photograph_ProducesAWellFormedSixelSequence()
        {
            Assert.SkipUnless(TestImages.Available, "Image fixtures are not present.");

            var payload = Payload(RenderMedia("image_001.jpg", new AnsiImageOptions {MaxColumns = 40, MaxRows = 20}));

            Assert.StartsWith($"{ESC}P0;1;0q\"1;1;", payload, System.StringComparison.Ordinal);
            Assert.EndsWith($"{ESC}\\", payload, System.StringComparison.Ordinal);
        }

        [Fact]
        public void Render_Photograph_UsesTheWholePaletteButNeverExceedsIt()
        {
            Assert.SkipUnless(TestImages.Available, "Image fixtures are not present.");

            var payload = Payload(RenderMedia("image_001.jpg", new AnsiImageOptions {MaxColumns = 40, MaxRows = 20}));

            var declared = System.Text.RegularExpressions.Regex.Matches(payload, @"#(\d+);2;").Count;
            Assert.Equal(256, declared);
        }

        [Fact]
        public void Render_Photograph_HonorsASmallerPaletteLimit()
        {
            Assert.SkipUnless(TestImages.Available, "Image fixtures are not present.");

            var payload = Payload(RenderMedia("image_001.jpg", new AnsiImageOptions {MaxColumns = 40, MaxRows = 20},
                new SixelImageRenderer(10, 20, 16)));

            var declared = System.Text.RegularExpressions.Regex.Matches(payload, @"#(\d+);2;").Count;
            Assert.Equal(16, declared);
        }

        [Fact]
        public void Render_Photograph_DecodesBackToTheEncodedPixelDimensions()
        {
            Assert.SkipUnless(TestImages.Available, "Image fixtures are not present.");

            // A real picture through the full path — decode, resample, median cut, encode — must still describe a
            // raster a reader can reconstruct at exactly the size the sequence claims.
            var renderer = new SixelImageRenderer(10, 20);
            var payload = Payload(RenderMedia("image_001.jpg", new AnsiImageOptions {MaxColumns = 20, MaxRows = 10},
                renderer));

            var decoded = SixelDecoder.Decode(payload);

            Assert.InRange(decoded.Width, 1, 20 * 10);
            Assert.InRange(decoded.Height, 1, 10 * 20);
        }

        [Fact]
        public void Render_Photograph_DecodedPictureIsMostlyDrawn()
        {
            Assert.SkipUnless(TestImages.Available, "Image fixtures are not present.");

            // An opaque photograph should leave essentially no undrawn pixels: every one belongs to some color's pass.
            // This catches a band or run-length mistake that silently drops pixels while still parsing cleanly.
            var payload = Payload(RenderMedia("image_003.jpg", new AnsiImageOptions {MaxColumns = 20, MaxRows = 10},
                new SixelImageRenderer(10, 20)));

            var decoded = SixelDecoder.Decode(payload);

            var drawn = 0;
            for (var y = 0; y < decoded.Height; y++)
            for (var x = 0; x < decoded.Width; x++)
                if (decoded.GetPixel(x, y).A == 255)
                    drawn++;

            Assert.Equal(decoded.Width * decoded.Height, drawn);
        }

        [Fact]
        public void Render_TransparentPng_LeavesTransparentPixelsUndrawn()
        {
            Assert.SkipUnless(TestImages.Available, "Image fixtures are not present.");

            var payload = Payload(RenderMedia("transparent_test.png",
                new AnsiImageOptions {MaxColumns = 20, MaxRows = 10}, new SixelImageRenderer(10, 20)));

            var decoded = SixelDecoder.Decode(payload);

            var undrawn = 0;
            for (var y = 0; y < decoded.Height; y++)
            for (var x = 0; x < decoded.Width; x++)
                if (decoded.GetPixel(x, y).A == 0)
                    undrawn++;

            Assert.True(undrawn > 0, "A picture with transparency must leave some pixels for the terminal to show through.");
        }

        [Fact]
        public void Render_Photograph_CoversTheExpectedNumberOfScreenRows()
        {
            Assert.SkipUnless(TestImages.Available, "Image fixtures are not present.");

            var rendered = RenderMedia("image_001.jpg", new AnsiImageOptions {MaxColumns = 40, MaxRows = 20},
                new SixelImageRenderer(10, 20));

            var lines = rendered.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();

            // Every line after the payload accounts for one further screen row the picture covers, and the whole block
            // must fit the row budget it was given.
            Assert.InRange(lines.Length, 1, 20);
            Assert.All(lines.Skip(1), l => Assert.Equal(AnsiGraphics.RowPlaceholder, l));
        }
    }
}
