// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/16/2026

using System;

namespace WolfCurses.Games.Chess
{
    /// <summary>
    ///     Which castles are still legally available, ignoring whether they happen to be blocked right now.
    ///     <para>
    ///         A right is lost permanently the moment the king moves, that rook moves, <b>or that rook is captured
    ///         on its home square</b>. The last one is the clause everybody forgets, and it is worth stating here
    ///         because it is not the moving side losing a right — it is the side being captured from, on a move it
    ///         did not make. A move generator that only clears rights for the piece that moved produces positions
    ///         where a side can castle with a rook that is no longer on the board.
    ///     </para>
    /// </summary>
    [Flags]
    public enum CastlingRightsEnum
    {
        /// <summary>Nobody may castle.</summary>
        None = 0,

        /// <summary>White may castle short (h1 rook).</summary>
        WhiteKingSide = 1,

        /// <summary>White may castle long (a1 rook).</summary>
        WhiteQueenSide = 2,

        /// <summary>Black may castle short (h8 rook).</summary>
        BlackKingSide = 4,

        /// <summary>Black may castle long (a8 rook).</summary>
        BlackQueenSide = 8,

        /// <summary>Every castle still available, which is how a game starts.</summary>
        All = WhiteKingSide | WhiteQueenSide | BlackKingSide | BlackQueenSide
    }
}
