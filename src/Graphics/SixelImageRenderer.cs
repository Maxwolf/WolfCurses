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
