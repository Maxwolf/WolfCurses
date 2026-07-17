// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System;
using System.IO;
using StbImageSharp;
using WolfCurses.Graphics;

namespace WolfCurses.Example.Graphics
{
    /// <summary>
    ///     Replaces WolfCurses' built-in decoders with StbImageSharp, by adapting it to the library's
    ///     <see cref="IImageDecoder" /> seam. This file is the whole of what that takes — copy it, swap StbImageSharp
    ///     for whatever imaging library you already have, and assign <c>ImageDecoders.Default</c> at start-up.
    ///     <para>
    ///         Nothing in this app installs it any more. WolfCurses decodes PNG, JPEG and GIF itself now, so the
    ///         example runs on the built-in decoders — which is the more useful thing for it to demonstrate, since it
    ///         proves they handle real photographs in a real application. This stays because it is what the library's
    ///         own "that format is not one of the three" error points at, and because compiling it here is what keeps
    ///         that pointer honest. <see cref="Program" /> has the one commented line that switches to it.
    ///     </para>
    ///     <para>
    ///         Worth doing when an application needs a format outside the built-in three (WebP, TIFF, PSD), when
    ///         decode speed turns out to matter, or simply so that one process is not decoding images two different
    ///         ways. StbImageSharp suits a package that runs everywhere: a managed, public-domain port of the widely
    ///         used stb_image, with no native binaries to ship per platform.
    ///     </para>
    /// </summary>
    public sealed class StbImageDecoder : IImageDecoder
    {
        /// <inheritdoc />
        public PixelBuffer Decode(Stream source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            // Force RGBA so a paletted PNG, a greyscale image, or a three-channel JPEG all arrive with a predictable
            // 4-bytes-per-pixel layout and a usable (fully opaque when absent) alpha channel.
            var image = ImageResult.FromStream(source, ColorComponents.RedGreenBlueAlpha);
            if (image == null || image.Data == null)
                throw new InvalidDataException("The stream did not contain a decodable image.");

            return new PixelBuffer(image.Width, image.Height, image.Data);
        }
    }
}
