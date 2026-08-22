// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;

namespace WolfCurses.Apps.CardFile
{
    /// <summary>
    ///     One card: a fixed set of named fields and what is written in them. No console anywhere in here, so the
    ///     whole of it can be driven from a test.
    ///     <para>
    ///         <b>The field names are the single source of truth and everything else reads them.</b> They are the
    ///         labels on the card, the columns of the list, the header row of the file, the names the reader looks
    ///         columns up by, and the options offered when choosing which columns to show. Five things that have
    ///         to agree, agreeing because there is only one of them.
    ///     </para>
    ///     <para>
    ///         Values are addressed by position rather than by property, which is what lets every one of those five
    ///         be written as a loop instead of five branches naming Phone.
    ///     </para>
    /// </summary>
    public sealed class Card
    {
        /// <summary>Which field is the name, which is what the index is ordered and looked up by.</summary>
        public const int NameField = 0;

        /// <summary>Which field is the note, the one field allowed to run to several lines.</summary>
        public const int NotesField = 5;

        /// <summary>What a card starting with anything other than a letter is filed under.</summary>
        public const char OtherLetter = '#';

        /// <summary>What each field is called.</summary>
        public static IReadOnlyList<string> FieldNames { get; } =
            new[] {"Name", "Kind", "Phone", "Email", "Address", "Notes"};

        /// <summary>What is written in each field, never null.</summary>
        private readonly string[] _values = new string[FieldNames.Count];

        /// <summary>Initializes a blank card.</summary>
        public Card()
        {
            for (var i = 0; i < _values.Length; i++)
                _values[i] = string.Empty;
        }

        /// <summary>Initializes a card from its values in field order; missing ones are left blank.</summary>
        /// <param name="values">The values, in the order of <see cref="FieldNames" />.</param>
        public Card(params string[] values) : this()
        {
            if (values == null)
                return;

            for (var i = 0; i < _values.Length && i < values.Length; i++)
                _values[i] = values[i] ?? string.Empty;
        }

        /// <summary>How many fields a card has.</summary>
        public int Fields => _values.Length;

        /// <summary>What is written in a field. Reading past the end gives empty; writing past it does nothing.</summary>
        /// <param name="field">Which field.</param>
        public string this[int field]
        {
            get => field >= 0 && field < _values.Length ? _values[field] : string.Empty;
            set
            {
                if (field >= 0 && field < _values.Length)
                    _values[field] = value ?? string.Empty;
            }
        }

        /// <summary>Whose card this is.</summary>
        public string Name => this[NameField];

        /// <summary>
        ///     The tab this card is filed behind: its first letter, or <see cref="OtherLetter" /> for a card whose
        ///     name does not start with one. A card index has always had that last tab and it is not tidiness: a
        ///     name beginning with a digit or a quote has to be filed somewhere reachable.
        /// </summary>
        public char IndexLetter
        {
            get
            {
                var name = Name;

                if (name.Length == 0 || !char.IsLetter(name[0]))
                    return OtherLetter;

                return char.ToUpperInvariant(name[0]);
            }
        }

        /// <summary>Whether any field of this card holds the given text, which is what Find asks.</summary>
        /// <param name="needle">The text looked for; case is ignored.</param>
        /// <returns>TRUE when some field holds it.</returns>
        public bool Matches(string needle)
        {
            if (string.IsNullOrEmpty(needle))
                return false;

            foreach (var value in _values)
            {
                if (value.Contains(needle, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>A copy of this card's values, which is the row it is written to a file as.</summary>
        /// <returns>The values in field order.</returns>
        public string[] ToRow()
        {
            return (string[]) _values.Clone();
        }

        /// <summary>
        ///     Puts two cards in filing order: by name, ignoring case, and by the exact name when two differ only
        ///     in it, so a deck's order does not depend on which of two spellings was read first.
        /// </summary>
        /// <param name="left">One card.</param>
        /// <param name="right">The other.</param>
        /// <returns>The usual less-than, equal, greater-than.</returns>
        public static int Compare(Card left, Card right)
        {
            var byName = string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);

            return byName != 0 ? byName : string.CompareOrdinal(left.Name, right.Name);
        }
    }
}
