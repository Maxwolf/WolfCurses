// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;

namespace WolfCurses.Documents
{
    /// <summary>
    ///     A delimited file's header row, turned into something a reader can ask questions of: which column is
    ///     called what, and what does this row hold in it.
    ///     <para>
    ///         <b>The missing half of <see cref="DelimitedText" />, and the code everybody writes after it.</b>
    ///         Splitting the text is the part that looks hard; reading a field back out is the part that is
    ///         actually wrong, because it is written as <c>row[3]</c> against a file whose columns are in the order
    ///         they were the day it was written. Move a column, add one, or hand-edit a row so it is a field short,
    ///         and that reader silently returns the wrong value or throws on the row rather than at the file.
    ///     </para>
    ///     <para>
    ///         <b><see cref="Value" /> answers empty for both of the ragged cases</b>, which is the whole point of
    ///         it: a column the file does not have, and a row that stops before reaching it. <c>DelimitedText</c>
    ///         leaves rows ragged deliberately, because only the caller knows whether a missing field is an empty
    ///         cell or a fault; this is that caller's usual answer, written once.
    ///     </para>
    ///     <para>
    ///         <b>Names are matched without case and with the ends trimmed, where the data is neither.</b> The
    ///         asymmetry is deliberate rather than an oversight: a header cell is a <i>name</i> and a data cell is
    ///         a <i>value</i>. A leading space in a value may well be data, so <c>DelimitedText</c> keeps it; a
    ///         leading space in a name is somebody having written <c>Name, Phone</c> with a space after the comma,
    ///         and honouring it would mean no column ever matched again.
    ///     </para>
    /// </summary>
    public sealed class DelimitedColumns
    {
        /// <summary>Where each name sits, first occurrence winning.</summary>
        private readonly Dictionary<string, int> _indexes;

        /// <summary>The names as they were read, in file order.</summary>
        private readonly List<string> _names;

        /// <summary>
        ///     Reads a header row.
        ///     <para>
        ///         A repeated name keeps its <b>first</b> column. Two columns cannot both be Phone, and the first
        ///         one is the one somebody's eye lands on reading the file, so a later duplicate is ignored rather
        ///         than allowed to shadow it.
        ///     </para>
        /// </summary>
        /// <param name="header">The header row, typically the first row <see cref="DelimitedText.Read" /> gave back.</param>
        public DelimitedColumns(IReadOnlyList<string> header)
        {
            var count = header?.Count ?? 0;

            _names = new List<string>(count);
            _indexes = new Dictionary<string, int>(count, StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < count; i++)
            {
                var name = (header[i] ?? string.Empty).Trim();

                _names.Add(name);

                if (name.Length > 0 && !_indexes.ContainsKey(name))
                    _indexes.Add(name, i);
            }
        }

        /// <summary>How many columns the header declared, blank and repeated ones included.</summary>
        public int Count => _names.Count;

        /// <summary>The column names in file order, trimmed, with any blanks and repeats still in place.</summary>
        public IReadOnlyList<string> Names => _names;

        /// <summary>Which column a name sits in, or -1 when the file has no such column.</summary>
        /// <param name="name">The column name; matched without case and with the ends trimmed.</param>
        /// <returns>The column index, or -1.</returns>
        public int IndexOf(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return -1;

            return _indexes.TryGetValue(name.Trim(), out var index) ? index : -1;
        }

        /// <summary>Whether the file has a column of that name.</summary>
        /// <param name="name">The column name.</param>
        /// <returns>TRUE when it is there.</returns>
        public bool Has(string name)
        {
            return IndexOf(name) >= 0;
        }

        /// <summary>
        ///     Whether every one of these columns is present, which is how a reader decides whether the first row
        ///     of a file is a header at all rather than the first record of a file that has none.
        /// </summary>
        /// <param name="names">The columns wanted. No names at all is vacuously true.</param>
        /// <returns>TRUE when all of them are there.</returns>
        public bool HasAll(params string[] names)
        {
            if (names == null)
                return true;

            foreach (var name in names)
            {
                if (!Has(name))
                    return false;
            }

            return true;
        }

        /// <summary>
        ///     What a row holds in a named column, or empty when it holds nothing there.
        ///     <para>
        ///         <b>Empty covers both ragged cases and never throws for either</b>: the file has no such column,
        ///         and the row stopped before reaching it. Those are the two shapes a hand-edited file arrives in,
        ///         and the <c>row[IndexOf(name)]</c> a caller would otherwise write gets the first one wrong and
        ///         throws on the second.
        ///     </para>
        /// </summary>
        /// <param name="row">The row to read.</param>
        /// <param name="name">The column name.</param>
        /// <returns>The value, or an empty string.</returns>
        public string Value(IReadOnlyList<string> row, string name)
        {
            var index = IndexOf(name);

            if (row == null || index < 0 || index >= row.Count)
                return string.Empty;

            return row[index] ?? string.Empty;
        }
    }
}
