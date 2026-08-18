// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

namespace WolfCurses.Games.Poker
{
    /// <summary>
    ///     What a five-card hand is, best last.
    ///     <para>
    ///         <b>Numbered in increasing order of strength on purpose</b>, so comparing two categories is comparing
    ///         two integers and <see cref="PokerHand" /> never needs a table of which beats which. The order is the
    ///         standard one and it is not arbitrary — each category is rarer than the one below it, which is the
    ///         whole reason the ranking is what it is.
    ///     </para>
    /// </summary>
    public enum PokerCategoryEnum
    {
        /// <summary>Nothing at all. The most likely hand by a distance.</summary>
        HighCard = 0,

        /// <summary>Two of a kind.</summary>
        Pair = 1,

        /// <summary>Two pairs.</summary>
        TwoPair = 2,

        /// <summary>Three of a kind.</summary>
        ThreeOfAKind = 3,

        /// <summary>Five in sequence. An ace may end it at either end.</summary>
        Straight = 4,

        /// <summary>Five of one suit.</summary>
        Flush = 5,

        /// <summary>Three of a kind and a pair.</summary>
        FullHouse = 6,

        /// <summary>Four of a kind.</summary>
        FourOfAKind = 7,

        /// <summary>A straight, all of one suit.</summary>
        StraightFlush = 8,

        /// <summary>Ten to ace, all of one suit. A straight flush, called out separately because it pays differently.</summary>
        RoyalFlush = 9
    }
}
