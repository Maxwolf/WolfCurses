// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System.IO;

namespace WolfCurses.Graphics
{
    /// <summary>
    ///     Turns an encoded image (the bytes of a PNG, JPEG, and so on) into a decoded <see cref="PixelBuffer" /> of
    ///     RGBA pixels. The rest of the ANSI graphics feature only ever deals in <see cref="PixelBuffer" />, so swapping
    ///     the decoder is the single seam a consuming application needs to touch to bring its own image loading library
    ///     (for example if it wants to avoid the default third-party dependency, or support an exotic format).
    /// </summary>
    /// <seealso cref="ImageDecoders" />
    /// <seealso cref="StbImageDecoder" />
    public interface IImageDecoder
    {
        /// <summary>
        ///     Reads an encoded image from the stream and returns its decoded pixels. Implementations should consume the
        ///     stream from its current position and are expected to throw when the data is not a supported image.
        /// </summary>
        /// <param name="source">Readable stream positioned at the start of the encoded image.</param>
        /// <returns>The decoded image as straight-alpha RGBA pixels.</returns>
        PixelBuffer Decode(Stream source);
    }
}
