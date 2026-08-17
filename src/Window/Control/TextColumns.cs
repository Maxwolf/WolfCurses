// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/16/2026

using System;
using System.Collections.Generic;
using System.Text;
using WolfCurses.Graphics;

namespace WolfCurses.Window.Control
{
    /// <summary>
    ///     Lays blocks of text out side by side, as columns, aligned at the top — a panel next to content, two
    ///     boxes on one row, a legend beside a chart.
    ///     <para>
    ///         <b>The whole difficulty is that an escape sequence has length but no width.</b> A styled row twenty
    ///         columns across can be several hundred characters long, so padding it to a common width with
    ///         <c>string.PadRight</c> — which counts characters — pads it by hundreds of columns too few and leaves
    ///         every column to its right shredded diagonally down the screen. Everything here measures with
    ///         <see cref="AnsiText.VisibleLength" />, which is the library's one escape walk.
    ///     </para>
    ///     <para>
    ///         <b>Do not put a true-pixel picture in a column.</b> A sixel or kitty payload is a single row that
    ///         paints across many, recognised by <see cref="AnsiGraphics" />' marker being the row's <i>first</i>
    ///         character — so anything placed to its left destroys it, and the rows it covers are placeholders that
    ///         know nothing about a neighbour. Text beside text; pictures get their own rows. Same rule as
    ///         <see cref="Box" />, and for the same reason.
    ///     </para>
    /// </summary>
    /// <example>
    ///     <code>
    ///     var board = new Box { Title = "Well" }.Render(well);
    ///     var panel = new Box { Title = "Score" }.Render(score);
    ///     string screen = TextColumns.Join(2, board, panel);
    ///     </code>
    /// </example>
    public static class TextColumns
    {
        /// <summary>
        ///     Joins blocks into columns, each column as wide as its own widest visible row, separated by
        ///     <paramref name="gap" /> blank columns.
        /// </summary>
        /// <param name="gap">How many blank columns to leave between neighbours; negative is treated as zero.</param>
        /// <param name="blocks">
        ///     The columns, left to right. A null or empty block is <b>kept</b> as a column — the ones either side
        ///     stay in order and both its gutters are drawn — but it is zero columns wide, because there is no
        ///     width it could claim. A caller who wants a slot to hold its size when it empties has to pad it,
        ///     since nothing here can guess the number.
        /// </param>
        /// <returns>The columns as one block of text, rows joined with the platform newline and none trailing.</returns>
        public static string Join(int gap, params string[] blocks)
        {
            if (blocks == null || blocks.Length == 0)
                return string.Empty;

            var columns = new List<string>[blocks.Length];
            var widths = new int[blocks.Length];
            var rows = 0;

            for (var i = 0; i < blocks.Length; i++)
            {
                columns[i] = SplitRows(blocks[i]);
                rows = Math.Max(rows, columns[i].Count);

                foreach (var row in columns[i])
                    widths[i] = Math.Max(widths[i], AnsiText.VisibleLength(row));
            }

            var gutter = new string(' ', Math.Max(0, gap));
            var sb = new StringBuilder();

            for (var row = 0; row < rows; row++)
            {
                if (row > 0)
                    sb.AppendLine();

                // Found once per row rather than assumed, so that a row which is empty in every remaining column
                // ends where it ends. Trailing spaces are invisible, but they are still cells the presenter writes
                // and diffs, and on a tall layout they are most of the frame.
                var last = -1;
                for (var i = 0; i < blocks.Length; i++)
                {
                    if (row < columns[i].Count && columns[i][row].Length > 0)
                        last = i;
                }

                for (var i = 0; i <= last; i++)
                {
                    if (i > 0)
                        sb.Append(gutter);

                    var text = row < columns[i].Count ? columns[i][row] : string.Empty;
                    sb.Append(text);

                    if (i < last)
                        sb.Append(' ', widths[i] - AnsiText.VisibleLength(text));
                }
            }

            return sb.ToString();
        }

        /// <summary>Joins two blocks into columns. The common case, spelled out.</summary>
        /// <param name="left">The left column.</param>
        /// <param name="right">The right column.</param>
        /// <param name="gap">How many blank columns to leave between them.</param>
        /// <returns>The two columns as one block of text.</returns>
        public static string Join(string left, string right, int gap = 2)
        {
            return Join(gap, left, right);
        }

        /// <summary>Splits a block into rows on any newline convention.</summary>
        /// <param name="text">The block to split; null is treated as empty.</param>
        /// <returns>The rows, with no newlines left in them.</returns>
        private static List<string> SplitRows(string text)
        {
            var normalized = (text ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n");
            return new List<string>(normalized.Split('\n'));
        }
    }
}
