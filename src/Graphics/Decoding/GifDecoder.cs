// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/17/2026

using System;
using System.Collections.Generic;
using System.IO;

namespace WolfCurses.Graphics.Decoding
{
    /// <summary>
    ///     Decodes GIF images: both versions of the header, global and local colour tables, interlacing, the
    ///     transparent colour index, every extension block the format can carry, and animation.
    ///     <para>
    ///         GIF is the built-in decoder that has to bring its own decompressor. PNG's zlib arrived in the framework
    ///         years ago and JPEG's entropy coding is inseparable from JPEG, but GIF's LZW predates DEFLATE, is used
    ///         by nothing else .NET ships, and is small enough to own outright — so it lives next door in
    ///         <see cref="LzwDecoder" /> and leaves this class doing what is genuinely GIF: a few little-endian
    ///         headers, a colour table or two, and one framing rule applied to everything.
    ///     </para>
    ///     <para>
    ///         <b>There are two ways in, and the difference is time.</b> <see cref="Decode" /> is the
    ///         <see cref="IImageDecoder" /> seam and answers with one still picture — the first frame — because that is
    ///         all the seam's return type can hold and all the rest of this library wants: an <see cref="AnsiImage" />
    ///         has no notion of time. <see cref="DecodeFrames(Stream)" /> hands back every frame with its delay, for a
    ///         caller that does. The first frame is what a browser shows before an animation starts and what a
    ///         thumbnailer picks, so the still answer stays the least surprising one.
    ///     </para>
    ///     <para>
    ///         Instances hold no decode state and may be shared freely across threads, which is what lets a single one
    ///         sit in <see cref="ImageDecoders.Default" /> for the life of the process. All the state an animation needs
    ///         lives in the walk itself, so two threads may decode different files through the same decoder at once.
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

        /// <summary>Leave the frame where it is and draw the next one over the top. Disposal method 0 and 1 both.</summary>
        private const int DisposalKeep = 0;

        /// <summary>Take the frame's rectangle back to the canvas before drawing the next one. Disposal method 2.</summary>
        private const int DisposalRestoreBackground = 2;

        /// <summary>Put back whatever the frame covered up. Disposal method 3.</summary>
        private const int DisposalRestorePrevious = 3;

        /// <summary>GIF counts delays in hundredths of a second.</summary>
        private const int MillisecondsPerDelayUnit = 10;

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

        /// <summary>
        ///     Decodes every frame of a GIF, in order, each one composited onto the logical screen and carrying the
        ///     delay the file gives it. A still GIF is simply an animation of one frame, so this never fails for
        ///     lack of animation.
        ///     <para>
        ///         <b>Frames arrive lazily, one at a time, and this matters.</b> Each frame is the whole logical screen
        ///         (see <see cref="GifFrame.Image" /> for why it must be), which for the 540x540, 91-frame
        ///         <c>media/animated.gif</c> in this repository comes to about 106 MB if every frame is held at once —
        ///         for a file of under 4 MB. Enumerated one at a time and turned into something smaller as they come,
        ///         which is what any caller here is going to do with them, the same file costs about two frames of
        ///         memory. Calling <c>ToList()</c> is a choice to pay the 106 MB, and an available one.
        ///     </para>
        ///     <para>
        ///         Bad data is reported from this call, not from the enumeration that follows it, so a caller cannot get
        ///         an exception out of a <c>foreach</c> running long after the stream it came from was closed. Damage
        ///         discovered mid-walk still surfaces from the enumeration, where the frames before it have already been
        ///         handed over — a truncated GIF yields what arrived.
        ///     </para>
        /// </summary>
        /// <param name="source">The stream to read the complete file from.</param>
        /// <returns>The frames, in the order they are shown.</returns>
        /// <exception cref="InvalidDataException">The data is not a GIF, or carries no image at all.</exception>
        public IEnumerable<GifFrame> DecodeFrames(Stream source)
        {
            return DecodeFramesBytes(DecoderGuards.ReadAll(source));
        }

