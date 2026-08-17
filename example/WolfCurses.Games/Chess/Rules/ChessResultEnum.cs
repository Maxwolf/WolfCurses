// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/16/2026

namespace WolfCurses.Games.Chess
{
    /// <summary>How a game ended, or that it has not.</summary>
    public enum ChessResultEnum
    {
        /// <summary>Still being played.</summary>
        InProgress = 0,

        /// <summary>The side to move is in check and has no legal move.</summary>
        Checkmate = 1,

        /// <summary>The side to move has no legal move and is <b>not</b> in check, which is a draw.</summary>
        Stalemate = 2,

        /// <summary>A hundred plies without a capture or a pawn move.</summary>
        FiftyMoveRule = 3,

        /// <summary>The same position, with the same rights, has appeared three times.</summary>
        ThreefoldRepetition = 4,

        /// <summary>Neither side has the material to force mate.</summary>
        InsufficientMaterial = 5,

        /// <summary>Somebody gave up.</summary>
        Resignation = 6
    }
}
