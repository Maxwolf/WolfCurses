// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/17/2026

using System.IO;

namespace WolfCurses.Graphics.Decoding
{
    /// <summary>
    ///     One component's finished samples, and the arithmetic that reads them back at the full image's resolution.
    ///     <para>
    ///         A JPEG stores each component at whatever resolution the encoder thought it could get away with. For
    ///         photographs that is almost always 4:2:0 — chroma at half the width and half the height of luma — which
    ///         works because the eye resolves colour far worse than brightness, and which means that getting a picture
    ///         back out of a JPEG ends in a resize.
    ///     </para>
    ///     <para>
    ///         The resize is bilinear, and driven entirely off the sampling factors: there is no case here for 4:2:0
    ///         or 4:2:2 or 4:4:4, only one formula that reduces to each of them. The formula's whole subtlety is the
    ///         half-pixel offsets in <see cref="BuildSampleMap" />, which is also why it is not the obvious
    ///         <c>x * factor / maxFactor</c>. Get those right and the common ratios come out identical to what
    ///         libjpeg's dedicated upsamplers produce, weight for weight; get them wrong and every chroma edge in the
    ///         picture shifts half a pixel.
    ///     </para>
    /// </summary>
    internal sealed class JpegPlane
    {
        private readonly int[] _columnHigh;
        private readonly int[] _columnLow;
        private readonly int[] _columnWeight;
        private readonly int[] _rowHigh;
        private readonly int[] _rowLow;
        private readonly int[] _rowWeight;
        private readonly byte[] _samples;
        private readonly int _stride;

        /// <summary>
        ///     Initializes a new instance of the <see cref="JpegPlane" /> class by inverse-transforming every block of
        ///     a component whose coefficients are complete.
        /// </summary>
        /// <param name="frame">The finished frame.</param>
        /// <param name="component">The component to realise.</param>
        internal JpegPlane(JpegFrame frame, JpegComponent component)
        {
            var quantTable = frame.QuantTables[component.QuantTableIndex] ??
                             throw new InvalidDataException(
                                 $"JPEG component {component.Id} is quantised with table " +
                                 $"{component.QuantTableIndex}, which no DQT segment ever defined.");

            _stride = component.BlocksPerLineForMcu * 8;
            _samples = new byte[_stride * component.BlocksPerColumnForMcu * 8];

            // One workspace for the whole component rather than one per block. The transform wants 64 ints of scratch
            // and nothing else, and a decoder that allocated them per block would spend more time in the collector
            // than in the transform.
            var workspace = new int[64];
            for (var row = 0; row < component.BlocksPerColumnForMcu; row++)
            for (var column = 0; column < component.BlocksPerLineForMcu; column++)
                JpegIdct.Transform(component.Coefficients, (row * component.BlocksPerLineForMcu + column) * 64,
                    quantTable, workspace, _samples, row * 8 * _stride + column * 8, _stride);

            // The maps clamp to the component's own size rather than the plane's. The difference is the padding the
            // encoder added to fill out the last block and the last MCU, which holds whatever it felt like putting
            // there and must never be read back into the picture.
            BuildSampleMap(frame.Width, component.HorizontalFactor, frame.MaxHorizontalFactor, component.Width,
                out _columnLow, out _columnHigh, out _columnWeight);
            BuildSampleMap(frame.Height, component.VerticalFactor, frame.MaxVerticalFactor, component.Height,
                out _rowLow, out _rowHigh, out _rowWeight);
        }

        /// <summary>Reads this component's value at a pixel of the finished image, 0 to 255.</summary>
        /// <param name="x">Column of the finished image.</param>
        /// <param name="y">Row of the finished image.</param>
        internal int Sample(int x, int y)
        {
            var left = _columnLow[x];
            var top = _rowLow[y] * _stride;
            var horizontal = _columnWeight[x];
            var vertical = _rowWeight[y];

            // A component sampled at the frame's own resolution maps one to one, which is every component of a 4:4:4
            // image and the luma of every other, so it is most of the samples ever taken and worth the branch.
            if (horizontal == 0 && vertical == 0)
                return _samples[top + left];

            var right = _columnHigh[x];
            var bottom = _rowHigh[y] * _stride;

            var upper = _samples[top + left] * (256 - horizontal) + _samples[top + right] * horizontal;
            var lower = _samples[bottom + left] * (256 - horizontal) + _samples[bottom + right] * horizontal;
            return (upper * (256 - vertical) + lower * vertical + 32768) >> 16;
        }

        /// <summary>
        ///     Builds the map from one axis of the finished image to one axis of a component's samples: for each
        ///     output position, the two samples that straddle it and how far between them it falls, in 256ths.
        ///     <para>
        ///         Both grids are measured from the centres of their cells, and that is the entire subtlety. A chroma
        ///         sample subsampled 2:1 does not sit on top of the first of the two luma pixels it covers, it sits on
        ///         the boundary between them — so output pixel 0 is a quarter of a step *before* the first chroma
        ///         sample's centre, not on it. Hence the half-pixel taken off each side, hence the negative positions
        ///         at the left edge that the clamp catches, and hence weights of 3:1 rather than 1:0 or 1:1. Those
        ///         3:1 weights are not a coincidence either: they are what libjpeg's hand-written 2:1 upsampler emits,
        ///         and this arrives at them from the general formula rather than by being told.
        ///     </para>
        /// </summary>
        /// <param name="outputSize">Pixels along this axis of the finished image.</param>
        /// <param name="factor">This component's sampling factor along this axis.</param>
        /// <param name="maxFactor">The frame's largest sampling factor along this axis.</param>
        /// <param name="sampleCount">Samples this component actually has along this axis, which the map clamps to.</param>
        /// <param name="low">Receives the sample at or before each output position.</param>
        /// <param name="high">Receives the sample after it.</param>
        /// <param name="weight">Receives how far between the two the output position falls, 0 to 255.</param>
        private static void BuildSampleMap(int outputSize, int factor, int maxFactor, int sampleCount, out int[] low,
            out int[] high, out int[] weight)
        {
            low = new int[outputSize];
            high = new int[outputSize];
            weight = new int[outputSize];

            for (var i = 0; i < outputSize; i++)
            {
                // ((i + 0.5) * factor / maxFactor) - 0.5, in 24.8 fixed point. Truncating division would round the
                // negative positions at the left edge toward zero, and those are exactly the ones this exists to get
                // right, so it floors instead.
                var numerator = ((2L * i + 1) * factor - maxFactor) * 128;
                var position = (int) (numerator >= 0
                    ? numerator / maxFactor
                    : -((-numerator + maxFactor - 1) / maxFactor));

                var index = position >> 8;
                low[i] = Clamp(index, sampleCount);
                high[i] = Clamp(index + 1, sampleCount);
                weight[i] = position & 255;
            }
        }

        /// <summary>Holds a sample index inside the component, so the edges of the picture repeat rather than run off.</summary>
        private static int Clamp(int index, int sampleCount)
        {
            if (index < 0) return 0;
            if (index >= sampleCount) return sampleCount - 1;
            return index;
        }
    }
}
