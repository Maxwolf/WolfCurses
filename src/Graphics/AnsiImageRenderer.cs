// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System;
using System.Text;

namespace WolfCurses.Graphics
{
    /// <summary>
    ///     Turns a decoded <see cref="PixelBuffer" /> into a string of text plus ANSI escape sequences that draws the
    ///     image in a terminal. The technique is the "half block" trick: each character cell prints the
    ///     <c>▀</c> (upper half block) glyph with its foreground color set to the top pixel and its background color set
    ///     to the bottom pixel, so a single row of characters shows two rows of pixels. That doubles the vertical
    ///     resolution and, with a normal 2:1 console font, makes the pixels come out square. This class is deliberately
    ///     free of any image-decoding dependency so it can be driven with synthetic <see cref="PixelBuffer" />s in tests.
    /// </summary>
    public static class AnsiImageRenderer
    {
        private const char UpperHalfBlock = '▀'; // ▀ ink on top half (foreground), bottom half is background
        private const char LowerHalfBlock = '▄'; // ▄ ink on bottom half (foreground), top half is background

        /// <summary>The ASCII escape control character (0x1B) that begins every ANSI control sequence.</summary>
        private const char Escape = (char) 27;

        private static readonly string _reset = Escape + "[0m";
        private static readonly string _defaultBackground = Escape + "[49m";

        /// <summary>Brightness ramp for <see cref="AnsiColorModeEnum.None" />, ordered darkest to lightest.</summary>
        private const string AsciiRamp = " .:-=+*#%@";

