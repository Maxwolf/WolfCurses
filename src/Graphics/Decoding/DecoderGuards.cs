// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/17/2026

using System;
using System.IO;

namespace WolfCurses.Graphics.Decoding
{
    /// <summary>
    ///     The limits every built-in decoder answers to, and the two checks that enforce them.
    ///     <para>
    ///         Decoding is the one place in this library that parses bytes it did not write, and an application using
    ///         <see cref="WolfCurses.Controls.FileDialog" /> lets a user point it at any file on the machine. Managed
    ///         code means a hostile image cannot corrupt memory the way it could in C — but it can still ask for an
    ///         allocation nothing can satisfy, and it does not have to be large to do it: an entirely legitimate
    ///         2400x2400 PNG in this repository's own <c>media/</c> folder is 163 KB on disk and 23 MB decompressed, a
    ///         141:1 ratio. A file built to be hostile rather than merely efficient does far better than that.
    ///     </para>
    ///     <para>
    ///         The defence is not a cap bolted on at the end but the order the decoders work in: every format states
    ///         its dimensions in a header before its compressed data, so the dimensions are checked first and then
    ///         nothing is read or allocated beyond what they imply. A bomb only expands if something keeps reading it.
    ///     </para>
    /// </summary>
    internal static class DecoderGuards
    {
        /// <summary>
        ///     The most pixels a decoded image may contain unless the application says otherwise, chosen as roughly
        ///     twice a 33-megapixel 8K frame: far past anything a terminal can show, far short of an allocation that
        ///     brings the process down. At 4 bytes each this is a 320 MB ceiling on the decoded buffer.
        /// </summary>
        internal const int DefaultMaxPixels = 80_000_000;

        /// <summary>
        ///     The most encoded bytes a decoder will read from a stream. Distinct from
        ///     <see cref="DefaultMaxPixels" />: this bounds the file going in, that bounds the pixels coming out, and a
        ///     compression bomb is precisely a small former promising a huge latter.
        /// </summary>
        internal const int DefaultMaxEncodedBytes = 256 * 1024 * 1024;

        /// <summary>
        ///     Reads a stream to its end, refusing to grow past <paramref name="maxBytes" />. The built-in decoders all
        ///     want random access to the whole file — every one of these formats back-references earlier bytes — so the
        ///     read happens once, here, rather than each decoder inventing its own.
        /// </summary>
        /// <param name="source">Readable stream positioned at the start of the encoded image.</param>
        /// <param name="maxBytes">Ceiling on the encoded size.</param>
        /// <returns>The stream's remaining bytes.</returns>
        internal static byte[] ReadAll(Stream source, int maxBytes = DefaultMaxEncodedBytes)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var buffer = new MemoryStream();
            var chunk = new byte[81920];

            int read;
            while ((read = source.Read(chunk, 0, chunk.Length)) > 0)
            {
                if (buffer.Length + read > maxBytes)
                    throw new InvalidDataException(
                        $"Encoded image is larger than the {maxBytes:N0} byte limit this decoder will read.");

                buffer.Write(chunk, 0, read);
            }

            return buffer.ToArray();
        }

        /// <summary>
        ///     Checks the size an image's header claims before anything is allocated or decompressed on the strength of
        ///     it. Multiplied in 64-bit math so a crafted 65535x65535 header cannot overflow into a small, innocent
        ///     looking product.
        /// </summary>
        /// <param name="format">Format name, used only to make the error say which decoder objected.</param>
        /// <param name="width">Width the header declares.</param>
        /// <param name="height">Height the header declares.</param>
        /// <param name="maxPixels">Ceiling from the decoder's own configuration.</param>
        internal static void ValidateDimensions(string format, int width, int height, int maxPixels)
        {
            if (width <= 0 || height <= 0)
                throw new InvalidDataException($"{format} image declares an empty {width}x{height} size.");

            var pixels = (long) width * height;
            if (pixels > maxPixels)
                throw new InvalidDataException(
                    $"{format} image declares {width}x{height} ({pixels:N0} pixels), beyond the {maxPixels:N0} pixel " +
                    "limit this decoder will allocate for. Raise the decoder's maxPixels if the image is genuinely " +
                    "this large.");
        }

        /// <summary>Validates a caller-supplied pixel ceiling.</summary>
        /// <param name="maxPixels">The value to check.</param>
        /// <param name="parameterName">Name to report on failure.</param>
        /// <returns><paramref name="maxPixels" />, when it is usable.</returns>
        internal static int ValidateMaxPixels(int maxPixels, string parameterName)
        {
            if (maxPixels < 1)
                throw new ArgumentOutOfRangeException(parameterName, maxPixels,
                    "A decoder must be allowed at least one pixel.");

            return maxPixels;
        }
    }
}
