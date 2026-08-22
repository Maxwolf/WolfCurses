// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;
using WolfCurses.Graphics;
using WolfCurses.Utility;

namespace WolfCurses.Window.Control
{
    /// <summary>
    ///     A record drawn as labelled fields: a column of names down the left, what each holds beside it, and one
    ///     of them picked out. The properties panel, the settings page, the card in a card index.
    ///     <para>
    ///         <b>It keeps its layout</b>, for the fourth time in this namespace and the same reason as
    ///         <see cref="MenuBar" />, <see cref="Keypad" /> and <see cref="MonthGrid" />: the rows are worked out
    ///         once and read by both the drawing and the hit test, so the field a click lands on cannot be a
    ///         different field from the one drawn there.
    ///     </para>
    ///     <para>
    ///         <b>Here that is not a nicety, because a field is not a row.</b> A value long enough to wrap, or one
    ///         given room for a note, occupies several; the field on screen row four is therefore not the fourth
    ///         field, and the hit test everybody writes first divides by one and is wrong for every field under
    ///         the first multi-line one. Every row of a field answers with that field, so clicking the third line
    ///         of a note picks the note.
    ///     </para>
    ///     <para>
    ///         <b>The label column is measured once for the whole list, not per field.</b> Same reasoning as
    ///         <see cref="MenuBarMenu.CheckColumns" />: the values line up with each other, and adding a longer
    ///         label moves them all together rather than leaving one field indented differently from its
    ///         neighbours. <see cref="MinimumLabelWidth" /> is how a caller stops the column moving at all when the
    ///         fields themselves change.
    ///     </para>
    ///     <para>
    ///         <b>Every rendered row is exactly <see cref="Width" /> visible columns</b>, blank ones included, so a
    ///         caller can put a border either side or draw something over the top without measuring anything. The
    ///         same rectangle invariant <see cref="TextGrid" /> holds.
    ///     </para>
    /// </summary>
    public sealed class FieldList
    {
        /// <summary>The fields, in the order they are drawn.</summary>
        private readonly List<FieldListEntry> _entries;

        /// <summary>Initializes a list of fields.</summary>
        /// <param name="entries">The fields, in drawing order.</param>
        public FieldList(params FieldListEntry[] entries)
        {
            _entries = new List<FieldListEntry>(entries ?? Array.Empty<FieldListEntry>());
        }

        /// <summary>The fields, in the order they are drawn.</summary>
        public IReadOnlyList<FieldListEntry> Entries => _entries;

        /// <summary>How many screen columns the whole list occupies, labels, gap and values together.</summary>
        public int Width { get; set; } = 40;

        /// <summary>How many blank columns separate the label column from the values.</summary>
        public int Gap { get; set; } = 1;

        /// <summary>
        ///     The narrowest the label column may be. Left at zero the column is exactly as wide as the longest
        ///     label, which moves every value when the fields change; setting it pins the column.
        /// </summary>
        public int MinimumLabelWidth { get; set; }

        /// <summary>Where a label sits in its column. Left, as a menu does it, unless said otherwise.</summary>
        public AnsiHorizontalAlignmentEnum LabelAlignment { get; set; } = AnsiHorizontalAlignmentEnum.Left;

        /// <summary>
        ///     What is drawn in the last column of a field whose value did not fit the rows kept for it.
        ///     <para>
        ///         On by default, because a control that quietly drops the end of a value is one where the only
        ///         way to learn the rest is there is to go looking for it. Set it to <c>'\0'</c> for a list that
        ///         should say nothing about what it cut.
        ///     </para>
        /// </summary>
        public char Overflow { get; set; } = '…';

        /// <summary>
        ///     Which field is picked out, or -1 for none, which is what a list nobody has moved through starts as.
        ///     <para>
        ///         Hidden until asked for, the same way a <see cref="Window{TCommands,TData}" /> menu holds its
        ///         highlight back until the first arrow key: a list that is only being read has nothing to point
        ///         at. A caller whose list is for choosing sets this to zero when it opens.
        ///     </para>
        /// </summary>
        public int Selected { get; set; } = -1;

        /// <summary>The screen row the first field is drawn on, which a hit test is measured against.</summary>
        public int Row { get; set; }

        /// <summary>The screen column the list's left edge is in.</summary>
        public int Column { get; set; }

        /// <summary>How a label is painted.</summary>
        public TextStyle LabelStyle { get; set; } = TextStyle.None;

        /// <summary>How a value is painted, and the gap beside it.</summary>
        public TextStyle ValueStyle { get; set; } = TextStyle.None;

        /// <summary>
        ///     How the picked-out field is painted, label and value alike. Both, rather than the value only: half
        ///     a highlighted row reads as a drawing fault rather than as a selection.
        /// </summary>
        public TextStyle SelectedStyle { get; set; } = TextStyle.None;

        /// <summary>Which colour vocabulary the styles resolve through; pinnable per instance for tests.</summary>
        public AnsiColorModeEnum ColorMode { get; set; } = AnsiColorModeEnum.Auto;

