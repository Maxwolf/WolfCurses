// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/17/2026

using System;
using System.IO;

namespace WolfCurses.Graphics.Decoding
{
    /// <summary>
    ///     The LZW variant a GIF raster is compressed with, unpacked back into palette indices.
    ///     <para>
    ///         The dictionary opens holding nothing but the palette — one code per colour, each standing for itself —
    ///         and grows by one entry per code read: the string just decoded, plus the first byte of the next one. The
    ///         encoder built the same table from the same bytes and never sent a word of it, which is the whole trick,
    ///         and also the whole difficulty. The decoder is always exactly one entry behind, so an encoder is allowed
    ///         to use a code the instant it defines it and the decoder has to recognise a code it has not written yet.
    ///     </para>
    ///     <para>
    ///         One of these is built per raster and thrown away. It is the per-decode state <see cref="GifDecoder" />
    ///         is careful not to hold, which is what leaves that class safe to share between threads.
    ///     </para>
    /// </summary>
    /// <seealso cref="GifDecoder" />
    internal sealed class LzwDecoder
    {
        /// <summary>
        ///     The dictionary's hard ceiling. Codes reach twelve bits and stop, so entry 4095 is the last one anything
        ///     can name and a 4096th would be unreachable.
        /// </summary>
        private const int MaxCodes = 4096;

        /// <summary>The widest a code ever gets, and the reason the dictionary has an end at all.</summary>
        private const int MaxCodeSize = 12;

        /// <summary>
        ///     Which code each entry carries on from, or -1 for the palette literals that start a string. Every
        ///     prefix necessarily names a lower-numbered entry than the one holding it, since an entry can only be
        ///     built from codes that already existed when it was added.
        /// </summary>
        private readonly short[] _prefix = new short[MaxCodes];

        /// <summary>The byte each entry adds to the end of the string its prefix names.</summary>
        private readonly byte[] _suffix = new byte[MaxCodes];

        /// <summary>
        ///     The byte each entry's string starts with, carried forward rather than re-walked. It is wanted once per
        ///     code — the new entry's suffix is the first byte of whatever came next — and walking a chain to find it
        ///     would turn decoding into quadratic work.
        /// </summary>
        private readonly byte[] _first = new byte[MaxCodes];

        /// <summary>
        ///     Scratch the run for one code is unwound into. A chain steps down by at least one entry each time, so no
        ///     string can be longer than the dictionary has slots and this cannot be too small.
        /// </summary>
        private readonly byte[] _run = new byte[MaxCodes];

        /// <summary>The code meaning "forget everything learned and start again".</summary>
        private readonly int _clearCode;

        /// <summary>The code meaning the raster is over. One past <see cref="_clearCode" />, always.</summary>
        private readonly int _endCode;

        /// <summary>Bits per palette index, as the raster declared it.</summary>
        private readonly int _minCodeSize;

        /// <summary>
        ///     Initializes a new instance of the <see cref="LzwDecoder" /> class for one raster's code size.
        /// </summary>
        /// <param name="minCodeSize">The byte the raster opens with: how many bits one palette index takes.</param>
        internal LzwDecoder(int minCodeSize)
        {
            // Two to eight, and neither end is arbitrary. A GIF colour table holds at most 256 entries, so nine bits
            // of literal would name colours that cannot exist. And below two the first free dictionary slot is
            // already past what the opening code width can address — which is why the format forbids it outright and
            // makes even a two-colour image pay for a bit it has no use for.
            if (minCodeSize < 2 || minCodeSize > 8)
                throw new InvalidDataException(
                    $"GIF raster declares an LZW minimum code size of {minCodeSize}, outside the 2 to 8 the format " +
                    "allows.");

            _minCodeSize = minCodeSize;
            _clearCode = 1 << minCodeSize;
            _endCode = _clearCode + 1;

            // The table starts knowing only the palette: code 0 is colour 0, and so on up. Everything above the two
            // control codes is learned from the data and nothing else.
            for (var i = 0; i < _clearCode; i++)
            {
                _prefix[i] = -1;
                _suffix[i] = (byte) i;
                _first[i] = (byte) i;
            }
        }

