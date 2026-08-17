// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/16/2026

using System;
using System.Collections.Generic;
using System.Text;
using WolfCurses.Graphics;

namespace WolfCurses.Games
{
    /// <summary>
    ///     Joins two blocks of text into two columns, row by row. What <see cref="Tetris.TetrisDialog" /> uses to put
    ///     its score panel beside the well instead of under it.
    ///     <para>
    ///         <b>The whole difficulty is that an escape sequence has length but no width.</b> A row of the well is a
    ///         couple of dozen visible columns wrapped in several hundred bytes of color, so padding the left column
    ///         to a common width with <c>PadRight</c> — which counts characters — pads it by hundreds of columns too
    ///         few and leaves the right column shredded diagonally down the screen.
    ///     </para>
    ///     <para>
    ///         The measurement is <see cref="AnsiText.VisibleLength" />. This class briefly carried its own copy of
    ///         that walk, because the library kept the only correct one internal — which was the
    ///         two-parser-divergence trap <c>ConsolePresenter</c> documents, reproduced one project downstream. The
    ///         library publishes the walk now, so all that is left here is the joining.
    ///     </para>
    /// </summary>
    internal static class SideBySide
    {
        /// <summary>Puts <paramref name="right" /> beside <paramref name="left" />, aligned at the top.</summary>
        /// <param name="left">The left column; its widest visible row sets the gutter position.</param>
        /// <param name="right">The right column.</param>
        /// <param name="gap">How many blank columns to leave between them.</param>
        /// <returns>The two columns as one block of text.</returns>
        public static string Join(string left, string right, int gap)
        {
            var leftRows = SplitRows(left);
            var rightRows = SplitRows(right);

            var leftWidth = 0;
            foreach (var row in leftRows)
                leftWidth = Math.Max(leftWidth, AnsiText.VisibleLength(row));

            var gutter = new string(' ', Math.Max(0, gap));
            var rows = Math.Max(leftRows.Count, rightRows.Count);
            var sb = new StringBuilder();

            for (var i = 0; i < rows; i++)
            {
                if (i > 0)
                    sb.AppendLine();

                var leftRow = i < leftRows.Count ? leftRows[i] : string.Empty;
                var rightRow = i < rightRows.Count ? rightRows[i] : string.Empty;

                // Nothing to the right of this row means nothing to pad toward, so the row ends where it ends —
                // trailing spaces are invisible but they are still cells the presenter has to write and diff.
                if (rightRow.Length == 0)
                {
                    sb.Append(leftRow);
                    continue;
                }

                sb.Append(leftRow)
                    .Append(' ', leftWidth - AnsiText.VisibleLength(leftRow))
                    .Append(gutter)
                    .Append(rightRow);
            }

            return sb.ToString();
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
