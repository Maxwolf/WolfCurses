// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;

namespace WolfCurses.Window.Control
{
    /// <summary>
    ///     The window a screen shows onto a table bigger than it: which row and which column sit at the top left,
    ///     which columns fit beside them, where a column is drawn, and which column a click landed in.
    ///     <para>
    ///         <see cref="Documents.TextViewport" /> is this idea for a document, and the difference between them is
    ///         the whole reason this exists: <b>a document scrolls in cells that are all one wide, and a table does
    ///         not.</b> Every sum in here would be a multiplication if columns were uniform, and every one of them
    ///         is the sum somebody writes inline, gets right for the fixed-width case, and then finds is wrong the
    ///         first time a column is widened.
    ///     </para>
    ///     <para>
    ///         Pure arithmetic, no console and no data. It does not hold the table either, so the widths and the
    ///         row count are passed in by the caller who has them, exactly as the text viewport takes a line count.
    ///         That keeps it usable over anything with rows and columns: a spreadsheet, a file listing, a database
    ///         browser, a log with fields.
    ///     </para>
    ///     <para>
    ///         <b>Only whole columns are drawn.</b> A column that does not fit is not shown half way, and whatever
    ///         cells are left over at the right stay blank. Splitting one would mean cutting a cell's text at a
    ///         column boundary that has no meaning in it, and a half-drawn column invites a click that lands in a
    ///         cell nobody can see.
    ///     </para>
    /// </summary>
    public sealed class TableViewport
    {
        /// <summary>Initializes a viewport of the given size, parked at the top left of the table.</summary>
        /// <param name="width">Screen columns the table body may use; at least one.</param>
        /// <param name="rows">Visible rows; at least one.</param>
        public TableViewport(int width = 1, int rows = 1)
        {
            Resize(width, rows);
        }

        /// <summary>The table row drawn on the viewport's first line.</summary>
        public int FirstRow { get; private set; }

        /// <summary>The table column drawn at the viewport's left edge.</summary>
        public int FirstColumn { get; private set; }

        /// <summary>How many screen columns the body has to draw into; at least one.</summary>
        public int Width { get; private set; }

        /// <summary>How many rows are visible; at least one.</summary>
        public int Rows { get; private set; }

        /// <summary>The table row one past the last visible one.</summary>
        public int LastRowExclusive => FirstRow + Rows;

        /// <summary>
        ///     Changes the visible size. Zero or negative becomes one, because a viewport with no rows has no
        ///     arithmetic that means anything and a headless host really does report a console size of zero.
        /// </summary>
        /// <param name="width">Screen columns for the body.</param>
        /// <param name="rows">Visible rows.</param>
        public void Resize(int width, int rows)
        {
            Width = Math.Max(1, width);
            Rows = Math.Max(1, rows);
        }

        /// <summary>Scrolls to an absolute origin, never above or left of the table's start.</summary>
        /// <param name="firstRow">The row to put on the first line.</param>
        /// <param name="firstColumn">The column to put at the left edge.</param>
        public void ScrollTo(int firstRow, int firstColumn)
        {
            FirstRow = Math.Max(0, firstRow);
            FirstColumn = Math.Max(0, firstColumn);
        }

        /// <summary>Scrolls by a delta, never above or left of the table's start.</summary>
        /// <param name="rows">Rows to move down; negative moves up.</param>
        /// <param name="columns">Columns to move right; negative moves left.</param>
        public void ScrollBy(int rows, int columns)
        {
            ScrollTo(FirstRow + rows, FirstColumn + columns);
        }

        /// <summary>
        ///     Pulls the origin back so that the last screenful is the furthest you can scroll, on both axes.
        ///     <para>
        ///         The sideways half is the one worth stating, because it is not "stop at the last column": with
        ///         columns of different widths, the furthest left-hand column is whichever one still leaves room for
        ///         every column after it, and that has to be found by walking backwards from the end. Stopping at
        ///         the last column instead parks a wide table with one narrow column on screen and the rest of the
        ///         window empty.
        ///     </para>
        /// </summary>
        /// <param name="rowCount">How many rows the table has.</param>
        /// <param name="widths">The width of every column, in order.</param>
        public void ClampToTable(int rowCount, IReadOnlyList<int> widths)
        {
            FirstRow = Math.Clamp(FirstRow, 0, Math.Max(0, rowCount - Rows));
            FirstColumn = Math.Clamp(FirstColumn, 0, LastColumnOrigin(widths));
        }

        /// <summary>
        ///     The furthest left a table may be scrolled: the lowest column number that still shows every column
        ///     after it.
        /// </summary>
        /// <param name="widths">The width of every column, in order.</param>
        /// <returns>The largest sensible <see cref="FirstColumn" />.</returns>
        public int LastColumnOrigin(IReadOnlyList<int> widths)
        {
            var count = widths?.Count ?? 0;
            if (count == 0)
                return 0;

            var used = 0;
            var origin = count - 1;

            // Backwards from the end, taking columns while they still fit. Where even the last column is wider than
            // the window this lands on it, which is the honest answer: it is as far right as there is to go.
            for (var column = count - 1; column >= 0; column--)
            {
                var width = ColumnWidth(widths, column);
                if (used + width > Width && column < count - 1)
                    break;

                used += width;
                origin = column;
            }

            return origin;
        }

