// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System.IO;

namespace WolfCurses.Graphics
{
    /// <summary>
    ///     Turns an encoded image (the bytes of a PNG, JPEG, and so on) into a decoded <see cref="PixelBuffer" /> of
    ///     RGBA pixels. The rest of the ANSI graphics feature only ever deals in <see cref="PixelBuffer" />, so this is
    ///     the single seam an application implements to bring its own image loading library.
    ///     <para>
    ///         Implementing this is optional: <see cref="BuiltInImageDecoder" /> handles PNG, JPEG and GIF out of the
    ///         box and is what <see cref="ImageDecoders.Default" /> starts as. Reach for the seam when you need a
    ///         format outside those three, when decode speed matters, or simply so one process is not decoding images
    ///         two different ways. Assign yours to <see cref="ImageDecoders.Default" /> at start-up, or pass it
    ///         per-call to <see cref="AnsiImage.FromFile" /> and friends; the example app adapts StbImageSharp in
    ///         about thirty lines.
    ///     </para>
    /// </summary>
    /// <seealso cref="ImageDecoders" />
    /// <seealso cref="BuiltInImageDecoder" />
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
