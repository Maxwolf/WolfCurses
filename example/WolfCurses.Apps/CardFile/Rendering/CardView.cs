// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System.Collections.Generic;
using WolfCurses.Window.Control;

namespace WolfCurses.Apps.CardFile
{
    /// <summary>
    ///     One card, drawn as a card: its fields down the left, what they hold beside them, and room at the bottom
    ///     for a note that runs to several lines.
    ///     <para>
    ///         <b>This view has no hit test</b>, which is the whole reason <see cref="FieldList" /> was worth
    ///         putting in the library. Each of the planner's four views needed one of its own because none of them
    ///         had a control that remembered where it drew things; the card is nothing but a control that does, so
    ///         the dialog asks the control directly and there is no arithmetic here to get wrong.
    ///     </para>
    /// </summary>
    internal static class CardView
    {
        /// <summary>
        ///     How many rows the note gets, chosen so the fields exactly fill the box: five one-line fields, this,
        ///     and the two border rows come to <see cref="CardChrome.BodyRows" />.
        /// </summary>
        public const int NoteLines = 8;

        /// <summary>How many rows a field is drawn in.</summary>
        /// <param name="field">Which field.</param>
        /// <returns>The rows kept for it.</returns>
        public static int LinesFor(int field)
        {
            return field == Card.NotesField ? NoteLines : 1;
        }

        /// <summary>Builds the fields a card is drawn as, positioned where the chrome puts them.</summary>
        /// <param name="width">The console width.</param>
        /// <returns>The field list, its values still blank.</returns>
        public static FieldList Build(int width)
        {
            var entries = new FieldListEntry[Card.FieldNames.Count];

            for (var i = 0; i < entries.Length; i++)
                entries[i] = new FieldListEntry(Card.FieldNames[i], string.Empty, LinesFor(i));

            return new FieldList(entries)
            {
                Width = width - 2,
                Gap = 2,
                Row = CardChrome.FieldRow,
                Column = CardChrome.FieldColumn,
                LabelStyle = DosTheme.Title,
                ValueStyle = DosTheme.Field,
                SelectedStyle = DosTheme.Highlight
            };
        }

        /// <summary>Copies a card's values into the fields, or empties them when there is no card.</summary>
        /// <param name="fields">The field list.</param>
        /// <param name="card">The card, or null.</param>
        public static void Fill(FieldList fields, Card card)
        {
            for (var i = 0; i < fields.Entries.Count; i++)
                fields.Entries[i].Value = card == null ? string.Empty : card[i];
        }

        /// <summary>Draws the view.</summary>
        /// <param name="fields">The chosen card's fields, already filled in.</param>
        /// <param name="heading">What is notched into the left of the top edge.</param>
        /// <param name="counter">What is notched into the right of it.</param>
        /// <param name="width">The console width.</param>
        /// <param name="height">How many rows the body gets.</param>
        /// <returns>The view's rows.</returns>
        public static IReadOnlyList<string> Render(FieldList fields, string heading, string counter, int width,
            int height)
        {
            var rows = new List<string>(height) {CardChrome.Titled(heading, counter, width)};

            var body = fields.Render();

            for (var i = 0; i < height - 2; i++)
                rows.Add(i < body.Count ? CardChrome.Framed(body[i]) : CardChrome.Blank(width));

            rows.Add(CardChrome.Bottom(width));

            return rows;
        }
    }
}
