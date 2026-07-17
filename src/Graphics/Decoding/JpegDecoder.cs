// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/17/2026

using System;
using System.IO;

namespace WolfCurses.Graphics.Decoding
{
    /// <summary>
    ///     Decodes JPEG images: baseline, extended sequential and progressive, at 8 bits a sample, in greyscale or
    ///     colour, at any chroma subsampling.
    ///     <para>
    ///         This is the expensive decoder to own, and PNG is the reason why. PNG hands its hard half to
    ///         <see cref="System.IO.Compression.ZLibStream" /> and keeps the bookkeeping; JPEG has no such seam
    ///         anywhere in it. What arrives is a Huffman-coded stream of quantised frequency coefficients, and turning
    ///         that back into pixels means every step the encoder took, in reverse and in full — decode the codes,
    ///         dequantise, unpick the zig-zag, invert an 8x8 discrete cosine transform, resample the chroma back up to
    ///         full resolution, convert out of YCbCr — with not one of those steps available from the framework.
    ///     </para>
    ///     <para>
    ///         Progressive JPEG (SOF2) is the reason it is also the largest. A baseline file sends each block once,
    ///         finished; a progressive one sends the whole image a dozen times over, each scan carrying one band of
    ///         coefficients at one bit of precision, so that a picture arriving down a slow line sharpens instead of
    ///         unrolling. Nothing can become a pixel until the last scan lands, which means holding every coefficient
    ///         of the whole image at once rather than one MCU's worth — two bytes a sample, so six bytes a pixel at
    ///         4:4:4, on top of the four the result itself costs. This decoder pays that on baseline files too, rather
    ///         than keep two code paths for one format.
    ///     </para>
    ///     <para>
    ///         Deliberately not implemented, and each rejected by name rather than decoded into nonsense: arithmetic
    ///         coding (SOF9, SOF10, SOF11 and DAC), the lossless and hierarchical modes (SOF3, SOF5, SOF6, SOF7, DHP),
    ///         12-bit samples, and four-component CMYK or YCCK. Between them these are the JPEG standard's long tail —
    ///         the parts of T.81 the world never adopted, plus the print-shop corner that cannot be read at all
    ///         without the Adobe APP14 marker to say which of two incompatible things the four components mean.
    ///     </para>
    ///     <para>
    ///         Instances hold no decode state and may be shared freely across threads, which is what lets a single one
    ///         sit in <see cref="ImageDecoders.Default" /> for the life of the process.
    ///     </para>
    /// </summary>
    /// <seealso cref="BuiltInImageDecoder" />
    public sealed class JpegDecoder : IImageDecoder
    {
        private const int Sof0 = 0xC0; // Baseline sequential.
        private const int Sof1 = 0xC1; // Extended sequential: a wider choice of tables, decoded identically.
        private const int Sof2 = 0xC2; // Progressive.
        private const int Sof3 = 0xC3; // Lossless.
        private const int Dht = 0xC4;
        private const int Sof5 = 0xC5; // Differential sequential.
        private const int Sof6 = 0xC6; // Differential progressive.
        private const int Sof7 = 0xC7; // Differential lossless.
        private const int Sof9 = 0xC9; // Extended sequential, arithmetic.
        private const int Sof10 = 0xCA; // Progressive, arithmetic.
        private const int Sof11 = 0xCB; // Lossless, arithmetic.
        private const int Dac = 0xCC; // Arithmetic coding conditioning.
        private const int Sof13 = 0xCD; // Differential sequential, arithmetic.
        private const int Sof14 = 0xCE; // Differential progressive, arithmetic.
        private const int Sof15 = 0xCF; // Differential lossless, arithmetic.
        private const int Rst0 = 0xD0;
        private const int Rst7 = 0xD7;
        private const int Soi = 0xD8;
        private const int Eoi = 0xD9;
        private const int Sos = 0xDA;
        private const int Dqt = 0xDB;
        private const int Dri = 0xDD;
        private const int Dhp = 0xDE; // Hierarchical progression.
        private const int Exp = 0xDF; // Expand reference component.
        private const int Tem = 0x01; // Temporary, for arithmetic coding. Standalone and meaningless here.