        /// <summary>
        ///     Renders the image to an ANSI string sized and colored according to <paramref name="options" />.
        /// </summary>
        /// <param name="image">The decoded image to draw.</param>
        /// <param name="options">Rendering options, or null to use the defaults (auto-fit the console, auto color).</param>
        /// <returns>
        ///     A multi-line string whose rows are separated by the platform newline (<c>Environment.NewLine</c>), to
        ///     match the rest of the library so the result drops cleanly into a window's rendered text. Each colored
        ///     line ends with a reset so the terminal state does not leak into whatever is printed next; the string has
        ///     no trailing newline.
        /// </returns>
        public static string Render(PixelBuffer image, AnsiImageOptions options = null)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));

            options ??= new AnsiImageOptions();

            var mode = options.ColorMode == AnsiColorModeEnum.Auto
                ? AnsiConsole.DetectColorMode()
                : options.ColorMode;

            int cols, rows;
            PixelBuffer grid;
            switch (options.Fit)
            {
                case AnsiImageFitEnum.Stretch:
                    // Fill the whole area exactly, ignoring aspect ratio (the picture is distorted to match).
                    (cols, rows) = ResolveBounds(options);
                    grid = image.Resize(cols, rows * 2);
                    break;

                case AnsiImageFitEnum.Cover:
                    // Fill the whole area preserving aspect ratio, cropping whatever spills over.
                    (cols, rows) = ResolveBounds(options);
                    grid = CoverResample(image, cols, rows * 2, options);
                    break;

                default: // Contain and ScaleDown both scale the whole image to fit inside the area.
                    (cols, rows) = ComputeTargetCells(image.Width, image.Height, options);
                    grid = image.Resize(cols, rows * 2);
                    break;
            }

            return mode == AnsiColorModeEnum.None
                ? RenderAscii(grid, cols, rows, options)
                : RenderHalfBlocks(grid, cols, rows, mode, options);
        }

        /// <summary>
        ///     Resolves the target area in character cells from the options, measuring the console window when a bound
        ///     is left unset (and keeping <see cref="AnsiImageOptions.RowMargin" /> free at the bottom in that case).
        /// </summary>
        internal static (int columns, int rows) ResolveBounds(AnsiImageOptions options)
        {
            var maxColumns = Math.Max(1, options.MaxColumns ?? AnsiConsole.SafeWindowWidth());
            var maxRows = Math.Max(1, options.MaxRows ?? Math.Max(1, AnsiConsole.SafeWindowHeight() - options.RowMargin));
            return (maxColumns, maxRows);
        }

        /// <summary>
        ///     Works out how many character columns and rows the image should occupy so that it fits inside the
        ///     configured (or console-derived) bounds while preserving its aspect ratio. Each character row holds two
        ///     stacked pixels, and the console cell aspect ratio is folded in so the picture is not squashed.
        /// </summary>
        internal static (int columns, int rows) ComputeTargetCells(int imageWidth, int imageHeight, AnsiImageOptions options)
        {
            var (maxColumns, maxRows) = ResolveBounds(options);

            if (options.Fit == AnsiImageFitEnum.ScaleDown)
            {
                // Never enlarge past the source's own pixels. Two pixels stack per character row, so the row budget
                // caps at half the image height; the column budget caps at the image width.
                maxColumns = Math.Min(maxColumns, imageWidth);
                maxRows = Math.Min(maxRows, Math.Max(1, imageHeight / 2));
            }

            var cellAspect = options.CellAspectRatio > 0 && double.IsFinite(options.CellAspectRatio)
                ? options.CellAspectRatio
                : 2.0;

            // gridAspect is the target ratio of (columns) to (pixel rows). A cell is `cellAspect` times as tall as it
            // is wide and holds two pixels, so each pixel is cellAspect/2 as tall as a column is wide; dividing the
            // image aspect by that factor gives the column-to-pixel-row ratio that keeps proportions on screen.
            var gridAspect = imageWidth / (double) imageHeight * (cellAspect / 2.0);
            if (!double.IsFinite(gridAspect) || gridAspect <= 0)
                gridAspect = 1.0;

            var maxPixelRows = maxRows * 2;

            // First assume we fill the available width, then shrink to fit the height if that overflows. The overflow
            // test is done in floating point BEFORE any cast to int, so an extreme (but finite) aspect cannot produce
            // a pixel-row count that wraps the cast; both branches then cast a value already bounded to a valid range.
            var columns = maxColumns;
            var pixelRowsExact = columns / gridAspect;
            int pixelRows;
            if (pixelRowsExact > maxPixelRows)
            {
                pixelRows = maxPixelRows;
                columns = (int) Math.Round(maxPixelRows * gridAspect, MidpointRounding.AwayFromZero);
            }
            else
            {
                pixelRows = (int) Math.Round(pixelRowsExact, MidpointRounding.AwayFromZero);
            }

            columns = Clamp(columns, 1, maxColumns);
            var rows = Clamp((int) Math.Round(pixelRows / 2.0, MidpointRounding.AwayFromZero), 1, maxRows);
            return (columns, rows);
        }

        /// <summary>Draws the grid using half-block glyphs and per-cell foreground/background colors.</summary>
        private static string RenderHalfBlocks(PixelBuffer grid, int cols, int rows, AnsiColorModeEnum mode, AnsiImageOptions options)
        {
            var sb = new StringBuilder(rows * cols * 8);
            var leftPad = options.CenterHorizontally
                ? Math.Max(0, ((options.MaxColumns ?? AnsiConsole.SafeWindowWidth()) - cols) / 2)
                : 0;

            for (var r = 0; r < rows; r++)
            {
                if (r > 0)
                    sb.Append(Environment.NewLine);

                if (leftPad > 0)
                    sb.Append(' ', leftPad);

                // Foreground/background escape currently in effect on this line. Null means "terminal default", which
                // is the state immediately after the reset that ends the previous line.
                string currentFg = null;
                string currentBg = null;

                for (var c = 0; c < cols; c++)
                {
                    var top = Resolve(grid.GetPixel(c, r * 2), options);
                    var bottom = Resolve(grid.GetPixel(c, r * 2 + 1), options);

                    char glyph;
                    Rgb24 fgColor = default;
                    var fgMatters = true;
                    Rgb24? bgColor; // null => terminal default background shows through

                    if (top.Visible && bottom.Visible)
                    {
                        glyph = UpperHalfBlock;
                        fgColor = top.Color;
                        bgColor = bottom.Color;
                    }
                    else if (top.Visible)
                    {
                        glyph = UpperHalfBlock;
                        fgColor = top.Color;
                        bgColor = null;
                    }
                    else if (bottom.Visible)
                    {
                        glyph = LowerHalfBlock;
                        fgColor = bottom.Color;
                        bgColor = null;
                    }
                    else
                    {
                        // Both pixels transparent: a plain space over the default background. The foreground color is
                        // irrelevant to a space, so leave it untouched to avoid emitting a needless escape.
                        glyph = ' ';
                        fgMatters = false;
                        bgColor = null;
                    }

                    if (fgMatters)
                    {
                        var fgEsc = ForegroundEscape(fgColor, mode);
                        if (!string.Equals(currentFg, fgEsc, StringComparison.Ordinal))
                        {
                            sb.Append(fgEsc);
                            currentFg = fgEsc;
                        }
                    }

                    var bgEsc = bgColor.HasValue ? BackgroundEscape(bgColor.Value, mode) : null;
                    if (!string.Equals(currentBg, bgEsc, StringComparison.Ordinal))
                    {
                        sb.Append(bgEsc ?? _defaultBackground);
                        currentBg = bgEsc;
                    }

                    sb.Append(glyph);
                }

                // Return the terminal to its default state at the end of every line so trailing content and the next
                // line's left padding are never tinted.
                sb.Append(_reset);
            }

            return sb.ToString();
        }

        /// <summary>Draws the grid as brightness-shaded ASCII for terminals that cannot show color at all.</summary>
        private static string RenderAscii(PixelBuffer grid, int cols, int rows, AnsiImageOptions options)
        {
            var sb = new StringBuilder(rows * (cols + 1));
            var leftPad = options.CenterHorizontally
                ? Math.Max(0, ((options.MaxColumns ?? AnsiConsole.SafeWindowWidth()) - cols) / 2)
                : 0;

            for (var r = 0; r < rows; r++)
            {
                if (r > 0)
                    sb.Append(Environment.NewLine);
                if (leftPad > 0)
                    sb.Append(' ', leftPad);

                for (var c = 0; c < cols; c++)
                {
                    var top = Resolve(grid.GetPixel(c, r * 2), options);
                    var bottom = Resolve(grid.GetPixel(c, r * 2 + 1), options);

                    // Average the luminance of whichever of the two stacked pixels are visible.
                    var count = 0;
                    var sum = 0;
                    if (top.Visible)
                    {
                        sum += Luma(top.Color);
                        count++;
                    }

                    if (bottom.Visible)
                    {
                        sum += Luma(bottom.Color);
                        count++;
                    }

                    if (count == 0)
                    {
                        sb.Append(' ');
                        continue;
                    }

                    var brightness = sum / count;
                    var index = brightness * (AsciiRamp.Length - 1) / 255;
                    sb.Append(AsciiRamp[index]);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        ///     Resolves a raw source pixel into "is it visible, and if so what opaque color is it" according to the
        ///     transparency options: either composited over a configured background, or thresholded so the terminal
        ///     background shows through where the image is transparent.
        /// </summary>
        private static (bool Visible, Rgb24 Color) Resolve(Rgba32 pixel, AnsiImageOptions options)
        {
            if (options.BackgroundColor.HasValue)
            {
                if (pixel.A == 255)
                    return (true, new Rgb24(pixel.R, pixel.G, pixel.B));

                var bg = options.BackgroundColor.Value;
                if (pixel.A == 0)
                    return (true, bg);

                // Standard "source over" compositing onto the opaque backdrop.
                var a = pixel.A;
                var inv = 255 - a;
                var r = (byte) ((pixel.R * a + bg.R * inv + 127) / 255);
                var g = (byte) ((pixel.G * a + bg.G * inv + 127) / 255);
                var b = (byte) ((pixel.B * a + bg.B * inv + 127) / 255);
                return (true, new Rgb24(r, g, b));
            }

            return pixel.A >= options.AlphaThreshold
                ? (true, new Rgb24(pixel.R, pixel.G, pixel.B))
                : (false, default);
        }

        /// <summary>Builds the escape that sets the foreground color for the given color mode.</summary>
        private static string ForegroundEscape(Rgb24 c, AnsiColorModeEnum mode)
        {
            switch (mode)
            {
                case AnsiColorModeEnum.Palette256:
                    return $"{Escape}[38;5;{Ansi256.FromRgb(c.R, c.G, c.B)}m";
                case AnsiColorModeEnum.Grayscale:
                    return $"{Escape}[38;5;{Ansi256.GrayFromRgb(c.R, c.G, c.B)}m";
                default:
                    return $"{Escape}[38;2;{c.R};{c.G};{c.B}m";
            }
        }

        /// <summary>Builds the escape that sets the background color for the given color mode.</summary>
        private static string BackgroundEscape(Rgb24 c, AnsiColorModeEnum mode)
        {
            switch (mode)
            {
                case AnsiColorModeEnum.Palette256:
                    return $"{Escape}[48;5;{Ansi256.FromRgb(c.R, c.G, c.B)}m";
                case AnsiColorModeEnum.Grayscale:
                    return $"{Escape}[48;5;{Ansi256.GrayFromRgb(c.R, c.G, c.B)}m";
                default:
                    return $"{Escape}[48;2;{c.R};{c.G};{c.B}m";
            }
        }

        /// <summary>
        ///     Produces the pixel grid for <see cref="AnsiImageFitEnum.Cover" />: crops the source to the sub-rectangle
        ///     whose proportions match the target area (anchored by the alignment options) and scales that crop to fill
        ///     the area exactly, so the whole scene is covered with no distortion.
        /// </summary>
        private static PixelBuffer CoverResample(PixelBuffer image, int areaColumns, int areaPixelRows, AnsiImageOptions options)
        {
            var cellAspect = options.CellAspectRatio > 0 && double.IsFinite(options.CellAspectRatio)
                ? options.CellAspectRatio
                : 2.0;

            var gridAspect = image.Width / (double) image.Height * (cellAspect / 2.0);
            if (!double.IsFinite(gridAspect) || gridAspect <= 0)
                gridAspect = 1.0;

            var areaGridAspect = areaColumns / (double) areaPixelRows;

            int cropWidth, cropHeight;
            if (gridAspect > areaGridAspect)
            {
                // Source is proportionally wider than the area: keep the full height, crop the sides.
                cropHeight = image.Height;
                cropWidth = (int) Math.Round(image.Width * (areaGridAspect / gridAspect), MidpointRounding.AwayFromZero);
            }
            else
            {
                // Source is proportionally taller than the area: keep the full width, crop top and bottom.
                cropWidth = image.Width;
                cropHeight = (int) Math.Round(image.Height * (gridAspect / areaGridAspect), MidpointRounding.AwayFromZero);
            }

            cropWidth = Clamp(cropWidth, 1, image.Width);
            cropHeight = Clamp(cropHeight, 1, image.Height);

            var cropX = AnchorOffset(options.HorizontalAlignment, image.Width - cropWidth);
            var cropY = AnchorOffset(options.VerticalAlignment, image.Height - cropHeight);

            return image.Crop(cropX, cropY, cropWidth, cropHeight).Resize(areaColumns, areaPixelRows);
        }

        /// <summary>Left/center/right offset of an item with the given slack (unused space) around it.</summary>
        private static int AnchorOffset(AnsiHorizontalAlignmentEnum alignment, int slack)
        {
            switch (alignment)
            {
                case AnsiHorizontalAlignmentEnum.Left: return 0;
                case AnsiHorizontalAlignmentEnum.Right: return slack;
                default: return slack / 2;
            }
        }

        /// <summary>Top/middle/bottom offset of an item with the given slack (unused space) around it.</summary>
        private static int AnchorOffset(AnsiVerticalAlignmentEnum alignment, int slack)
        {
            switch (alignment)
            {
                case AnsiVerticalAlignmentEnum.Top: return 0;
                case AnsiVerticalAlignmentEnum.Bottom: return slack;
                default: return slack / 2;
            }
        }

        /// <summary>Rec. 601 luminance of a color, 0-255.</summary>
        private static int Luma(Rgb24 c)
        {
            return (c.R * 299 + c.G * 587 + c.B * 114) / 1000;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
