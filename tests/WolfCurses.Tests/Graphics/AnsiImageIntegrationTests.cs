using System.IO;
using WolfCurses.Graphics;
using WolfCurses.Tests.Support;
using Xunit;

namespace WolfCurses.Tests.Graphics
{
    /// <summary>
    ///     End-to-end tests against the real fixture images at the repository root: they prove the default
    ///     StbImageSharp decoder actually handles the formats in the media folder — including
    ///     <em>progressive</em> JPEG (image_001, image_003) and a PNG with an alpha channel (the transparent Tux) — and
    ///     that the full decode-plus-render pipeline produces sane output. They skip (rather than fail) when the
    ///     repository fixtures are not present.
    /// </summary>
    public class AnsiImageIntegrationTests
    {
        private static readonly AnsiImageOptions FixedBounds = new()
        {
            MaxColumns = 80,
            MaxRows = 40,
            ColorMode = AnsiColorMode.TrueColor,
            CellAspectRatio = 2.0
        };

        [Theory]
        [InlineData("image_001.jpg", 1280, 987)] // progressive JPEG
        [InlineData("image_002.jpg", 2000, 1500)] // baseline JPEG
        [InlineData("image_003.jpg", 1024, 768)] // progressive JPEG
        [InlineData("image_004.png", 665, 447)] // RGB PNG
        public void Decode_KnownFixture_HasExpectedDimensions(string fileName, int width, int height)
        {
            Assert.SkipUnless(TestImages.Available, "Repository image fixtures are not present.");

            var image = AnsiImage.FromFile(TestImages.Media(fileName));

            Assert.Equal(width, image.Width);
            Assert.Equal(height, image.Height);
        }

        [Fact]
        public void Decode_ProjectLogo_Succeeds()
        {
            Assert.SkipUnless(TestImages.Available && File.Exists(TestImages.Logo),
                "Project logo fixture is not present.");

            var image = AnsiImage.FromFile(TestImages.Logo);

            Assert.True(image.Width > 0);
            Assert.True(image.Height > 0);
        }

        [Theory]
        [InlineData("image_001.jpg")]
        [InlineData("image_002.jpg")]
        [InlineData("image_003.jpg")]
        [InlineData("image_004.png")]
        public void Render_KnownFixture_FitsBoundsAndLineCountMatches(string fileName)
        {
            Assert.SkipUnless(TestImages.Available, "Repository image fixtures are not present.");

            var image = AnsiImage.FromFile(TestImages.Media(fileName));
            var (cols, rows) = AnsiImageRenderer.ComputeTargetCells(image.Width, image.Height, FixedBounds);

            var ansi = image.ToAnsi(FixedBounds);

            Assert.False(string.IsNullOrEmpty(ansi));
            Assert.InRange(cols, 1, 80);
            Assert.InRange(rows, 1, 40);
            // One '\n' between each pair of rows, none trailing.
            Assert.Equal(rows, ansi.Split('\n').Length);
        }

        [Fact]
        public void Render_OpaqueJpeg_HasNoTransparentSpaces()
        {
            Assert.SkipUnless(TestImages.Available, "Repository image fixtures are not present.");

            // A JPEG has no alpha channel, so every cell has two opaque pixels and no cell degrades to a bare space.
            var ansi = AnsiImage.FromFile(TestImages.Media("image_002.jpg")).ToAnsi(FixedBounds);
            Assert.DoesNotContain(' ', ansi);
        }

        [Fact]
        public void Render_TransparentPng_ProducesTransparentSpaces()
        {
            Assert.SkipUnless(TestImages.Available, "Repository image fixtures are not present.");

            var path = FindTransparentFixture();
            Assert.SkipUnless(path != null, "Transparent PNG fixture is not present.");

            // The Tux image has fully transparent corners; without a background those cells become spaces so the
            // terminal shows through. This proves the alpha channel is honored end to end.
            var image = AnsiImage.FromFile(path);
            var ansi = image.ToAnsi(FixedBounds);
            Assert.Contains(' ', ansi);

            // With an opaque background configured, nothing stays transparent: no bare spaces remain.
            var composited = image.ToAnsi(new AnsiImageOptions
            {
                MaxColumns = 80,
                MaxRows = 40,
                ColorMode = AnsiColorMode.TrueColor,
                CellAspectRatio = 2.0,
                BackgroundColor = new Rgb24(0, 0, 0)
            });
            Assert.DoesNotContain(' ', composited);
        }

        /// <summary>The fixture is named "transparent test.png"; tolerate the underscore spelling too.</summary>
        private static string FindTransparentFixture()
        {
            foreach (var name in new[] {"transparent test.png", "transparent_test.png"})
            {
                var candidate = TestImages.Media(name);
                if (candidate != null && File.Exists(candidate))
                    return candidate;
            }

            return null;
        }
    }
}
