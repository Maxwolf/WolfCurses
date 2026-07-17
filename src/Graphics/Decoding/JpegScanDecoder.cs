// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/17/2026

using System;
using System.IO;

namespace WolfCurses.Graphics.Decoding
{
    /// <summary>
    ///     One scan: its header, and the entropy-coded data that follows it.
    ///     <para>
    ///         A sequential JPEG has one scan and it carries the whole picture. A progressive one has a dozen or more,
    ///         each carrying a slice of it — some band of the coefficients, at some precision, for some component —
    ///         and the picture only exists once they have all been laid on top of one another. That is the difference
    ///         between the two formats, and it is the whole of the difference: five routines below instead of one, all
    ///         of them writing into the same coefficient buffer, which by the last scan holds what a baseline file
    ///         would have written in a single pass.
    ///     </para>
    ///     <para>
    ///         An instance is one scan's worth of state and is thrown away with the scan.
    ///     </para>
    /// </summary>
    internal sealed class JpegScanDecoder
    {
        private readonly JpegHuffmanTable[] _acTables;
        private readonly int _approximationHigh;
        private readonly int _approximationLow;
        private readonly JpegComponent[] _components;
        private readonly JpegHuffmanTable[] _dcTables;
        private readonly JpegFrame _frame;

        /// <summary>Each component's running DC value. Reset at the start of a scan and at every restart marker.</summary>
        private readonly int[] _predictors;

        private readonly JpegBitReader _reader;
        private readonly int _spectralEnd;
        private readonly int _spectralStart;

        /// <summary>
        ///     Blocks still to come in an end-of-band run, not counting the one being decoded. Progressive only: an
        ///     encoder that finds the same band empty in a stretch of blocks says so once, and this is what remembers.
        /// </summary>
        private int _eobRun;

        /// <summary>Initializes a new instance of the <see cref="JpegScanDecoder" /> class by reading a SOS header.</summary>
        /// <param name="frame">The frame this scan contributes to.</param>
        /// <param name="data">The whole file.</param>
        /// <param name="headerStart">First byte of the SOS payload, past the marker and its length.</param>
        /// <param name="headerEnd">One past the last byte of the SOS payload, which is where the entropy data starts.</param>
        internal JpegScanDecoder(JpegFrame frame, byte[] data, int headerStart, int headerEnd)
        {
            _frame = frame;

            var p = headerStart;
            if (p >= headerEnd)
                throw new InvalidDataException("JPEG scan header is empty.");

            var count = data[p++];
            if (count < 1 || count > 4)
                throw new InvalidDataException($"JPEG scan declares {count} components; the format allows 1 to 4.");
            if (p + count * 2 + 3 > headerEnd)
                throw new InvalidDataException("JPEG scan header is shorter than the components it declares.");

            _components = new JpegComponent[count];
            _dcTables = new JpegHuffmanTable[count];
            _acTables = new JpegHuffmanTable[count];
            _predictors = new int[count];

            for (var i = 0; i < count; i++)
            {
                var id = data[p++];
                var selectors = data[p++];
                var dc = selectors >> 4;
                var ac = selectors & 15;

                if (dc > 3 || ac > 3)
                    throw new InvalidDataException(
                        $"JPEG scan points component {id} at Huffman tables {dc} and {ac}; only tables 0 to 3 exist.");

                _components[i] = frame.FindComponent(id);

                // Fetched but not checked for existence: a scan is entitled to leave the selector for the half it does
                // not carry pointing at a table nobody defined, and progressive files routinely do. Whether the table
                // is really there is a question for the routine that needs it.
                _dcTables[i] = frame.DcTables[dc];
                _acTables[i] = frame.AcTables[ac];
            }

            _spectralStart = data[p++];
            _spectralEnd = data[p++];
            var approximation = data[p];
            _approximationHigh = approximation >> 4;
            _approximationLow = approximation & 15;

            if (frame.Progressive)
            {
                ValidateProgressiveBand(count);
            }
            else
            {
                // A sequential scan always carries every coefficient at full precision, and these three bytes have no
                // other legal value. Encoders do put other things in them, though, and libjpeg has quietly overridden
                // them rather than fail for thirty years — files in the wild depend on that, so this does the same
                // rather than reject a picture that is otherwise perfectly decodable.
                _spectralStart = 0;
                _spectralEnd = 63;
                _approximationHigh = 0;
                _approximationLow = 0;
            }

            _reader = new JpegBitReader(data, headerEnd);
        }

