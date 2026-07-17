// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/17/2026

using System;
using System.IO;

namespace WolfCurses.Graphics.Decoding
{
    /// <summary>
    ///     Decodes GIF images: both versions of the header, global and local colour tables, interlacing, the
    ///     transparent colour index, and every extension block the format can carry.
    ///     <para>
    ///         GIF is the built-in decoder that has to bring its own decompressor. PNG's zlib arrived in the framework
    ///         years ago and JPEG's entropy coding is inseparable from JPEG, but GIF's LZW predates DEFLATE, is used
    ///         by nothing else .NET ships, and is small enough to own outright — so it lives next door in
    ///         <see cref="LzwDecoder" /> and leaves this class doing what is genuinely GIF: a few little-endian
    ///         headers, a colour table or two, and one framing rule applied to everything.
    ///     </para>
    ///     <para>
    ///         <b>An animated GIF decodes to its first frame</b>, by decision rather than omission. Everything
    ///         downstream of a decoder here paints one still picture into a terminal: no type carries a second frame
    ///         and <see cref="AnsiImage" /> has no notion of time, so there is nowhere for the rest to go. The frame a
    ///         file opens with is what a browser shows before its animation starts and what a thumbnailer picks, which
    ///         makes it the answer least likely to surprise. Nothing past that frame is read at all, so the disposal
    ///         methods and delays that only mean something to an animation are stepped over rather than honoured.
    ///     </para>
    ///     <para>
    ///         Instances hold no decode state and may be shared freely across threads, which is what lets a single one
    ///         sit in <see cref="ImageDecoders.Default" /> for the life of the process.
    ///     </para>
    /// </summary>
    /// <seealso cref="BuiltInImageDecoder" />
    public sealed class GifDecoder : IImageDecoder
    {
        /// <summary>"GIF" and a three-character version.</summary>
        private const int SignatureBytes = 6;

        /// <summary>The signature and the logical screen descriptor: everything ahead of the first colour table.</summary>
        private const int HeaderBytes = 13;

        /// <summary>An image descriptor once its introducer is past: position, size, and a packed byte.</summary>
        private const int ImageDescriptorBytes = 9;

        /// <summary>What a graphic control extension has to carry before its transparent index means anything.</summary>
        private const int GraphicControlBytes = 4;

        /// <summary>Announces an extension block. The three introducers are all printable ASCII — '!' here, then
        /// ',' and ';' — which is what a 1987 format looked like when it had to survive being posted through
        /// text-oriented systems.</summary>
        private const byte ExtensionIntroducer = 0x21;

        /// <summary>Announces a frame.</summary>
        private const byte ImageIntroducer = 0x2C;

        /// <summary>Ends the file.</summary>
        private const byte TrailerIntroducer = 0x3B;

        /// <summary>The one extension label that says anything about pixels.</summary>
        private const byte GraphicControlLabel = 0xF9;

        /// <summary>
        ///     GIF's four interlace passes as {yOrigin, yStep}. An interlaced frame's rows arrive in four groups —
        ///     every eighth row, then the eighth rows falling halfway between those, then the quarters, then every
        ///     row still missing — so that a picture coming down a 1987 modem was legible, if coarse, long before it
        ///     had finished arriving. The four partition the rows exactly: every row belongs to one pass, and none to
        ///     two.
        /// </summary>
        private static readonly int[][] _interlacePasses =
        {
            new[] {0, 8},
            new[] {4, 8},
            new[] {2, 4},
            new[] {1, 2}
        };

        /// <summary>A frame stored plainly, top to bottom, expressed as a single pass so both paths share one loop.</summary>
        private static readonly int[][] _singlePass = {new[] {0, 1}};

        /// <summary>
        ///     Initializes a new instance of the <see cref="GifDecoder" /> class.
        /// </summary>
        /// <param name="maxPixels">
        ///     The largest image, in pixels, this decoder will decode. See <see cref="DecoderGuards" /> for why the
        ///     ceiling exists and why it is checked against the header rather than the result.
        /// </param>
        public GifDecoder(int maxPixels = DecoderGuards.DefaultMaxPixels)
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

