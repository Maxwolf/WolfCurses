// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/16/2026

using System;
using System.IO;
using StbImageSharp;
using WolfCurses.Graphics;

namespace WolfCurses.Tests.Support
{
    /// <summary>
    ///     Test-only image decoder, so the integration tests can turn the repository's real PNG and JPEG fixtures into
    ///     pixels. The library ships no decoder of its own (that would put a third-party dependency in the package), so
    ///     the tests bring one exactly as a consuming application would.
    ///     <para>
    ///         The example app has a near-identical adapter, and that duplication is deliberate rather than an
    ///         oversight: these are the library's own tests, so having them compile a file out of the demo app would
    ///         point the dependency backwards and let a change over there break the test suite. Both are a thin
    ///         forwarding call, and each project owns its own.
    ///     </para>
    ///     <para>
    ///         Tests pass this explicitly to <see cref="AnsiImage.FromFile" /> rather than assigning
    ///         <see cref="ImageDecoders.Default" />, which keeps the process-wide default at its unconfigured stand-in
    ///         where <see cref="Graphics.UnconfiguredImageDecoderTests" /> can still assert what it does.
    ///     </para>
    /// </summary>
    internal sealed class StbImageDecoder : IImageDecoder
    {
        /// <inheritdoc />
        public PixelBuffer Decode(Stream source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var image = ImageResult.FromStream(source, ColorComponents.RedGreenBlueAlpha);
            if (image == null || image.Data == null)
                throw new InvalidDataException("The stream did not contain a decodable image.");

            return new PixelBuffer(image.Width, image.Height, image.Data);
        }
    }
}
