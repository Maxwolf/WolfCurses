// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

namespace WolfCurses.Graphics
{
    /// <summary>
    ///     Knobs that control how a <see cref="PixelBuffer" /> is turned into an ANSI string by
    ///     <see cref="AnsiImageRenderer" />. Sensible defaults make the common case ("show this picture, sized to fit
    ///     the console, right now") a one-liner; every field exists to override one aspect of that.
    /// </summary>
    public sealed class AnsiImageOptions
    {
        /// <summary>
        ///     Maximum width in terminal character columns. When null the renderer measures the console window (falling
        ///     back to 80 columns when there is no console, e.g. output redirected to a file).
        /// </summary>
        public int? MaxColumns { get; set; }

        /// <summary>
        ///     Maximum height in terminal character rows. When null the renderer measures the console window and
        ///     subtracts <see cref="RowMargin" /> (falling back to 24 rows when there is no console). Because each
        ///     character row shows two stacked image pixels, the effective vertical pixel budget is twice this.
        /// </summary>
        public int? MaxRows { get; set; }

        /// <summary>
        ///     How the image is scaled to the target area. Defaults to <see cref="AnsiImageFitEnum.Contain" /> (show the
        ///     whole image, letterboxed). Use <see cref="AnsiImageFitEnum.Cover" /> to fill the scene by cropping, or
        ///     <see cref="AnsiImageFitEnum.Stretch" /> to fill it by distorting.
        /// </summary>
        public AnsiImageFitEnum Fit { get; set; } = AnsiImageFitEnum.Contain;

        /// <summary>
        ///     Which part of the image is kept when <see cref="AnsiImageFitEnum.Cover" /> crops horizontally. Defaults to
        ///     <see cref="AnsiHorizontalAlignmentEnum.Center" />.
        /// </summary>
        public AnsiHorizontalAlignmentEnum HorizontalAlignment { get; set; } = AnsiHorizontalAlignmentEnum.Center;

        /// <summary>
        ///     Which part of the image is kept when <see cref="AnsiImageFitEnum.Cover" /> crops vertically. Defaults to
        ///     <see cref="AnsiVerticalAlignmentEnum.Middle" />.
        /// </summary>
        public AnsiVerticalAlignmentEnum VerticalAlignment { get; set; } = AnsiVerticalAlignmentEnum.Middle;

        /// <summary>
        ///     How much color fidelity to emit. Defaults to <see cref="AnsiColorModeEnum.Auto" />, which detects terminal
        ///     support at render time.
        /// </summary>
        public AnsiColorModeEnum ColorMode { get; set; } = AnsiColorModeEnum.Auto;

        /// <summary>
        ///     The height-to-width ratio of a single terminal character cell (how many times taller a cell is than it is
        ///     wide). Typical monospace console fonts are about twice as tall as they are wide, so the default of 2.0
        ///     makes the two stacked half-block pixels come out square and the picture keep its proportions. Lower or
        ///     raise it if a particular terminal/font stretches the result.
        /// </summary>
        public double CellAspectRatio { get; set; } = 2.0;

        /// <summary>
        ///     Alpha value at or above which a pixel counts as visible when there is no <see cref="BackgroundColor" />
        ///     to composite against. Pixels below it are left out entirely so the terminal background shows through,
        ///     which is how transparency is honored. Ignored when <see cref="BackgroundColor" /> is set.
        /// </summary>
        public byte AlphaThreshold { get; set; } = 128;

        /// <summary>
        ///     When set, every pixel is alpha-composited over this opaque color and the result is always fully drawn
        ///     (no cell is left transparent). Leave it null to instead let the terminal background show through wherever
        ///     the image is transparent — the usual choice for a TUI. Setting it is handy when you want the image to sit
        ///     on a known solid backdrop regardless of the terminal theme.
        /// </summary>
        public Rgb24? BackgroundColor { get; set; }

        /// <summary>
        ///     When true, each rendered row is left-padded with spaces so the image is centered within
        ///     <see cref="MaxColumns" />. When false the image is flush against the left margin.
        /// </summary>
        public bool CenterHorizontally { get; set; }

        /// <summary>
        ///     Number of character rows to leave free at the bottom of the console when <see cref="MaxRows" /> is being
        ///     auto-detected. One row of head-room keeps the final line from pushing the view up and scrolling. Ignored
        ///     when <see cref="MaxRows" /> is set explicitly.
        /// </summary>
        public int RowMargin { get; set; } = 1;
    }
}