        /// <summary>Decodes a GIF already held in memory.</summary>
        /// <param name="data">The complete file.</param>
        /// <returns>The decoded image: the file's first frame, placed on the logical screen.</returns>
        internal PixelBuffer DecodeBytes(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (!HasSignature(data))
                throw new InvalidDataException("Data does not begin with a GIF signature.");
            if (data.Length < HeaderBytes)
                throw new InvalidDataException(
                    $"GIF is {data.Length} bytes, too short to hold even a logical screen descriptor.");

            // The logical screen is the canvas every frame is placed on, and the size of the image this returns —
            // not the size of the frame, which is free to be smaller and to sit anywhere within it. GIF is
            // little-endian throughout: a CompuServe format from 1987, laid out for the machines in front of it,
            // where PNG came along later and chose network order.
            var width = ReadUInt16(data, SignatureBytes);
            var height = ReadUInt16(data, SignatureBytes + 2);
            var packed = data[SignatureBytes + 4];

            DecoderGuards.ValidateDimensions("GIF", width, height, MaxPixels);

            // The background colour index names the entry the canvas is filled with where no frame covers it; see
            // FillBackground, which is where the one condition on that lives. The pixel aspect ratio in the byte
            // after it is skipped: it describes the display's pixels rather than the image's, and a PixelBuffer has
            // nowhere to put it.
            var backgroundIndex = data[SignatureBytes + 5];
            var offset = HeaderBytes;

            byte[] globalPalette = null;
            if ((packed & 0x80) != 0)
                globalPalette = ReadColorTable(data, ref offset, 2 << (packed & 0x07));

            // Blocks run one after another, each announced by a single byte, until a frame or the trailer. Only the
            // first frame is wanted, so this walks whatever extensions sit in front of it and stops at the image —
            // keeping the transparent index from the last graphic control extension it passed, since that is the one
            // describing the frame about to arrive.
            var transparentIndex = -1;
            while (offset < data.Length)
            {
                var introducer = data[offset++];
                switch (introducer)
                {
                    case ExtensionIntroducer:
                    {
                        if (offset >= data.Length)
                            throw new InvalidDataException("GIF extension is cut off before its label.");

                        var label = data[offset++];
                        var payload = ReadSubBlocks(data, ref offset);

                        // Every extension there has ever been is framed identically, which is the point of the
                        // design: a decoder that has never heard of a label can still step over it exactly, so a
                        // comment, a plain-text block, and a NETSCAPE2.0 loop count all cost the same nothing here.
                        if (label != GraphicControlLabel)
                            break;

                        if (payload.Length < GraphicControlBytes)
                            throw new InvalidDataException(
                                $"GIF graphic control extension carries {payload.Length} bytes rather than the " +
                                $"{GraphicControlBytes} it must.");

                        // Bit 0 of the packed field decides whether the index three bytes along means anything. The
                        // byte is present either way and is arbitrary when the flag is clear, so it can only be read
                        // through the flag.
                        transparentIndex = (payload[0] & 0x01) != 0 ? payload[3] : -1;
                        break;
                    }

                    case ImageIntroducer:
                        return DecodeFrame(data, ref offset, width, height, globalPalette, backgroundIndex,
                            transparentIndex);

                    case TrailerIntroducer:
                        throw new InvalidDataException("GIF reaches its trailer without carrying an image.");

                    default:
                        throw new InvalidDataException(
                            $"GIF contains a block introduced by 0x{introducer:X2}, which is not an image, an " +
                            "extension, or the trailer.");
                }
            }

            throw new InvalidDataException("GIF ends without an image or a trailer.");
        }

