// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

using System;
using System.Collections.Generic;
using WolfCurses.Core;
using WolfCurses.Games.Cards;

namespace WolfCurses.Games.Poker
{
    /// <summary>
    ///     Five-card draw against a paytable — the "Jacks or Better" game, where you are paid for what you make
    ///     rather than for beating anybody.
    ///     <para>
    ///         <b>Draw poker rather than hold'em, and that is a scoping decision worth stating.</b> Hold'em needs
    ///         opponents, and opponents need betting models, bluffing and position — which is a far bigger game than
    ///         anything else in this arcade and would bury the part that is actually interesting to read. Draw poker
    ///         keeps the whole of the interest in <see cref="PokerHand" />: what five cards are worth, including the
    ///         two ace rules that everybody gets wrong. It also pairs properly with blackjack next door, which is
    ///         the opposite shape — a game entirely about an opponent with no hand ranking at all.
    ///     </para>
    /// </summary>
    public sealed class PokerGame
    {
        /// <summary>What the player starts with.</summary>
        public const int StartingChips = 500;

        /// <summary>The stake per hand.</summary>
        public const int BetSize = 25;

        private readonly Deck _deck;
        private readonly List<Card> _hand = new(PokerHand.HandSize);
        private readonly bool[] _held = new bool[PokerHand.HandSize];

        /// <summary>Initializes a new instance of the <see cref="PokerGame" /> class and deals the first hand.</summary>
        /// <param name="random">The simulation's shared random source.</param>
        public PokerGame(Randomizer random)
        {
            _deck = new Deck(random ?? throw new ArgumentNullException(nameof(random)));
            Chips = StartingChips;
            Deal();
        }

        /// <summary>The five cards in front of the player.</summary>
        public IReadOnlyList<Card> Hand => _hand;

        /// <summary>What the player has left.</summary>
        public int Chips { get; private set; }

        /// <summary>The most they have had at once, which is the score worth keeping.</summary>
        public int BestChips { get; private set; } = StartingChips;

        /// <summary>How many hands have been played out.</summary>
        public int HandsPlayed { get; private set; }

        /// <summary>Whether the player is choosing what to keep, as opposed to looking at a finished hand.</summary>
        public bool IsChoosing { get; private set; }

        /// <summary>What the finished hand was, once it is finished.</summary>
        public PokerCategoryEnum Category { get; private set; }

        /// <summary>What the last hand paid, negative when it only cost the stake.</summary>
        public int LastPayout { get; private set; }

        /// <summary>What just happened, for the screen to show.</summary>
        public string Message { get; private set; } = string.Empty;

        /// <summary>True once there is not enough left to play another hand.</summary>
        public bool IsBroke => Chips < BetSize && !IsChoosing;

        /// <summary>Whether a card is being kept.</summary>
        /// <param name="index">Which card, from zero.</param>
        /// <returns>True when it is held.</returns>
        public bool IsHeld(int index)
        {
            return index >= 0 && index < _held.Length && _held[index];
        }

        /// <summary>Keeps or releases a card. Ignored once the hand is finished.</summary>
        /// <param name="index">Which card, from zero.</param>
        public void ToggleHold(int index)
        {
            if (!IsChoosing || index < 0 || index >= _held.Length)
                return;

            _held[index] = !_held[index];
        }

        /// <summary>The hand as cards on a table. A held card is face up like any other — the marker is drawn under it.</summary>
        /// <returns>What a renderer needs.</returns>
        public IReadOnlyList<TableCard> Table()
        {
            var table = new List<TableCard>(_hand.Count);
            foreach (var card in _hand)
                table.Add(new TableCard(card));

            return table;
        }

        /// <summary>Deals five fresh cards and takes the stake.</summary>
        public void Deal()
        {
            if (Chips < BetSize)
            {
                IsChoosing = false;
                Message = "Out of chips. ENTER for a fresh stake, ESC to leave the table.";
                return;
            }

            Chips -= BetSize;
            _hand.Clear();
            Array.Clear(_held);

            for (var i = 0; i < PokerHand.HandSize; i++)
                _hand.Add(_deck.Deal());

            IsChoosing = true;
            LastPayout = 0;
            Category = PokerCategoryEnum.HighCard;
            // Names the key rather than the action. "Then draw" is a description of what happens next and
            // leaves the player hunting for how to do it; the prompt says SPACE too, but the message is where
            // they are already looking.
            Message = "Press 1-5 to keep cards, then SPACE to draw.";
        }

        /// <summary>
        ///     Replaces everything not held, scores what is left, and pays it.
        /// </summary>
        public void Draw()
        {
            if (!IsChoosing)
                return;

            for (var i = 0; i < _hand.Count; i++)
            {
                if (!_held[i])
                    _hand[i] = _deck.Deal();
            }

            IsChoosing = false;
            HandsPlayed++;

            var (category, _) = PokerHand.Evaluate(_hand);
            Category = category;

            var payout = Payout(category);
            LastPayout = payout;
            Chips += payout;

            if (Chips > BestChips)
                BestChips = Chips;

            Message = payout > 0
                ? $"{PokerHand.Describe(category)} - paid {payout}. ENTER for the next hand."
                : $"{PokerHand.Describe(category)} - no good. Lost {BetSize}. ENTER for the next hand.";

            if (Chips < BetSize)
                Message += "  Out of chips - ENTER for a fresh stake, ESC to leave.";
        }

        /// <summary>
        ///     What a category pays, as a multiple of the stake.
        ///     <para>
        ///         <b>A pair only pays from jacks up</b>, which is the rule the game is named after and the only
        ///         place the paytable needs to know a rank rather than a category. It is also what stops the game
        ///         being a coin flip: a pair turns up in nearly half of all hands.
        ///     </para>
        /// </summary>
        /// <param name="category">What the hand made.</param>
        /// <returns>What it is worth, less the stake already taken.</returns>
        private int Payout(PokerCategoryEnum category)
        {
            var multiple = category switch
            {
                PokerCategoryEnum.RoyalFlush => 250,
                PokerCategoryEnum.StraightFlush => 50,
                PokerCategoryEnum.FourOfAKind => 25,
                PokerCategoryEnum.FullHouse => 9,
                PokerCategoryEnum.Flush => 6,
                PokerCategoryEnum.Straight => 4,
                PokerCategoryEnum.ThreeOfAKind => 3,
                PokerCategoryEnum.TwoPair => 2,
                PokerCategoryEnum.Pair => JacksOrBetter() ? 1 : 0,
                _ => 0
            };

            return multiple*BetSize;
        }

        /// <summary>Whether the pair in the hand is jacks or better, which is the only pair that pays.</summary>
        private bool JacksOrBetter()
        {
            var counts = new Dictionary<CardRankEnum, int>();
            foreach (var card in _hand)
                counts[card.Rank] = counts.TryGetValue(card.Rank, out var seen) ? seen + 1 : 1;

            foreach (var (rank, count) in counts)
            {
                if (count != 2)
                    continue;

                // Aces count, and they are rank one - so this cannot be a plain "greater than ten" comparison, which
                // is the obvious version and pays nothing for the best pair in the game.
                if (rank == CardRankEnum.Ace || rank >= CardRankEnum.Jack)
                    return true;
            }

            return false;
        }

        /// <summary>Stacks the deck so a test can deal an exact hand rather than shuffling until one turns up.</summary>
        /// <param name="cards">The cards to deal next, in order.</param>
        internal void StackDeck(params Card[] cards)
        {
            _deck.StackDeck(cards);
        }
    }
}
