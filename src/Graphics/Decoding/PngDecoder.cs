// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/17/2026

using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace WolfCurses.Graphics.Decoding
{
    /// <summary>
    ///     Decodes PNG images, in full: every colour type (greyscale, truecolour, palette, and both alpha variants),
    ///     every bit depth from 1 to 16, transparency in all three forms it can take, and Adam7 interlacing.
    ///     <para>
    ///         This is the cheapest of the three built-in decoders to own, because the expensive half of PNG is not
    ///         PNG. The image data is a zlib stream, and .NET has shipped <see cref="ZLibStream" /> since 6 — so DEFLATE
    ///         arrives already written, already native, and already maintained by somebody else. What is left is
    ///         unpicking the chunk structure, undoing the per-scanline filters, and widening whatever sample format the
    ///         file used into RGBA.
    ///     </para>
    ///     <para>
    ///         Instances hold no decode state and may be shared freely across threads, which is what lets a single one
    ///         sit in <see cref="ImageDecoders.Default" /> for the life of the process.
    ///     </para>
    /// </summary>
    /// <seealso cref="BuiltInImageDecoder" />
    public sealed class PngDecoder : IImageDecoder
    {
        /// <summary>The eight bytes that open every PNG. The odd ones are a deliberate transfer-corruption trap.</summary>
        private static readonly byte[] _signature = {0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A};

        /// <summary>
        ///     Adam7's seven passes as {xOrigin, yOrigin, xStep, yStep}. An interlaced PNG is not one image but seven
        ///     sparse ones, each a lattice of the full picture, sent coarsest first so a slow connection could show
        ///     something before the whole file arrived. They share one zlib stream but not their filtering: each pass
        ///     is its own little image with its own scanlines and its own zeroed "previous row" to start from.
        /// </summary>
        private static readonly int[][] _adam7Passes =
        {
            new[] {0, 0, 8, 8},
            new[] {4, 0, 8, 8},
            new[] {0, 4, 4, 8},
            new[] {2, 0, 4, 4},
            new[] {0, 2, 2, 4},
            new[] {1, 0, 2, 2},
            new[] {0, 1, 1, 2}
        };

        /// <summary>A non-interlaced image, expressed as a single pass so both paths share one loop.</summary>
        private static readonly int[][] _singlePass = {new[] {0, 0, 1, 1}};

        /// <summary>
        ///     Initializes a new instance of the <see cref="PngDecoder" /> class.
        /// </summary>
        /// <param name="maxPixels">
        ///     The largest image, in pixels, this decoder will decode. See <see cref="DecoderGuards" /> for why the
        ///     ceiling exists and why it is checked against the header rather than the result.
        /// </param>
        public PngDecoder(int maxPixels = DecoderGuards.DefaultMaxPixels)
        {
            MaxPixels = DecoderGuards.ValidateMaxPixels(maxPixels, nameof(maxPixels));
        }

        /// <summary>The largest image, in pixels, this decoder will decode.</summary>
        public int MaxPixels { get; }

        /// <inheritdoc />
        public PixelBuffer Decode(Stream source)
        {
            return DecodeBytes(DecoderGuards.ReadAll(source));
        }

        /// <summary>Decodes a PNG already held in memory.</summary>
        /// <param name="data">The complete file.</param>
        /// <returns>The decoded image.</returns>
        internal PixelBuffer DecodeBytes(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (!HasSignature(data))
                throw new InvalidDataException("Data does not begin with a PNG signature.");

            int width = 0, height = 0, bitDepth = 0, colorType = -1, interlace = 0;
            byte[] palette = null;
            byte[] paletteAlpha = null;
            var transparentGray = -1;
            int[] transparentRgb = null;
            var idat = new MemoryStream();

            // Chunks run length/type/data/CRC until IEND. The CRC is skipped rather than verified: it catches
            // transmission corruption, which is not the failure this decoder is defending against, and a file that
            // decodes cleanly with a stale CRC is more useful to a caller than an exception.
            var offset = _signature.Length;
            while (offset + 8 <= data.Length)
            {
                var length = ReadInt32(data, offset);
                if (length < 0)
                    throw new InvalidDataException("PNG chunk declares a negative length.");

                var type = Encoding.ASCII.GetString(data, offset + 4, 4);
                var start = offset + 8;
                if ((long) start + length + 4 > data.Length)
                    throw new InvalidDataException($"PNG {type} chunk runs past the end of the file.");

                switch (type)
                {
                    case "IHDR":
                        if (length != 13)
                            throw new InvalidDataException($"PNG IHDR chunk is {length} bytes rather than 13.");

                        width = ReadInt32(data, start);
                        height = ReadInt32(data, start + 4);
                        bitDepth = data[start + 8];
                        colorType = data[start + 9];
                        interlace = data[start + 12];

                        DecoderGuards.ValidateDimensions("PNG", width, height, MaxPixels);
                        ValidateFormat(bitDepth, colorType, data[start + 10], data[start + 11], interlace);
                        break;

                    case "PLTE":
                        if (length == 0 || length % 3 != 0 || length > 256 * 3)
                            throw new InvalidDataException($"PNG palette is {length} bytes, not 3 per entry up to 256.");

                        palette = new byte[length];
                        Array.Copy(data, start, palette, 0, length);
                        break;

                    case "tRNS":
                        switch (colorType)
                        {
                            case 3:
                                // One alpha per palette entry, and it may stop early: entries past the end are opaque.
                                paletteAlpha = new byte[length];
                                Array.Copy(data, start, paletteAlpha, 0, length);
                                break;
                            case 0 when length >= 2:
                                // A single grey level declared transparent. It is written 16-bit on the wire
                                // whatever the image's depth — at depth 8 and below the value simply lands in the
                                // low byte — and it is compared at that full precision, never against the narrowed
                                // 8-bit sample this decoder ends up storing. Two 16-bit greys can share a high byte
                                // while being different colours, and narrowing first would turn the second of them
                                // transparent for no better reason than that it rounds to the same output byte.
                                transparentGray = (data[start] << 8) | data[start + 1];
                                break;
                            case 2 when length >= 6:
                                transparentRgb = new[]
                                {
                                    (data[start] << 8) | data[start + 1],
                                    (data[start + 2] << 8) | data[start + 3],
                                    (data[start + 4] << 8) | data[start + 5]
                                };
                                break;
                        }

                        break;

                    case "IDAT":
                        idat.Write(data, start, length);
                        break;

                    case "IEND":
                        offset = data.Length;
                        continue;
                }

                offset = start + length + 4;
            }

            if (colorType < 0)
                throw new InvalidDataException("PNG has no IHDR chunk.");
            if (idat.Length == 0)
                throw new InvalidDataException("PNG has no image data.");
            if (colorType == 3 && palette == null)
                throw new InvalidDataException("PNG uses a palette but carries no PLTE chunk.");

            var channels = ChannelsFor(colorType);
            var raw = Inflate(idat, TotalRawBytes(width, height, channels, bitDepth, interlace));

            return Reconstruct(raw, width, height, bitDepth, colorType, channels, interlace,
                palette, paletteAlpha, transparentGray, transparentRgb);
        }

        /// <summary>Turns unfiltered scanlines into finished pixels, one Adam7 pass at a time.</summary>
        private static PixelBuffer Reconstruct(byte[] raw, int width, int height, int bitDepth, int colorType,
            int channels, int interlace, byte[] palette, byte[] paletteAlpha, int transparentGray,
            int[] transparentRgb)
        {
            var result = new PixelBuffer(width, height);

            // The filter's notion of "the pixel to the left" is a fixed byte distance, rounded up to 1 for the
            // sub-byte depths where several pixels share a byte and "left" has no byte to point at.
            var filterUnit = Math.Max(1, channels * bitDepth / 8);

            // What a raw sample must be multiplied by to fill the 0-255 range: 1-bit black/white becomes 0/255, 2-bit
            // steps by 85, 4-bit by 17, and 8-bit is already there. 16-bit is not on this ladder — it narrows by
            // division rather than stretching by multiplication, so ToByte handles it separately.
            var grayScale = bitDepth switch {1 => 255, 2 => 85, 4 => 17, _ => 1};
            var paletteCount = palette == null ? 0 : palette.Length / 3;

            // Reads a sample at its full stored precision: 0-65535 at depth 16, and the raw packed value at every
            // depth below. Full precision matters because tRNS is compared against this, and a palette index is
            // this — neither survives being narrowed first.
            int Sample(byte[] row, int rowStart, int index)
            {
                switch (bitDepth)
                {
                    case 8:
                        return row[rowStart + index];
                    case 16:
                        return (row[rowStart + index * 2] << 8) | row[rowStart + index * 2 + 1];
                    default:
                        var perByte = 8 / bitDepth;
                        var packed = row[rowStart + index / perByte];
                        var shift = 8 - bitDepth * (index % perByte + 1);
                        return (packed >> shift) & ((1 << bitDepth) - 1);
                }
            }

            // Narrows a colour sample to the 8 bits a PixelBuffer channel holds. Deliberately not applied to palette
            // indices, which are addresses rather than intensities and mean nothing once scaled.
            byte ToByte(int sample)
            {
                return bitDepth == 16 ? (byte) (sample >> 8) : (byte) (sample * grayScale);
            }

            Rgba32 ReadPixel(byte[] row, int rowStart, int px)
            {
                switch (colorType)
                {
                    case 0:
                    {
                        var level = Sample(row, rowStart, px);
                        var value = ToByte(level);
                        return new Rgba32(value, value, value, level == transparentGray ? (byte) 0 : (byte) 255);
                    }

                    case 2:
                    {
                        var r = Sample(row, rowStart, px * 3);
                        var g = Sample(row, rowStart, px * 3 + 1);
                        var b = Sample(row, rowStart, px * 3 + 2);
                        var clear = transparentRgb != null &&
                                    r == transparentRgb[0] && g == transparentRgb[1] && b == transparentRgb[2];
                        return new Rgba32(ToByte(r), ToByte(g), ToByte(b), clear ? (byte) 0 : (byte) 255);
                    }

                    case 3:
                    {
                        var index = Sample(row, rowStart, px);
                        if (index >= paletteCount)
                            throw new InvalidDataException(
                                $"PNG pixel refers to palette entry {index}, beyond the {paletteCount} the file has.");

                        var alpha = paletteAlpha != null && index < paletteAlpha.Length ? paletteAlpha[index] : (byte) 255;
                        return new Rgba32(palette[index * 3], palette[index * 3 + 1], palette[index * 3 + 2], alpha);
                    }

                    case 4:
                    {
                        var value = ToByte(Sample(row, rowStart, px * 2));
                        return new Rgba32(value, value, value, ToByte(Sample(row, rowStart, px * 2 + 1)));
                    }

                    default:
                        return new Rgba32(
                            ToByte(Sample(row, rowStart, px * 4)),
                            ToByte(Sample(row, rowStart, px * 4 + 1)),
                            ToByte(Sample(row, rowStart, px * 4 + 2)),
                            ToByte(Sample(row, rowStart, px * 4 + 3)));
                }
            }

            var offset = 0;
            foreach (var pass in interlace == 1 ? _adam7Passes : _singlePass)
            {
                int xOrigin = pass[0], yOrigin = pass[1], xStep = pass[2], yStep = pass[3];
                var passWidth = PassExtent(width, xOrigin, xStep);
                var passHeight = PassExtent(height, yOrigin, yStep);
                if (passWidth == 0 || passHeight == 0)
                    continue;

                var scanlineBytes = (passWidth * channels * bitDepth + 7) / 8;
                var rows = Unfilter(raw, ref offset, passHeight, scanlineBytes, filterUnit);

                for (var py = 0; py < passHeight; py++)
                {
                    var rowStart = py * scanlineBytes;
                    for (var px = 0; px < passWidth; px++)
                        result.SetPixel(xOrigin + px * xStep, yOrigin + py * yStep, ReadPixel(rows, rowStart, px));
                }
            }

            return result;
        }

        /// <summary>
        ///     Reverses the per-scanline filter each row was encoded with, in place, top to bottom.
        ///     <para>
        ///         Filtering is PNG's whole compression trick: before DEFLATE ever sees a row, each byte has had a
        ///         prediction from its already-decoded neighbours subtracted from it, which turns a smooth gradient into
        ///         a run of near-zeroes. Undoing it has to run in the same order, because every prediction is made from
        ///         bytes that must already have been reconstructed — which is exactly why this cannot be vectorised or
        ///         parallelised, and why it is the slowest part of decoding a PNG.
        ///     </para>
        /// </summary>
        private static byte[] Unfilter(byte[] raw, ref int offset, int height, int scanlineBytes, int filterUnit)
        {
            var rows = new byte[height * scanlineBytes];

            for (var y = 0; y < height; y++)
            {
                if (offset + 1 + scanlineBytes > raw.Length)
                    throw new InvalidDataException("PNG image data ends before its declared height.");

                var filter = raw[offset++];
                if (filter > 4)
                    throw new InvalidDataException($"PNG scanline uses unknown filter type {filter}.");

                var rowStart = y * scanlineBytes;
                Array.Copy(raw, offset, rows, rowStart, scanlineBytes);
                offset += scanlineBytes;

                if (filter == 0)
                    continue;

                for (var i = 0; i < scanlineBytes; i++)
                {
                    // a = the same byte of the pixel to the left, b = directly above, c = above-left. Off the edge of
                    // the image they are zero, which is what makes the first row and first pixel work.
                    var a = i >= filterUnit ? rows[rowStart + i - filterUnit] : 0;
                    var b = y > 0 ? rows[rowStart - scanlineBytes + i] : 0;
                    var c = y > 0 && i >= filterUnit ? rows[rowStart - scanlineBytes + i - filterUnit] : 0;

                    rows[rowStart + i] = filter switch
                    {
                        1 => (byte) (rows[rowStart + i] + a),
                        2 => (byte) (rows[rowStart + i] + b),
                        3 => (byte) (rows[rowStart + i] + ((a + b) >> 1)),
                        _ => (byte) (rows[rowStart + i] + Paeth(a, b, c))
                    };
                }
            }

            return rows;
        }

        /// <summary>
        ///     PNG's Paeth predictor: of the three already-decoded neighbours, pick whichever is closest to the
        ///     linear estimate a + b - c. Its virtue is that it follows an edge in any direction rather than assuming
        ///     one, which is why it is the filter most encoders reach for most often.
        /// </summary>
        private static int Paeth(int a, int b, int c)
        {
            var estimate = a + b - c;
            var da = Math.Abs(estimate - a);
            var db = Math.Abs(estimate - b);
            var dc = Math.Abs(estimate - c);

            // The tie-breaking order is normative, not a preference: an encoder and decoder that break ties
            // differently reconstruct different images.
            if (da <= db && da <= dc) return a;
            return db <= dc ? b : c;
        }

        /// <summary>
        ///     Inflates the concatenated IDAT chunks, reading exactly the number of bytes the header implies and not
        ///     one more.
        ///     <para>
        ///         This is where a compression bomb dies. The size comes from IHDR, IHDR has already been checked
        ///         against <see cref="MaxPixels" />, and this reads to that size and stops — so a file claiming to be
        ///         2x2 cannot expand to a gigabyte however its zlib stream is built, because nothing will read the
        ///         gigabyte. Trailing data beyond the declared size is simply left unread rather than treated as an
        ///         error; it cannot hurt anything, and rejecting it would fail files that decode perfectly well.
        ///     </para>
        /// </summary>
        private static byte[] Inflate(MemoryStream idat, long expected)
        {
            if (expected > int.MaxValue)
                throw new InvalidDataException(
                    $"PNG expands to {expected:N0} bytes of scanlines, more than a single array can hold.");

            idat.Position = 0;
            var raw = new byte[expected];

            using var stream = new ZLibStream(idat, CompressionMode.Decompress);
            var filled = 0;
            while (filled < raw.Length)
            {
                var read = stream.Read(raw, filled, raw.Length - filled);
                if (read == 0)
                    throw new InvalidDataException(
                        $"PNG image data holds {filled:N0} bytes but its header describes {raw.Length:N0}.");

                filled += read;
            }

            return raw;
        }

        /// <summary>The exact size the inflated stream must be: every pass's scanlines, each with its filter byte.</summary>
        private static long TotalRawBytes(int width, int height, int channels, int bitDepth, int interlace)
        {
            long total = 0;
            foreach (var pass in interlace == 1 ? _adam7Passes : _singlePass)
            {
                var passWidth = PassExtent(width, pass[0], pass[2]);
                var passHeight = PassExtent(height, pass[1], pass[3]);
                if (passWidth == 0 || passHeight == 0)
                    continue;

                total += (long) passHeight * (1 + (passWidth * channels * bitDepth + 7) / 8);
            }

            return total;
        }

        /// <summary>How many rows or columns of the full image an Adam7 pass covers along one axis.</summary>
        private static int PassExtent(int size, int origin, int step)
        {
            return size <= origin ? 0 : (size - origin + step - 1) / step;
        }

        /// <summary>Samples per pixel for a PNG colour type.</summary>
        private static int ChannelsFor(int colorType)
        {
            return colorType switch
            {
                0 => 1, // greyscale
                2 => 3, // truecolour
                3 => 1, // palette index
                4 => 2, // greyscale + alpha
                6 => 4, // truecolour + alpha
                _ => throw new InvalidDataException($"PNG declares unknown colour type {colorType}.")
            };
        }

        /// <summary>
        ///     Rejects headers the format does not allow. Bit depth is not free to vary per colour type — 16-bit
        ///     palette entries and 2-bit truecolour are not merely unsupported here, they cannot be written by a
        ///     conforming encoder — so a file claiming one is malformed rather than exotic.
        /// </summary>
        private static void ValidateFormat(int bitDepth, int colorType, int compression, int filter, int interlace)
        {
            var allowed = colorType switch
            {
                0 => bitDepth is 1 or 2 or 4 or 8 or 16,
                2 => bitDepth is 8 or 16,
                3 => bitDepth is 1 or 2 or 4 or 8,
                4 => bitDepth is 8 or 16,
                6 => bitDepth is 8 or 16,
                _ => throw new InvalidDataException($"PNG declares unknown colour type {colorType}.")
            };

            if (!allowed)
                throw new InvalidDataException($"PNG colour type {colorType} cannot carry {bitDepth}-bit samples.");
            if (compression != 0)
                throw new InvalidDataException($"PNG declares compression method {compression}; only 0 (zlib) exists.");
            if (filter != 0)
                throw new InvalidDataException($"PNG declares filter method {filter}; only 0 exists.");
            if (interlace > 1)
                throw new InvalidDataException($"PNG declares interlace method {interlace}; only 0 and 1 (Adam7) exist.");
        }

        /// <summary>True when the data opens with the PNG signature.</summary>
        internal static bool HasSignature(byte[] data)
        {
            if (data == null || data.Length < _signature.Length)
                return false;

            for (var i = 0; i < _signature.Length; i++)
                if (data[i] != _signature[i])
                    return false;

            return true;
        }

        /// <summary>Reads one of PNG's big-endian 32-bit integers.</summary>
        private static int ReadInt32(byte[] data, int offset)
        {
            return (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
        }
    }
}
