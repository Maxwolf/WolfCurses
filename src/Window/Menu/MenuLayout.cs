// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/20/2026

using System;
using System.Collections.Generic;
using System.Text;
using WolfCurses.Window.Control;

namespace WolfCurses.Window.Menu
{
    /// <summary>
    ///     Lays a window's numbered menu out for the prompt buffer. Pure and dimension-driven — it is handed the rows
    ///     and the console size and asked for the text, so it can be unit-tested without a console — which is the whole
    ///     reason it lives here rather than inline in <see cref="Window{TCommands,TData}" />.
    ///     <para>
    ///         A menu that fits its terminal is drawn one item per line exactly as it always was, so an untouched menu
    ///         is byte-for-byte what the pre-column library produced (the highlight cursor aside, which
    ///         <see cref="ListNavigator" /> already gated). A menu too tall for the console — the input prompt lives
    ///         <em>below</em> it and gets pushed off the bottom and clipped once the list outgrows the screen — is
    ///         instead reflowed column-major into as many columns as it takes to fit, so the prompt stays visible and
    ///         the number the user is typing with it.
    ///     </para>
    /// </summary>
    internal static class MenuLayout
    {
        /// <summary>A menu shorter than this is never reflowed: too few items to be worth columns, and it keeps a
        ///     small menu single-column regardless of how tight a headless test host reports the console to be.</summary>
        private const int MinItemsToReflow = 8;

        /// <summary>The narrowest a column may be before the width simply cannot hold another one.</summary>
        private const int MinCellWidth = 14;

        /// <summary>Blank columns between one column of the menu and the next.</summary>
        private const int ColumnGap = 2;

        /// <summary>
        ///     Composes the menu block: one item per line while the list fits <paramref name="availableRows" />, or
        ///     column-major across several columns otherwise.
        /// </summary>
        /// <param name="rows">The undecorated "N. Description" rows, in menu order.</param>
        /// <param name="highlightedIndex">The highlighted row's index, or -1 when nothing is highlighted.</param>
        /// <param name="availableRows">How many console rows the menu may use before it has to reflow.</param>
        /// <param name="totalWidth">The console width the columns are fitted into.</param>
        /// <returns>The composed menu, one physical row per line, each ending in a newline.</returns>
        public static string Compose(IReadOnlyList<string> rows, int highlightedIndex, int availableRows,
            int totalWidth)
        {
            var columns = ComputeColumnCount(rows.Count, availableRows, totalWidth);
            if (columns <= 1)
            {
                // The original single-column rendering, unchanged: the two-space indent, or the cursor once summoned.
                var single = new StringBuilder();
                for (var i = 0; i < rows.Count; i++)
                    single.Append(ListNavigator.DecorateRow(rows[i], i == highlightedIndex))
                        .Append(Environment.NewLine);
                return single.ToString();
            }

            return ComposeColumns(rows, highlightedIndex, columns, totalWidth);
        }

        /// <summary>
        ///     How many columns the menu needs: one while it fits in <paramref name="availableRows" />, otherwise just
        ///     enough to fit vertically, capped by how many readable columns the width has room for so a reflow never
        ///     produces slivers too narrow to read.
        /// </summary>
        /// <param name="itemCount">How many menu items there are.</param>
        /// <param name="availableRows">Console rows the menu may use.</param>
        /// <param name="totalWidth">Console width.</param>
        /// <returns>The column count, at least one.</returns>
        public static int ComputeColumnCount(int itemCount, int availableRows, int totalWidth)
        {
            if (itemCount < MinItemsToReflow)
                return 1;

            if (availableRows < 1)
                availableRows = 1;

            if (itemCount <= availableRows)
                return 1;

            // Enough columns that ceil(itemCount / columns) fits the rows available.
            var neededByHeight = (itemCount + availableRows - 1) / availableRows;

            // ...but never so many that a column has no room to say anything.
            var fitByWidth = Math.Max(1, (totalWidth + ColumnGap) / (MinCellWidth + ColumnGap));

            return Math.Clamp(neededByHeight, 1, fitByWidth);
        }

        /// <summary>
        ///     Draws the rows column-major (down the first column, then the next) into
        ///     <paramref name="columns" /> fixed-width columns. The <see cref="ListNavigator" /> cursor decorates the
        ///     one highlighted cell in place, and every cell is fitted to its column so the grid stays square even with
        ///     a highlight escape or a truncation ellipsis inside it.
        /// </summary>
        private static string ComposeColumns(IReadOnlyList<string> rows, int highlightedIndex, int columns,
            int totalWidth)
        {
            // One column of slack so a full physical row never lands on the console's last cell, which scrolls a
            // classic console. Each cell carries DecorateRow's two-column cursor prefix; the text gets what is left.
            var usableWidth = Math.Max(columns * 4, totalWidth - 1);
            var cellWidth = Math.Max(4, (usableWidth - (columns - 1) * ColumnGap) / columns);
            var innerWidth = Math.Max(1, cellWidth - 2);

            var rowsPerColumn = (rows.Count + columns - 1) / columns;
            var separator = new string(' ', ColumnGap);

            var sb = new StringBuilder();
            for (var r = 0; r < rowsPerColumn; r++)
            {
                for (var c = 0; c < columns; c++)
                {
                    // Column-major: consecutive items go down a column, so the numbers read top-to-bottom just like
                    // the single-column menu these columns replaced.
                    var index = c * rowsPerColumn + r;
                    if (index >= rows.Count)
                        break; // Only the last column's bottom rows are ever short, so nothing past here is filled.

                    if (c > 0)
                        sb.Append(separator);

                    var cell = Fit(rows[index], innerWidth);
                    sb.Append(ListNavigator.DecorateRow(cell, index == highlightedIndex));
                }

                sb.Append(Environment.NewLine);
            }

            return sb.ToString();
        }

        /// <summary>
        ///     Fits text to an exact visible width: padded with spaces when short, truncated with a single-character
        ///     ellipsis when long so the columns stay aligned and the loss is visible rather than silent.
        /// </summary>
        /// <param name="text">The text to fit (plain — no escape sequences, so its length is its visible width).</param>
        /// <param name="width">The exact width to produce.</param>
        /// <returns>Text of exactly <paramref name="width" /> visible characters.</returns>
        internal static string Fit(string text, int width)
        {
            if (width <= 0)
                return string.Empty;

            if (text.Length == width)
                return text;

            if (text.Length < width)
                return text + new string(' ', width - text.Length);

            return width == 1 ? "…" : text.Substring(0, width - 1) + "…";
        }
    }
}
