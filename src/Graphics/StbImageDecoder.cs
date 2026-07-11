// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System;
using System.IO;
using StbImageSharp;

namespace WolfCurses.Graphics
{
    /// <summary>
    ///     The default <see cref="IImageDecoder" />, backed by StbImageSharp (a managed, public-domain port of the
    ///     widely used stb_image decoder). It is pure managed code with no native binaries, keeping the library
    ///     cross-platform, and decodes the formats a text UI is likely to embed: PNG (including an alpha channel for
    ///     transparency), baseline <em>and</em> progressive JPEG, BMP, GIF, TGA and PSD. Output is always forced to
    ///     four channel RGBA so downstream code has one uniform layout to reason about.
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
