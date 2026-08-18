// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

namespace WolfCurses.Games.Cards
{
    /// <summary>
    ///     The thirteen ranks, numbered so that the value <i>is</i> the pip count — <c>(int) Seven == 7</c>.
    ///     <para>
    ///         <b>The ace is one here and high everywhere it matters</b>, which is deliberate and is the only thing
    ///         about this enum worth knowing. Numbering it fourteen would make poker comparisons fall out for free
    ///         and would break the pip cards, which are the ones you actually count; numbering it one keeps the
    ///         arithmetic honest and pushes "an ace is high" into the two places that care —
    ///         <see cref="Poker.PokerHand" />, which sorts it up, and <see cref="Blackjack.BlackjackHand" />, which
    ///         scores it as eleven until that would bust.
    ///     </para>
    /// </summary>
    public enum CardRankEnum
    {
        /// <summary>Ace. One by number, high by convention — see the type docs.</summary>
        Ace = 1,

        /// <summary>Two.</summary>
        Two = 2,

        /// <summary>Three.</summary>
        Three = 3,

        /// <summary>Four.</summary>
        Four = 4,

        /// <summary>Five.</summary>
        Five = 5,

        /// <summary>Six.</summary>
        Six = 6,

        /// <summary>Seven.</summary>
        Seven = 7,

        /// <summary>Eight.</summary>
        Eight = 8,

        /// <summary>Nine.</summary>
        Nine = 9,

        /// <summary>Ten.</summary>
        Ten = 10,

        /// <summary>Jack.</summary>
        Jack = 11,

        /// <summary>Queen.</summary>
        Queen = 12,

        /// <summary>King.</summary>
        King = 13
    }
}
