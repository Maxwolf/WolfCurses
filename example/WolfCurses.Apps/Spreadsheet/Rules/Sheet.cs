// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;
using System.Globalization;

namespace WolfCurses.Apps.Spreadsheet
{
    /// <summary>
    ///     The grid: what is typed in each cell, what each cell is worth, how wide each column is drawn, and which
    ///     runs of cells are merged.
    ///     <para>
    ///         <b>What a cell holds is the text somebody typed, and nothing else.</b> A cell containing
    ///         <c>=SUM(B5:B16)</c> holds those eleven characters; the number it comes to is worked out on demand and
    ///         never stored. That is what makes saving exact, since the file gets back the formula rather than the
    ///         answer, and it is the same stance the text buffer next door takes on remembering a line ending
    ///         instead of normalizing it.
    ///     </para>
    ///     <para>
    ///         The grid has a fixed size and is stored sparsely, which is what a spreadsheet is: mostly empty, and
    ///         you may move the cursor into the empty part. Two hundred rows of twenty-six columns is fifty-two
    ///         hundred cells and this holds only the few dozen that have anything in them.
    ///     </para>
    ///     <para>
    ///         <b>Worked-out values are cached, and the whole cache is thrown away whenever any cell changes.</b>
    ///         Cheap, and correct for a reason worth saying: any cell may refer to any other, so working out which
    ///         cached values a single edit invalidates means keeping a dependency graph, which is a great deal more
    ///         machinery than a sheet this size can justify.
    ///     </para>
    /// </summary>
    public sealed class Sheet
    {
        /// <summary>How many rows a sheet has unless told otherwise.</summary>
        public const int DefaultRowCount = 200;

        /// <summary>How many columns a sheet has unless told otherwise, which is A to Z.</summary>
        public const int DefaultColumnCount = 26;

        /// <summary>How wide a column is drawn unless it has been changed.</summary>
        public const int DefaultColumnWidth = 12;

        /// <summary>The narrowest a column may be made, which still shows a character and its padding.</summary>
        public const int MinimumColumnWidth = 3;

        /// <summary>The widest a column may be made.</summary>
        public const int MaximumColumnWidth = 60;

        /// <summary>What was typed in each cell that has anything in it.</summary>
        private readonly Dictionary<CellAddress, string> _cells = new();

        /// <summary>What each of those cells came to, until something changes and this is emptied.</summary>
        private readonly Dictionary<CellAddress, SheetValue> _values = new();

        /// <summary>Which cells are being worked out right now, which is how a loop is caught.</summary>
        private readonly HashSet<CellAddress> _working = new();

        /// <summary>The merged runs, at most one covering any given cell.</summary>
        private readonly List<SheetMerge> _merges = new();

        /// <summary>How wide each column is drawn.</summary>
        private readonly int[] _widths;

        /// <summary>Initializes a new instance of the <see cref="Sheet" /> class.</summary>
        /// <param name="rowCount">How many rows.</param>
        /// <param name="columnCount">How many columns.</param>
        public Sheet(int rowCount = DefaultRowCount, int columnCount = DefaultColumnCount)
        {
            RowCount = Math.Max(1, rowCount);
            ColumnCount = Math.Max(1, columnCount);

            _widths = new int[ColumnCount];

            for (var column = 0; column < ColumnCount; column++)
                _widths[column] = DefaultColumnWidth;
        }

        /// <summary>How many rows the grid has, whether or not anything is in them.</summary>
        public int RowCount { get; }

        /// <summary>How many columns the grid has.</summary>
        public int ColumnCount { get; }

        /// <summary>How wide each column is drawn, which is what the viewport scrolls by.</summary>
        public IReadOnlyList<int> ColumnWidths => _widths;

        /// <summary>The merged runs.</summary>
        public IReadOnlyList<SheetMerge> Merges => _merges;

