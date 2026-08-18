// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

using System.Collections.Generic;
using WolfCurses.Games.Cards;

namespace WolfCurses.Games.Blackjack
{
    /// <summary>
    ///     A blackjack hand, and what it is worth.
    ///     <para>
    ///         <b>The whole difficulty of blackjack is the ace</b>, and it is worth stating the rule precisely
    ///         because the obvious implementations are both wrong. An ace is eleven unless that busts the hand, in
    ///         which case it is one — but with two aces only <i>one</i> of them can be eleven, since two elevens is
    ///         twenty-two before anything else is dealt. So the rule is not "each ace chooses its own value"; it is
    ///         "count every ace as one, then add ten once if there is an ace and the hand can afford it". That is a
    ///         single conditional and it is correct for any number of aces.
    ///     </para>
    /// </summary>
    public sealed class BlackjackHand
    {
        private readonly List<Card> _cards = new();

        /// <summary>The cards, in the order they were dealt.</summary>
        public IReadOnlyList<Card> Cards => _cards;

        /// <summary>How many cards are in the hand.</summary>
        public int Count => _cards.Count;

        /// <summary>
        ///     What the hand is worth, counting an ace as eleven where that fits under twenty-one.
        /// </summary>
        public int Value
        {
            get
            {
                var total = 0;
                var aces = 0;

                foreach (var card in _cards)
                {
                    if (card.Rank == CardRankEnum.Ace)
                        aces++;

                    total += card.IsFace ? 10 : (int) card.Rank;
                }

                // Ten added at most once, however many aces there are: a second ace counted high would bust on its
                // own. This one line is the entire ace rule.
                return aces > 0 && total + 10 <= 21 ? total + 10 : total;
            }
        }

        /// <summary>
        ///     Whether the hand is "soft" — it contains an ace being counted as eleven, so the next card cannot bust
        ///     it. The dealer's rule below cares, and so does anyone deciding whether to hit.
        /// </summary>
        public bool IsSoft
        {
            get
            {
                var total = 0;
                var aces = 0;

                foreach (var card in _cards)
                {
                    if (card.Rank == CardRankEnum.Ace)
                        aces++;

                    total += card.IsFace ? 10 : (int) card.Rank;
                }

                return aces > 0 && total + 10 <= 21;
            }
        }

        /// <summary>Over twenty-one.</summary>
        public bool IsBust => Value > 21;

        /// <summary>
        ///     A natural: twenty-one on the first two cards. Distinct from any other twenty-one, because it pays more
        ///     and because it beats a twenty-one made from three cards.
        /// </summary>
        public bool IsBlackjack => _cards.Count == 2 && Value == 21;

        /// <summary>Adds a card.</summary>
        /// <param name="card">The card dealt.</param>
        public void Add(Card card)
        {
            _cards.Add(card);
        }

        /// <summary>Empties the hand for the next deal.</summary>
        public void Clear()
        {
            _cards.Clear();
        }

        /// <summary>The hand as cards on a table, all face up.</summary>
        /// <returns>What a renderer needs.</returns>
        public IReadOnlyList<TableCard> ToTable()
        {
            var table = new List<TableCard>(_cards.Count);
            foreach (var card in _cards)
                table.Add(new TableCard(card));

            return table;
        }
    }
}
