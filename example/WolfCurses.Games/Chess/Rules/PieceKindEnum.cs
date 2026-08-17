// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/16/2026

namespace WolfCurses.Games.Chess
{
    /// <summary>
    ///     What a piece is. <see cref="None" /> is an empty square rather than a piece, which is why it is zero — a
    ///     freshly allocated board is empty without anything having to say so.
    /// </summary>
    public enum PieceKindEnum
    {
        /// <summary>No piece: an empty square.</summary>
        None = 0,

        /// <summary>Moves one forward, captures diagonally, and is the only piece that does those differently.</summary>
        Pawn = 1,

        /// <summary>The only piece that jumps.</summary>
        Knight = 2,

        /// <summary>Slides diagonally, and so never leaves the colour of square it started on.</summary>
        Bishop = 3,

        /// <summary>Slides orthogonally, and is the piece castling moves alongside the king.</summary>
        Rook = 4,

        /// <summary>Slides both ways.</summary>
        Queen = 5,

        /// <summary>Moves one square. Cannot be captured — the rules end the game first.</summary>
        King = 6
    }
}
