// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System;
using System.IO;
using StbImageSharp;
using WolfCurses.Graphics;

namespace WolfCurses.Example.Graphics
{
    /// <summary>
    ///     Teaches WolfCurses how to read PNG and JPEG files, by adapting StbImageSharp to the library's
    ///     <see cref="IImageDecoder" /> seam. This lives here in the example rather than in the library on purpose: a
    ///     decoder for real image formats means a third-party dependency, and WolfCurses leaves that choice to the
    ///     application instead of forcing it on everyone who installs the package.
    ///     <para>
    ///         This is the whole of it — an application that wants image loading adds the StbImageSharp package, copies
    ///         this file, and assigns <c>ImageDecoders.Default</c> at start-up (see <see cref="Program" />). Swap in any
    ///         other library — ImageSharp, SkiaSharp, System.Drawing, your own — by implementing the same one method.
    ///     </para>
    ///     <para>
    ///         StbImageSharp is used here because it suits a package that runs everywhere: a managed, public-domain
    ///         port of the widely used stb_image, with no native binaries to ship per platform. It decodes what a text
    ///         UI is likely to embed — PNG (with an alpha channel for transparency), baseline <em>and</em> progressive
    ///         JPEG, BMP, GIF, TGA, PSD.
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