        /// <summary>Decodes every frame of a GIF already held in memory.</summary>
        /// <param name="data">The complete file.</param>
        /// <returns>The frames, in the order they are shown.</returns>
        internal IEnumerable<GifFrame> DecodeFramesBytes(byte[] data)
        {
            // Eagerly, ahead of the iterator: this method is not itself an iterator, so everything here runs when it is
            // called rather than when the result is first enumerated. That is the point of the split. A caller who
            // hands over bytes that are not a GIF should hear about it from the call that took them.
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (!HasSignature(data))
                throw new InvalidDataException("Data does not begin with a GIF signature.");
            if (data.Length < HeaderBytes)
                throw new InvalidDataException(
                    $"GIF is {data.Length} bytes, too short to hold even a logical screen descriptor.");

            DecoderGuards.ValidateDimensions("GIF", ReadUInt16(data, SignatureBytes),
                ReadUInt16(data, SignatureBytes + 2), MaxPixels);

            return Frames(data);
        }

        /// <summary>Decodes a GIF already held in memory.</summary>
        /// <param name="data">The complete file.</param>
        /// <returns>The decoded image: the file's first frame, placed on the logical screen.</returns>
        internal PixelBuffer DecodeBytes(byte[] data)
        {
            // Reading one frame out of the walk is all it costs to read one frame: the iterator is abandoned the moment
            // this returns, so nothing past the first frame is decompressed, composited, or even looked at. Which is
            // the whole of what this method has ever promised.
            foreach (var frame in DecodeFramesBytes(data))
                return frame.Image;

            // Unreachable: a walk that finds no frame throws rather than ending quietly. Here because the compiler
            // cannot know that, and because if it ever becomes reachable, an exception beats a null.
            throw new InvalidDataException("GIF carries no image.");
        }

        /// <summary>
        ///     Walks the file's blocks, maintaining the canvas that the frames are composited onto, and yields the
        ///     canvas as it stands after each frame is drawn.
        ///     <para>
        ///         The canvas persisting across frames <b>is</b> the animation. A GIF's second and later frames are
        ///         differences: a rectangle around what changed, with everything that did not change left transparent so
        ///         the previous frame shows through. What each frame's disposal method then says is what to do with that
        ///         rectangle once the frame's time is up, and it is the one part of the format that only means anything
        ///         to something drawing the frames in order — which is why it is stepped over entirely by
        ///         <see cref="DecodeBytes" /> and honoured here.
        ///     </para>
        /// </summary>
        private IEnumerable<GifFrame> Frames(byte[] data)
        {
            // The logical screen is the canvas every frame is placed on, and the size of every image this yields — not
            // the size of any frame, which is free to be smaller and to sit anywhere within it. GIF is little-endian
            // throughout: a CompuServe format from 1987, laid out for the machines in front of it, where PNG came along
            // later and chose network order.
            var screenWidth = ReadUInt16(data, SignatureBytes);
            var screenHeight = ReadUInt16(data, SignatureBytes + 2);
            var packed = data[SignatureBytes + 4];

            // The background colour index names the entry the canvas is filled with where no frame covers it; see
            // BackgroundColor, which is where the one condition on that lives. The pixel aspect ratio in the byte after
            // it is skipped: it describes the display's pixels rather than the image's, and a PixelBuffer has nowhere
            // to put it.
            var backgroundIndex = data[SignatureBytes + 5];
            var offset = HeaderBytes;

            byte[] globalPalette = null;
            if ((packed & 0x80) != 0)
                globalPalette = ReadColorTable(data, ref offset, 2 << (packed & 0x07));

            var canvas = new PixelBuffer(screenWidth, screenHeight);
            byte[] restorePoint = null;
            var painted = false;

            // A graphic control extension describes the single frame that follows it, so these are picked up as one is
            // passed and spent when the frame lands. A frame with no extension in front of it gets these defaults,
            // which is what every GIF written before 89a is.
            var transparentIndex = -1;
            var disposal = DisposalKeep;
            var delay = 0;

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
                        // (The loop count is genuinely skipped, not merely unread: both this repository's animated
                        // fixtures ask to loop forever, which is what any caller with a loop of its own does anyway.)
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
                        disposal = (payload[0] >> 2) & 0x07;
                        delay = payload[1] | (payload[2] << 8);
                        break;
                    }

