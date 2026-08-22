// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;

namespace WolfCurses.Apps.CardFile
{
    /// <summary>
    ///     The cards, kept in filing order. No console anywhere in here.
    ///     <para>
    ///         <b>The deck is sorted, always, and that is what makes it an index rather than a list.</b> A card
    ///         file whose cards are in the order somebody happened to type them cannot be flipped to a letter, and
    ///         the letter tabs are the whole idea. The consequence is that a card <i>moves</i> when it is renamed,
    ///         which is why <see cref="Resort" /> hands back where it went: a cursor left on the old position is
    ///         pointing at somebody else.
    ///     </para>
    /// </summary>
    public sealed class CardDeck
    {
        /// <summary>The cards, in filing order.</summary>
        private readonly List<Card> _cards = new();

        /// <summary>The cards, in filing order.</summary>
        public IReadOnlyList<Card> Cards => _cards;

        /// <summary>How many cards there are.</summary>
        public int Count => _cards.Count;

        /// <summary>Whether anything has changed since the deck was loaded or last saved.</summary>
        public bool IsModified { get; private set; }

        /// <summary>
        ///     The line ending the file arrived with, written back out unchanged, the same stance the planner and
        ///     the text buffer take: opening a file and saving it untouched should give the same bytes.
        /// </summary>
        public string NewLine { get; set; } = Environment.NewLine;

        /// <summary>
        ///     Files a card, and says where it landed.
        ///     <para>
        ///         A card with no name is refused rather than filed under nothing. The index is by name and the
        ///         tabs are by first letter, so a nameless card is one nothing on this screen could ever reach.
        ///     </para>
        /// </summary>
        /// <param name="card">The card.</param>
        /// <returns>Where it was filed, or -1 when it was refused.</returns>
        public int Add(Card card)
        {
            if (card == null || string.IsNullOrWhiteSpace(card.Name))
                return -1;

            var at = Place(card);

            _cards.Insert(at, card);
            IsModified = true;

            return at;
        }

        /// <summary>Throws a card away.</summary>
        /// <param name="index">Which card.</param>
        /// <returns>TRUE when there was one there to throw away.</returns>
        public bool RemoveAt(int index)
        {
            if (index < 0 || index >= _cards.Count)
                return false;

            _cards.RemoveAt(index);
            IsModified = true;

            return true;
        }

        /// <summary>
        ///     Puts a card back in filing order after it has been edited, and says where it went. The card itself
        ///     is followed rather than the position, since a rename is exactly the edit that moves it.
        /// </summary>
        /// <param name="index">Where the card is now.</param>
        /// <returns>Where it is after refiling, or the index unchanged when there is no such card.</returns>
        public int Resort(int index)
        {
            if (index < 0 || index >= _cards.Count)
                return index;

            var card = _cards[index];

            _cards.RemoveAt(index);
            var at = Place(card);
            _cards.Insert(at, card);

            IsModified = true;

            return at;
        }

        /// <summary>Says the deck matches what is on disk, which is what saving it makes true.</summary>
        public void MarkSaved()
        {
            IsModified = false;
        }

        /// <summary>Marks the deck as changed, for an edit made to a card the deck cannot see happening.</summary>
        public void Touch()
        {
            IsModified = true;
        }

        /// <summary>The first card filed behind a letter tab, or -1 when that tab has nothing behind it.</summary>
        /// <param name="letter">The tab; matched against <see cref="Card.IndexLetter" />.</param>
        /// <returns>The card's index, or -1.</returns>
        public int FirstBehind(char letter)
        {
            var wanted = char.ToUpperInvariant(letter);

            for (var i = 0; i < _cards.Count; i++)
            {
                if (_cards[i].IndexLetter == wanted)
                    return i;
            }

            return -1;
        }

        /// <summary>Whether any card is filed behind a letter tab, which is what greys out the empty ones.</summary>
        /// <param name="letter">The tab.</param>
        /// <returns>TRUE when something is behind it.</returns>
        public bool HasBehind(char letter)
        {
            return FirstBehind(letter) >= 0;
        }

        /// <summary>
        ///     The next card holding some text, searching every field.
        ///     <para>
        ///         <b>It starts after the card given and wraps all the way round to it</b>, which is the same
        ///         asymmetry <see cref="Documents.TextSearch" /> documents and for the same reason: searching from
        ///         where you are finds where you are, forever, and Find Next never moves. Wrapping the whole way
        ///         means a deck with one match still finds it rather than reporting nothing.
        ///     </para>
        /// </summary>
        /// <param name="needle">The text looked for; case is ignored.</param>
        /// <param name="after">The card to start after; -1 searches from the beginning.</param>
        /// <returns>The card's index, or -1 when nothing holds it.</returns>
        public int Find(string needle, int after)
        {
            if (string.IsNullOrEmpty(needle) || _cards.Count == 0)
                return -1;

            for (var step = 1; step <= _cards.Count; step++)
            {
                var at = ((after + step) % _cards.Count + _cards.Count) % _cards.Count;

                if (_cards[at].Matches(needle))
                    return at;
            }

            return -1;
        }

        /// <summary>
        ///     Where a card belongs: after every card that files before it, and after any that file the same, so
        ///     two cards with one name keep the order they were read in rather than swapping about on every edit.
        /// </summary>
        /// <param name="card">The card to place.</param>
        /// <returns>The index to insert it at.</returns>
        private int Place(Card card)
        {
            for (var i = 0; i < _cards.Count; i++)
            {
                if (Card.Compare(_cards[i], card) > 0)
                    return i;
            }

            return _cards.Count;
        }
    }
}
