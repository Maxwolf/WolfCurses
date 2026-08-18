// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

namespace WolfCurses.Games.Cards
{
    /// <summary>
    ///     The four suits, in the order the artwork files are named and the order a fresh deck comes in.
    ///     <para>
    ///         No suit outranks another in either game here — blackjack does not look at suit at all, and poker uses
    ///         it only to ask whether five cards share one. Games that do rank suits (bridge, hearts) can order these
    ///         however they like without this enum having an opinion.
    ///     </para>
    /// </summary>
    public enum CardSuitEnum
    {
        /// <summary>Clubs, black.</summary>
        Clubs = 0,

        /// <summary>Diamonds, red.</summary>
        Diamonds = 1,

        /// <summary>Hearts, red.</summary>
        Hearts = 2,

        /// <summary>Spades, black.</summary>
        Spades = 3
    }
}