                    case ImageIntroducer:
                    {
                        if (offset + ImageDescriptorBytes > data.Length)
                            throw new InvalidDataException("GIF image descriptor runs past the end of the file.");

                        var left = ReadUInt16(data, offset);
                        var top = ReadUInt16(data, offset + 2);
                        var frameWidth = ReadUInt16(data, offset + 4);
                        var frameHeight = ReadUInt16(data, offset + 6);
                        var framePacked = data[offset + 8];
                        offset += ImageDescriptorBytes;

                        // The frame gets a check of its own, and it is not the screen's check repeated: it is the
                        // frame, not the canvas, that decides how many indices come out of the decompressor and
                        // therefore what is allocated to hold them. Nothing stops a file declaring a 65535x65535 frame
                        // on a 1x1 screen, and the clipping in Paint would throw all of it away — after paying for it.
                        DecoderGuards.ValidateDimensions("GIF frame", frameWidth, frameHeight, MaxPixels);

                        // A local colour table replaces the global one for this frame rather than adding to it. Most
                        // files carry only a global one; an optimiser that gave each frame its own palette leaves the
                        // global one unread, and a file with no global table at all is perfectly legal so long as every
                        // frame brings its own.
                        var palette = (framePacked & 0x80) != 0
                            ? ReadColorTable(data, ref offset, 2 << (framePacked & 0x07))
                            : globalPalette;
                        if (palette == null)
                            throw new InvalidDataException(
                                "GIF frame has no colour table of its own and the file has no global one.");

                        if (offset >= data.Length)
                            throw new InvalidDataException("GIF frame ends before its LZW minimum code size.");

                        var minCodeSize = data[offset++];
                        var codes = ReadSubBlocks(data, ref offset);
                        var indices = new byte[frameWidth * frameHeight];
                        var count = new LzwDecoder(minCodeSize).Decode(codes, indices);

                        // Once, and from the first frame's point of view, because that is the only frame the canvas is
                        // ever blank behind. Every frame after it is composited over whatever the last one left.
                        if (!painted)
                            FillBackground(canvas, globalPalette, backgroundIndex, transparentIndex, left, top,
                                frameWidth, frameHeight);

                        // A frame asking to be undone afterwards has to be photographed before it is drawn, since
                        // drawing it is what destroys the thing being kept.
                        if (disposal == DisposalRestorePrevious)
                            restorePoint = (byte[]) canvas.Data.Clone();

                        Paint(canvas, left, top, frameWidth, frameHeight, (framePacked & 0x40) != 0, indices, count,
                            palette, transparentIndex);
                        painted = true;

                        // A copy, not the canvas: the next frame is about to be drawn onto it, and a caller holding
                        // what it was handed would watch it change underneath. This is the allocation the laziness
                        // documented on DecodeFrames exists to keep down to one or two at a time.
                        yield return new GifFrame(
                            new PixelBuffer(screenWidth, screenHeight, (byte[]) canvas.Data.Clone()),
                            TimeSpan.FromMilliseconds((long) delay * MillisecondsPerDelayUnit));

                        // Disposal happens after the frame has had its time, which is now, and prepares the canvas the
                        // next frame will be composited onto. Methods 0 and 1 both mean "leave it", which is what the
                        // canvas already holds, so the overwhelmingly common case does nothing at all.
                        switch (disposal)
                        {
                            case DisposalRestoreBackground:
                                ClearRectangle(canvas, globalPalette, backgroundIndex, transparentIndex, left, top,
                                    frameWidth, frameHeight);
                                break;

                            // Only ever null when a file declares this on a frame it cannot apply to, which the copy
                            // above makes impossible; guarded because a decoder does not get to assume a file is sane.
                            case DisposalRestorePrevious when restorePoint != null:
                                Array.Copy(restorePoint, canvas.Data, restorePoint.Length);
                                break;
                        }

                        transparentIndex = -1;
                        disposal = DisposalKeep;
                        delay = 0;
                        break;
                    }

                    case TrailerIntroducer:
                        if (!painted)
                            throw new InvalidDataException("GIF reaches its trailer without carrying an image.");

                        yield break;