        /// <summary>Decodes the whole scan into the frame's coefficients.</summary>
        /// <returns>The offset just past the entropy-coded data, for the marker loop to carry on from.</returns>
        internal int Decode()
        {
            // Interleaving is decided by the component count and nothing else. With one component there are no MCUs to
            // speak of and the scan is a plain raster of that component's own blocks — which is a different, smaller
            // count than the MCU-padded one, and the reason a component keeps both.
            var interleaved = _components.Length > 1;
            var mcuCount = interleaved
                ? _frame.McusPerLine * _frame.McusPerColumn
                : _components[0].BlocksPerLine * _components[0].BlocksPerColumn;

            var restartInterval = _frame.RestartInterval;
            var untilRestart = restartInterval > 0 ? restartInterval : int.MaxValue;

            for (var mcu = 0; mcu < mcuCount; mcu++)
            {
                if (untilRestart == 0)
                {
                    // Every restart interval decodes independently of the others, and this is what makes that true:
                    // the DC predictors and any end-of-band run in flight are dropped at the marker. That is the whole
                    // point of restarts — a decoder that loses its place picks the picture back up at the next marker
                    // instead of losing everything below it.
                    if (!_reader.TryConsumeRestart())
                        throw new InvalidDataException(
                            $"JPEG scan declares a restart marker every {restartInterval} MCUs but none follows MCU " +
                            $"{mcu}.");

                    Array.Clear(_predictors, 0, _predictors.Length);
                    _eobRun = 0;
                    untilRestart = restartInterval;
                }

                untilRestart--;

                if (!interleaved)
                {
                    var single = _components[0];
                    DecodeBlock(0, mcu / single.BlocksPerLine, mcu % single.BlocksPerLine);
                    continue;
                }

                var mcuRow = mcu / _frame.McusPerLine;
                var mcuColumn = mcu % _frame.McusPerLine;
                for (var i = 0; i < _components.Length; i++)
                {
                    var component = _components[i];
                    for (var v = 0; v < component.VerticalFactor; v++)
                    for (var h = 0; h < component.HorizontalFactor; h++)
                        DecodeBlock(i, mcuRow * component.VerticalFactor + v, mcuColumn * component.HorizontalFactor + h);
                }
            }

            return _reader.Position;
        }

        /// <summary>
        ///     Recovers a signed coefficient from the magnitude-and-bits form the format codes it in.
        ///     <para>
        ///         The Huffman symbol has already given the bit count; the bits themselves say where the value sits
        ///         among the two runs of that width, which for n bits are the negatives from -(2^n - 1) to -2^(n-1)
        ///         and the positives from 2^(n-1) to 2^n - 1. A leading zero means the negative run. That is why this
        ///         shifts the whole span down rather than sign-extending: the sign is not a bit, it is which half of
        ///         an evenly split range the value landed in.
        ///     </para>
        /// </summary>
        private static int Extend(int value, int magnitude)
        {
            return value < 1 << (magnitude - 1) ? value - (1 << magnitude) + 1 : value;
        }

