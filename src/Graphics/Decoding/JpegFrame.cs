// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/17/2026

using System.IO;

namespace WolfCurses.Graphics.Decoding
{
    /// <summary>
    ///     Everything one decode of one JPEG knows: the geometry the frame header declared, the tables the markers
    ///     have defined so far, and the coefficients the scans have filled in.
    ///     <para>
    ///         This type exists so that <see cref="JpegDecoder" /> can hold nothing. A decoder instance is meant to
    ///         live in <see cref="ImageDecoders.Default" /> for the life of the process and be called from wherever,
    ///         so every byte of per-decode state lives here instead, in an object a single call owns and then drops.
    ///     </para>
    /// </summary>
    internal sealed class JpegFrame
    {
        /// <summary>Image width in pixels, as the frame header declares it.</summary>
        internal int Width { get; set; }

        /// <summary>Image height in pixels, as the frame header declares it.</summary>
        internal int Height { get; set; }

        /// <summary>
        ///     True for SOF2. Progressive and sequential JPEG differ only in how the scans carve up the coefficients
        ///     between them; by the time the coefficients are complete the two are indistinguishable, which is why
        ///     everything downstream of them is shared.
        /// </summary>
        internal bool Progressive { get; set; }

        /// <summary>The frame's components, in the order the frame header lists them. Null until a SOF marker arrives.</summary>
        internal JpegComponent[] Components { get; set; }

        /// <summary>The largest horizontal sampling factor in the frame, which sets what the others are relative to.</summary>
        internal int MaxHorizontalFactor { get; private set; }

        /// <summary>The largest vertical sampling factor in the frame.</summary>
        internal int MaxVerticalFactor { get; private set; }

        /// <summary>Minimum coded units across the image.</summary>
        internal int McusPerLine { get; private set; }

        /// <summary>Minimum coded units down the image.</summary>
        internal int McusPerColumn { get; private set; }

        /// <summary>
        ///     How many MCUs the encoder emits between restart markers, or zero when it emits none. From DRI, which
        ///     may appear more than once and change it between scans.
        /// </summary>
        internal int RestartInterval { get; set; }

        /// <summary>
        ///     The four quantisation tables a file may define, each held in zig-zag order exactly as DQT wrote it, and
        ///     null where no DQT has defined one.
        /// </summary>
        internal int[][] QuantTables { get; } = new int[4][];

        /// <summary>The four DC Huffman tables a file may define, and null where no DHT has defined one.</summary>
        internal JpegHuffmanTable[] DcTables { get; } = new JpegHuffmanTable[4];

        /// <summary>The four AC Huffman tables a file may define, and null where no DHT has defined one.</summary>
        internal JpegHuffmanTable[] AcTables { get; } = new JpegHuffmanTable[4];

        /// <summary>
        ///     Works out the block geometry everything later depends on, and allocates the coefficients, once a frame
        ///     header has supplied the dimensions and the components.
        ///     <para>
        ///         Each component ends up with two block counts, and the difference between them is a classic place to
        ///         put a bug. An interleaved scan walks whole MCUs, so it covers
        ///         <see cref="JpegComponent.BlocksPerLineForMcu" /> blocks per row — enough to pad the image out to a
        ///         whole number of MCUs. A scan carrying one component has no MCUs to pad to, so it walks only the
        ///         <see cref="JpegComponent.BlocksPerLine" /> blocks the component's own samples need, which is fewer.
        ///         Both write into the same buffer, so the buffer is sized and strided for the padded count and the
        ///         narrower walk simply leaves the right-hand columns alone.
        ///     </para>
        /// </summary>
        internal void AllocateCoefficients()
        {
            foreach (var component in Components)
            {
                if (component.HorizontalFactor > MaxHorizontalFactor)
                    MaxHorizontalFactor = component.HorizontalFactor;
                if (component.VerticalFactor > MaxVerticalFactor)
                    MaxVerticalFactor = component.VerticalFactor;
            }

            // An MCU holds one block of every component at that component's own sampling, so whatever the components
            // do individually it always covers 8 * the largest factor pixels of the finished image in each direction.
            McusPerLine = CeilDivide(Width, 8 * MaxHorizontalFactor);
            McusPerColumn = CeilDivide(Height, 8 * MaxVerticalFactor);

            foreach (var component in Components)
            {
                component.Width = CeilDivide(Width * component.HorizontalFactor, MaxHorizontalFactor);
                component.Height = CeilDivide(Height * component.VerticalFactor, MaxVerticalFactor);
                component.BlocksPerLine = CeilDivide(Width * component.HorizontalFactor, 8 * MaxHorizontalFactor);
                component.BlocksPerColumn = CeilDivide(Height * component.VerticalFactor, 8 * MaxVerticalFactor);
                component.BlocksPerLineForMcu = McusPerLine * component.HorizontalFactor;
                component.BlocksPerColumnForMcu = McusPerColumn * component.VerticalFactor;

                // The dimensions have already been past DecoderGuards.ValidateDimensions, which is the real ceiling.
                // This only catches the arithmetic overflowing on the way to an allocation that could not have been
                // satisfied anyway, so that it fails saying what was asked for rather than wrapping to something small
                // and plausible and reading off the end of it later.
                var total = (long) component.BlocksPerLineForMcu * component.BlocksPerColumnForMcu * 64;
                if (total > int.MaxValue)
                    throw new InvalidDataException(
                        $"JPEG component {component.Id} needs {total:N0} coefficients, more than a single array holds.");

                component.Coefficients = new short[(int) total];
            }
        }

        /// <summary>Finds the component a scan header names, by the identifier the frame header gave it.</summary>
        /// <param name="id">The component selector from the scan header.</param>
        internal JpegComponent FindComponent(int id)
        {
            foreach (var component in Components)
                if (component.Id == id)
                    return component;

            throw new InvalidDataException($"JPEG scan names component {id}, which its frame header never declared.");
        }

        /// <summary>Divides, rounding up. Both operands are positive everywhere this is used, so the usual trick is safe.</summary>
        private static int CeilDivide(int value, int divisor)
        {
            return (value + divisor - 1) / divisor;
        }
    }
}
