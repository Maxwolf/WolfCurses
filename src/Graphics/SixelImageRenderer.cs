// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/16/2026

using System;
using System.Text;

namespace WolfCurses.Graphics
{
    /// <summary>
    ///     An <see cref="IImageRenderer" /> that draws with sixel: real pixels, not colored character cells. Where
    ///     <see cref="HalfBlockImageRenderer" /> gets two pixels per character row, sixel paints the whole area the
    ///     picture occupies at the terminal's own resolution — on a typical 10x20-pixel cell that is roughly two hundred
    ///     pixels per cell instead of two, so a photograph looks like a photograph rather than an impression of one.
    ///     <para>
    ///         The cost is support: sixel is a DEC protocol that only some terminals implement (xterm built with sixel
    ///         support, foot, WezTerm, mlterm, contour, Windows Terminal 1.22+, and others; notably not most of the
    ///         rest). Nothing here detects that — a terminal without sixel prints the escape sequence as garbage — so an
    ///         application should install this deliberately rather than by default:
    ///         <code>
    ///             ImageRenderers.Default = new SixelImageRenderer();
    ///         </code>
    ///     </para>
    ///     <para>
    ///         Output follows <see cref="AnsiGraphics" />' contract for multi-row pictures, so it drops into a window's
    ///         rendered text like any other string and <see cref="ConsolePresenter" /> draws it correctly. As with every
    ///         renderer here, rendering is expensive (it resamples, builds a palette, and encodes) — render once and
    ///         cache the string rather than calling from <c>OnRenderWindow</c>.
    ///     </para>
    /// </summary>
    /// <remarks>
    ///     The encoding follows the DEC sixel specification. The format's shape — six-pixel vertical bands, one pass per
    ///     color per band, run-length compression — is common to every implementation of it; libsixel and node-sixel are
    ///     the usual references.
    /// </remarks>
    public sealed class SixelImageRenderer : IImageRenderer
    {
        /// <summary>The ASCII escape control character (0x1B) that begins every ANSI control sequence.</summary>
        private const char Escape = (char) 27;

        /// <summary>Pixel rows per sixel band: the format's fixed unit, one character encoding six stacked pixels.</summary>
        private const int BandHeight = 6;

        /// <summary>Sixel data characters start here, so that a band of six empty pixels is the printable '?'.</summary>
        private const int SixelDataOrigin = 0x3F;

        /// <summary>
        ///     Shortest run worth compressing. A run-length escape costs "!" plus the digits plus the character, so
        ///     spelling out three or fewer characters is never longer and often shorter.
        /// </summary>
        private const int MinRunToCompress = 4;

        /// <summary>
        ///     Initializes a new instance of the <see cref="SixelImageRenderer" /> class.
        /// </summary>
        /// <param name="cellPixelWidth">
        ///     How many pixels wide one terminal character cell is. Sixel works in pixels while the rest of the library
        ///     works in cells, so this is what converts between them. There is no reliable cross-terminal way to ask —
        ///     the query is an escape sequence whose reply would race the library's own input handling — so it is a knob
        ///     with a common default rather than something detected. Raise it if pictures come out too small.
        /// </param>
        /// <param name="cellPixelHeight">How many pixels tall one terminal character cell is; see above.</param>
        /// <param name="maxPaletteColors">
        ///     How many colors the picture may use, 1-256. Sixel is indexed and 256 registers is what terminals
        ///     reliably provide, so that is the default and the ceiling.
        /// </param>
        public SixelImageRenderer(int cellPixelWidth = 10, int cellPixelHeight = 20, int maxPaletteColors = 256)
        {
            if (cellPixelWidth < 1)
                throw new ArgumentOutOfRangeException(nameof(cellPixelWidth), cellPixelWidth,
                    "A character cell must be at least one pixel wide.");
            if (cellPixelHeight < 1)
                throw new ArgumentOutOfRangeException(nameof(cellPixelHeight), cellPixelHeight,
                    "A character cell must be at least one pixel tall.");
            if (maxPaletteColors < 1 || maxPaletteColors > 256)
                throw new ArgumentOutOfRangeException(nameof(maxPaletteColors), maxPaletteColors,
                    "A sixel palette holds between 1 and 256 colors.");

            CellPixelWidth = cellPixelWidth;
            CellPixelHeight = cellPixelHeight;
            MaxPaletteColors = maxPaletteColors;
        }

        /// <summary>Pixels across one terminal character cell.</summary>
        public int CellPixelWidth { get; }

        /// <summary>Pixels down one terminal character cell.</summary>
        public int CellPixelHeight { get; }

        /// <summary>Maximum number of colors the encoded picture may use.</summary>
        public int MaxPaletteColors { get; }

