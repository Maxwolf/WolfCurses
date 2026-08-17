// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/16/2026

namespace WolfCurses.Games.Chess
{
    /// <summary>Which side a piece belongs to. Also which side is to move.</summary>
    public enum PieceColorEnum
    {
        /// <summary>Moves first, and moves up the board from rank 1 toward rank 8.</summary>
        White = 0,

        /// <summary>Moves down the board, from rank 8 toward rank 1.</summary>
        Black = 1
    }
}
