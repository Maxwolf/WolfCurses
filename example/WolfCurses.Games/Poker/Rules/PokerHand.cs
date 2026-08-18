// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

using System;
using System.Collections.Generic;
using WolfCurses.Games.Cards;

namespace WolfCurses.Games.Poker
{
    /// <summary>
    ///     Works out what five cards are worth. Pure, static, and the only interesting code in the poker game.
    ///     <para>
    ///         <b>The ace is the whole problem, twice over.</b> It is high for every purpose except one: the wheel,
    ///         A-2-3-4-5, which is a straight with the ace playing low. So the straight test runs twice — once with
    ///         aces high and once with the ace demoted to zero — and takes whichever works. Getting this wrong is the
    ///         single most common bug in a hand evaluator, and it is invisible until somebody is dealt the one hand
    ///         in two hundred and fifty-four that exposes it.
    ///     </para>
    ///     <para>
    ///         The second ace trap is in the *ranking*: a wheel is the <i>lowest</i> straight, so the hand's high
    ///         card is the five and not the ace. Reporting the ace would make A-2-3-4-5 beat 9-10-J-Q-K, which is
    ///         backwards.
    ///     </para>
    /// </summary>
    public static class PokerHand
    {
        /// <summary>How many cards a hand holds.</summary>
        public const int HandSize = 5;

        /// <summary>
        ///     Names what five cards are.
        /// </summary>
        /// <param name="cards">Exactly five cards, in any order.</param>
        /// <returns>The category, and the rank that names it — the pair's rank, the straight's top card.</returns>
        public static (PokerCategoryEnum Category, int HighRank) Evaluate(IReadOnlyList<Card> cards)
        {
            if (cards == null)
                throw new ArgumentNullException(nameof(cards));

            if (cards.Count != HandSize)
                throw new ArgumentException($"A poker hand is {HandSize} cards, not {cards.Count}.", nameof(cards));

            // Aces high: rank 1 becomes 14, so every comparison below is a plain integer one.
            var ranks = new int[HandSize];
            var flush = true;

            for (var i = 0; i < HandSize; i++)
            {
                ranks[i] = cards[i].Rank == CardRankEnum.Ace ? 14 : (int) cards[i].Rank;
                flush &= cards[i].Suit == cards[0].Suit;
            }

            Array.Sort(ranks);
            Array.Reverse(ranks);

            var (straight, straightHigh) = IsStraight(ranks);

            if (straight && flush)
            {
                return straightHigh == 14
                    ? (PokerCategoryEnum.RoyalFlush, 14)
                    : (PokerCategoryEnum.StraightFlush, straightHigh);
            }

            // Grouped by how many of each rank there are, biggest group first and higher rank breaking a tie - which
            // is what makes "the pair's rank" fall out of the first group without a second pass.
            var groups = GroupByCount(ranks);

            if (groups[0].Count == 4)
                return (PokerCategoryEnum.FourOfAKind, groups[0].Rank);

            if (groups[0].Count == 3 && groups.Count > 1 && groups[1].Count == 2)
                return (PokerCategoryEnum.FullHouse, groups[0].Rank);

            if (flush)
                return (PokerCategoryEnum.Flush, ranks[0]);

            if (straight)
                return (PokerCategoryEnum.Straight, straightHigh);

            if (groups[0].Count == 3)
                return (PokerCategoryEnum.ThreeOfAKind, groups[0].Rank);

            if (groups[0].Count == 2 && groups.Count > 1 && groups[1].Count == 2)
                return (PokerCategoryEnum.TwoPair, groups[0].Rank);

            if (groups[0].Count == 2)
                return (PokerCategoryEnum.Pair, groups[0].Rank);

            return (PokerCategoryEnum.HighCard, ranks[0]);
        }

        /// <summary>The name of a category, as a person would say it.</summary>
        /// <param name="category">What the hand is.</param>
        /// <returns>Its name.</returns>
        public static string Describe(PokerCategoryEnum category)
        {
            return category switch
            {
                PokerCategoryEnum.RoyalFlush => "Royal flush",
                PokerCategoryEnum.StraightFlush => "Straight flush",
                PokerCategoryEnum.FourOfAKind => "Four of a kind",
                PokerCategoryEnum.FullHouse => "Full house",
                PokerCategoryEnum.Flush => "Flush",
                PokerCategoryEnum.Straight => "Straight",
                PokerCategoryEnum.ThreeOfAKind => "Three of a kind",
                PokerCategoryEnum.TwoPair => "Two pair",
                PokerCategoryEnum.Pair => "Pair",
                _ => "Nothing"
            };
        }

        /// <summary>
        ///     Whether five sorted ranks run in sequence, and what the top card of that run is.
        ///     <para>
        ///         Tried twice: once as dealt, and once with an ace counted as one. The second pass is the wheel,
        ///         A-2-3-4-5, and its high card is the <b>five</b> — an ace playing low does not also get to be the
        ///         top of the straight it is at the bottom of.
        ///     </para>
        /// </summary>
        /// <param name="descending">The five ranks, aces as fourteen, sorted high to low.</param>
        /// <returns>Whether it is a straight, and its high card.</returns>
        private static (bool Straight, int High) IsStraight(IReadOnlyList<int> descending)
        {
            if (Runs(descending))
                return (true, descending[0]);

            // The ace is at the front, being fourteen. Move it to the back as a one and re-sort by hand: the other
            // four are already in order, so this is just "5 4 3 2 1" when it is the wheel and nonsense otherwise.
            if (descending[0] != 14)
                return (false, 0);

            var low = new int[HandSize];
            for (var i = 0; i < HandSize - 1; i++)
                low[i] = descending[i + 1];

            low[HandSize - 1] = 1;

            return Runs(low) ? (true, low[0]) : (false, 0);
        }

        /// <summary>Whether five ranks sorted high to low each step down by exactly one.</summary>
        private static bool Runs(IReadOnlyList<int> descending)
        {
            for (var i = 1; i < descending.Count; i++)
            {
                if (descending[i] != descending[i - 1] - 1)
                    return false;
            }

            return true;
        }

        /// <summary>
        ///     Counts how many of each rank there are, biggest group first, higher rank breaking ties.
        /// </summary>
        /// <param name="ranks">The five ranks, aces as fourteen.</param>
        /// <returns>The groups, most numerous first.</returns>
        private static List<(int Rank, int Count)> GroupByCount(IReadOnlyList<int> ranks)
        {
            var counts = new Dictionary<int, int>();
            foreach (var rank in ranks)
                counts[rank] = counts.TryGetValue(rank, out var seen) ? seen + 1 : 1;

            var groups = new List<(int Rank, int Count)>(counts.Count);
            foreach (var (rank, count) in counts)
                groups.Add((rank, count));

            groups.Sort((a, b) => a.Count != b.Count ? b.Count.CompareTo(a.Count) : b.Rank.CompareTo(a.Rank));
            return groups;
        }
    }
}
