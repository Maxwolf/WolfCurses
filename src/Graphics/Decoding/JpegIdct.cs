// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/17/2026

namespace WolfCurses.Graphics.Decoding
{
    /// <summary>
    ///     The 8x8 inverse discrete cosine transform, together with the dequantisation and zig-zag reordering that
    ///     feed it: one block of coefficients in, one block of samples out.
    ///     <para>
    ///         The transform is separable, so a 2-D inverse is eight 1-D ones down the columns followed by eight
    ///         across the rows. That alone takes it from 4096 multiplies a block to a few hundred, and the 1-D stage
    ///         here — the Loeffler-Ligtenberg-Moschytz factorisation that every JPEG implementation of the last thirty
    ///         years uses — takes it down again to a dozen, by splitting the eight inputs into an even half whose four
    ///         coefficients need only one rotation between them and an odd half whose four share a common term.
    ///     </para>
    ///     <para>
    ///         The arithmetic is fixed point, the rotation constants scaled by 2^12. That is a decision about where to
    ///         spend 32 bits: more fractional bits would buy precision that rounding to a byte at the end throws away,
    ///         fewer would start to show. Nothing is checked for overflow, because for the 8-bit samples this decoder
    ///         accepts nothing can overflow — a conforming encoder's coefficients are bounded by about +/-2048, and
    ///         the intermediates by a comfortable margin under 2^31. A corrupt file can carry larger ones, and in
    ///         managed arithmetic those wrap rather than trap: nonsense in, differently shaped nonsense out, with the
    ///         clamp at the end keeping it inside a byte either way.
    ///     </para>
    /// </summary>
    internal static class JpegIdct
    {
        /// <summary>Fractional bits in the rotation constants.</summary>
        private const int ConstantBits = 12;

        /// <summary>
        ///     Extra fractional bits the column pass leaves for the row pass to consume. Rounding the intermediate
        ///     back to whole samples between the two would cost about a bit of accuracy in the result for nothing;
        ///     keeping two of them costs a shift.
        /// </summary>
        private const int PassOneBits = 2;

        /// <summary>
        ///     What the row pass has to shift away: the constants' 12 bits, the column pass's spare 2, and 3 more
        ///     because each pass carries a factor of sqrt(8) that this factorisation never puts back.
        /// </summary>
        private const int FinalShift = ConstantBits + PassOneBits + 3;

        // The rotation constants, each named for the irrational value it approximates, scaled by 2^ConstantBits and
        // rounded. Naming them for their values is what every implementation of this factorisation does, and for good
        // reason: they are products of cosines with no shorter honest name.
        private const int Fix0_298631336 = 1223;
        private const int Fix0_390180644 = 1598;
        private const int Fix0_541196100 = 2216;
        private const int Fix0_765366865 = 3135;
        private const int Fix0_899976223 = 3686;
        private const int Fix1_175875602 = 4816;
        private const int Fix1_501321110 = 6149;
        private const int Fix1_847759065 = 7568;
        private const int Fix1_961570560 = 8035;
        private const int Fix2_053119869 = 8410;
        private const int Fix2_562915447 = 10498;
        private const int Fix3_072711026 = 12586;

        /// <summary>
        ///     Where each coefficient of a block belongs, read in the order the format sends them. A block travels
        ///     along the diagonals outward from its top-left corner, which puts the coefficients in roughly descending
        ///     order of how much they matter to the picture and — the real payoff — gathers the zeroes a quantised
        ///     block is mostly made of into one run at the end. That run is what end-of-block then codes in a single
        ///     symbol, and it is most of the reason a JPEG is small.
        /// </summary>
        private static readonly int[] _zigZagOrder =
        {
            0, 1, 8, 16, 9, 2, 3, 10,
            17, 24, 32, 25, 18, 11, 4, 5,
            12, 19, 26, 33, 40, 48, 41, 34,
            27, 20, 13, 6, 7, 14, 21, 28,
            35, 42, 49, 56, 57, 50, 43, 36,
            29, 22, 15, 23, 30, 37, 44, 51,
            58, 59, 52, 45, 38, 31, 39, 46,
            53, 60, 61, 54, 47, 55, 62, 63
        };

