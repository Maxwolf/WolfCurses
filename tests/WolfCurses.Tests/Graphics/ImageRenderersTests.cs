using System;
using WolfCurses.Graphics;
using Xunit;

namespace WolfCurses.Tests.Graphics
{
    /// <summary>
    ///     Covers the renderer seam itself: the default that keeps existing applications drawing exactly as before, the
    ///     guard that makes a mis-configured start-up fail loudly, and choosing a renderer per call.
    ///     <para>
    ///         Assigning <see cref="ImageRenderers.Default" /> is deliberately not exercised here. It is process-wide
    ///         mutable state and these tests run in parallel, so swapping it would race every other test that renders —
    ///         the same reason <see cref="ImageDecoders" />' equivalent is left alone.
    ///     </para>
    /// </summary>
    public class ImageRenderersTests
    {
        private static PixelBuffer Pixel(Rgba32 color)
        {
            var buffer = new PixelBuffer(1, 1);
            buffer.SetPixel(0, 0, color);
            return buffer;
        }

        [Fact]
        public void Default_IsHalfBlocks_SoExistingApplicationsAreUnaffected()
        {
            // Half blocks work in any color terminal; the true-pixel protocols do not, and cannot be detected without
            // reading a reply from the terminal. So the safe renderer has to be the one you get without asking.
            Assert.IsType<HalfBlockImageRenderer>(ImageRenderers.Default);
        }

        [Fact]
        public void Default_AssigningNull_ThrowsRatherThanBreakingAtFirstImage()
        {
            Assert.Throws<ArgumentNullException>(() => ImageRenderers.Default = null);
        }

        [Fact]
        public void HalfBlockRenderer_MatchesTheStaticRendererItWraps()
        {
            var image = Pixel(new Rgba32(255, 0, 0, 255));
            var options = new AnsiImageOptions
            {
                MaxColumns = 1,
                MaxRows = 1,
                ColorMode = AnsiColorModeEnum.TrueColor
            };

            Assert.Equal(AnsiImageRenderer.Render(image, options), new HalfBlockImageRenderer().Render(image, options));
        }

        [Fact]
        public void ToAnsi_WithAnExplicitRenderer_UsesItInsteadOfTheDefault()
        {
            var image = AnsiImage.FromPixels(Pixel(new Rgba32(255, 0, 0, 255)));
            var options = new AnsiImageOptions {MaxColumns = 1, MaxRows = 1, Fit = AnsiImageFitEnum.Stretch};

            var rendered = image.ToAnsi(options, new SixelImageRenderer(1, 1));

            // Sixel output, not the half blocks the default would have produced.
            Assert.Contains($"{(char) 27}P0;1;0q", rendered, StringComparison.Ordinal);
        }

        [Fact]
        public void ToAnsi_WithANullRenderer_Throws()
        {
            var image = AnsiImage.FromPixels(Pixel(new Rgba32(255, 0, 0, 255)));

            Assert.Throws<ArgumentNullException>(() => image.ToAnsi(new AnsiImageOptions(), null));
        }
    }
}
