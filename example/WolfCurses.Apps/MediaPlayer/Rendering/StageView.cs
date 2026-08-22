// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;
using WolfCurses.Graphics;
using WolfCurses.Window.Control;

namespace WolfCurses.Apps.MediaPlayer
{
    /// <summary>
    ///     The picture area: a frame of video, a set of bars for something with no picture in it, or a page telling
    ///     you what this is and what was found on the machine.
    ///     <para>
    ///         <b>Encoding a frame is expensive and rendering happens a thousand times a second</b>, so nothing
    ///         here is called per frame drawn. The dialog encodes a picture only when the picture has changed and
    ///         keeps the string; this is the same lesson the demo's image screens record, arriving in the one place
    ///         where the cost is thirty times a second rather than once.
    ///     </para>
    ///     <para>
    ///         <b>Rows are always exactly the height asked for.</b> A stage that changed height as a file opened
    ///         would move the scrub bar and the key hints under it, and the row a click lands on is measured from
    ///         the top.
    ///     </para>
    /// </summary>
    internal static class StageView
    {
        /// <summary>How wide one bar of the visualizer is drawn.</summary>
        private const int BarWidth = 2;

        /// <summary>How many blank columns separate one bar from the next.</summary>
        private const int BarGap = 1;

        /// <summary>
        ///     How big a picture should arrive for the stage, in pixels, given what will be drawing it.
        ///     <para>
        ///         <b>Asked of the renderer rather than assumed</b>, which is what
        ///         <see cref="IImageRenderer.CellPixelWidth" /> is for: half blocks put one pixel across and two
        ///         down in a cell, a true-pixel renderer ten and twenty. Getting this from the renderer means the
        ///         source produces exactly what is wanted and nothing is ever resampled here, which is the whole
        ///         difference between thirty frames a second and three.
        ///     </para>
        /// </summary>
        /// <param name="renderer">Whichever renderer the terminal got.</param>
        /// <param name="columns">How many columns the stage has.</param>
        /// <param name="rows">How many rows the stage has.</param>
        /// <param name="quality">
        ///     One for every pixel the renderer can use, two for half of them, and so on.
        ///     <para>
        ///         <b>This changes the resolution and not the size on screen.</b> The picture still covers the same
        ///         columns and rows; there are simply fewer pixels in it, and the true-pixel renderers stretch what
        ///         they are given rather than resampling it - sixel builds its palette from the source pixels and
        ///         widens the runs arithmetically, kitty hands the terminal the small buffer with the cell
        ///         rectangle it should fill. Measured on a 4K source into a 78x16 stage: full resolution is 89ms a
        ///         frame and eleven a second, half is 35ms and twenty-nine, a third is 26ms and thirty-nine. Same
        ///         picture, same place, three times the frame rate.
        ///     </para>
        /// </param>
        /// <returns>The pixel size to ask for.</returns>
        public static (int Width, int Height) PixelSize(IImageRenderer renderer, int columns, int rows,
            int quality = 1)
        {
            var divisor = Math.Max(1, quality);

            var width = Math.Max(2, columns) * Math.Max(1, renderer.CellPixelWidth) / divisor;
            var height = Math.Max(2, rows) * Math.Max(1, renderer.CellPixelHeight) / divisor;

            width = Math.Max(2, width);
            height = Math.Max(2, height);

            // Every codec in the world wants even numbers, and an odd one is refused outright by some of them.
            return (width - width % 2, height - height % 2);
        }

        /// <summary>Turns a decoded frame into the rows that draw it.</summary>
        /// <param name="frame">The frame.</param>
        /// <param name="width">How many columns the stage has.</param>
        /// <param name="rows">How many rows the stage has.</param>
        /// <returns>The stage's rows.</returns>
        public static IReadOnlyList<string> Picture(PixelBuffer frame, int width, int rows)
        {
            if (frame == null)
                return Blank(width, rows);

            var drawn = ImageRenderers.Default.Render(frame, new AnsiImageOptions
            {
                MaxColumns = width,
                MaxRows = rows,
                Fit = AnsiImageFitEnum.Contain,

                // No margin and no centring: the frame arrived letterboxed to the exact shape of the stage, so
                // anything added here would push it off the bottom.
                RowMargin = 0,
                CenterHorizontally = false
            });

            return FitRows(drawn.Split('\n'), width, rows);
        }

