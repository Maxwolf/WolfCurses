// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/17/2026

using System;
using System.IO;
using System.Text;
using WolfCurses.Graphics.Decoding;

namespace WolfCurses.Graphics
{
    /// <summary>
    ///     The decoder <see cref="ImageDecoders.Default" /> starts out as: recognizes PNG, JPEG and GIF from their
    ///     leading bytes and hands the data to whichever of the three built-in decoders owns that format. It is why
    ///     <see cref="AnsiImage.FromFile" /> works with no set-up at all.
    ///     <para>
    ///         These three are written from their specifications and live in this package, so the library still has no
    ///         dependencies — which is the point. A decoder is the one part of an image pipeline that everybody needs
    ///         and nobody wants to choose, and the usual answer is to make every consumer of a terminal UI library take
    ///         a transitive dependency on an imaging library to show a logo. Owning the formats outright is what avoids
    ///         imposing that.
    ///     </para>
    ///     <para>
    ///         What it is not is the fastest decoder available, and it does not try to be. It is pure managed code
    ///         aimed at correctness and legibility; a picture bound for a terminal is about to be scaled down to a few
    ///         thousand pixels anyway, so the time spent decoding it is not what anyone will notice. Applications that
    ///         do notice, or that need a format outside these three, replace it — the seam is unchanged and one line:
    ///         <code>
    ///             ImageDecoders.Default = new StbImageDecoder();
    ///         </code>
    ///     </para>
    ///     <para>
    ///         Holds no decode state and may be shared across threads.
    ///     </para>
    /// </summary>
    /// <seealso cref="IImageDecoder" />
    /// <seealso cref="PngDecoder" />
    /// <seealso cref="JpegDecoder" />
    /// <seealso cref="GifDecoder" />
    public sealed class BuiltInImageDecoder : IImageDecoder
    {
        private readonly GifDecoder _gif;
        private readonly JpegDecoder _jpeg;
        private readonly PngDecoder _png;

        /// <summary>
        ///     Initializes a new instance of the <see cref="BuiltInImageDecoder" /> class.
        /// </summary>
        /// <param name="maxPixels">
        ///     The largest image, in pixels, any of the built-in decoders will decode. The default is generous enough
        ///     that no real photograph reaches it; it exists so a malformed or hostile file cannot ask for an
        ///     allocation the process cannot survive.
        /// </param>
        public BuiltInImageDecoder(int maxPixels = DecoderGuards.DefaultMaxPixels)
        {
            MaxPixels = DecoderGuards.ValidateMaxPixels(maxPixels, nameof(maxPixels));
            _png = new PngDecoder(maxPixels);
            _jpeg = new JpegDecoder(maxPixels);
            _gif = new GifDecoder(maxPixels);
        }

        /// <summary>The largest image, in pixels, this decoder will decode.</summary>
        public int MaxPixels { get; }

        /// <inheritdoc />
        public PixelBuffer Decode(Stream source)
        {
            var data = DecoderGuards.ReadAll(source);

            // Dispatch on what the bytes say they are rather than on a file extension, which the stream overload does
            // not have and which lies often enough to be worth ignoring even when it does.
            if (PngDecoder.HasSignature(data))
                return _png.DecodeBytes(data);
            if (JpegDecoder.HasSignature(data))
                return _jpeg.DecodeBytes(data);
            if (GifDecoder.HasSignature(data))
                return _gif.DecodeBytes(data);

            throw new InvalidDataException(
                $"The data is not a PNG, JPEG or GIF{DescribeOpeningBytes(data)}. Those are the three formats " +
                "WolfCurses decodes on its own, which is what lets the package carry no dependencies. Anything else " +
                "— WebP, TIFF, BMP, an SVG, or a file that is simply not an image — needs a decoder from elsewhere:" +
                Environment.NewLine + Environment.NewLine +
                "    ImageDecoders.Default = new StbImageDecoder();" +
                Environment.NewLine + Environment.NewLine +
                "Implement IImageDecoder over whatever image library you already use — it is a single method — or " +
                "copy the StbImageSharp adapter from the example app (example/WolfCurses.Example/Graphics/). Pixels " +
                "you have already decoded need no decoder at all: AnsiImage.FromPixels takes a PixelBuffer.");
        }

        /// <summary>
        ///     Describes what the data actually starts with, to turn "not an image" into something diagnosable. Names
        ///     the handful of formats likely to be mistaken for one of the supported three, and otherwise falls back to
        ///     the raw bytes, which are enough to spot the usual culprits — an HTML error page saved as a .jpg, or a
        ///     Git LFS pointer that was never fetched.
        /// </summary>
        private static string DescribeOpeningBytes(byte[] data)
        {
            if (data.Length == 0)
                return " (the stream was empty)";

            var known = Identify(data);
            if (known != null)
                return $" (it looks like {known})";

            var count = Math.Min(4, data.Length);
            var hex = new StringBuilder(" (it starts 0x");
            for (var i = 0; i < count; i++)
                hex.Append(data[i].ToString("X2"));

            return hex.Append(')').ToString();
        }

        /// <summary>Names a format from its magic bytes, or null when nothing is recognized.</summary>
        private static string Identify(byte[] data)
        {
            if (StartsWith(data, "RIFF") && data.Length >= 12 && StartsWith(data, "WEBP", 8))
                return "a WebP";
            if (StartsWith(data, "BM"))
                return "a BMP";
            if (StartsWith(data, "II*\0") || StartsWith(data, "MM\0*"))
                return "a TIFF";
            if (StartsWith(data, "qoif"))
                return "a QOI";
            if (data.Length >= 12 && StartsWith(data, "ftyp", 4))
                return "an ISO base-media file, perhaps HEIC or AVIF";
            if (StartsWith(data, "<?xml") || StartsWith(data, "<svg"))
                return "an SVG or some other XML";
            if (StartsWith(data, "<!DOCTYPE") || StartsWith(data, "<html"))
                return "an HTML page";
            if (StartsWith(data, "version https://git-lfs"))
                return "a Git LFS pointer whose content was never fetched";
            if (StartsWith(data, "PK"))
                return "a ZIP archive";

            return null;
        }

        /// <summary>True when the data carries the given ASCII text at the given offset.</summary>
        private static bool StartsWith(byte[] data, string text, int offset = 0)
        {
            if (data.Length < offset + text.Length)
                return false;

            for (var i = 0; i < text.Length; i++)
                if (data[offset + i] != text[i])
                    return false;

            return true;
        }
    }
}
