// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/17/2026

using System;
using System.IO;
using WolfCurses.Graphics;
using WolfCurses.Tests.Support;
using Xunit;

namespace WolfCurses.Tests.Graphics
{
    /// <summary>
    ///     Checks the built-in decoders against StbImageSharp on the repository's real fixtures — an independent
    ///     implementation, written by other people from the same specifications, reading the same bytes.
    ///     <para>
    ///         This is the test that makes owning three image formats a reasonable idea rather than a reckless one.
    ///         Unit tests of a decoder can only assert what its author believed the format meant, which is exactly the
    ///         thing most likely to be wrong; a second implementation disagreeing is the only cheap way to find out
    ///         that a spec was misread. It is worth keeping even though it means the test project depends on
    ///         StbImageSharp — the package itself still depends on nothing, which is the part that matters.
    ///     </para>
    /// </summary>
    public class DecoderDifferentialTests
    {
        private static readonly BuiltInImageDecoder _builtIn = new();

        [Theory]
        [InlineData("image_004.png")] // 8-bit truecolour
        [InlineData("transparent_test.png")] // 8-bit truecolour with alpha
        [InlineData("cool.gif")] // GIF: 426x318, global table, one interlaced frame
        [InlineData("animated.gif")] // GIF: 540x540, 91 frames
        [InlineData("transparent_anim.gif")] // GIF: 200x197, 8 frames, transparency + disposal 2
        public void LosslessFormat_MatchesStbExactly(string fixture)
        {
            // PNG and GIF are lossless and every step of decoding them is exactly specified, so there is no rounding
            // to hide behind: two correct decoders produce identical bytes, and a single differing channel is a real
            // defect. (Only the first frame of an animated GIF is compared, because that is all this decodes.)
            var (mine, stb) = DecodeBoth(fixture);
            var (mean, max, beyondTwo) = Compare(mine, stb);

            Assert.Equal(stb.Width, mine.Width);
            Assert.Equal(stb.Height, mine.Height);
            Assert.True(max == 0,
                $"{fixture}: expected an exact match, got mean {mean:F4}, max {max}, {beyondTwo:P4} of channels off by more than 2.");
        }

        [Theory]
        [InlineData("logo.jpg")] // baseline, 4:4:4
        [InlineData("image_002.jpg")] // baseline, 4:4:4, 2000x1500
        [InlineData("image_001.jpg")] // progressive, 4:2:0 - exercises chroma upsampling
        [InlineData("image_003.jpg")] // progressive, 4:4:4
        public void Jpeg_MatchesStbWithinIdctRounding(string fixture)
        {
            // JPEG's inverse DCT is not exactly specified — the standard defines the transform mathematically and
            // lets implementations approximate it, so every decoder rounds slightly differently and matching stb byte
            // for byte is neither possible nor desirable. What rounding cannot do is produce a *structured* error:
            // a misread Huffman table, a botched progressive refinement or swapped chroma planes give means in the
            // tens and visible 8x8 blocking, nowhere near these bounds.
            //
            // The bounds are deliberately close to what these files actually do (mean ~0.01, max 3, and the largest
            // per-channel mean is green — stb truncates its green term, which is stb's rounding rather than anyone's
            // bug). A tolerance loose enough to be obviously safe would be loose enough to sleep through a subtly
            // wrong IDCT constant, which is the regression worth catching here; a structural break is caught by any
            // threshold at all. Both decoders are deterministic integer arithmetic and stb's version is pinned, so
            // there is no flakiness to leave headroom for.
            var (mine, stb) = DecodeBoth(fixture);
            var (mean, max, beyondTwo) = Compare(mine, stb);

            Assert.Equal(stb.Width, mine.Width);
            Assert.Equal(stb.Height, mine.Height);
            Assert.True(mean < 0.05, $"{fixture}: mean absolute difference {mean:F4} is too large for IDCT rounding.");
            Assert.True(beyondTwo < 0.0005,
                $"{fixture}: {beyondTwo:P4} of channels differ by more than 2, which is structural rather than rounding.");
            Assert.True(max <= 8, $"{fixture}: worst channel differs by {max}.");
        }

        [Fact]
        public void Jpeg_ProgressiveAndBaseline_AgreeOnAlpha()
        {
            // JPEG carries no alpha, so every pixel must come out opaque. Worth its own assertion because a decoder
            // that left the buffer's alpha at its zeroed default would still pass a colour comparison against stb's
            // RGB and then render as nothing at all.
            Assert.SkipUnless(TestImages.Available, "Image fixtures are not present in media/.");

            foreach (var fixture in new[] {"logo.jpg", "image_001.jpg"})
            {
                var pixels = Decode(fixture, _builtIn);
                for (var y = 0; y < pixels.Height; y += 37)
                for (var x = 0; x < pixels.Width; x += 37)
                    Assert.Equal(255, pixels.GetPixel(x, y).A);
            }
        }

        /// <summary>
        ///     Decodes a fixture with both implementations, skipping when that particular file is absent. Checked per
        ///     file rather than per folder, since the fixtures have arrived in batches and a checkout that kept only
        ///     some of them should still run the tests for the ones it has.
        /// </summary>
        private static (PixelBuffer Mine, PixelBuffer Stb) DecodeBoth(string fixture)
        {
            var path = TestImages.Media(fixture);
            Assert.SkipUnless(path != null && File.Exists(path), $"Fixture media/{fixture} is not present.");

            return (Decode(fixture, _builtIn), Decode(fixture, TestImages.Decoder));
        }

        private static PixelBuffer Decode(string fixture, IImageDecoder decoder)
        {
            using var stream = File.OpenRead(TestImages.Media(fixture));
            return decoder.Decode(stream);
        }

        /// <summary>
        ///     Compares two decodes channel by channel. Reports the mean and worst absolute difference, and the share
        ///     of channels off by more than two — the last being the one that separates rounding from a bug, since
        ///     rounding is everywhere and tiny while a bug is concentrated and large.
        /// </summary>
        private static (double Mean, int Max, double BeyondTwo) Compare(PixelBuffer mine, PixelBuffer stb)
        {
            if (mine.Width != stb.Width || mine.Height != stb.Height)
                return (double.MaxValue, int.MaxValue, 1.0);

            long total = 0;
            var max = 0;
            var beyondTwo = 0;

            for (var i = 0; i < mine.Data.Length; i++)
            {
                var difference = Math.Abs(mine.Data[i] - stb.Data[i]);
                total += difference;
                if (difference > max) max = difference;
                if (difference > 2) beyondTwo++;
            }

            return ((double) total / mine.Data.Length, max, (double) beyondTwo / mine.Data.Length);
        }
    }
}