        /// <summary>Rejects a progressive scan header that does not describe a band this decoder could apply.</summary>
        private void ValidateProgressiveBand(int count)
        {
            if (_spectralEnd > 63 || _spectralStart > _spectralEnd)
                throw new InvalidDataException(
                    $"JPEG progressive scan declares the spectral band {_spectralStart} to {_spectralEnd}, which is " +
                    "not a range within the 64 coefficients of a block.");

            if (_spectralStart == 0 && _spectralEnd != 0)
                throw new InvalidDataException(
                    $"JPEG progressive scan starts at coefficient 0, the DC term, but runs on to {_spectralEnd}. DC " +
                    "and AC coefficients are never sent in the same progressive scan.");

            if (_spectralStart != 0 && count != 1)
                throw new InvalidDataException(
                    $"JPEG progressive AC scan carries {count} components. Only a DC scan may interleave; an AC scan " +
                    "carries exactly one component, because its end-of-band runs count that component's blocks.");

            if (_approximationHigh > 13 || _approximationLow > 13)
                throw new InvalidDataException(
                    $"JPEG progressive scan declares successive approximation bits {_approximationHigh} and " +
                    $"{_approximationLow}; the format allows 0 to 13.");

            if (_approximationHigh != 0 && _approximationHigh != _approximationLow + 1)
                throw new InvalidDataException(
                    $"JPEG progressive scan refines bit {_approximationLow} of coefficients a previous scan sent down " +
                    $"to bit {_approximationHigh}. Successive approximation descends one bit at a time, so this scan " +
                    "would leave a hole no later scan could fill.");
        }

        /// <summary>Decodes one block, into whichever of the five coefficient layouts this scan is carrying.</summary>
        /// <param name="index">Which of this scan's components the block belongs to.</param>
        /// <param name="blockRow">The block's row within that component.</param>
        /// <param name="blockColumn">The block's column within that component.</param>
        private void DecodeBlock(int index, int blockRow, int blockColumn)
        {
            var component = _components[index];
            var block = component.Coefficients;
            var offset = (blockRow * component.BlocksPerLineForMcu + blockColumn) * 64;

            if (!_frame.Progressive)
            {
                DecodeBaseline(index, block, offset);
                return;
            }

            if (_spectralStart == 0)
            {
                if (_approximationHigh == 0)
                    DecodeDcFirst(index, block, offset);
                else
                    RefineDc(block, offset);

                return;
            }

            if (_approximationHigh == 0)
                DecodeAcFirst(index, block, offset);
            else
                RefineAc(index, block, offset);
        }

        /// <summary>Decodes a whole block at full precision: the sequential case, and the only one a SOF0 file uses.</summary>
        private void DecodeBaseline(int index, short[] block, int offset)
        {
            block[offset] = (short) NextDcValue(index);

            var table = AcTable(index);
            for (var k = 1; k < 64; k++)
            {
                var symbol = table.Decode(_reader);
                var run = symbol >> 4;
                var size = symbol & 15;

                if (size == 0)
                {
                    if (run != 15)
                        break; // End of block: every coefficient from here to 63 is zero.

                    k += 15; // A run of sixteen zeroes: fifteen here and one more from the loop's own step.
                    continue;
                }

                k += run;
                if (k > 63)
                    throw new InvalidDataException(
                        $"JPEG block places a coefficient at zig-zag index {k}, past the 63 an 8x8 block holds.");

                block[offset + k] = (short) Extend(_reader.GetBits(size), size);
            }
        }

        /// <summary>Decodes the DC term's leading bits, in the first of a progressive file's scans to mention it.</summary>
        private void DecodeDcFirst(int index, short[] block, int offset)
        {
            // The point transform: this scan carries the DC value shifted right by Al, so shifting it back leaves the
            // low bits at zero for the refinement scans to fill in one at a time.
            block[offset] = (short) (NextDcValue(index) << _approximationLow);
        }

        /// <summary>Appends one already-known bit to the DC term.</summary>
        private void RefineDc(short[] block, int offset)
        {
            // One bit per block and no Huffman coding at all, which makes a DC refinement the one scan a corrupt file
            // cannot desynchronise: every block costs exactly one bit whatever the bit says.
            //
            // The OR works for negative values as well as positive ones, and not by accident. The point transform is
            // an arithmetic shift, so the value this is completing is the true one with its low bits cleared — in
            // two's complement, for either sign — and putting those bits back is exactly an OR.
            if (_reader.GetBit() != 0)
                block[offset] |= (short) (1 << _approximationLow);
        }