        /// <summary>Fractional bits in the colour conversion constants below.</summary>
        private const int ColorBits = 16;

        private const int ColorHalf = 1 << (ColorBits - 1);

        // The YCbCr to RGB constants, from JFIF, scaled by 2^ColorBits and rounded. The values are the specification's
        // own to five places rather than the exact ones the derivation gives (0.344136 and 0.714136): every decoder in
        // the world rounds where JFIF rounded, and matching them matters more here than the last two decimals do.
        private const int Fix1_40200 = 91881;
        private const int Fix0_34414 = 22553;
        private const int Fix0_71414 = 46802;
        private const int Fix1_77200 = 116130;

        /// <summary>Initializes a new instance of the <see cref="JpegDecoder" /> class.</summary>
        /// <param name="maxPixels">
        ///     The largest image, in pixels, this decoder will decode. See <see cref="DecoderGuards" /> for why the
        ///     ceiling exists and why it is checked against the header rather than the result.
        /// </param>
        public JpegDecoder(int maxPixels = DecoderGuards.DefaultMaxPixels)
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

        /// <summary>Decodes a JPEG already held in memory.</summary>
        /// <param name="data">The complete file.</param>
        /// <returns>The decoded image.</returns>
        internal PixelBuffer DecodeBytes(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (!HasSignature(data))
                throw new InvalidDataException("Data does not begin with a JPEG SOI marker.");

            // Every scrap of decode state lives in this one object, which one call owns and then drops. That is what
            // lets a single instance of this decoder sit in ImageDecoders.Default and serve any number of threads.
            var frame = new JpegFrame();
            var scans = 0;
            var offset = 2;

            while (true)
            {
                var marker = NextMarker(data, ref offset);
                if (marker < 0 || marker == Eoi)
                    break;

                // The standalone markers carry no length and no payload. A stray RSTn only turns up here when a scan
                // left a trailing one behind, which some encoders do and which means nothing.
                if (marker == Soi || marker == Tem || (marker >= Rst0 && marker <= Rst7))
                    continue;

                if (offset + 2 > data.Length)
                    throw new InvalidDataException($"JPEG marker FF{marker:X2} has no room for a segment length.");

                var length = (data[offset] << 8) | data[offset + 1];
                if (length < 2)
                    throw new InvalidDataException(
                        $"JPEG marker FF{marker:X2} declares a segment {length} bytes long; the length counts itself, " +
                        "so 2 is the smallest a segment can be.");

                var start = offset + 2;
                var end = offset + length;
                if (end > data.Length)
                    throw new InvalidDataException(
                        $"JPEG marker FF{marker:X2} declares a segment running {end - data.Length} bytes past the end " +
                        "of the file.");

                switch (marker)
                {
                    case Dqt:
                        ReadQuantTables(frame, data, start, end);
                        break;

                    case Dht:
                        ReadHuffmanTables(frame, data, start, end);
                        break;

                    case Dri:
                        if (end - start < 2)
                            throw new InvalidDataException("JPEG DRI segment is too short to hold a restart interval.");

                        frame.RestartInterval = (data[start] << 8) | data[start + 1];
                        break;

                    case Sof0:
                    case Sof1:
                    case Sof2:
                        ReadFrameHeader(frame, marker == Sof2, data, start, end);
                        break;

                    case Sos:
                        if (frame.Components == null)
                            throw new InvalidDataException(
                                "JPEG has a scan before any frame header, so there is nothing to say what the scan's " +
                                "data describes.");

                        // The scan reads on past its own header, through entropy-coded data that has no length field
                        // and ends only when the next marker turns up, so it is the scan that says where the marker
                        // loop resumes.
                        offset = new JpegScanDecoder(frame, data, start, end).Decode();
                        scans++;
                        continue;

                    default:
                        // APPn, COM and the rest of the metadata are skipped by falling through to the step below.
                        RejectUnsupported(marker);
                        break;
                }

                offset = end;
            }

            if (frame.Components == null)
                throw new InvalidDataException("JPEG has no frame header, so there is nothing to decode.");
            if (scans == 0)
                throw new InvalidDataException("JPEG has a frame header but no scan, so it carries no image data.");

            return BuildImage(frame);
        }

