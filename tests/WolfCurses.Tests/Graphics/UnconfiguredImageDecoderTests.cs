// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/16/2026

using System;
using System.IO;
using WolfCurses.Graphics;
using Xunit;

namespace WolfCurses.Tests.Graphics
{
    /// <summary>
    ///     Pins what happens when an application loads an image without choosing a decoder. The library ships none, so
    ///     this is the out-of-the-box experience for anyone who calls <see cref="AnsiImage.FromFile" /> before wiring
    ///     <see cref="ImageDecoders.Default" /> — and the whole reason the unconfigured default is a throwing stand-in
    ///     rather than null. It must fail with an explanation, not a NullReferenceException.
    ///     <para>
    ///         These read the process-wide <see cref="ImageDecoders.Default" />, which is safe only because no other
    ///         test assigns it: the integration tests pass their decoder per-call instead. Installing one anywhere in
    ///         the suite would make the first test here fail depending on run order.
    ///     </para>
    /// </summary>
    public class UnconfiguredImageDecoderTests
    {
        [Fact]
        public void Default_WithNothingConfigured_IsTheUnconfiguredStandIn()
        {
            Assert.IsType<UnconfiguredImageDecoder>(ImageDecoders.Default);
        }

        [Fact]
        public void Default_IsNeverNull()
        {
            Assert.NotNull(ImageDecoders.Default);
        }

        [Fact]
        public void Decode_WithNothingConfigured_ThrowsExplanationRatherThanNullReference()
        {
            using var stream = new MemoryStream(new byte[] {1, 2, 3, 4});

            var ex = Assert.Throws<InvalidOperationException>(() => AnsiImage.FromStream(stream));

            // The message is the entire value of this type, so it is worth asserting it actually helps: say what is
            // wrong, name the property to assign, and point at the example. A bare "not supported" would leave the
            // reader no better off than the NullReferenceException this exists to replace.
            Assert.Contains("No image decoder is configured", ex.Message, StringComparison.Ordinal);
            Assert.Contains("ImageDecoders.Default", ex.Message, StringComparison.Ordinal);
            Assert.Contains("IImageDecoder", ex.Message, StringComparison.Ordinal);
            Assert.Contains("example", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void RenderFile_WithNothingConfigured_ThrowsTheSameExplanation()
        {
            // Goes through the convenience entry point, which has no decoder parameter at all, so the process-wide
            // default is the only thing it can use. Uses a file that exists (this assembly) to prove the decoder is
            // what refused, not File.OpenRead.
            var self = typeof (UnconfiguredImageDecoderTests).Assembly.Location;

            var ex = Assert.Throws<InvalidOperationException>(() => AnsiImage.RenderFile(self));

            Assert.Contains("No image decoder is configured", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void FromPixels_WithNothingConfigured_StillWorks()
        {
            // Already-decoded pixels never touch a decoder, so the whole rendering half of the feature is usable with
            // no dependency at all. This is what makes shipping without a decoder reasonable rather than crippling.
            var pixels = new PixelBuffer(1, 2);
            pixels.SetPixel(0, 0, new Rgba32(255, 0, 0, 255));
            pixels.SetPixel(0, 1, new Rgba32(0, 0, 255, 255));

            var ansi = AnsiImage.FromPixels(pixels).ToAnsi(new AnsiImageOptions {MaxColumns = 1, MaxRows = 1});

            Assert.False(string.IsNullOrEmpty(ansi));
        }

        [Fact]
        public void Default_AssigningNull_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => ImageDecoders.Default = null);
        }
    }
}