        /// <summary>Decodes the leading bits of a band of AC coefficients, in the first scan to carry that band.</summary>
        private void DecodeAcFirst(int index, short[] block, int offset)
        {
            if (_eobRun > 0)
            {
                // This block falls inside a run the encoder declared empty across this whole band. There is nothing to
                // read and nothing to write: the coefficients are already zero.
                _eobRun--;
                return;
            }

            var table = AcTable(index);
            for (var k = _spectralStart; k <= _spectralEnd; k++)
            {
                var symbol = table.Decode(_reader);
                var run = symbol >> 4;
                var size = symbol & 15;

                if (size == 0)
                {
                    if (run != 15)
                    {
                        // EOBn. The band is empty from here to the end of this block, and empty across the whole of
                        // the next 2^run + extra blocks too. This block is one of them, hence the subtraction: what is
                        // kept is how many blocks after this one the run still covers.
                        _eobRun = (1 << run) - 1;
                        if (run > 0)
                            _eobRun += _reader.GetBits(run);

                        break;
                    }

                    k += 15;
                    continue;
                }

                k += run;
                if (k > _spectralEnd)
                    throw new InvalidDataException(
                        $"JPEG progressive scan places a coefficient at zig-zag index {k}, past the {_spectralEnd} " +
                        "its spectral band ends at.");

                block[offset + k] = (short) (Extend(_reader.GetBits(size), size) << _approximationLow);
            }
        }

        /// <summary>
        ///     Appends one bit to every coefficient of a band that already has some, and lets coefficients that had
        ///     none become non-zero. The trickiest routine in the format.
        ///     <para>
        ///         The difficulty is that a refinement scan is coding two things at once into one stream of symbols.
        ///         A coefficient that was zero in every previous scan may become non-zero here, and those arrivals are
        ///         coded the usual way, as a run of skipped coefficients and a value — except that the value is always
        ///         one bit, worth plus or minus 1 at this scan's precision, because a coefficient becoming non-zero
        ///         now is by definition one whose leading bit is this one. Meanwhile every coefficient that was
        ///         already non-zero needs one correction bit saying whether its magnitude grows.
        ///     </para>
        ///     <para>
        ///         Those two are interleaved with no marker between them, and the rule that untangles them is this:
        ///         the run counts only coefficients that were zero when the scan began. Already-non-zero coefficients
        ///         are invisible to it — the run steps straight over them — but stepping over one costs a correction
        ///         bit, taken from the stream on the way past. So the meaning of the next bit depends on coefficients
        ///         decoded in scans that finished long ago, which is why this cannot be resynchronised by inspection
        ///         and why getting it subtly wrong produces noise rather than a slightly wrong picture.
        ///     </para>
        /// </summary>
        private void RefineAc(int index, short[] block, int offset)
        {
            var positive = 1 << _approximationLow;
            var negative = -1 << _approximationLow;
            var k = _spectralStart;

            if (_eobRun == 0)
            {
                var table = AcTable(index);
                for (; k <= _spectralEnd; k++)
                {
                    var symbol = table.Decode(_reader);
                    var run = symbol >> 4;
                    var size = symbol & 15;
                    var arrival = 0;

                    if (size != 0)
                    {
                        if (size != 1)
                            throw new InvalidDataException(
                                $"JPEG refinement scan declares a {size}-bit coefficient. A coefficient that first " +
                                "becomes non-zero in a refinement scan is worth exactly plus or minus one at this " +
                                "scan's precision, so its size can only ever be 1.");

                        arrival = _reader.GetBit() != 0 ? positive : negative;
                    }
                    else if (run != 15)
                    {
                        // EOBn: no more coefficients arrive in this band, for this block or the next 2^run + extra of
                        // them. Note that this counts the run whole, including the block in hand, where DecodeAcFirst
                        // takes one off the same figure straight away. The two are not inconsistent: there the block
                        // is finished the moment the run is read, so it is counted off at once, and here it is not —
                        // the correction bits below are still to come — so it is counted off after them instead.
                        _eobRun = 1 << run;
                        if (run > 0)
                            _eobRun += _reader.GetBits(run);

                        break;
                    }

                    // Walk to the coefficient the run points at, correcting every already-non-zero one passed on the
                    // way. A run of 15 with no value is the zero-run symbol and arrives here with nothing to place,
                    // which skips sixteen zero-history coefficients and no more.
                    while (k <= _spectralEnd)
                    {
                        var position = offset + k;
                        if (block[position] != 0)
                            AppendCorrectionBit(block, position, positive, negative);
                        else if (--run < 0)
                            break;

                        k++;
                    }

                    if (arrival == 0)
                        continue;

                    if (k > _spectralEnd)
                        throw new InvalidDataException(
                            "JPEG refinement scan runs past the end of its spectral band looking for the coefficient " +
                            "its run length points at.");

                    block[offset + k] = (short) arrival;
                }
            }

            if (_eobRun <= 0)
                return;

            // The part that is easy to miss, and fatal to miss. An end-of-band in a refinement scan says only that no
            // more coefficients become non-zero — not that the block is finished. Every coefficient from here to the
            // end of the band that was already non-zero still has a correction bit waiting in the stream, and leaving
            // those bits unread puts every block after this one out of step.
            for (; k <= _spectralEnd; k++)
            {
                var position = offset + k;
                if (block[position] != 0)
                    AppendCorrectionBit(block, position, positive, negative);
            }

            // And now the block really is finished, so it comes off the run. This is the one decrement, and it serves
            // both the run that was declared above and one carried in from an earlier block.
            _eobRun--;
        }