        /// <summary>Dequantises, reorders and inverse-transforms one block, writing its 8x8 samples out as bytes.</summary>
        /// <param name="coefficients">The component's coefficients, in zig-zag order.</param>
        /// <param name="coefficientOffset">Where this block starts within them.</param>
        /// <param name="quantTable">The 64 quantisation factors, also in zig-zag order.</param>
        /// <param name="workspace">64 ints of scratch, supplied by the caller so that a block does not allocate.</param>
        /// <param name="output">The component's sample plane.</param>
        /// <param name="outputOffset">Where this block's top-left sample goes within it.</param>
        /// <param name="outputStride">Samples per row of the plane.</param>
        internal static void Transform(short[] coefficients, int coefficientOffset, int[] quantTable, int[] workspace,
            byte[] output, int outputOffset, int outputStride)
        {
            // Dequantise and unpick the zig-zag in one step. Both sides arrive in zig-zag order — the scan decoder
            // keeps coefficients the way they were sent, and DQT writes its table the same way — so the two line up
            // index for index and only the destination has to be reordered.
            for (var k = 0; k < 64; k++)
                workspace[_zigZagOrder[k]] = coefficients[coefficientOffset + k] * quantTable[k];

            for (var column = 0; column < 8; column++)
            {
                // A column holding nothing but its DC term inverts to a constant, and in a photograph most columns of
                // most blocks are exactly that: quantisation is what makes a JPEG small, and what it mostly does is
                // turn the high-frequency coefficients into zeroes. Skipping the butterflies here is worth more than
                // any amount of care spent on the butterflies themselves.
                if (workspace[column + 8] == 0 && workspace[column + 16] == 0 && workspace[column + 24] == 0 &&
                    workspace[column + 32] == 0 && workspace[column + 40] == 0 && workspace[column + 48] == 0 &&
                    workspace[column + 56] == 0)
                {
                    var flat = workspace[column] << PassOneBits;
                    for (var row = 0; row < 8; row++)
                        workspace[column + row * 8] = flat;

                    continue;
                }

                var t = Transform1D(workspace[column], workspace[column + 8], workspace[column + 16],
                    workspace[column + 24], workspace[column + 32], workspace[column + 40], workspace[column + 48],
                    workspace[column + 56]);

                const int shift = ConstantBits - PassOneBits;
                const int round = 1 << (shift - 1);
                workspace[column] = (t.Even0 + t.Odd3 + round) >> shift;
                workspace[column + 8] = (t.Even1 + t.Odd2 + round) >> shift;
                workspace[column + 16] = (t.Even2 + t.Odd1 + round) >> shift;
                workspace[column + 24] = (t.Even3 + t.Odd0 + round) >> shift;
                workspace[column + 32] = (t.Even3 - t.Odd0 + round) >> shift;
                workspace[column + 40] = (t.Even2 - t.Odd1 + round) >> shift;
                workspace[column + 48] = (t.Even1 - t.Odd2 + round) >> shift;
                workspace[column + 56] = (t.Even0 - t.Odd3 + round) >> shift;
            }

            for (var row = 0; row < 8; row++)
            {
                var i = row * 8;
                var t = Transform1D(workspace[i], workspace[i + 1], workspace[i + 2], workspace[i + 3],
                    workspace[i + 4], workspace[i + 5], workspace[i + 6], workspace[i + 7]);

                // The level shift rides along in the same constant that rounds off the fixed point. The encoder took
                // 128 off every sample before transforming it, precisely so that a mid-grey block would come out with
                // a DC term of zero rather than one at the top of its range; this is where that is given back.
                const int round = (1 << (FinalShift - 1)) + (128 << FinalShift);
                var o = outputOffset + row * outputStride;
                output[o] = ClampToByte((t.Even0 + t.Odd3 + round) >> FinalShift);
                output[o + 1] = ClampToByte((t.Even1 + t.Odd2 + round) >> FinalShift);
                output[o + 2] = ClampToByte((t.Even2 + t.Odd1 + round) >> FinalShift);
                output[o + 3] = ClampToByte((t.Even3 + t.Odd0 + round) >> FinalShift);
                output[o + 4] = ClampToByte((t.Even3 - t.Odd0 + round) >> FinalShift);
                output[o + 5] = ClampToByte((t.Even2 - t.Odd1 + round) >> FinalShift);
                output[o + 6] = ClampToByte((t.Even1 - t.Odd2 + round) >> FinalShift);
                output[o + 7] = ClampToByte((t.Even0 - t.Odd3 + round) >> FinalShift);
            }
        }