        /// <summary>
        ///     Expands one raster's codes into palette indices.
        /// </summary>
        /// <param name="data">The raster's sub-blocks, concatenated back into the single bitstream they always were.</param>
        /// <param name="destination">
        ///     Receives the indices. Its length is the frame's pixel count and doubles as the ceiling on how much a
        ///     stream is allowed to produce.
        /// </param>
        /// <returns>How many indices the stream actually yielded, which need not be the whole frame.</returns>
        internal int Decode(byte[] data, byte[] destination)
        {
            var codeSize = _minCodeSize + 1;
            var available = _endCode + 1;
            var previous = -1;
            var written = 0;
            var read = 0;
            var bits = 0;
            var bitCount = 0;

            // Nothing here can spin. Every pass consumes at least three bits from a buffer that does not grow, the
            // reader gives up the moment it runs dry, and the dictionary has a ceiling — so the worst a crafted
            // stream can do is decode itself and stop, even if it is nothing but clear codes.
            while (written < destination.Length)
            {
                // GIF packs codes least-significant-bit first and lets them straddle bytes freely, so bytes pile in
                // at the top of the window and codes come off the bottom.
                while (bitCount < codeSize)
                {
                    if (read >= data.Length)
                        return written;

                    bits |= data[read++] << bitCount;
                    bitCount += 8;
                }

                var code = bits & ((1 << codeSize) - 1);
                bits >>= codeSize;
                bitCount -= codeSize;

                if (code == _clearCode)
                {
                    codeSize = _minCodeSize + 1;
                    available = _endCode + 1;
                    previous = -1;
                    continue;
                }

                if (code == _endCode)
                    return written;

                if (previous < 0)
                {
                    // Nothing has been decoded since the last clear, so there is no string to extend and the table
                    // holds only the palette: whatever arrives first can be a colour and nothing else.
                    if (code >= _clearCode)
                        throw new InvalidDataException(
                            $"GIF raster opens with code {code}, which names a dictionary entry that does not exist " +
                            "yet.");

                    written = Emit(code, destination, written);
                    previous = code;
                    continue;
                }

                if (code > available)
                    throw new InvalidDataException(
                        $"GIF raster refers to code {code} when the dictionary has only reached {available}.");

                // A full dictionary stops learning rather than clearing itself. Encoders that never send a clear are
                // relying on exactly this, and the alternative — resetting unbidden — would silently decode the rest
                // of the file into noise, since the encoder would still be using the old table.
                if (available < MaxCodes)
                {
                    // The one case where a code names the entry being defined by that same code: the encoder found a
                    // string, added it, and used it in the same breath. Such a string is always the previous one
                    // followed by its own first byte, which is what the entry below says — so defining it before
                    // expanding the code is the whole of the fix.
                    _prefix[available] = (short) previous;
                    _first[available] = _first[previous];
                    _suffix[available] = code < available ? _first[code] : _first[previous];
                    available++;

                    // The width grows the moment the next free slot needs another bit to name it, and stops at
                    // twelve. This has to happen on the same code the encoder did it on, or every code after is read
                    // at the wrong width and the raster dissolves.
                    if (available == 1 << codeSize && codeSize < MaxCodeSize)
                        codeSize++;
                }

                written = Emit(code, destination, written);
                previous = code;
            }

            return written;
        }

        /// <summary>
        ///     Appends the string a code stands for to the output, stopping at the frame's last pixel.
        ///     <para>
        ///         An entry is a byte and a pointer at the entry one shorter, so a string can only be read backwards.
        ///         It is unwound into the tail of a fixed buffer and copied out forwards, which also keeps a hostile
        ///         chain from recursing: the walk is a loop with a hard bound rather than a stack that can be aimed at.
        ///     </para>
        /// </summary>
        private int Emit(int code, byte[] destination, int written)
        {
            var length = 0;
            var walk = code;
            while (walk >= 0)
            {
                if (length >= _run.Length)
                    throw new InvalidDataException(
                        $"GIF dictionary entry {code} expands to more than the {_run.Length} bytes any entry can " +
                        "reach; the raster is corrupt.");

                _run[_run.Length - 1 - length] = _suffix[walk];
                length++;
                walk = _prefix[walk];
            }

            // A run that overruns the frame is trimmed rather than refused. See GifDecoder.Compose for why a raster
            // is allowed to disagree with the size its own frame declared.
            var take = Math.Min(length, destination.Length - written);
            Array.Copy(_run, _run.Length - length, destination, written, take);
            return written + take;
        }
    }
}