        /// <summary>Whether anything has been changed since the sheet was loaded or last saved.</summary>
        public bool IsModified { get; private set; }

        /// <summary>
        ///     The line ending the file arrived with, written back out unchanged. Remembered rather than normalized
        ///     for the same reason the text buffer remembers one: opening a file and saving it untouched should
        ///     produce the same bytes, or the program is a reformatter.
        /// </summary>
        public string NewLine { get; set; } = Environment.NewLine;

        /// <summary>What was typed in a cell.</summary>
        /// <param name="address">Which cell.</param>
        /// <returns>Its text, which is empty for a cell nobody has touched.</returns>
        public string GetText(CellAddress address)
        {
            return _cells.TryGetValue(address, out var text) ? text : string.Empty;
        }

        /// <summary>What was typed in a cell.</summary>
        /// <param name="row">Its row.</param>
        /// <param name="column">Its column.</param>
        /// <returns>Its text.</returns>
        public string GetText(int row, int column)
        {
            return GetText(new CellAddress(row, column));
        }

        /// <summary>Types something into a cell, or empties it.</summary>
        /// <param name="address">Which cell.</param>
        /// <param name="text">What to put in it. Null or empty empties the cell rather than storing a blank.</param>
        public void SetText(CellAddress address, string text)
        {
            if (address.Row >= RowCount || address.Column >= ColumnCount)
                return;

            if (string.IsNullOrEmpty(text))
                _cells.Remove(address);
            else
                _cells[address] = text;

            Invalidate();
        }

        /// <summary>Types something into a cell.</summary>
        /// <param name="row">Its row.</param>
        /// <param name="column">Its column.</param>
        /// <param name="text">What to put in it.</param>
        public void SetText(int row, int column, string text)
        {
            SetText(new CellAddress(row, column), text);
        }

        /// <summary>
        ///     What a cell is worth.
        ///     <para>
        ///         The loop guard is the part that has to be here rather than in the evaluator: a cell asking for
        ///         its own value, directly or round a longer chain, is only visible from the place that knows which
        ///         cells are already part way through being worked out. Without it the two would call each other
        ///         until the stack ran out, taking the program with it.
        ///     </para>
        /// </summary>
        /// <param name="address">Which cell.</param>
        /// <returns>Its value.</returns>
        public SheetValue GetValue(CellAddress address)
        {
            if (_values.TryGetValue(address, out var cached))
                return cached;

            // Already part way through working this one out, so it needs its own answer to produce its own answer.
            // Deliberately not cached: it is the answer for this path rather than for the cell.
            if (!_working.Add(address))
                return SheetValue.FromError(FormulaErrors.Circular);

            try
            {
                var value = Compute(address);
                _values[address] = value;

                return value;
            }
            finally
            {
                _working.Remove(address);
            }
        }

        /// <summary>What a cell is worth.</summary>
        /// <param name="row">Its row.</param>
        /// <param name="column">Its column.</param>
        /// <returns>Its value.</returns>
        public SheetValue GetValue(int row, int column)
        {
            return GetValue(new CellAddress(row, column));
        }

        /// <summary>Reads a cell's text and decides what it is.</summary>
        /// <param name="address">Which cell.</param>
        /// <returns>Its value.</returns>
        private SheetValue Compute(CellAddress address)
        {
            var text = GetText(address);

            if (string.IsNullOrEmpty(text))
                return SheetValue.Empty;

            if (text[0] == '=')
                return FormulaEvaluator.Evaluate(this, text.Substring(1));

            // The invariant culture, matching the file: a sheet written where the decimal separator is a comma has
            // to mean the same numbers when it is opened somewhere it is a point.
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                ? SheetValue.FromNumber(number)
                : SheetValue.FromText(text);
        }

        /// <summary>How wide a column is drawn.</summary>
        /// <param name="column">Which column.</param>
        /// <returns>Its width.</returns>
        public int GetColumnWidth(int column)
        {
            return column >= 0 && column < ColumnCount ? _widths[column] : DefaultColumnWidth;
        }

