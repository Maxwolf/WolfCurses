using System;
using System.IO;
using WolfCurses.Graphics;
using WolfCurses.Graphics.Decoding;
using WolfCurses.Tests.Support;
using Xunit;

namespace WolfCurses.Tests.Graphics
{
    /// <summary>
    ///     That the dimension ceiling is wired into <b>every</b> decoder, not only PNG.
    ///     <para>
    ///         The guard is the library's security posture: <c>FileDialog</c> ships in this package, so a user can
    ///         point a decoder at any file on the machine, and the rule is that dimensions are checked <i>before</i>
    ///         anything is read or allocated on their strength. Both existing guard tests drive PNG, and the
    ///         differential tests only ever read well-formed fixtures — so deleting the call from
    ///         <see cref="JpegDecoder" /> or <see cref="GifDecoder" /> passes the whole suite today.
    ///     </para>
    /// </summary>
    public class DecoderDimensionGuardTests
    {
        [Theory]
        [InlineData("image_002.jpg", "2000x1500")] // baseline JPEG
        [InlineData("image_001.jpg", "1280x987")] // progressive JPEG, a different code path in
        [InlineData("cool.gif", "426x318")] // GIF logical screen
        [InlineData("animated.gif", "540x540")] // animated GIF
        [InlineData("image_004.png", "665x447")] // PNG, so the four are pinned together
        public void EveryDecoderRefusesAnImageBiggerThanItsCeiling(string fixture, string dimensions)
        {
            Assert.SkipUnless(TestImages.Available, "Image fixtures are not present in media/.");

            var path = TestImages.Media(fixture);
            Assert.SkipUnless(path != null && File.Exists(path), $"media/{fixture} is not present.");

            using var stream = File.OpenRead(path);

            var thrown = Assert.Throws<InvalidDataException>(() => new BuiltInImageDecoder(1000).Decode(stream));

            // The message names the size it refused, because "too big" without a number is not diagnosable.
            Assert.Contains(dimensions, thrown.Message, StringComparison.Ordinal);
            Assert.Contains("limit", thrown.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData("image_002.jpg")]
        [InlineData("cool.gif")]
        public void ARoomyCeilingLetsTheSameFileThrough(string fixture)
        {
            // The other side of the boundary. Without it, a decoder that threw on everything would pass the test
            // above and look like a working guard.
            Assert.SkipUnless(TestImages.Available, "Image fixtures are not present in media/.");

            var path = TestImages.Media(fixture);
            Assert.SkipUnless(path != null && File.Exists(path), $"media/{fixture} is not present.");

            using var stream = File.OpenRead(path);
            var pixels = new BuiltInImageDecoder().Decode(stream);

            Assert.True(pixels.Width > 1 && pixels.Height > 1);
        }

        [Fact]
        public void AnImpossibleGifHeaderIsRefusedBeforeAnythingIsAllocated()
        {
            // Hand-built rather than a fixture, because the point is that the refusal happens on the strength of
            // the HEADER: thirteen bytes claiming a four-billion-pixel screen, with no image data behind them at
            // all. A decoder that read first and checked afterwards would run out of file instead.
            var header = new byte[]
            {
                (byte) 'G', (byte) 'I', (byte) 'F', (byte) '8', (byte) '9', (byte) 'a',
                0xFF, 0xFF, // width  65535
                0xFF, 0xFF, // height 65535
                0x00, 0x00, 0x00
            };

            var thrown = Assert.Throws<InvalidDataException>(
                () => new BuiltInImageDecoder().Decode(new MemoryStream(header)));

            Assert.Contains("65535x65535", thrown.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void TheByteCeilingHasBothSides()
        {
            // DecoderGuards.ReadAll bounds the file going in, where the dimension check bounds the pixels coming
            // out. Pinned at the boundary rather than only past it.
            Assert.Throws<InvalidDataException>(() => DecoderGuards.ReadAll(new MemoryStream(new byte[100]), 16));

            var read = DecoderGuards.ReadAll(new MemoryStream(new byte[100]), 100);
            Assert.Equal(100, read.Length);
        }
    }
}
