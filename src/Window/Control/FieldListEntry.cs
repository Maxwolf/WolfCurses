// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

namespace WolfCurses.Window.Control
{
    /// <summary>
    ///     One labelled field on a <see cref="FieldList" />: what it is called, what it holds, and how many rows
    ///     the list keeps for it.
    /// </summary>
    public sealed class FieldListEntry
    {
        /// <summary>Initializes a field.</summary>
        /// <param name="label">What the field is called. The colon, if one is wanted, belongs in here.</param>
        /// <param name="value">What it holds. Null is the same as empty.</param>
        /// <param name="lines">
        ///     How many rows the list keeps for the value, one at a minimum. See <see cref="Lines" /> for why this
        ///     is a reservation rather than a maximum.
        /// </param>
        public FieldListEntry(string label, string value = null, int lines = 1)
        {
            Label = label ?? string.Empty;
            Value = value ?? string.Empty;
            Lines = lines;
        }

        /// <summary>What the field is called, drawn in the label column.</summary>
        public string Label { get; set; }

        /// <summary>What the field holds, wrapped into the value column.</summary>
        public string Value { get; set; }

        /// <summary>
        ///     How many rows the list keeps for this field, one at a minimum.
        ///     <para>
        ///         <b>A reservation, not a maximum.</b> The rows are drawn whether the value fills them or not, so
        ///         a field does not change height as it is typed into and nothing below it moves. That is the same
        ///         choice <see cref="MonthGrid" /> makes in always drawing six weeks, and for the same reason: a
        ///         layout that resizes while you edit it is one where the thing you were about to click has gone
        ///         somewhere else.
        ///     </para>
        /// </summary>
        public int Lines { get; set; }
    }
}
