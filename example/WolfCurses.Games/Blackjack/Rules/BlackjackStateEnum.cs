// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

namespace WolfCurses.Games.Blackjack
{
    /// <summary>Where a round of blackjack has got to.</summary>
    public enum BlackjackStateEnum
    {
        /// <summary>The player is deciding whether to draw. The only state in which the hole card stays hidden.</summary>
        PlayerTurn = 0,

        /// <summary>The dealer is drawing to its fixed rule. Passed through rather than waited in — nobody chooses anything.</summary>
        DealerTurn = 1,

        /// <summary>The round is settled and paid, waiting for the player to deal again.</summary>
        RoundOver = 2
    }
}