        /// <summary>True when the data opens with a JPEG start-of-image marker followed by the start of another.</summary>
        internal static bool HasSignature(byte[] data)
        {
            // JPEG has no magic number, only a marker: FF D8, start of image. The third byte is worth insisting on
            // because two are a weak enough sniff to fire on files that are not images at all, and every real JPEG
            // follows SOI immediately with another marker, so its FF is free to check.
            return data != null && data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF;
        }

        /// <summary>
        ///     Finds the next marker and steps past it, returning its code, or -1 at the end of the data.
        ///     <para>
        ///         A marker is 0xFF followed by a code that is neither 0x00 (which would make the pair stuffed data)
        ///         nor 0xFF (which is fill). Anything else between segments is padding some encoder left behind, and
        ///         is skipped rather than complained about.
        ///     </para>
        /// </summary>
        private static int NextMarker(byte[] data, ref int offset)
        {
            while (offset + 1 < data.Length)
            {
                if (data[offset] != 0xFF)
                {
                    offset++;
                    continue;
                }

                var code = data[offset + 1];
                if (code == 0x00 || code == 0xFF)
                {
                    offset++;
                    continue;
                }

                offset += 2;
                return code;
            }

            return -1;
        }

        /// <summary>Rejects the markers that mean this file is a kind of JPEG this decoder does not implement.</summary>
        private static void RejectUnsupported(int marker)
        {
            switch (marker)
            {
                case Sof3:
                case Sof5:
                case Sof6:
                case Sof7:
                case Dhp:
                case Exp:
                    throw new InvalidDataException(
                        $"JPEG marker FF{marker:X2} selects a lossless or hierarchical mode, which this decoder does " +
                        "not implement. Neither is really the same format: lossless JPEG codes predictions of pixels " +
                        "rather than transform coefficients, and hierarchical JPEG stacks whole frames into a " +
                        "resolution pyramid.");

                case Sof9:
                case Sof10:
                case Sof11:
                case Sof13:
                case Sof14:
                case Sof15:
                case Dac:
                    throw new InvalidDataException(
                        $"JPEG marker FF{marker:X2} selects arithmetic coding, which this decoder does not implement. " +
                        "It is a different entropy coder rather than a variation on the Huffman tables, and it is rare " +
                        "enough that the code to read it would be code nothing ever exercised.");
            }
        }

