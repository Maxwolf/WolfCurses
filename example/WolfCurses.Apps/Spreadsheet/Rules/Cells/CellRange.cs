// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;

namespace WolfCurses.Apps.Spreadsheet
{
    /// <summary>
    ///     A rectangle of cells, written <c>B2:D10</c>.
    ///     <para>
    ///         <b>It normalizes its corners the moment it is built</b>, so that a range reads top-left to
    ///         bottom-right however it was made. That matters because the commonest way to make one is by dragging,
    ///         and dragging upwards or leftwards puts the moving end before the fixed one. Every consumer here
    ///         wants the pair sorted, which is the same stance <c>TextBuffer</c> takes on a selection.
    ///     </para>
    /// </summary>
    public readonly struct CellRange : IEquatable<CellRange>
    {
        /// <summary>Initializes a new instance of the <see cref="CellRange" /> struct from two opposite corners.</summary>
        /// <param name="anchor">One corner.</param>
        /// <param name="other">The one across from it.</param>
        public CellRange(CellAddress anchor, CellAddress other)
        {
            FirstRow = Math.Min(anchor.Row, other.Row);
            LastRow = Math.Max(anchor.Row, other.Row);
            FirstColumn = Math.Min(anchor.Column, other.Column);
            LastColumn = Math.Max(anchor.Column, other.Column);
        }

        /// <summary>Initializes a range covering one cell.</summary>
        /// <param name="only">The cell.</param>
        public CellRange(CellAddress only) : this(only, only)
        {
        }

        /// <summary>The topmost row in the range.</summary>
        public int FirstRow { get; }

        /// <summary>The bottommost row, which is inside the range rather than one past it.</summary>
        public int LastRow { get; }

        /// <summary>The leftmost column in the range.</summary>
        public int FirstColumn { get; }

        /// <summary>The rightmost column, which is inside the range.</summary>
        public int LastColumn { get; }

        /// <summary>How many rows the range covers.</summary>
        public int RowCount => LastRow - FirstRow + 1;

        /// <summary>How many columns the range covers.</summary>
        public int ColumnCount => LastColumn - FirstColumn + 1;

        /// <summary>How many cells there are altogether.</summary>
        public int CellCount => RowCount * ColumnCount;

        /// <summary>The top left corner.</summary>
        public CellAddress TopLeft => new(FirstRow, FirstColumn);

        /// <summary>The bottom right corner.</summary>
        public CellAddress BottomRight => new(LastRow, LastColumn);

        /// <summary>Whether a cell falls inside the rectangle.</summary>
        /// <param name="address">The cell to test.</param>
        /// <returns>TRUE when it is in the range.</returns>
        public bool Contains(CellAddress address)
        {
            return address.Row >= FirstRow && address.Row <= LastRow &&
                   address.Column >= FirstColumn && address.Column <= LastColumn;
        }

        /// <summary>
        ///     Every cell in the range, reading along each row before moving down to the next, which is the order a
        ///     person reads a table and therefore the order a SUM should add them up in.
        /// </summary>
        /// <returns>The cells.</returns>
        public IEnumerable<CellAddress> Cells()
        {
            for (var row = FirstRow; row <= LastRow; row++)
            {
                for (var column = FirstColumn; column <= LastColumn; column++)
                    yield return new CellAddress(row, column);
            }
        }

        /// <summary>Reads a range written the way a person writes it.</summary>
        /// <param name="text">Something like <c>B2:D10</c>, or a single address, which is a range of one cell.</param>
        /// <param name="range">The range it names.</param>
        /// <returns>TRUE when the text really was a range.</returns>
        public static bool TryParse(string text, out CellRange range)
        {
            range = new CellRange(CellAddress.Origin);

            if (string.IsNullOrWhiteSpace(text))
                return false;

            var colon = text.IndexOf(':');

            if (colon < 0)
            {
                if (!CellAddress.TryParse(text, out var single))
                    return false;

                range = new CellRange(single);
                return true;
            }

            if (!CellAddress.TryParse(text.Substring(0, colon), out var from) ||
                !CellAddress.TryParse(text.Substring(colon + 1), out var to))
                return false;

            range = new CellRange(from, to);
            return true;
        }

        /// <inheritdoc />
        public bool Equals(CellRange other)
        {
            return FirstRow == other.FirstRow && LastRow == other.LastRow &&
                   FirstColumn == other.FirstColumn && LastColumn == other.LastColumn;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is CellRange other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return HashCode.Combine(FirstRow, LastRow, FirstColumn, LastColumn);
        }

        /// <summary>Compares two ranges.</summary>
        /// <param name="left">The first.</param>
        /// <param name="right">The second.</param>
        /// <returns>TRUE when they cover the same rectangle.</returns>
        public static bool operator ==(CellRange left, CellRange right)
        {
            return left.Equals(right);
        }

        /// <summary>Compares two ranges.</summary>
        /// <param name="left">The first.</param>
        /// <param name="right">The second.</param>
        /// <returns>TRUE when they cover different rectangles.</returns>
        public static bool operator !=(CellRange left, CellRange right)
        {
            return !left.Equals(right);
        }

        /// <summary>How a person writes this range down.</summary>
        /// <returns>Something like <c>B2:D10</c>, or one address when the range is a single cell.</returns>
        public override string ToString()
        {
            return CellCount == 1 ? TopLeft.ToString() : TopLeft + ":" + BottomRight;
        }
    }
}
