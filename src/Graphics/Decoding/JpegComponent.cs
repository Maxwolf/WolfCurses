// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/17/2026

namespace WolfCurses.Graphics.Decoding
{
    /// <summary>
    ///     One colour component of a JPEG frame: what the frame header said about it, the geometry that follows from
    ///     that, and the coefficients the scans fill in.
    ///     <para>
    ///         A component is not a plane of pixels but a plane of 8x8 blocks, and it need not be the size of the
    ///         image. The frame header gives each one a pair of sampling factors, and a component's own resolution is
    ///         its factors over the largest factors in the frame — which is how 4:2:0 is expressed: luma at 2x2,
    ///         chroma at 1x1, so the chroma planes are half the width and half the height and cost a quarter as much.
    ///     </para>
    /// </summary>
    internal sealed class JpegComponent
    {
        /// <summary>Initializes a new instance of the <see cref="JpegComponent" /> class from its frame header entry.</summary>
        /// <param name="id">The identifier scans use to name this component. Arbitrary, but conventionally 1, 2, 3.</param>
        /// <param name="horizontalFactor">Horizontal sampling factor, 1 to 4.</param>
        /// <param name="verticalFactor">Vertical sampling factor, 1 to 4.</param>
        /// <param name="quantTableIndex">Which of the frame's four quantisation tables this component was quantised with.</param>
        internal JpegComponent(int id, int horizontalFactor, int verticalFactor, int quantTableIndex)
        {
            Id = id;
            HorizontalFactor = horizontalFactor;
            VerticalFactor = verticalFactor;
            QuantTableIndex = quantTableIndex;
        }

        /// <summary>The identifier scans use to name this component.</summary>
        internal int Id { get; }

        /// <summary>Horizontal sampling factor, 1 to 4.</summary>
        internal int HorizontalFactor { get; }

        /// <summary>Vertical sampling factor, 1 to 4.</summary>
        internal int VerticalFactor { get; }

        /// <summary>Which of the frame's four quantisation tables dequantises this component.</summary>
        internal int QuantTableIndex { get; }

        /// <summary>This component's own width in samples, which is the image's width scaled by its sampling factors.</summary>
        internal int Width { get; set; }

        /// <summary>This component's own height in samples.</summary>
        internal int Height { get; set; }

        /// <summary>Blocks needed to cover <see cref="Width" />: what a scan carrying this component alone walks.</summary>
        internal int BlocksPerLine { get; set; }

        /// <summary>Blocks needed to cover <see cref="Height" />.</summary>
        internal int BlocksPerColumn { get; set; }

        /// <summary>
        ///     Blocks per row once the image is padded out to whole MCUs: what an interleaved scan walks, and the
        ///     stride of <see cref="Coefficients" />. Never smaller than <see cref="BlocksPerLine" /> and often larger,
        ///     because an MCU cannot be half sent.
        /// </summary>
        internal int BlocksPerLineForMcu { get; set; }

        /// <summary>Block rows once the image is padded out to whole MCUs.</summary>
        internal int BlocksPerColumnForMcu { get; set; }

        /// <summary>
        ///     Every quantised coefficient of the component, 64 to a block, in zig-zag order, strided by
        ///     <see cref="BlocksPerLineForMcu" />. Still quantised: dequantisation happens at the inverse transform,
        ///     because a progressive file may revisit any of these several times before they are finished.
        /// </summary>
        internal short[] Coefficients { get; set; }
    }
}