        /// <summary>Decodes the image descriptor at <paramref name="offset" /> and everything hanging off it.</summary>
        private PixelBuffer DecodeFrame(byte[] data, ref int offset, int screenWidth, int screenHeight,
            byte[] globalPalette, int backgroundIndex, int transparentIndex)
        {
            if (offset + ImageDescriptorBytes > data.Length)
                throw new InvalidDataException("GIF image descriptor runs past the end of the file.");

            var left = ReadUInt16(data, offset);
            var top = ReadUInt16(data, offset + 2);
            var frameWidth = ReadUInt16(data, offset + 4);
            var frameHeight = ReadUInt16(data, offset + 6);
            var packed = data[offset + 8];
            offset += ImageDescriptorBytes;

            // The frame gets a check of its own, and it is not the screen's check repeated: it is the frame, not the
            // canvas, that decides how many indices come out of the decompressor and therefore what is allocated to
            // hold them. Nothing stops a file declaring a 65535x65535 frame on a 1x1 screen, and the clipping below
            // would throw all of it away — after paying for it.
            DecoderGuards.ValidateDimensions("GIF frame", frameWidth, frameHeight, MaxPixels);

            // A local colour table replaces the global one for this frame rather than adding to it. Most files carry
            // only a global one; an optimiser that gave each frame its own palette leaves the global one unread, and
            // a file with no global table at all is perfectly legal so long as every frame brings its own.
            var palette = (packed & 0x80) != 0
                ? ReadColorTable(data, ref offset, 2 << (packed & 0x07))
                : globalPalette;
            if (palette == null)
                throw new InvalidDataException("GIF frame has no colour table of its own and the file has no global one.");

            if (offset >= data.Length)
                throw new InvalidDataException("GIF frame ends before its LZW minimum code size.");

            var minCodeSize = data[offset++];
            var codes = ReadSubBlocks(data, ref offset);
            var indices = new byte[frameWidth * frameHeight];
            var count = new LzwDecoder(minCodeSize).Decode(codes, indices);
            var interlaced = (packed & 0x40) != 0;

            return Compose(screenWidth, screenHeight, left, top, frameWidth, frameHeight, interlaced, indices, count,
                palette, globalPalette, backgroundIndex, transparentIndex);
        }

        /// <summary>Paints decoded indices onto the logical screen, in whatever order the frame's passes ask for.</summary>
        private static PixelBuffer Compose(int screenWidth, int screenHeight, int left, int top, int frameWidth,
            int frameHeight, bool interlaced, byte[] indices, int count, byte[] palette, byte[] globalPalette,
            int backgroundIndex, int transparentIndex)
        {
            // A frame is composited onto the canvas rather than being the canvas: it may be smaller than the logical
            // screen and sit anywhere within it, and a pixel holding the transparent index is simply not composited
            // at all. Both leave the canvas showing, which is what FillBackground is deciding the colour of.
            var canvas = new PixelBuffer(screenWidth, screenHeight);
            FillBackground(canvas, globalPalette, backgroundIndex, transparentIndex, left, top, frameWidth,
                frameHeight);

            var paletteCount = palette.Length / 3;

            var passes = interlaced ? _interlacePasses : _singlePass;
            var pass = 0;
            var row = passes[0][0];
            var column = 0;

            // A raster is allowed to disagree with the size its frame declared, in either direction: a short one
            // leaves the rest of the canvas as it found it, and a long one is trimmed. Truncation is the common way
            // for a GIF to be damaged, and half a picture is worth more to something that only wants to display it
            // than an exception is.
            for (var i = 0; i < count && row < frameHeight; i++)
            {
                int index = indices[i];
                if (index != transparentIndex)
                {
                    if (index >= paletteCount)
                        throw new InvalidDataException(
                            $"GIF pixel refers to colour {index}, beyond the {paletteCount} its table holds.");

                    // Clipped rather than refused, the way PixelBuffer.DrawImage treats an overhanging overlay: a
                    // frame reaching past the screen it was declared against is a broken encoder, not a broken file.
                    var x = left + column;
                    var y = top + row;
                    if ((uint) x < (uint) screenWidth && (uint) y < (uint) screenHeight)
                        canvas.SetPixel(x, y, new Rgba32(
                            palette[index * 3], palette[index * 3 + 1], palette[index * 3 + 2], 255));
                }

                if (++column < frameWidth)
                    continue;

                // End of a row: down by the pass's stride, and when that leaves the frame, on to the next pass. A pass
                // whose first row is already past the bottom is empty, which any frame four rows or shorter has at
                // least one of; the same loop steps over those rather than needing a case of its own.
                column = 0;
                row += passes[pass][1];
                while (row >= frameHeight && pass + 1 < passes.Length)
                    row = passes[++pass][0];
            }

            return canvas;
        }