        /// <summary>How wide the label column is: the longest label, but never so wide that no value fits.</summary>
        public int LabelWidth
        {
            get
            {
                var longest = Math.Max(0, MinimumLabelWidth);

                foreach (var entry in _entries)
                    longest = Math.Max(longest, entry.Label?.Length ?? 0);

                return Math.Clamp(longest, 0, Math.Max(0, Width - EffectiveGap - 1));
            }
        }

        /// <summary>How wide the value column is, which is whatever the labels and the gap left over.</summary>
        public int ValueWidth => Math.Max(0, Width - LabelWidth - EffectiveGap);

        /// <summary>How many screen rows the whole list occupies, which is the sum of what its fields reserved.</summary>
        public int Height
        {
            get
            {
                var rows = 0;

                foreach (var entry in _entries)
                    rows += Math.Max(1, entry.Lines);

                return rows;
            }
        }

        /// <summary>The picked-out field, or null when nothing is.</summary>
        public FieldListEntry SelectedEntry =>
            Selected >= 0 && Selected < _entries.Count ? _entries[Selected] : null;

        /// <summary>The gap, kept inside what the list is actually wide.</summary>
        private int EffectiveGap => Math.Clamp(Gap, 0, Math.Max(0, Width));

        /// <summary>The screen row a field's label is drawn on, or -1 when there is no such field.</summary>
        /// <param name="index">Which field.</param>
        /// <returns>The screen row, or -1.</returns>
        public int RowOf(int index)
        {
            if (index < 0 || index >= _entries.Count)
                return -1;

            var offset = 0;

            for (var i = 0; i < index; i++)
                offset += Math.Max(1, _entries[i].Lines);

            return Row + offset;
        }

        /// <summary>
        ///     Which field is drawn at a cell, or -1 for everywhere off the list.
        ///     <para>
        ///         Every row a field occupies answers with that field, so the third line of a note picks the note.
        ///         This is the arithmetic the retained layout exists for: the fields are walked, never divided.
        ///     </para>
        /// </summary>
        /// <param name="row">The screen row.</param>
        /// <param name="column">The screen column.</param>
        /// <returns>The field's index, or -1.</returns>
        public int FieldAt(int row, int column)
        {
            var offset = row - Row;

            if (offset < 0 || column < Column || column >= Column + Width)
                return -1;

            for (var i = 0; i < _entries.Count; i++)
            {
                var lines = Math.Max(1, _entries[i].Lines);

                if (offset < lines)
                    return i;

                offset -= lines;
            }

            return -1;
        }

        /// <summary>Draws the list, one string per screen row.</summary>
        /// <returns>The rows, top to bottom, each exactly <see cref="Width" /> visible columns.</returns>
        public IReadOnlyList<string> Render()
        {
            var rows = new List<string>(Height);

            var labelWidth = LabelWidth;
            var valueWidth = ValueWidth;
            var gap = EffectiveGap;

            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                var reserved = Math.Max(1, entry.Lines);
                var lines = Wrap(entry.Value, valueWidth);

                var chosen = i == Selected;
                var labelStyle = chosen ? SelectedStyle : LabelStyle;
                var valueStyle = chosen ? SelectedStyle : ValueStyle;

                for (var line = 0; line < reserved; line++)
                {
                    var text = line < lines.Count ? lines[line] : string.Empty;

                    // The last row kept for a value that needed more says so, rather than ending mid-word with
                    // nothing to suggest the rest of it is anywhere.
                    if (line == reserved - 1 && lines.Count > reserved)
                        text = Cut(text, valueWidth);

                    rows.Add(new TextRow {ColorMode = ColorMode}
                        .Append(
                            line == 0
                                ? AnsiText.Fit(entry.Label, labelWidth, LabelAlignment)
                                : new string(' ', labelWidth),
                            labelStyle)
                        .Append(' ', gap, valueStyle)
                        .Append(AnsiText.Fit(text, valueWidth), valueStyle)
                        .Render());
                }
            }

            return rows;
        }

        /// <summary>
        ///     Breaks a value into the lines it is drawn on.
        ///     <para>
        ///         Line breaks already in the value are kept, since a value holding several lines is exactly what a
        ///         note is, and wrapping happens within each of them.
        ///     </para>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="width">How wide the value column is.</param>
        /// <returns>The lines.</returns>
        private static List<string> Wrap(string value, int width)
        {
            var lines = new List<string>();

            if (string.IsNullOrEmpty(value) || width <= 0)
                return lines;

            foreach (var line in value.WordWrap(width).Split('\n'))
                lines.Add(line.TrimEnd('\r'));

            // Word wrapping ends every line it produces, including the last, so the split leaves an empty one
            // behind that was never a line of the value.
            if (lines.Count > 0 && lines[lines.Count - 1].Length == 0)
                lines.RemoveAt(lines.Count - 1);

            return lines;
        }

        /// <summary>Marks a line as having had more after it, when the caller wants that said at all.</summary>
        /// <param name="text">The line as it would have been drawn.</param>
        /// <param name="width">How wide the value column is.</param>
        /// <returns>The line with its last column given over to the overflow mark.</returns>
        private string Cut(string text, int width)
        {
            if (Overflow == '\0' || width <= 0)
                return text;

            var kept = text ?? string.Empty;

            if (kept.Length > width - 1)
                kept = kept.Substring(0, width - 1);

            return kept.PadRight(width - 1) + Overflow;
        }
    }
}
