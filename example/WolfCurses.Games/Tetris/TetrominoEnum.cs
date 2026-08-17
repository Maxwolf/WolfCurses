// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/16/2026

namespace WolfCurses.Games.Tetris
{
    /// <summary>
    ///     The seven tetrominoes, named after the letters they look like — which is what everybody has called them
    ///     since 1984, and is also how their colors are remembered.
    /// </summary>
    public enum TetrominoEnum
    {
        /// <summary>The straight four, the only piece that clears four rows at once.</summary>
        I = 1,

        /// <summary>The square, and the only piece rotating does nothing to.</summary>
        O = 2,

        /// <summary>The three-with-a-nub.</summary>
        T = 3,

        /// <summary>One of the two S-bends.</summary>
        S = 4,

        /// <summary>The other S-bend, mirrored.</summary>
        Z = 5,

        /// <summary>The left-handed L.</summary>
        J = 6,

        /// <summary>The right-handed L.</summary>
        L = 7
    }
}