        /// <summary>Changes how wide a column is drawn, which is a view setting rather than an edit to the data.</summary>
        /// <param name="column">Which column.</param>
        /// <param name="width">How wide, clamped to something readable.</param>
        public void SetColumnWidth(int column, int width)
        {
            if (column < 0 || column >= ColumnCount)
                return;

            _widths[column] = Math.Clamp(width, MinimumColumnWidth, MaximumColumnWidth);
        }

        /// <summary>The merge covering a cell, if any covers it.</summary>
        /// <param name="row">Its row.</param>
        /// <param name="column">Its column.</param>
        /// <returns>The merge, or null.</returns>
        public SheetMerge? MergeAt(int row, int column)
        {
            foreach (var merge in _merges)
            {
                if (merge.Covers(row, column))
                    return merge;
            }

            return null;
        }

        /// <summary>
        ///     Draws a run of cells on one row as a single wide one. Any merge it overlaps is dropped first, so
        ///     that no cell is ever covered twice and the drawing and the hit test cannot disagree about which
        ///     merge a cell belongs to.
        /// </summary>
        /// <param name="row">Which row.</param>
        /// <param name="firstColumn">The leftmost column, which keeps the text.</param>
        /// <param name="columnCount">How many columns to cover.</param>
        public void Merge(int row, int firstColumn, int columnCount)
        {
            var merge = new SheetMerge(row, firstColumn,
                Math.Min(columnCount, Math.Max(1, ColumnCount - firstColumn)));

            _merges.RemoveAll(existing => existing.Row == merge.Row &&
                                          existing.FirstColumn <= merge.LastColumn &&
                                          existing.LastColumn >= merge.FirstColumn);

            // One cell is not a merge, so asking for one is how a merge is undone by hand.
            if (merge.ColumnCount > 1)
                _merges.Add(merge);

            IsModified = true;
        }

        /// <summary>Puts a merged run back to ordinary cells.</summary>
        /// <param name="row">Its row.</param>
        /// <param name="column">Any column it covers.</param>
        /// <returns>TRUE when there was a merge there to undo.</returns>
        public bool Unmerge(int row, int column)
        {
            var removed = _merges.RemoveAll(merge => merge.Covers(row, column)) > 0;

            if (removed)
                IsModified = true;

            return removed;
        }

        /// <summary>How many rows have anything in them, which is what gets saved rather than the whole grid.</summary>
        public int UsedRowCount
        {
            get
            {
                var used = 0;

                foreach (var address in _cells.Keys)
                    used = Math.Max(used, address.Row + 1);

                return used;
            }
        }

        /// <summary>How many columns have anything in them.</summary>
        public int UsedColumnCount
        {
            get
            {
                var used = 0;

                foreach (var address in _cells.Keys)
                    used = Math.Max(used, address.Column + 1);

                return used;
            }
        }

        /// <summary>
        ///     The filled part of the grid as rows of text, ready to be written to a file. Rows are the full used
        ///     width rather than ragged, since a table with a hole in the middle of it is easier to read back than
        ///     one whose rows are different lengths.
        /// </summary>
        /// <returns>The rows.</returns>
        public IEnumerable<string[]> Rows()
        {
            var rows = UsedRowCount;
            var columns = Math.Max(1, UsedColumnCount);

            for (var row = 0; row < rows; row++)
            {
                var line = new string[columns];

                for (var column = 0; column < columns; column++)
                    line[column] = GetText(row, column);

                yield return line;
            }
        }

        /// <summary>Says the sheet matches what is on disk, which is what saving it makes true.</summary>
        public void MarkSaved()
        {
            IsModified = false;
        }

        /// <summary>Throws away every worked-out value, because any of them may have depended on what just changed.</summary>
        private void Invalidate()
        {
            _values.Clear();
            IsModified = true;
        }
    }
}
