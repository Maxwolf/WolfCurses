// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Globalization;
using System.Text;

namespace WolfCurses.Apps.Spreadsheet
{
    /// <summary>
    ///     Where a cell is, and how it is written down: row 6, column 1 is <c>B7</c>.
    ///     <para>
    ///         Both halves count from zero inside the program and are written from one outside it, which is the
    ///         single conversion in here and the one every reader will look for. Columns are lettered in what is
    ///         called bijective base twenty-six: A to Z, then AA rather than BA, because there is no letter standing
    ///         for nothing.
    ///     </para>
    /// </summary>
    public readonly struct CellAddress : IEquatable<CellAddress>
    {
        /// <summary>Initializes a new instance of the <see cref="CellAddress" /> struct.</summary>
        /// <param name="row">The row, counting from zero.</param>
        /// <param name="column">The column, counting from zero.</param>
        public CellAddress(int row, int column)
        {
            Row = Math.Max(0, row);
            Column = Math.Max(0, column);
        }

        /// <summary>The top left cell, which is where a sheet opens.</summary>
        public static CellAddress Origin => new(0, 0);

        /// <summary>The row, counting from zero.</summary>
        public int Row { get; }

        /// <summary>The column, counting from zero.</summary>
        public int Column { get; }

        /// <summary>
        ///     The letters a column is written with: A, B, ... Z, AA, AB.
        ///     <para>
        ///         The subtraction is what makes it bijective, and leaving it out is the classic version of this
        ///         bug: without it the column after Z is BA, because 26 divided by 26 is 1 and 1 means B. There is
        ///         no zero digit in this system, so every carry has to borrow one.
        ///     </para>
        /// </summary>
        /// <param name="column">The column, counting from zero.</param>
        /// <returns>Its letters.</returns>
        public static string ColumnName(int column)
        {
            var sb = new StringBuilder();
            var value = Math.Max(0, column);

            while (true)
            {
                sb.Insert(0, (char) ('A' + value % 26));

                value = value / 26 - 1;

                if (value < 0)
                    break;
            }

            return sb.ToString();
        }

        /// <summary>Reads an address written the way a person writes it.</summary>
        /// <param name="text">Something like <c>B7</c>. Case and surrounding spaces do not matter.</param>
        /// <param name="address">The address it names.</param>
        /// <returns>TRUE when the text really was an address.</returns>
        public static bool TryParse(string text, out CellAddress address)
        {
            address = Origin;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            var trimmed = text.Trim();
            var at = 0;
            var column = 0;

            while (at < trimmed.Length && char.IsLetter(trimmed[at]))
            {
                // Same bijection the other way about: every letter is worth one more than its position suggests.
                column = column * 26 + (char.ToUpperInvariant(trimmed[at]) - 'A' + 1);
                at++;
            }

            if (at == 0 || at == trimmed.Length)
                return false;

            if (!int.TryParse(trimmed.Substring(at), NumberStyles.None, CultureInfo.InvariantCulture, out var row) ||
                row < 1)
                return false;

            address = new CellAddress(row - 1, column - 1);
            return true;
        }

        /// <inheritdoc />
        public bool Equals(CellAddress other)
        {
            return Row == other.Row && Column == other.Column;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is CellAddress other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return HashCode.Combine(Row, Column);
        }

        /// <summary>Compares two addresses.</summary>
        /// <param name="left">The first.</param>
        /// <param name="right">The second.</param>
        /// <returns>TRUE when they are the same cell.</returns>
        public static bool operator ==(CellAddress left, CellAddress right)
        {
            return left.Equals(right);
        }

        /// <summary>Compares two addresses.</summary>
        /// <param name="left">The first.</param>
        /// <param name="right">The second.</param>
        /// <returns>TRUE when they are different cells.</returns>
        public static bool operator !=(CellAddress left, CellAddress right)
        {
            return !left.Equals(right);
        }

        /// <summary>How a person writes this cell down.</summary>
        /// <returns>Something like <c>B7</c>.</returns>
        public override string ToString()
        {
            return ColumnName(Column) + (Row + 1).ToString(CultureInfo.InvariantCulture);
        }
    }
}
