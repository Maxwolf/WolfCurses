// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

using System;
using System.Collections.Generic;
using WolfCurses.Core;

namespace WolfCurses.Games.Cards
{
    /// <summary>
    ///     A deck of cards that can be shuffled and dealt from. Shared by every card game here, which is the whole
    ///     point of there being a <c>Cards</c> folder at all — blackjack and poker disagree about almost everything
    ///     and agree completely about what a deck is.
    ///     <para>
    ///         The shuffle itself is <see cref="Randomizer.Shuffle{T}" />, from the library. It used to be four
    ///         lines here and four more in <see cref="Tetris.Rules.TetrisWell" />'s piece bag, which is exactly the
    ///         kind of duplication these examples exist to notice.
    ///     </para>
    /// </summary>
    public sealed class Deck
    {
        private readonly Randomizer _random;
        private readonly List<Card> _cards = new(52);
        private int _dealt;

        /// <summary>Initializes a new instance of the <see cref="Deck" /> class, shuffled and ready.</summary>
        /// <param name="random">The simulation's shared random source.</param>
        public Deck(Randomizer random)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
            Shuffle();
        }

        /// <summary>How many cards are left to deal.</summary>
        public int Remaining => _cards.Count - _dealt;

        /// <summary>How many cards a full deck holds.</summary>
        public int Count => _cards.Count;

        /// <summary>Puts all fifty-two back and shuffles them.</summary>
        public void Shuffle()
        {
            _cards.Clear();

            foreach (CardSuitEnum suit in Enum.GetValues(typeof (CardSuitEnum)))
            foreach (CardRankEnum rank in Enum.GetValues(typeof (CardRankEnum)))
                _cards.Add(new Card(rank, suit));

            _random.Shuffle(_cards);
            _dealt = 0;
        }

        /// <summary>
        ///     Deals the next card off the top.
        ///     <para>
        ///         Dealing off an exhausted deck reshuffles rather than throwing. A card game that runs out mid-hand
        ///         has a bug in its own accounting, but the answer to it is not a crash in front of the player, and a
        ///         reshuffle is what a real dealer does with the discards anyway.
        ///     </para>
        /// </summary>
        /// <returns>The next card.</returns>
        public Card Deal()
        {
            if (Remaining == 0)
                Shuffle();

            return _cards[_dealt++];
        }

        /// <summary>
        ///     Takes a named card out of what is left, for tests that need a particular hand rather than one they
        ///     had to deal their way into. Leaves the deck one card shorter and otherwise untouched.
        /// </summary>
        /// <param name="card">The card to find.</param>
        /// <returns>True when it was still in the deck.</returns>
        internal bool TakeSpecific(Card card)
        {
            for (var i = _dealt; i < _cards.Count; i++)
            {
                if (_cards[i] != card)
                    continue;

                (_cards[i], _cards[_dealt]) = (_cards[_dealt], _cards[i]);
                _dealt++;
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Stacks the deck so the next cards dealt are the ones given, in order. For tests that need an exact
        ///     hand — a pair of aces, a made flush — rather than shuffling until one turns up.
        /// </summary>
        /// <param name="cards">What to deal next.</param>
        internal void StackDeck(params Card[] cards)
        {
            if (cards == null)
                return;

            Shuffle();

            for (var i = 0; i < cards.Length; i++)
            {
                var at = _cards.IndexOf(cards[i]);
                (_cards[i], _cards[at]) = (_cards[at], _cards[i]);
            }
        }
    }
}