        /// <summary>
        ///     One 1-D inverse transform: eight coefficients in, and the four even-half and four odd-half terms the
        ///     eight outputs are built from out.
        ///     <para>
        ///         Handing back the halves rather than the outputs is not an optimisation, it is what makes one
        ///         routine serve both passes. The last step of the transform is a butterfly — output n is even n plus
        ///         odd n, and its mirror at 7 - n is the same two subtracted — and the two passes want to do that
        ///         butterfly at different scales, one keeping spare fractional bits and the other rounding all the way
        ///         down to a byte. So the butterfly is the caller's.
        ///     </para>
        /// </summary>
        private static (int Even0, int Even1, int Even2, int Even3, int Odd0, int Odd1, int Odd2, int Odd3)
            Transform1D(int s0, int s1, int s2, int s3, int s4, int s5, int s6, int s7)
        {
            // Even half: coefficients 0, 2, 4 and 6, the ones a half-resolution reconstruction would keep. Only 2 and
            // 6 need multiplying, and they share the term they are rotated by; 0 and 4 need nothing but a sum and a
            // difference, scaled up to meet the fixed point the rotation lands in.
            var rotation = (s2 + s6) * Fix0_541196100;
            var evenInner = rotation - s6 * Fix1_847759065;
            var evenOuter = rotation + s2 * Fix0_765366865;
            var sum = (s0 + s4) << ConstantBits;
            var difference = (s0 - s4) << ConstantBits;

            var even0 = sum + evenOuter;
            var even3 = sum - evenOuter;
            var even1 = difference + evenInner;
            var even2 = difference - evenInner;

            // Odd half: coefficients 1, 3, 5 and 7. Every one of them reaches every output, so a direct evaluation
            // would need sixteen multiplies. Instead one term is common to all four, and four more are shared between
            // pairs of them — and the pattern that falls out is worth reading, because it is the whole trick: each
            // output takes its own coefficient, plus exactly the two shared terms that its coefficient went into.
            var common = (s7 + s5 + s3 + s1) * Fix1_175875602;
            var share71 = common - (s7 + s1) * Fix0_899976223;
            var share53 = common - (s5 + s3) * Fix2_562915447;
            var share73 = -((s7 + s3) * Fix1_961570560);
            var share51 = -((s5 + s1) * Fix0_390180644);

            var odd0 = s7 * Fix0_298631336 + share71 + share73;
            var odd1 = s5 * Fix2_053119869 + share53 + share51;
            var odd2 = s3 * Fix3_072711026 + share53 + share73;
            var odd3 = s1 * Fix1_501321110 + share71 + share51;

            return (even0, even1, even2, even3, odd0, odd1, odd2, odd3);
        }

        /// <summary>
        ///     Clamps a finished sample into the byte range the format's samples live in. The transform is accurate
        ///     enough that this is not papering over its error — but ringing around a hard edge genuinely does push
        ///     the reconstruction past both ends of the range, and reliably so, which is why the clamp is not optional.
        /// </summary>
        private static byte ClampToByte(int value)
        {
            if (value < 0) return 0;
            if (value > 255) return 255;
            return (byte) value;
        }
    }
}
