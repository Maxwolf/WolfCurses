// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/17/2026

using System;
using System.IO;
using System.Text;
using WolfCurses.Graphics;
using WolfCurses.Tests.Support;
using Xunit;

namespace WolfCurses.Tests.Graphics
{
    /// <summary>
    ///     Pins what happens when an application loads an image without configuring anything: it works. The library
    ///     decodes PNG, JPEG and GIF itself, so <see cref="ImageDecoders.Default" /> is a real decoder from the first
    ///     line of <c>Main</c> and most applications never touch it.
    ///     <para>
    ///         These read the process-wide <see cref="ImageDecoders.Default" /> without assigning it, and no other test
    ///         assigns it either — the integration tests pass their decoder per-call. That was once load-bearing (the
    ///         default used to be a stand-in that only threw, so installing one anywhere would break these by run
    ///         order); it is now merely tidy, since the default being replaced would leave these testing whatever the
    ///         replacement does instead.
    ///     </para>
    /// </summary>
    public class BuiltInImageDecoderTests
    {
        /// <summary>
        ///     A complete 1x1 GIF holding one red pixel, hand-assembled because the repository has no GIF fixture.
        ///     Header, logical screen descriptor, a two-entry global colour table, the image descriptor, and an LZW
        ///     stream of exactly three codes: clear, the pixel, end-of-information.
        /// </summary>
        private static byte[] RedPixelGif =>
        [
            0x47, 0x49, 0x46, 0x38, 0x39, 0x61, // "GIF89a"
            0x01, 0x00, 0x01, 0x00, // 1x1 logical screen
            0x80, 0x00, 0x00, // global colour table, 2 entries
            0xFF, 0x00, 0x00, // entry 0: red
            0x00, 0x00, 0x00, // entry 1: black
            0x2C, // image descriptor
            0x00, 0x00, 0x00, 0x00, // at 0,0
            0x01, 0x00, 0x01, 0x00, // 1x1
            0x00, // no local table, not interlaced
            0x02, // LZW minimum code size
            0x02, 0x44, 0x01, // one sub-block: clear(4), 0, end(5)
            0x00, // block terminator
            0x3B // trailer
        ];

        [Fact]
        public void Default_WithNothingConfigured_IsTheBuiltInDecoder()
        {
            Assert.IsType<BuiltInImageDecoder>(ImageDecoders.Default);
        }

        [Fact]
        public void Default_IsNeverNull()
        {
            Assert.NotNull(ImageDecoders.Default);
        }