        /// <inheritdoc />
        public string Render(PixelBuffer image, AnsiImageOptions options = null)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));

            options ??= new AnsiImageOptions();

            // When the fit is an upscale on both axes, every output pixel is (under nearest-neighbour) a straight
            // copy of some source pixel — so the upscaled RGBA buffer never needs to exist. Quantize the ~100K
            // source pixels instead of the ~1.6M output pixels and stretch runs arithmetically while encoding.
            // Downscales keep the existing resize-first pipeline (area-averaging is what a downscale needs).
            var (cropX, cropY, cropWidth, cropHeight, targetWidth, targetHeight) =
                ImageFit.ResolveFit(image, options, CellPixelWidth, CellPixelHeight);
            if (targetWidth >= cropWidth && targetHeight >= cropHeight)
                return RenderUpscaled(image, options, cropX, cropY, cropWidth, cropHeight, targetWidth, targetHeight);

            var pixels = ImageFit.FitToPixels(image, options, CellPixelWidth, CellPixelHeight);
            var opaque = options.BackgroundColor.HasValue
                ? ImageFit.Flatten(pixels, options.BackgroundColor.Value)
                : pixels;

            // Transparency is the whole reason a threshold exists here: with a background color the picture is already
            // flattened onto it and every pixel is drawn, so nothing can be see-through.
            var alphaThreshold = options.BackgroundColor.HasValue ? (byte) 0 : options.AlphaThreshold;
            var palette = ColorPalette.Build(opaque, alphaThreshold, MaxPaletteColors);

            var rows = Math.Max(1, (opaque.Height + CellPixelHeight - 1) / CellPixelHeight);
            var payload = Encode(opaque, palette, alphaThreshold);
            return AnsiGraphics.PayloadBlock(payload, rows);
        }

        /// <summary>
        ///     The upscale path: composite, threshold and palette at SOURCE resolution, then encode the enlarged
        ///     picture directly from a per-source-pixel palette index map using precomputed nearest-neighbour
        ///     coordinate mappings. Pointwise operations (alpha threshold, background flatten) commute with
        ///     nearest-neighbour enlargement, so doing them before the "scale" is exact; the only difference from the
        ///     legacy path is the resampling filter itself (nearest-neighbour instead of box).
        /// </summary>
        private string RenderUpscaled(PixelBuffer image, AnsiImageOptions options, int cropX, int cropY,
            int cropWidth, int cropHeight, int targetWidth, int targetHeight)
        {
            var source = cropX == 0 && cropY == 0 && cropWidth == image.Width && cropHeight == image.Height
                ? image
                : image.Crop(cropX, cropY, cropWidth, cropHeight);
            var opaque = options.BackgroundColor.HasValue
                ? ImageFit.Flatten(source, options.BackgroundColor.Value)
                : source;

            var alphaThreshold = options.BackgroundColor.HasValue ? (byte) 0 : options.AlphaThreshold;
            var palette = ColorPalette.Build(opaque, alphaThreshold, MaxPaletteColors);

            var rows = Math.Max(1, (targetHeight + CellPixelHeight - 1) / CellPixelHeight);
            var payload = EncodeUpscaled(opaque, palette, alphaThreshold, targetWidth, targetHeight);
            return AnsiGraphics.PayloadBlock(payload, rows);
        }

        /// <summary>
        ///     Encodes an enlarged copy of <paramref name="source" /> without ever materializing it. Consecutive output
        ///     columns that read the same source column form a group whose run length is known arithmetically, so a
        ///     band is walked per source column rather than per output pixel; output rows that read the same source
        ///     row are merged into one lookup carrying several mask bits.
        /// </summary>
        private string EncodeUpscaled(PixelBuffer source, ColorPalette palette, byte alphaThreshold, int outWidth,
            int outHeight)
        {
            var w = source.Width;
            var h = source.Height;

            // Palette index per source pixel, -1 meaning "below the alpha threshold, never drawn". One dictionary
            // lookup per SOURCE pixel is the whole point: the legacy path did one per OUTPUT pixel.
            var srcIndex = new short[w * h];
            var data = source.Data;
            for (int p = 0, o = 0; p < srcIndex.Length; p++, o += 4)
            {
                srcIndex[p] = data[o + 3] < alphaThreshold
                    ? (short) -1
                    : (short) palette.IndexOf(ColorPalette.Pack(data[o], data[o + 1], data[o + 2]));
            }

            // Center-based nearest-neighbour mapping, exact identity when the sizes match.
            var srcRow = new int[outHeight];
            for (var dy = 0; dy < outHeight; dy++)
            {
                var sy = (int) ((2L * dy + 1) * h / (2L * outHeight));
                srcRow[dy] = sy >= h ? h - 1 : sy;
            }

            // Group output columns by shared source column: each group's mask is constant across its width, so the
            // group's length multiplies into the run instead of being discovered by scanning output pixels.
            var groupSrc = new int[outWidth];
            var groupLen = new int[outWidth];
            var groupStart = new int[outWidth];
            var groups = 0;
            var prevSx = -1;
            for (var dx = 0; dx < outWidth; dx++)
            {
                var sx = (int) ((2L * dx + 1) * w / (2L * outWidth));
                if (sx >= w)
                    sx = w - 1;
                if (sx != prevSx)
                {
                    groupSrc[groups] = sx;
                    groupStart[groups] = dx;
                    groupLen[groups] = 0;
                    groups++;
                    prevSx = sx;
                }

                groupLen[groups - 1]++;
            }

            var builder = new StringBuilder(outWidth * outHeight / 3 + 2048);
            builder.Append(Escape).Append("P0;1;0q");
            builder.Append("\"1;1;").Append(outWidth).Append(';').Append(outHeight);

            for (var i = 0; i < palette.Colors.Length; i++)
            {
                var color = palette.Colors[i];
                builder.Append('#').Append(i).Append(";2;")
                    .Append(ToPercent(color.R)).Append(';')
                    .Append(ToPercent(color.G)).Append(';')
                    .Append(ToPercent(color.B));
            }

            EncodeUpscaledBands(srcIndex, srcRow, groupSrc, groupLen, groupStart, groups, w, outHeight,
                palette.Colors.Length, builder);

            builder.Append(Escape).Append('\\');
            return builder.ToString();
        }

        /// <summary>Emits every six-output-row band of the enlarged picture from the source index map.</summary>
        private static void EncodeUpscaledBands(short[] srcIndex, int[] srcRow, int[] groupSrc, int[] groupLen,
            int[] groupStart, int groups, int sourceWidth, int outHeight, int colorCount, StringBuilder builder)
        {
            var masks = new byte[colorCount][];
            var firstGroup = new int[colorCount];
            var lastGroup = new int[colorCount];

            // Distinct source rows inside the current band, with the output-row bits each covers: a 3x vertical
            // upscale makes three output rows read one source row, which becomes one lookup carrying three bits.
            var rowOffsets = new int[BandHeight];
            var rowBits = new byte[BandHeight];

            for (var top = 0; top < outHeight; top += BandHeight)
            {
                Array.Clear(masks, 0, masks.Length);
                var bandRows = Math.Min(BandHeight, outHeight - top);

                var distinct = 0;
                var prevSy = -1;
                for (var r = 0; r < bandRows; r++)
                {
                    var sy = srcRow[top + r];
                    if (sy != prevSy)
                    {
                        rowOffsets[distinct] = sy * sourceWidth;
                        rowBits[distinct] = 0;
                        distinct++;
                        prevSy = sy;
                    }

                    rowBits[distinct - 1] |= (byte) (1 << r);
                }

                for (var g = 0; g < groups; g++)
                {
                    var sx = groupSrc[g];
                    for (var d = 0; d < distinct; d++)
                    {
                        var index = srcIndex[rowOffsets[d] + sx];
                        if (index < 0)
                            continue; // Below the alpha threshold: belongs to no color's mask, stays transparent.

                        var mask = masks[index];
                        if (mask == null)
                        {
                            mask = masks[index] = new byte[groups];
                            firstGroup[index] = g;
                        }

                        mask[g] |= rowBits[d];
                        lastGroup[index] = g;
                    }
                }

                var first = true;
                for (var index = 0; index < masks.Length; index++)
                {
                    var mask = masks[index];
                    if (mask == null)
                        continue; // This color does not appear in this band at all.

                    if (!first)
                        builder.Append('$');

                    builder.Append('#').Append(index);

                    // Everything left of the color's first appearance is one arithmetic run of empty masks; groups
                    // after its last appearance are trailing zeros and (as in the legacy encoder) simply dropped.
                    var lead = groupStart[firstGroup[index]];
                    if (lead > 0)
                        AppendRun(0, lead, builder);

                    var runMask = mask[firstGroup[index]];
                    var runLen = groupLen[firstGroup[index]];
                    for (var g = firstGroup[index] + 1; g <= lastGroup[index]; g++)
                    {
                        if (mask[g] == runMask)
                        {
                            runLen += groupLen[g];
                            continue;
                        }

                        AppendRun(runMask, runLen, builder);
                        runMask = mask[g];
                        runLen = groupLen[g];
                    }

                    AppendRun(runMask, runLen, builder);
                    first = false;
                }

                if (top + BandHeight < outHeight)
                    builder.Append('-'); // Graphics newline: down to the next band. Never after the last one.
            }
        }

        /// <summary>
        ///     Encodes the image as a complete sixel escape sequence.
        ///     <para>
        ///         Sixel divides the picture into bands six pixels tall and draws one color at a time: within a band,
        ///         each character carries six stacked pixels as a bitmask of which of them belong to the color currently
        ///         selected. So a band is walked once per color that appears in it, returning to the left margin between
        ///         passes, and the passes overlay to make the finished band.
        ///     </para>
        /// </summary>
        private string Encode(PixelBuffer image, ColorPalette palette, byte alphaThreshold)
        {
            var builder = new StringBuilder(image.Width * image.Height / 4);

            // P2 = 1 means "leave undrawn pixels alone", which is what makes transparency work: pixels below the alpha
            // threshold are simply never emitted, so whatever the terminal already had shows through.
            builder.Append(Escape).Append("P0;1;0q");
            builder.Append("\"1;1;").Append(image.Width).Append(';').Append(image.Height);

            for (var i = 0; i < palette.Colors.Length; i++)
            {
                var color = palette.Colors[i];
                builder.Append('#').Append(i).Append(";2;")
                    .Append(ToPercent(color.R)).Append(';')
                    .Append(ToPercent(color.G)).Append(';')
                    .Append(ToPercent(color.B));
            }

            var band = new byte[palette.Colors.Length][];
            for (var top = 0; top < image.Height; top += BandHeight)
            {
                EncodeBand(image, palette, alphaThreshold, top, band, builder);
                if (top + BandHeight < image.Height)
                    builder.Append('-'); // Graphics newline: down to the next band. Never after the last one.
            }

            builder.Append(Escape).Append('\\');
            return builder.ToString();
        }

        /// <summary>
        ///     Emits one six-pixel-tall band: for every color present in it, the run-length compressed bitmasks that
        ///     place that color's pixels across the band's width.
        /// </summary>
        private void EncodeBand(PixelBuffer image, ColorPalette palette, byte alphaThreshold, int top, byte[][] band,
            StringBuilder builder)
        {
            Array.Clear(band, 0, band.Length);
            var height = Math.Min(BandHeight, image.Height - top);

            for (var x = 0; x < image.Width; x++)
            {
                for (var row = 0; row < height; row++)
                {
                    var pixel = image.GetPixel(x, top + row);
                    if (pixel.A < alphaThreshold)
                        continue; // Undrawn, so it belongs to no color's mask and stays transparent.

                    var index = palette.IndexOf(ColorPalette.Pack(pixel.R, pixel.G, pixel.B));
                    band[index] ??= new byte[image.Width];
                    band[index][x] |= (byte) (1 << row);
                }
            }

            var first = true;
            for (var index = 0; index < band.Length; index++)
            {
                if (band[index] == null)
                    continue; // This color does not appear in this band at all.

                // A graphics carriage return returns to the left margin so the next color overlays this same band. It
                // goes strictly *between* passes: a trailing one is known to upset some terminals' rendering.
                if (!first)
                    builder.Append('$');

                builder.Append('#').Append(index);
                AppendRuns(band[index], builder);
                first = false;
            }
        }

        /// <summary>
        ///     Appends one color's row of six-pixel bitmasks, run-length compressed. Trailing empty masks are dropped:
        ///     nothing needs to be said about pixels this color does not reach.
        /// </summary>
        private static void AppendRuns(byte[] masks, StringBuilder builder)
        {
            var end = masks.Length;
            while (end > 0 && masks[end - 1] == 0)
                end--;

            var run = 0;
            for (var x = 0; x < end; x++)
            {
                run++;
                if (x + 1 < end && masks[x + 1] == masks[x])
                    continue;

                AppendRun(masks[x], run, builder);
                run = 0;
            }
        }

        /// <summary>Appends a single run of one repeated bitmask, compressed when that is actually shorter.</summary>
        private static void AppendRun(byte mask, int run, StringBuilder builder)
        {
            var data = (char) (SixelDataOrigin + mask);

            if (run >= MinRunToCompress)
            {
                builder.Append('!').Append(run).Append(data);
                return;
            }

            for (var i = 0; i < run; i++)
                builder.Append(data);
        }

        /// <summary>
        ///     Converts an 8-bit channel to the 0-100 scale sixel color registers use. Sixel predates 24-bit color and
        ///     specifies its components as percentages, so this is lossy by design — a hair over 2% per step.
        /// </summary>
        private static int ToPercent(byte channel)
        {
            return (int) Math.Round(channel * 100.0 / 255.0, MidpointRounding.AwayFromZero);
        }
    }
}
