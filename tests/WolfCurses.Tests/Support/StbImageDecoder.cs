// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/16/2026

using System;
using System.IO;
using StbImageSharp;
using WolfCurses.Graphics;

namespace WolfCurses.Tests.Support
{
    /// <summary>
    ///     Test-only image decoder. It has two jobs, and the second is the important one.
    ///     <para>
    ///         It loads the repository's real PNG and JPEG fixtures for the rendering tests, exercising
    ///         <see cref="IImageDecoder" /> the way a consuming application that brings its own imaging library would —
    ///         which is worth keeping now that the library has a decoder of its own, because otherwise nothing would
    ///         prove the seam still fits anything else.
    ///     </para>
    ///     <para>
    ///         And it is the oracle <see cref="Graphics.DecoderDifferentialTests" /> checks the built-in decoders
    ///         against: a separate implementation, written by other people from the same specifications. A decoder's
    ///         own unit tests can only assert what its author believed the format meant, so a second opinion is the
    ///         only cheap way to catch a misread spec. The test project taking this dependency costs nothing that
    ///         matters — the package still has none.
    ///     </para>
    ///     <para>
    ///         The example app has a near-identical adapter, and that duplication is deliberate rather than an
    ///         oversight: these are the library's own tests, so having them compile a file out of the demo app would
    ///         point the dependency backwards and let a change over there break the test suite. Both are a thin
    ///         forwarding call, and each project owns its own.
    ///     </para>
    ///     <para>
    ///         Tests pass this explicitly to <see cref="AnsiImage.FromFile" /> rather than assigning
    ///         <see cref="ImageDecoders.Default" />, so the default stays what an application would actually get.
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