        /// <summary>
        ///     Fills the canvas the frame is about to be composited onto, which is only ever seen where the frame does
        ///     not reach or does not paint.
        ///     <para>
        ///         The logical screen descriptor names an entry for exactly this, and a file with no transparency gets
        ///         it: that is what the specification says the background colour is for, and what ffmpeg and GDI+ both
        ///         do. <b>A file that declares a transparent index gets nothing</b>, and that is the whole of the
        ///         condition. Such a file is meant to be composited over something it cannot see, so the canvas outside
        ///         its frame is the same nothing its transparent pixels are — painting a colour there would leave the
        ///         border around a cropped sticker more opaque than the sticker. ffmpeg and Pillow read it the same
        ///         way. (stb_image is the odd one out and simply has a bug here: it fills the background with the
        ///         palette entry's red and blue channels swapped, and only when the index is not zero.)
        ///     </para>
        /// </summary>
        private static void FillBackground(PixelBuffer canvas, byte[] globalPalette, int backgroundIndex,
            int transparentIndex, int left, int top, int frameWidth, int frameHeight)
        {
            if (transparentIndex >= 0)
                return;

            // The index names an entry of the global table specifically, never a frame's local one, and means nothing
            // without it. One past the end is a decorative field that does not fit rather than corrupt pixel data, so
            // it is dropped rather than thrown over.
            if (globalPalette == null || backgroundIndex >= globalPalette.Length / 3)
                return;

            // Nothing survives under a frame that covers the screen, and with no transparent index there are no holes
            // in it either — so the common case, which is every frame an encoder writes full-size, pays nothing.
            if (left <= 0 && top <= 0 && left + frameWidth >= canvas.Width && top + frameHeight >= canvas.Height)
                return;

            var data = canvas.Data;
            for (var i = 0; i < data.Length; i += PixelBuffer.BytesPerPixel)
            {
                data[i] = globalPalette[backgroundIndex * 3];
                data[i + 1] = globalPalette[backgroundIndex * 3 + 1];
                data[i + 2] = globalPalette[backgroundIndex * 3 + 2];
                data[i + 3] = 255;
            }
        }

        /// <summary>
        ///     Reads a chain of sub-blocks — a length byte, then that many bytes, over and over until a length of
        ///     zero — and hands back the lot concatenated.
        ///     <para>
        ///         For an extension the chain is only framing, and identical framing everywhere is what makes an
        ///         unknown block skippable. For a raster the concatenation is the point: an LZW code may straddle a
        ///         sub-block boundary, so what looks like a sequence of payloads is one bitstream that has been
        ///         chopped into 255-byte pieces to fit a format where no length field is wider than a byte.
        ///     </para>
        ///     <para>
        ///         A chain that runs off the end of the file hands back what did arrive rather than throwing. A GIF
        ///         stops mid-sub-block because it was truncated, which is the ordinary way for one to be damaged, and
        ///         the rest of this decoder is already built to make what it can of a short raster — Tk has shipped a
        ///         demo image in this state for years, and stb, ffmpeg and Pillow all draw it. Refusing it here would
        ///         be this class disagreeing with itself.
        ///     </para>
        /// </summary>
        private static byte[] ReadSubBlocks(byte[] data, ref int offset)
        {
            var buffer = new MemoryStream();
            while (offset < data.Length)
            {
                int length = data[offset++];
                if (length == 0)
                    return buffer.ToArray();

                var take = Math.Min(length, data.Length - offset);
                buffer.Write(data, offset, take);
                offset += take;
            }

            return buffer.ToArray();
        }

        /// <summary>Reads a colour table of the given entry count, three bytes each, red first.</summary>
        private static byte[] ReadColorTable(byte[] data, ref int offset, int entries)
        {
            var bytes = entries * 3;
            if (offset + bytes > data.Length)
                throw new InvalidDataException(
                    $"GIF colour table of {entries} entries runs past the end of the file.");

            var table = new byte[bytes];
            Array.Copy(data, offset, table, 0, bytes);
            offset += bytes;
            return table;
        }

        /// <summary>True when the data opens with a GIF signature.</summary>
        internal static bool HasSignature(byte[] data)
        {
            // "GIF" and then a three-character version. Only two were ever issued, and what separates them is which
            // blocks may appear rather than how any byte is laid out — so this is the last place that has any reason
            // to care which one it read.
            return data != null && data.Length >= SignatureBytes &&
                   data[0] == 'G' && data[1] == 'I' && data[2] == 'F' &&
                   data[3] == '8' && (data[4] == '7' || data[4] == '9') && data[5] == 'a';
        }

        /// <summary>Reads one of GIF's little-endian 16-bit integers.</summary>
        private static int ReadUInt16(byte[] data, int offset)
        {
            return data[offset] | (data[offset + 1] << 8);
        }
    }
}
