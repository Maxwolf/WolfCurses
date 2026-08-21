// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;

namespace WolfCurses.Apps.Spreadsheet
{
    /// <summary>
    ///     A run of cells on one row drawn as a single wide one, which is how a heading or a line of instructions
    ///     gets to be longer than a column.
    ///     <para>
    ///         <b>Sideways only, and that is a deliberate limit rather than an unfinished one.</b> A merge that also
    ///         spanned rows would mean the renderer could no longer draw a row without knowing which of its cells an
    ///         earlier row had already claimed, and every hit test would have to ask the same question. A banner is
    ///         a row, so the whole of what this application needs is the easy half.
    ///     </para>
    ///     <para>
    ///         The leftmost cell owns the text; the ones it covers keep whatever they contain and are simply not
    ///         drawn, exactly as a real spreadsheet hides rather than deletes them.
    ///     </para>
    /// </summary>
    public readonly struct SheetMerge
    {
        /// <summary>Initializes a new instance of the <see cref="SheetMerge" /> struct.</summary>
        /// <param name="row">Which row it is on.</param>
        /// <param name="firstColumn">The leftmost column, which owns the text.</param>
        /// <param name="columnCount">How many columns it covers, at least one.</param>
        public SheetMerge(int row, int firstColumn, int columnCount)
        {
            Row = Math.Max(0, row);
            FirstColumn = Math.Max(0, firstColumn);
            ColumnCount = Math.Max(1, columnCount);
        }

        /// <summary>Which row it is on.</summary>
        public int Row { get; }

        /// <summary>The leftmost column, which owns the text.</summary>
        public int FirstColumn { get; }

        /// <summary>How many columns it covers.</summary>
        public int ColumnCount { get; }

        /// <summary>The rightmost column it covers, which is inside the merge.</summary>
        public int LastColumn => FirstColumn + ColumnCount - 1;

        /// <summary>The cell that owns the text.</summary>
        public CellAddress Anchor => new(Row, FirstColumn);

        /// <summary>Whether a cell is anywhere inside this merge, including the one that owns it.</summary>
        /// <param name="row">The row.</param>
        /// <param name="column">The column.</param>
        /// <returns>TRUE when the cell is covered.</returns>
        public bool Covers(int row, int column)
        {
            return row == Row && column >= FirstColumn && column <= LastColumn;
        }
    }
}