                    default:
                        throw new InvalidDataException(
                            $"GIF contains a block introduced by 0x{introducer:X2}, which is not an image, an " +
                            "extension, or the trailer.");
                }
            }

            // Running out of file is only an error when nothing was found in it. A GIF that stops after some frames was
            // truncated, and the frames that did arrive have already been handed over — refusing at this point would
            // mean taking them back.
            if (!painted)
                throw new InvalidDataException("GIF ends without an image or a trailer.");
        }

        /// <summary>Paints decoded indices onto the canvas, in whatever order the frame's passes ask for.</summary>
        private static void Paint(PixelBuffer canvas, int left, int top, int frameWidth, int frameHeight,
            bool interlaced, byte[] indices, int count, byte[] palette, int transparentIndex)
        {
            // A frame is composited onto the canvas rather than being the canvas: it may be smaller than the logical
            // screen and sit anywhere within it, and a pixel holding the transparent index is simply not composited at
            // all — which in an animation is how a frame says "whatever was here is still right".
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
                    if ((uint) x < (uint) canvas.Width && (uint) y < (uint) canvas.Height)
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
        }

        /// <summary>
        ///     Fills the canvas the first frame is about to be composited onto, which is only ever seen where that frame
        ///     does not reach or does not paint.
        /// </summary>
        private static void FillBackground(PixelBuffer canvas, byte[] globalPalette, int backgroundIndex,
            int transparentIndex, int left, int top, int frameWidth, int frameHeight)
        {
            // Nothing survives under a frame that covers the screen, and a frame with no transparent index has no holes
            // in it either — so the common case, which is every first frame an encoder writes, pays nothing.
            if (transparentIndex < 0 && left <= 0 && top <= 0 && left + frameWidth >= canvas.Width &&
                top + frameHeight >= canvas.Height)
                return;

            var fill = BackgroundColor(globalPalette, backgroundIndex, transparentIndex);
            if (fill.A == 0)
                return; // a new PixelBuffer is already transparent black

            var data = canvas.Data;
            for (var i = 0; i < data.Length; i += PixelBuffer.BytesPerPixel)
            {
                data[i] = fill.R;
                data[i + 1] = fill.G;
                data[i + 2] = fill.B;
                data[i + 3] = fill.A;
            }
        }

        /// <summary>
        ///     Takes one frame's rectangle back to the canvas colour, for disposal method 2, leaving the rest of the
        ///     screen as it was.
        /// </summary>
        private static void ClearRectangle(PixelBuffer canvas, byte[] globalPalette, int backgroundIndex,
            int transparentIndex, int left, int top, int frameWidth, int frameHeight)
        {
            var fill = BackgroundColor(globalPalette, backgroundIndex, transparentIndex);

            var right = Math.Min(left + frameWidth, canvas.Width);
            var bottom = Math.Min(top + frameHeight, canvas.Height);
            for (var y = Math.Max(top, 0); y < bottom; y++)
            for (var x = Math.Max(left, 0); x < right; x++)
                canvas.SetPixel(x, y, fill);
        }

        /// <summary>
        ///     The colour the canvas shows where no frame is covering it.
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
        ///     <para>
        ///         The same rule decides what disposal method 2 clears a frame's rectangle to, which is what keeps this
        ///         class agreeing with itself across a whole animation rather than only on its first frame. It also
        ///         happens to be what browsers do: every animation worth the name declares transparency, so in practice
        ///         method 2 nearly always means "back to nothing".
        ///     </para>
        /// </summary>
        private static Rgba32 BackgroundColor(byte[] globalPalette, int backgroundIndex, int transparentIndex)
        {
            if (transparentIndex >= 0)
                return new Rgba32(0, 0, 0, 0);

            // The index names an entry of the global table specifically, never a frame's local one, and means nothing
            // without it. One past the end is a decorative field that does not fit rather than corrupt pixel data, so
            // it is dropped rather than thrown over.
            if (globalPalette == null || backgroundIndex >= globalPalette.Length / 3)
                return new Rgba32(0, 0, 0, 0);

            return new Rgba32(globalPalette[backgroundIndex * 3], globalPalette[backgroundIndex * 3 + 1],
                globalPalette[backgroundIndex * 3 + 2], 255);
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