        /// <summary>
        ///     How many columns are drawn, counting from <see cref="FirstColumn" />.
        ///     <para>
        ///         <b>Never fewer than one.</b> A column wider than the whole window still has to be drawn, clipped,
        ///         or a table with one wide column would render as nothing at all and there would be no cell to
        ///         click on to get anywhere else.
        ///     </para>
        /// </summary>
        /// <param name="widths">The width of every column, in order.</param>
        /// <returns>The number of whole columns that fit.</returns>
        public int VisibleColumns(IReadOnlyList<int> widths)
        {
            var count = widths?.Count ?? 0;
            if (count == 0 || FirstColumn >= count)
                return 0;

            var used = 0;
            var visible = 0;

            for (var column = FirstColumn; column < count; column++)
            {
                var width = ColumnWidth(widths, column);
                if (used + width > Width)
                    break;

                used += width;
                visible++;
            }

            return Math.Max(1, visible);
        }

        /// <summary>
        ///     Scrolls the least amount that brings a cell fully into view, and reports whether it had to move.
        ///     This is what every cursor movement calls, so the selected cell is never off screen after a key press.
        /// </summary>
        /// <param name="row">The row to reveal.</param>
        /// <param name="column">The column to reveal.</param>
        /// <param name="widths">The width of every column, in order.</param>
        /// <returns>TRUE when the origin changed.</returns>
        public bool EnsureVisible(int row, int column, IReadOnlyList<int> widths)
        {
            var firstRow = FirstRow;
            var firstColumn = FirstColumn;

            if (row < FirstRow)
                firstRow = row;
            else if (row >= FirstRow + Rows)
                firstRow = row - Rows + 1;

            if (column < firstColumn)
            {
                firstColumn = column;
            }
            else
            {
                // Sideways cannot be done by subtraction, because how many columns a step to the right costs
                // depends on how wide the ones being scrolled off happen to be. Give up ground one column at a time
                // until the target fits, which also terminates at the target itself when it is wider than the whole
                // window.
                while (firstColumn < column && !Fits(firstColumn, column, widths))
                    firstColumn++;
            }

            if (firstRow == FirstRow && firstColumn == FirstColumn)
                return false;

            ScrollTo(firstRow, firstColumn);
            return true;
        }

        /// <summary>Whether a column is wholly on screen when the table starts at a given origin.</summary>
        /// <param name="origin">The proposed left-hand column.</param>
        /// <param name="column">The column that has to fit.</param>
        /// <param name="widths">The width of every column, in order.</param>
        /// <returns>TRUE when it fits.</returns>
        private bool Fits(int origin, int column, IReadOnlyList<int> widths)
        {
            var used = 0;

            for (var i = origin; i <= column; i++)
                used += ColumnWidth(widths, i);

            return used <= Width;
        }

        /// <summary>
        ///     Where a column is drawn, as an offset from the body's left edge.
        ///     <para>
        ///         The minus one is the useful half, exactly as it is on <c>TextViewport.TryToScreen</c>: a column
        ///         scrolled out of view has no cell to draw in, and a renderer that assumes one paints it over
        ///         whichever column happens to be there.
        ///     </para>
        /// </summary>
        /// <param name="column">The table column.</param>
        /// <param name="widths">The width of every column, in order.</param>
        /// <returns>The screen offset, or -1 when that column is not currently drawn.</returns>
        public int ColumnOffset(int column, IReadOnlyList<int> widths)
        {
            if (column < FirstColumn || column >= FirstColumn + VisibleColumns(widths))
                return -1;

            var offset = 0;

            for (var i = FirstColumn; i < column; i++)
                offset += ColumnWidth(widths, i);

            return offset;
        }

        /// <summary>
        ///     Which column a screen offset falls in, which is the whole of a mouse hit test sideways.
        ///     <para>
        ///         Answers -1 for the empty ground to the right of the last drawn column, rather than the nearest
        ///         column: a click out there means nothing, and rounding it to the last column would move the cursor
        ///         somewhere the user did not point at.
        ///     </para>
        /// </summary>
        /// <param name="screenColumn">The offset from the body's left edge.</param>
        /// <param name="widths">The width of every column, in order.</param>
        /// <returns>The table column, or -1 when the offset is not over one.</returns>
        public int ColumnAt(int screenColumn, IReadOnlyList<int> widths)
        {
            if (screenColumn < 0)
                return -1;

            var visible = VisibleColumns(widths);
            var offset = 0;

            for (var i = 0; i < visible; i++)
            {
                var column = FirstColumn + i;
                var width = ColumnWidth(widths, column);

                if (screenColumn < offset + width)
                    return column;

                offset += width;
            }

            return -1;
        }

        /// <summary>Which table row a screen line shows. Not clamped, because the viewport does not have the table.</summary>
        /// <param name="screenRow">The line within the body, zero at the top.</param>
        /// <returns>The table row drawn there.</returns>
        public int RowAt(int screenRow)
        {
            return FirstRow + screenRow;
        }

        /// <summary>Where a table row is drawn, when it is drawn at all.</summary>
        /// <param name="row">The table row.</param>
        /// <param name="screenRow">The line within the body.</param>
        /// <returns>TRUE when that row is currently visible.</returns>
        public bool TryRowToScreen(int row, out int screenRow)
        {
            screenRow = row - FirstRow;

            return screenRow >= 0 && screenRow < Rows;
        }

        /// <summary>
        ///     One column's width.
        ///     <para>
        ///         <b>Never less than one, and that floor is load-bearing rather than tidy.</b> A column of zero
        ///         width would make <see cref="ColumnAt" /> ambiguous (two columns at the same offset) and would let
        ///         <see cref="EnsureVisible" /> give up ground forever without the target ever coming closer.
        ///     </para>
        /// </summary>
        /// <param name="widths">The width of every column, in order.</param>
        /// <param name="column">Which one.</param>
        /// <returns>The width, at least one.</returns>
        private static int ColumnWidth(IReadOnlyList<int> widths, int column)
        {
            if (widths == null || column < 0 || column >= widths.Count)
                return 1;

            return Math.Max(1, widths[column]);
        }
    }
}