        /// <summary>Reads one correction bit and grows an already-non-zero coefficient's magnitude if it is set.</summary>
        private void AppendCorrectionBit(short[] block, int position, int positive, int negative)
        {
            if (_reader.GetBit() == 0)
                return;

            // Growing means away from zero in whichever direction the coefficient already points, which is why the
            // sign is read off the value rather than sent. The test that the bit is not set already cannot fail on a
            // well-formed file — this scan is the first to say anything about that bit — and exists to keep a corrupt
            // one from walking a coefficient away to nothing in particular.
            if ((block[position] & positive) != 0)
                return;

            block[position] = (short) (block[position] + (block[position] >= 0 ? positive : negative));
        }

        /// <summary>Reads a DC difference and folds it into the component's running value.</summary>
        private int NextDcValue(int index)
        {
            var magnitude = DcTable(index).Decode(_reader);
            if (magnitude > 15)
                throw new InvalidDataException(
                    $"JPEG DC coefficient declares a {magnitude}-bit magnitude; the format allows at most 15.");

            // A DC term is coded as its difference from the previous block of the same component, because neighbouring
            // blocks of a photograph are mostly the same brightness. It is also why one corrupt block ruins every
            // block after it, all the way to the next restart marker.
            if (magnitude != 0)
                _predictors[index] += Extend(_reader.GetBits(magnitude), magnitude);

            return _predictors[index];
        }

        /// <summary>The DC table this scan selected for one of its components, or an error naming the one it wanted.</summary>
        private JpegHuffmanTable DcTable(int index)
        {
            return _dcTables[index] ?? throw new InvalidDataException(
                $"JPEG scan reads DC coefficients for component {_components[index].Id} with a Huffman table that no " +
                "DHT segment ever defined.");
        }

        /// <summary>The AC table this scan selected for one of its components, or an error naming the one it wanted.</summary>
        private JpegHuffmanTable AcTable(int index)
        {
            return _acTables[index] ?? throw new InvalidDataException(
                $"JPEG scan reads AC coefficients for component {_components[index].Id} with a Huffman table that no " +
                "DHT segment ever defined.");
        }
    }
}
