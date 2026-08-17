// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/16/2026

namespace WolfCurses.Games.Chess
{
    /// <summary>
    ///     Why a square is being highlighted. Ordered by which wins when a square qualifies for more than one, which
    ///     it routinely does — the cursor sits on a legal target of the piece you are holding, and the king in check
    ///     is often also the square the last move landed next to.
    /// </summary>
    public enum ChessSquareMarkEnum
    {
        /// <summary>Nothing special; draw the square its own colour.</summary>
        None = 0,

        /// <summary>The move just played came from or went to here.</summary>
        LastMove = 1,

        /// <summary>The piece being held may legally move here.</summary>
        Target = 2,

        /// <summary>A king is in check here.</summary>
        Check = 3,

        /// <summary>The piece the player has picked up.</summary>
        Selected = 4,

        /// <summary>Where the player's cursor is. Highest, because it is the thing they are steering.</summary>
        Cursor = 5
    }
}