        /// <summary>Reads a SOF segment: the image's size, and the components it is made of.</summary>
        private void ReadFrameHeader(JpegFrame frame, bool progressive, byte[] data, int start, int end)
        {
            if (frame.Components != null)
                throw new InvalidDataException(
                    "JPEG carries a second frame header. Only hierarchical JPEG has more than one frame, and this " +
                    "decoder does not implement it.");
            if (end - start < 6)
                throw new InvalidDataException("JPEG frame header is too short to hold an image size.");

            var precision = data[start];
            var height = (data[start + 1] << 8) | data[start + 2];
            var width = (data[start + 3] << 8) | data[start + 4];
            var count = data[start + 5];

            if (precision != 8)
                throw new InvalidDataException(
                    $"JPEG declares {precision}-bit samples, which this decoder does not implement. 12-bit JPEG " +
                    "exists and turns up in medical and scientific imaging, but nothing that produces a photograph " +
                    "writes it.");

            if (height == 0)
                throw new InvalidDataException(
                    "JPEG declares a height of zero, which means the real height arrives in a DNL marker after the " +
                    "first scan. This decoder does not implement DNL: it exists for encoders that were streaming out " +
                    "a picture they had not finished measuring, and essentially nothing writes it.");

            // Checked before a single coefficient is allocated. Everything below is sized from these two numbers and
            // nothing else, which is what makes this a bound rather than a hint.
            DecoderGuards.ValidateDimensions("JPEG", width, height, MaxPixels);

            if (count == 4)
                throw new InvalidDataException(
                    "JPEG declares four components, which makes it CMYK or YCCK — a print-oriented colour model this " +
                    "decoder does not implement. Which of the two it is cannot be told from the frame at all: it is in " +
                    "the Adobe APP14 marker, along with an inversion convention that only Adobe's own encoder explains.");
            if (count != 1 && count != 3)
                throw new InvalidDataException(
                    $"JPEG declares {count} components. This decoder reads 1 (greyscale) and 3 (colour).");
            if (end - start < 6 + count * 3)
                throw new InvalidDataException("JPEG frame header is shorter than the components it declares.");

            var components = new JpegComponent[count];
            for (var i = 0; i < count; i++)
            {
                var p = start + 6 + i * 3;
                var id = data[p];
                var factors = data[p + 1];
                var horizontal = factors >> 4;
                var vertical = factors & 15;
                var quantTable = data[p + 2];

                if (horizontal < 1 || horizontal > 4 || vertical < 1 || vertical > 4)
                    throw new InvalidDataException(
                        $"JPEG component {id} declares sampling factors {horizontal}x{vertical}; the format allows 1 " +
                        "to 4 in each direction.");
                if (quantTable > 3)
                    throw new InvalidDataException(
                        $"JPEG component {id} is quantised with table {quantTable}; only tables 0 to 3 exist.");

                components[i] = new JpegComponent(id, horizontal, vertical, quantTable);
            }

            frame.Width = width;
            frame.Height = height;
            frame.Progressive = progressive;
            frame.Components = components;
            frame.AllocateCoefficients();
        }

        /// <summary>Reads a DQT segment, which may define any number of tables back to back.</summary>
        private static void ReadQuantTables(JpegFrame frame, byte[] data, int start, int end)
        {
            var p = start;
            while (p < end)
            {
                var header = data[p++];
                var precision = header >> 4;
                var id = header & 15;

                if (id > 3)
                    throw new InvalidDataException(
                        $"JPEG quantisation table is numbered {id}; only tables 0 to 3 exist.");
                if (precision > 1)
                    throw new InvalidDataException(
                        $"JPEG quantisation table {id} declares element precision {precision}; only 0 (8-bit) and 1 " +
                        "(16-bit) exist.");

                // 16-bit tables are only legal alongside 12-bit samples, which the frame header rejects anyway. They
                // are read rather than refused here because reading them costs nothing and DQT may well arrive before
                // the frame header that would have the standing to object.
                var size = precision == 0 ? 1 : 2;
                if (p + 64 * size > end)
                    throw new InvalidDataException(
                        $"JPEG quantisation table {id} is cut short by the end of its segment.");

                // Kept exactly as it arrives, in zig-zag order, because that is the order the coefficients are kept in
                // too: the two then line up index for index, and only the inverse transform ever needs either unpicked.
                var table = new int[64];
                for (var i = 0; i < 64; i++)
                {
                    table[i] = precision == 0 ? data[p] : (data[p] << 8) | data[p + 1];
                    p += size;
                }

                frame.QuantTables[id] = table;
            }
        }

        /// <summary>Reads a DHT segment, which may define any number of tables back to back.</summary>
        private static void ReadHuffmanTables(JpegFrame frame, byte[] data, int start, int end)
        {
            var p = start;
            while (p < end)
            {
                var header = data[p++];
                var tableClass = header >> 4;
                var id = header & 15;

                if (id > 3)
                    throw new InvalidDataException($"JPEG Huffman table is numbered {id}; only tables 0 to 3 exist.");
                if (tableClass > 1)
                    throw new InvalidDataException(
                        $"JPEG Huffman table {id} declares class {tableClass}; only 0 (DC) and 1 (AC) exist.");
                if (p + 16 > end)
                    throw new InvalidDataException($"JPEG Huffman table {id} is cut short by the end of its segment.");

                var counts = new int[17];
                var total = 0;
                for (var length = 1; length <= 16; length++)
                {
                    counts[length] = data[p++];
                    total += counts[length];
                }

                if (p + total > end)
                    throw new InvalidDataException(
                        $"JPEG Huffman table {id} declares {total} codes but its segment has room for {end - p}.");

                var values = new byte[total];
                Array.Copy(data, p, values, 0, total);
                p += total;

                if (tableClass == 0)
                    frame.DcTables[id] = new JpegHuffmanTable(counts, values);
                else
                    frame.AcTables[id] = new JpegHuffmanTable(counts, values);
            }
        }

