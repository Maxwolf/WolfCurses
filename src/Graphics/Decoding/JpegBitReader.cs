// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/17/2026

namespace WolfCurses.Graphics.Decoding
{
    /// <summary>
    ///     Reads the bit stream inside a JPEG scan, one Huffman code or one magnitude field at a time.
    ///     <para>
    ///         Entropy-coded data is the only part of a JPEG that is not byte-aligned, and the only part that cannot
    ///         contain a raw 0xFF. Markers have to stay findable by scanning for that byte, so an encoder needing to
    ///         emit 0xFF as data follows it with a 0x00 that means nothing and is not part of the picture. Undoing
    ///         that stuffing is this class's real job; the bit buffer around it is the easy half.
    ///     </para>
    ///     <para>
    ///         Reaching the end of the data, or the marker that ends the scan, starves the reader: it feeds zeroes
    ///         from then on rather than throwing. That is deliberate, and it is what lets a truncated file decode to
    ///         a partial picture instead of an exception. It is also safe, because nothing here decides when to stop
    ///         — every caller is bounded by the block and MCU counts the frame header declared, so a starved reader
    ///         cannot spin, it can only fill the rest of the image with whatever zeroes happen to decode to.
    ///     </para>
    /// </summary>
    internal sealed class JpegBitReader
    {
        private readonly byte[] _data;

        /// <summary>
        ///     Bits read from the stream but not yet consumed, held in the low <see cref="_bitCount" /> bits. Anything
        ///     above that is stale and is masked off on the way out rather than cleared on the way in.
        /// </summary>
        private uint _bitBuffer;

        private int _bitCount;
        private int _position;

        /// <summary>Set once the reader hits a marker or the end of the file; from then on it produces only zeroes.</summary>
        private bool _starved;

        /// <summary>Initializes a new instance of the <see cref="JpegBitReader" /> class.</summary>
        /// <param name="data">The whole file.</param>
        /// <param name="offset">Where the entropy-coded data starts, which is the first byte after the SOS header.</param>
        internal JpegBitReader(byte[] data, int offset)
        {
            _data = data;
            _position = offset;
        }

        /// <summary>
        ///     The first byte the reader has not taken. After a scan this is at or before the marker that ends it —
        ///     before, when the last few codes were satisfied out of the bit buffer without needing another byte —
        ///     which is why whoever reads next has to search forward for the marker rather than assume it is here.
        /// </summary>
        internal int Position => _position;

        /// <summary>Reads a single bit.</summary>
        internal int GetBit()
        {
            return GetBits(1);
        }

        /// <summary>Reads and consumes the next <paramref name="count" /> bits, most significant first.</summary>
        /// <param name="count">How many bits to read; never more than 16, and zero reads nothing and returns zero.</param>
        internal int GetBits(int count)
        {
            if (count == 0)
                return 0;

            Fill(count);
            _bitCount -= count;
            return (int) ((_bitBuffer >> _bitCount) & ((1u << count) - 1));
        }

        /// <summary>Reads the next <paramref name="count" /> bits without consuming them.</summary>
        internal int PeekBits(int count)
        {
            Fill(count);
            return (int) ((_bitBuffer >> (_bitCount - count)) & ((1u << count) - 1));
        }

        /// <summary>
        ///     Consumes bits a previous <see cref="PeekBits" /> already fetched. Valid only for a count that peek has
        ///     just guaranteed is in the buffer.
        /// </summary>
        internal void SkipBits(int count)
        {
            _bitCount -= count;
        }

        /// <summary>
        ///     Consumes the RSTn marker that closes a restart interval, and returns whether one was there.
        ///     <para>
        ///         Restart markers are byte-aligned — the encoder pads the last code out with 1 bits so the marker
        ///         starts on a byte — so the first thing to go is whatever is left in the bit buffer. The search that
        ///         follows exists because the buffer only ever reads the bytes it needs, and so may have stopped short
        ///         of the marker rather than run into it. It only moves forward, which is what bounds it.
        ///     </para>
        /// </summary>
        internal bool TryConsumeRestart()
        {
            _bitCount = 0;

            while (_position + 1 < _data.Length)
            {
                if (_data[_position] != 0xFF)
                {
                    _position++;
                    continue;
                }

                var code = _data[_position + 1];
                if (code >= 0xD0 && code <= 0xD7)
                {
                    _position += 2;
                    _starved = false;
                    return true;
                }

                // 0xFF 0x00 is stuffed data and 0xFF 0xFF is fill; neither is a marker, so neither ends the search.
                if (code == 0x00 || code == 0xFF)
                {
                    _position++;
                    continue;
                }

                return false; // Some other marker, which means the scan has already ended.
            }

            return false;
        }

        /// <summary>
        ///     Tops the bit buffer up to at least <paramref name="count" /> bits.
        ///     <para>
        ///         <paramref name="count" /> is never more than 16 — the longest Huffman code and the widest magnitude
        ///         field are both 16 bits — and that is what keeps the buffer inside its 32: it is only ever refilled
        ///         from at most 15 bits, and only ever by a byte at a time, so it tops out at 23.
        ///     </para>
        /// </summary>
        private void Fill(int count)
        {
            while (_bitCount < count)
            {
                _bitBuffer = (_bitBuffer << 8) | (uint) NextByte();
                _bitCount += 8;
            }
        }

        /// <summary>Takes the next byte of entropy-coded data, unstuffing it, or zero once the scan is over.</summary>
        private int NextByte()
        {
            if (_starved)
                return 0;

            if (_position >= _data.Length)
            {
                _starved = true;
                return 0;
            }

            var value = _data[_position];
            if (value != 0xFF)
            {
                _position++;
                return value;
            }

            // A 0xFF is followed by the 0x00 that says it was data, or by the code of the marker that ends the scan.
            // A run of further 0xFFs in between is legal fill, which is why this looks for the first byte that is
            // neither rather than just at the next one.
            var scan = _position + 1;
            while (scan < _data.Length && _data[scan] == 0xFF)
                scan++;

            if (scan >= _data.Length)
            {
                _starved = true;
                return 0;
            }

            if (_data[scan] == 0x00)
            {
                _position = scan + 1;
                return 0xFF;
            }

            // A real marker. The position lands on its 0xFF rather than past it, so that whoever reads the stream
            // next finds the marker whole.
            _position = scan - 1;
            _starved = true;
            return 0;
        }
    }
}
