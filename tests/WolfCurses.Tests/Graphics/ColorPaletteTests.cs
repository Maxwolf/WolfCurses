using System;
using System.Linq;
using WolfCurses.Graphics;
using Xunit;

namespace WolfCurses.Tests.Graphics
{
    /// <summary>
    ///     Covers the median-cut color reduction behind the indexed true-pixel renderers: that it keeps colors exactly
    ///     when they already fit, never exceeds the requested palette size, always has an entry for every color it will
    ///     be asked about, ignores pixels too transparent to be drawn, and spends its entries where the pixels actually
    ///     are rather than where the stray colors are.
    /// </summary>
    public class ColorPaletteTests
    {
        private static PixelBuffer Row(params Rgba32[] pixels)
        {
            var buffer = new PixelBuffer(pixels.Length, 1);
            for (var x = 0; x < pixels.Length; x++)
                buffer.SetPixel(x, 0, pixels[x]);
            return buffer;
        }

        [Fact]
        public void Build_FewerColorsThanTheLimit_KeepsEveryColorExactly()
        {
            var image = Row(
                new Rgba32(255, 0, 0, 255),
                new Rgba32(0, 255, 0, 255),
                new Rgba32(0, 0, 255, 255));

            var palette = ColorPalette.Build(image, 128, 256);

            Assert.Equal(3, palette.Colors.Length);
            var reds = palette.Colors.Select(c => (c.R, c.G, c.B)).OrderBy(c => c.Item3).ToArray();
            Assert.Contains(((byte) 255, (byte) 0, (byte) 0), reds);
            Assert.Contains(((byte) 0, (byte) 255, (byte) 0), reds);
            Assert.Contains(((byte) 0, (byte) 0, (byte) 255), reds);
        }

        [Fact]
        public void Build_RepeatedColors_AreCountedOnce()
        {
            var red = new Rgba32(255, 0, 0, 255);
            var palette = ColorPalette.Build(Row(red, red, red, red), 128, 256);

            Assert.Single(palette.Colors);
        }

        [Fact]
        public void Build_MoreColorsThanTheLimit_IsReducedToIt()
        {
            var pixels = Enumerable.Range(0, 200).Select(i => new Rgba32((byte) i, 0, 0, 255)).ToArray();
            var palette = ColorPalette.Build(Row(pixels), 128, 8);

            Assert.Equal(8, palette.Colors.Length);
        }

        [Fact]
        public void Build_EveryOpaqueColorInTheImage_HasAnEntry()
        {
            // The palette is built from the very pixels that will be encoded, so the encoder can look up any of them
            // without a nearest-color search. A missing entry would throw at encode time.
            var pixels = Enumerable.Range(0, 300).Select(i => new Rgba32((byte) (i % 256), (byte) (i / 2), 0, 255))
                .ToArray();
            var image = Row(pixels);
            var palette = ColorPalette.Build(image, 128, 16);

            for (var x = 0; x < image.Width; x++)
            {
                var pixel = image.GetPixel(x, 0);
                var index = palette.IndexOf(ColorPalette.Pack(pixel.R, pixel.G, pixel.B));
                Assert.InRange(index, 0, palette.Colors.Length - 1);
            }
        }

        [Fact]
        public void Build_TransparentPixels_TakeNoPartInThePalette()
        {
            // A transparent pixel is never drawn, so spending a palette entry on its color would waste one.
            var image = Row(
                new Rgba32(255, 0, 0, 255),
                new Rgba32(0, 255, 0, 0));

            var palette = ColorPalette.Build(image, 128, 256);

            Assert.Single(palette.Colors);
            Assert.Equal(255, palette.Colors[0].R);
        }

        [Fact]
        public void Build_FullyTransparentImage_YieldsAnEmptyPalette()
        {
            var palette = ColorPalette.Build(new PixelBuffer(4, 4), 128, 256);

            Assert.Empty(palette.Colors);
        }

        [Fact]
        public void Build_AlphaAtTheThreshold_CountsAsVisible()
        {
            var palette = ColorPalette.Build(Row(new Rgba32(255, 0, 0, 128)), 128, 256);

            Assert.Single(palette.Colors);
        }

        [Fact]
        public void Build_EntryIsWeightedByPixelCount_NotByColorCount()
        {
            // Three hundred black pixels and a single red one, forced to share one entry. The entry is the average of
            // the colors the box holds weighted by how often each occurs, so it must land essentially on black — a
            // plain unweighted average of the two distinct colors would put it halfway, at (128, 0, 0), and tint the
            // whole picture.
            var pixels = Enumerable.Repeat(new Rgba32(0, 0, 0, 255), 300)
                .Append(new Rgba32(255, 0, 0, 255))
                .ToArray();

            var palette = ColorPalette.Build(Row(pixels), 128, 1);

            var only = Assert.Single(palette.Colors);
            Assert.InRange(only.R, 0, 2);
            Assert.Equal(0, only.G);
            Assert.Equal(0, only.B);
        }

        [Fact]
        public void Build_LonePixelFarFromTheRest_IsAbsorbedRatherThanGivenAScarceEntry()
        {
            // Documents a deliberate property of median cut: it divides by where the *pixels* are, not by where the
            // colors are. Nine hundred pixels of near-identical dark reds plus one stray white, given two entries,
            // spends both on the dark mass and folds the white into one of them. The single mis-drawn pixel costs far
            // less than handing half the palette to it would.
            var pixels = Enumerable.Range(0, 900).Select(i => new Rgba32((byte) (i % 3), 0, 0, 255))
                .Append(new Rgba32(255, 255, 255, 255))
                .ToArray();

            var palette = ColorPalette.Build(Row(pixels), 128, 2);

            Assert.Equal(2, palette.Colors.Length);
            Assert.DoesNotContain(palette.Colors, c => c.R == 255 && c.G == 255 && c.B == 255);
            Assert.All(palette.Colors, c => Assert.InRange(c.R, 0, 10));
        }

        [Fact]
        public void Build_KeepsPaletteEntriesInFirstSeenOrder()
        {
            // Entry order is contract, not cosmetics: palette register numbers are positional in the sixel stream,
            // the counting sort is stable so its ties resolve by this order, and the encoder's output is only
            // reproducible across versions if the same image always yields the same entry sequence. First-seen scan
            // order is what the Dictionary this class used to build on produced; a hash-ordered export would pass
            // every pixel-level test while silently reordering every multi-color palette (a review catch).
            var palette = ColorPalette.Build(Row(
                new Rgba32(0, 255, 0, 255),
                new Rgba32(255, 255, 255, 255),
                new Rgba32(255, 0, 0, 255),
                new Rgba32(0, 255, 0, 255)), 128, 256);

            Assert.Equal(3, palette.Colors.Length);
            Assert.Equal(new Rgb24(0, 255, 0), palette.Colors[0]);
            Assert.Equal(new Rgb24(255, 255, 255), palette.Colors[1]);
            Assert.Equal(new Rgb24(255, 0, 0), palette.Colors[2]);
        }

        [Fact]
        public void Build_NullImage_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => ColorPalette.Build(null, 128, 256));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1000)]
        public void Build_PaletteSizeOutsideTheSixelRange_IsClamped(int requested)
        {
            var pixels = Enumerable.Range(0, 400).Select(i => new Rgba32((byte) (i % 256), (byte) i, 0, 255)).ToArray();
            var palette = ColorPalette.Build(Row(pixels), 128, requested);

            Assert.InRange(palette.Colors.Length, 1, 256);
        }
    }
}
