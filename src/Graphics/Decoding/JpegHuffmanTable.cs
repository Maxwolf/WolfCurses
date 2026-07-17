// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/17/2026

using System.IO;

namespace WolfCurses.Graphics.Decoding
{
    /// <summary>
    ///     One of the Huffman tables a DHT segment defines, built for decoding.
    ///     <para>
    ///         A DHT segment never writes a single code down. It carries how many codes are one bit long, how many are
    ///         two, and so on to sixteen, and then the values those codes mean in order — and that is enough, because
    ///         the codes themselves are fixed by a rule rather than chosen: hand them out in increasing length,
    ///         counting up within a length and doubling on the way to the next. Every JPEG encoder and decoder runs
    ///         that same procedure and arrives at the same table, which is why the format can afford to leave it out.
    ///     </para>
    /// </summary>
    internal sealed class JpegHuffmanTable
    {
        /// <summary>
        ///     How many bits the fast path resolves in one look. Eight is the usual choice and the reason is the shape
        ///     of the data rather than the shape of the cache: the whole point of a Huffman table is that the symbols
        ///     an image actually uses get the short codes, so a byte of lookahead settles almost every code in a real
        ///     picture and the bit-at-a-time path below is left for the rare ones.
        /// </summary>
        private const int LookaheadBits = 8;

        /// <summary>Indexed by the next 8 bits: (length &lt;&lt; 8) | value for a code that fits, or 0 for one that does not.</summary>
        private readonly int[] _lookahead = new int[1 << LookaheadBits];

        /// <summary>Largest code of each length, or -1 where the table has no codes that long.</summary>
        private readonly int[] _maxCode = new int[17];

        /// <summary>Smallest code of each length.</summary>
        private readonly int[] _minCode = new int[17];

        /// <summary>Where each length's values start in <see cref="_values" />.</summary>
        private readonly int[] _valuePointer = new int[17];

        private readonly byte[] _values;

        /// <summary>Initializes a new instance of the <see cref="JpegHuffmanTable" /> class.</summary>
        /// <param name="countsByLength">How many codes are of each length, indexed 1 to 16. Index 0 is unused.</param>
        /// <param name="values">What the codes mean, in the order the codes run.</param>
        internal JpegHuffmanTable(int[] countsByLength, byte[] values)
        {
            var total = 0;
            for (var length = 1; length <= 16; length++)
                total += countsByLength[length];

            if (total != values.Length)
                throw new InvalidDataException(
                    $"JPEG Huffman table declares {total} codes but carries {values.Length} values.");

            _values = values;

            var code = 0;
            var valueIndex = 0;
            for (var length = 1; length <= 16; length++)
            {
                var count = countsByLength[length];
                if (count == 0)
                {
                    // -1 is below every code, so the decoder's "does the code end here" test can never pass at a
                    // length the table skipped, and it reads on.
                    _maxCode[length] = -1;
                    code <<= 1;
                    continue;
                }

                _valuePointer[length] = valueIndex;
                _minCode[length] = code;
                valueIndex += count;
                code += count;
                _maxCode[length] = code - 1;

                // An over-subscribed table asks for more codes of a length than that length has room for. It is not a
                // table that decodes badly, it is not a table at all: the assignment rule has run out of prefixes, so
                // some of these codes are prefixes of others and no bit stream built on them means one thing.
                if (code > 1 << length)
                    throw new InvalidDataException(
                        $"JPEG Huffman table is over-subscribed: it declares more codes of length {length} than the " +
                        $"{1 << length} such a code can hold.");

                code <<= 1;
            }

            BuildLookahead(countsByLength);
        }

        /// <summary>Reads one Huffman code from the stream and returns the value it stands for.</summary>
        internal int Decode(JpegBitReader reader)
        {
            var entry = _lookahead[reader.PeekBits(LookaheadBits)];
            if (entry != 0)
            {
                // Zero is a safe miss marker rather than an ambiguous one: a hit always carries a length of at least
                // 1 in its high bits, so it can never itself be zero however small the value is.
                reader.SkipBits(entry >> 8);
                return entry & 0xFF;
            }

            // The lookahead missed, which means no code of 8 bits or fewer matches what is coming. So take those 8
            // bits as the start of a longer code and keep extending until one of the remaining lengths claims it.
            var code = reader.GetBits(LookaheadBits);
            var length = LookaheadBits;
            while (true)
            {
                code = (code << 1) | reader.GetBit();
                length++;

                if (length > 16)
                    throw new InvalidDataException(
                        "JPEG entropy data holds a Huffman code longer than the 16 bits the format allows, which means " +
                        "it is not a code in the table the scan selected.");

                if (code <= _maxCode[length])
                    break;
            }

            return _values[_valuePointer[length] + code - _minCode[length]];
        }

        /// <summary>
        ///     Fills the fast-path table. Every code short enough to fit in the lookahead claims each index whose
        ///     leading bits are that code — one entry for an 8-bit code, 128 for a 1-bit one — so a single peek
        ///     answers with both the value and how far to advance. Codes longer than the lookahead claim nothing and
        ///     leave their indices zero.
        /// </summary>
        private void BuildLookahead(int[] countsByLength)
        {
            var code = 0;
            var valueIndex = 0;
            for (var length = 1; length <= LookaheadBits; length++)
            {
                for (var i = 0; i < countsByLength[length]; i++)
                {
                    var span = 1 << (LookaheadBits - length);
                    var start = code << (LookaheadBits - length);
                    var entry = (length << 8) | _values[valueIndex];
                    for (var j = 0; j < span; j++)
                        _lookahead[start + j] = entry;

                    valueIndex++;
                    code++;
                }

                code <<= 1;
            }
        }
    }
}