        /// <summary>Turns the finished coefficients into pixels: transform, resample, convert colour.</summary>
        private static PixelBuffer BuildImage(JpegFrame frame)
        {
            var planes = new JpegPlane[frame.Components.Length];
            for (var i = 0; i < planes.Length; i++)
                planes[i] = new JpegPlane(frame, frame.Components[i]);

            var result = new PixelBuffer(frame.Width, frame.Height);
            var pixels = result.Data;
            var offset = 0;

            // JPEG has no alpha and no way to express one, so every pixel is opaque. There is a convention for faking
            // it with a second file, and it is not this decoder's business.
            if (planes.Length == 1)
            {
                for (var y = 0; y < frame.Height; y++)
                for (var x = 0; x < frame.Width; x++)
                {
                    var gray = (byte) planes[0].Sample(x, y);
                    pixels[offset] = gray;
                    pixels[offset + 1] = gray;
                    pixels[offset + 2] = gray;
                    pixels[offset + 3] = 255;
                    offset += 4;
                }

                return result;
            }

            var transform = !HasRgbComponentIds(frame);
            for (var y = 0; y < frame.Height; y++)
            for (var x = 0; x < frame.Width; x++)
            {
                var first = planes[0].Sample(x, y);
                var second = planes[1].Sample(x, y);
                var third = planes[2].Sample(x, y);

                if (transform)
                {
                    // Both chroma channels are stored biased by 128 so they can be unsigned, which is why every term
                    // here is a difference from it rather than the sample itself.
                    var blue = second - 128;
                    var red = third - 128;
                    pixels[offset] = ClampToByte(first + ((Fix1_40200 * red + ColorHalf) >> ColorBits));
                    pixels[offset + 1] = ClampToByte(first +
                                                     ((-Fix0_34414 * blue - Fix0_71414 * red + ColorHalf) >> ColorBits));
                    pixels[offset + 2] = ClampToByte(first + ((Fix1_77200 * blue + ColorHalf) >> ColorBits));
                }
                else
                {
                    pixels[offset] = (byte) first;
                    pixels[offset + 1] = (byte) second;
                    pixels[offset + 2] = (byte) third;
                }

                pixels[offset + 3] = 255;
                offset += 4;
            }

            return result;
        }

        /// <summary>
        ///     True when a colour frame's components are labelled 'R', 'G' and 'B' rather than 1, 2 and 3.
        ///     <para>
        ///         A three-component JPEG is YCbCr essentially always, and nothing in the frame says so — what carries
        ///         it is the JFIF or Adobe APP segment, out of band, and this decoder skips both. What it does read is
        ///         the component identifiers, and an encoder writing untransformed RGB labels them with those three
        ///         ASCII letters. It is a convention rather than a rule, but it is the one libjpeg has decided by for
        ///         thirty years, and the alternative to honouring it is decoding such a file to lurid nonsense.
        ///     </para>
        /// </summary>
        private static bool HasRgbComponentIds(JpegFrame frame)
        {
            var components = frame.Components;
            return components[0].Id == 'R' && components[1].Id == 'G' && components[2].Id == 'B';
        }

        /// <summary>Clamps a converted channel into the byte range. Saturated colours land outside it routinely.</summary>
        private static byte ClampToByte(int value)
        {
            if (value < 0) return 0;
            if (value > 255) return 255;
            return (byte) value;
        }
    }
}