        /// <summary>
        ///     Draws the bars for something with no picture in it.
        /// </summary>
        /// <param name="chart">The chart, already styled.</param>
        /// <param name="bands">The band heights, from zero to one.</param>
        /// <param name="peaks">Where each band has recently been.</param>
        /// <param name="caption">A line under the bars saying what is playing.</param>
        /// <param name="width">How many columns the stage has.</param>
        /// <param name="rows">How many rows the stage has.</param>
        /// <returns>The stage's rows.</returns>
        public static IReadOnlyList<string> Bars(ColumnChart chart, double[] bands, double[] peaks, string caption,
            int width, int rows)
        {
            chart.Height = Math.Max(1, rows - 3);
            chart.ColumnWidth = BarWidth;
            chart.Gap = BarGap;

            var drawn = chart.Render(bands, peaks);
            var barsWidth = chart.WidthFor(bands.Length);
            var left = Math.Max(0, (width - barsWidth) / 2);

            var lines = new List<string>(rows) {Row(string.Empty, width)};

            foreach (var bar in drawn)
                lines.Add(PlayerChrome.Fill(DosTheme.Field.Apply(new string(' ', left)) + bar, width));

            lines.Add(Row(string.Empty, width));
            lines.Add(Row(Centre(caption, width), width));

            return FitRows(lines.ToArray(), width, rows);
        }

        /// <summary>
        ///     The page shown when nothing is open, which is also where the machine's own capabilities are
        ///     reported. Discoverability rather than decoration: this screen can fail in three separate ways
        ///     depending on what is installed and what the terminal can do, and a blank rectangle explains none of
        ///     them.
        /// </summary>
        /// <param name="width">How many columns the stage has.</param>
        /// <param name="rows">How many rows the stage has.</param>
        /// <returns>The stage's rows.</returns>
        public static IReadOnlyList<string> Idle(int width, int rows)
        {
            var lines = new List<string>
            {
                string.Empty,
                Centre("WolfCurses Media Player", width),
                string.Empty,
                Centre("F3 opens a file.  F7 plays a test pattern.  F8 plays a test tone.", width),
                Centre("SPACE plays and pauses, the arrows seek, and the bar can be clicked.", width),
                string.Empty
            };

            foreach (var line in FfmpegTools.Report())
                lines.Add("  " + line);

            lines.Add(string.Empty);
            lines.Add("  " + PictureReport());

            var rendered = new List<string>(rows);

            foreach (var line in lines)
                rendered.Add(Row(line, width));

            return FitRows(rendered.ToArray(), width, rows);
        }

        /// <summary>What this terminal will make of a picture, in one line.</summary>
        /// <returns>The report.</returns>
        public static string PictureReport()
        {
            if (!AnsiConsole.SupportsPictures())
                return "Pictures  not here. Sound still plays and the bars still move.";

            var renderer = ImageRenderers.Default;

            return renderer.DrawsTruePixels
                ? "Pictures  " + renderer.Name + ", which is real pixels."
                : "Pictures  " + renderer.Name + ", which is character cells.";
        }

        /// <summary>Pads or clips a set of rows to exactly the height wanted.</summary>
        /// <param name="rows">What was drawn.</param>
        /// <param name="width">How many columns the stage has.</param>
        /// <param name="height">How many rows the stage has.</param>
        /// <returns>Exactly that many rows.</returns>
        private static IReadOnlyList<string> FitRows(IReadOnlyList<string> rows, int width, int height)
        {
            var fitted = new List<string>(height);

            for (var i = 0; i < height; i++)
            {
                var row = i < rows.Count ? rows[i].TrimEnd('\r') : string.Empty;

                fitted.Add(AnsiGraphics.IsPictureRow(row) ? row : Row(row, width));
            }

            return fitted;
        }

        /// <summary>One row of the stage, painted and padded to the full width.</summary>
        /// <param name="content">What goes on it.</param>
        /// <param name="width">The console width.</param>
        /// <returns>The row.</returns>
        private static string Row(string content, int width)
        {
            return DosTheme.Field.Apply(AnsiText.Fit(content, width));
        }

        /// <summary>Blank rows, for a stage with nothing on it yet.</summary>
        /// <param name="width">The console width.</param>
        /// <param name="rows">How many rows.</param>
        /// <returns>The rows.</returns>
        private static IReadOnlyList<string> Blank(int width, int rows)
        {
            var blank = new List<string>(rows);

            for (var i = 0; i < rows; i++)
                blank.Add(Row(string.Empty, width));

            return blank;
        }

        /// <summary>Text with spaces in front of it so it sits in the middle.</summary>
        /// <param name="text">The text.</param>
        /// <param name="width">The console width.</param>
        /// <returns>The padded text.</returns>
        private static string Centre(string text, int width)
        {
            var left = Math.Max(0, (width - (text?.Length ?? 0)) / 2);

            return new string(' ', left) + text;
        }

        /// <summary>How many bars fit across the stage.</summary>
        /// <param name="width">The console width.</param>
        /// <returns>The band count.</returns>
        public static int BandsFor(int width)
        {
            return Math.Clamp((width - 4) / (BarWidth + BarGap), 4, 48);
        }
    }
}