        [Fact]
        public void Default_AssigningNull_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => ImageDecoders.Default = null);
        }

        [Fact]
        public void FromFile_WithNothingConfigured_JustWorks()
        {
            // The headline claim of the whole feature: a real photograph off disk, through the process-wide default,
            // with no set-up line anywhere. This used to throw.
            Assert.SkipUnless(TestImages.Available, "Image fixtures are not present in media/.");

            var image = AnsiImage.FromFile(TestImages.Media("image_004.png"));

            Assert.Equal(665, image.Pixels.Width);
            Assert.Equal(447, image.Pixels.Height);
        }

        [Theory]
        [InlineData("image_004.png")] // PNG
        [InlineData("logo.jpg")] // baseline JPEG
        [InlineData("image_001.jpg")] // progressive JPEG
        public void Decode_DispatchesOnContentNotExtension(string fixture)
        {
            Assert.SkipUnless(TestImages.Available, "Image fixtures are not present in media/.");

            using var stream = File.OpenRead(TestImages.Media(fixture));
            var pixels = new BuiltInImageDecoder().Decode(stream);

            Assert.True(pixels.Width > 1 && pixels.Height > 1);
        }

        [Fact]
        public void Decode_Gif_IsDispatchedToTheGifDecoder()
        {
            var pixels = new BuiltInImageDecoder().Decode(new MemoryStream(RedPixelGif));

            Assert.Equal(1, pixels.Width);
            Assert.Equal(1, pixels.Height);

            var pixel = pixels.GetPixel(0, 0);
            Assert.Equal(255, pixel.R);
            Assert.Equal(0, pixel.G);
            Assert.Equal(0, pixel.B);
            Assert.Equal(255, pixel.A);
        }

        [Fact]
        public void Decode_UnrecognizedFormat_ThrowsExplanationNamingTheSeam()
        {
            // Straight at the decoder, not through AnsiImage: the seam's contract is still that bad data throws, and
            // that is what anything wanting to handle the failure itself relies on. AnsiImage is the layer that turns
            // this into a checkerboard instead — see AnsiImageErrorTextureTests.
            using var stream = new MemoryStream([1, 2, 3, 4]);

            var ex = Assert.Throws<InvalidDataException>(() => new BuiltInImageDecoder().Decode(stream));

            // The message is the whole value of failing here rather than deeper, so it is worth asserting it actually
            // helps: say what is wrong, say which formats do work, name the property to assign, and point at the
            // example. A bare "unsupported format" would leave the reader no better off.
            Assert.Contains("PNG, JPEG or GIF", ex.Message, StringComparison.Ordinal);
            Assert.Contains("ImageDecoders.Default", ex.Message, StringComparison.Ordinal);
            Assert.Contains("IImageDecoder", ex.Message, StringComparison.Ordinal);
            Assert.Contains("example", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Decode_EmptyStream_SaysItWasEmpty()
        {
            var ex = Assert.Throws<InvalidDataException>(() => new BuiltInImageDecoder().Decode(new MemoryStream()));

            Assert.Contains("empty", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Decode_SomethingThatIsNotAnImageAtAll_NamesWhatItActuallyIs()
        {
            // The failure worth designing for is not "a WebP arrived", it is "the .jpg on disk is an error page the
            // download never noticed". Naming it turns a baffling decode error into an obvious one.
            var html = Encoding.ASCII.GetBytes("<!DOCTYPE html><html><body>404 Not Found</body></html>");

            var ex = Assert.Throws<InvalidDataException>(() => new BuiltInImageDecoder().Decode(new MemoryStream(html)));

            Assert.Contains("HTML", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Decode_Webp_SaysSoRatherThanGuessing()
        {
            var webp = Encoding.ASCII.GetBytes("RIFF____WEBPVP8 ");

            var ex = Assert.Throws<InvalidDataException>(() => new BuiltInImageDecoder().Decode(new MemoryStream(webp)));

            Assert.Contains("WebP", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Decode_HeaderClaimingAnImpossibleSize_IsRefusedBeforeAnythingIsAllocated()
        {
            // A 33-byte file declaring a 60-gigapixel image. Nothing may be allocated on the strength of a header
            // that has not been checked, which is the entire defence against a decompression bomb: the file is tiny,
            // so no limit on the input catches this — only refusing to believe the dimensions does.
            var ex = Assert.Throws<InvalidDataException>(
                () => new BuiltInImageDecoder().Decode(new MemoryStream(PngHeaderDeclaring(250_000, 250_000))));

            Assert.Contains("250000x250000", ex.Message, StringComparison.Ordinal);
            Assert.Contains("limit", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Constructor_RejectsAnUnusablePixelLimit()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BuiltInImageDecoder(0));
        }

        [Fact]
        public void MaxPixels_IsHonoredForImagesThatAreMerelyLargerThanConfigured()
        {
            // The ceiling is a knob, not just a bomb guard: an application that knows it only ever shows thumbnails
            // can turn it right down, and a legitimate photograph past the line is refused the same way.
            Assert.SkipUnless(TestImages.Available, "Image fixtures are not present in media/.");

            using var stream = File.OpenRead(TestImages.Media("image_004.png"));

            var ex = Assert.Throws<InvalidDataException>(() => new BuiltInImageDecoder(1000).Decode(stream));

            Assert.Contains("665x447", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void FromPixels_NeedsNoDecoderAtAll()
        {
            // Already-decoded pixels never reach a decoder, so an application with its own imaging library can use the
            // whole rendering half of the feature without any of this being involved.
            var pixels = new PixelBuffer(1, 2);
            pixels.SetPixel(0, 0, new Rgba32(255, 0, 0, 255));
            pixels.SetPixel(0, 1, new Rgba32(0, 0, 255, 255));

            var ansi = AnsiImage.FromPixels(pixels).ToAnsi(new AnsiImageOptions {MaxColumns = 1, MaxRows = 1});

            Assert.False(string.IsNullOrEmpty(ansi));
        }

        /// <summary>
        ///     Builds a PNG that is nothing but a signature and an IHDR claiming the given size — no image data at all.
        ///     Enough to prove the dimensions are rejected on sight, since a decoder that got as far as wanting pixels
        ///     would complain about their absence instead.
        /// </summary>
        private static byte[] PngHeaderDeclaring(int width, int height)
        {
            var png = new MemoryStream();
            png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
            png.Write([0x00, 0x00, 0x00, 0x0D]); // IHDR length
            png.Write(Encoding.ASCII.GetBytes("IHDR"));
            png.Write([(byte) (width >> 24), (byte) (width >> 16), (byte) (width >> 8), (byte) width]);
            png.Write([(byte) (height >> 24), (byte) (height >> 16), (byte) (height >> 8), (byte) height]);
            png.Write([8, 2, 0, 0, 0]); // 8-bit truecolour, no interlace
            png.Write([0, 0, 0, 0]); // CRC, unchecked by this decoder
            return png.ToArray();
        }
    }
}
