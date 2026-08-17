// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/16/2026

using System;

namespace WolfCurses.Games.Tetris
{
    /// <summary>
    ///     One piece, in one orientation: a square grid of filled and empty cells, plus which of the seven it is.
    ///     Immutable — rotating hands back a new one — so the well can try a rotation, look at it, and throw it away
    ///     if it does not fit, which is the whole of what a wall kick is.
    /// </summary>
    public sealed class Tetromino
    {
        private readonly bool[,] _cells;

        private Tetromino(TetrominoEnum kind, bool[,] cells)
        {
            Kind = kind;
            _cells = cells;
        }

        /// <summary>Which of the seven this is.</summary>
        public TetrominoEnum Kind { get; }

        /// <summary>How many cells across the square box this piece rotates inside is.</summary>
        public int Size => _cells.GetLength(0);

        /// <summary>Whether the cell at that spot in the piece's own box is filled.</summary>
        /// <param name="x">Column within the piece's box.</param>
        /// <param name="y">Row within the piece's box.</param>
        /// <returns>True when that part of the box is part of the piece.</returns>
        public bool IsFilled(int x, int y) => _cells[y, x];

        /// <summary>
        ///     Builds a piece in its spawn orientation.
        ///     <para>
        ///         Each piece rotates inside its own square box, and the box sizes are not all the same: the I needs
        ///         four so its two orientations are not off-center, the O needs only two (which is also why rotating
        ///         it does nothing — a full 2x2 rotates onto itself), and the rest fit in three. Writing them as rows
        ///         of text rather than as coordinate lists is deliberate: a wrong cell in a picture is visible, and a
        ///         wrong cell in a list of numbers is not.
        ///     </para>
        /// </summary>
        /// <param name="kind">Which piece to build.</param>
        /// <returns>The piece, oriented the way it arrives at the top of the well.</returns>
        public static Tetromino Create(TetrominoEnum kind)
        {
            var rows = kind switch
            {
                TetrominoEnum.I => new[]
                {
                    "....",
                    "####",
                    "....",
                    "...."
                },
                TetrominoEnum.O => new[]
                {
                    "##",
                    "##"
                },
                TetrominoEnum.T => new[]
                {
                    ".#.",
                    "###",
                    "..."
                },
                TetrominoEnum.S => new[]
                {
                    ".##",
                    "##.",
                    "..."
                },
                TetrominoEnum.Z => new[]
                {
                    "##.",
                    ".##",
                    "..."
                },
                TetrominoEnum.J => new[]
                {
                    "#..",
                    "###",
                    "..."
                },
                TetrominoEnum.L => new[]
                {
                    "..#",
                    "###",
                    "..."
                },
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "That is not one of the seven pieces.")
            };

            var size = rows.Length;
            var cells = new bool[size, size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                cells[y, x] = rows[y][x] == '#';

            return new Tetromino(kind, cells);
        }

        /// <summary>
        ///     The same piece turned a quarter turn.
        ///     <para>
        ///         Rotating within the piece's own box rather than about a computed center is what keeps this to one
        ///         line of arithmetic and makes it exactly reversible: four clockwise turns are the identity, for
        ///         every piece, with no accumulated drift to correct for. What it costs is that a piece can shift
        ///         within its box as it turns, which is the reason the well has to try shoving it sideways when the
        ///         turn does not fit.
        ///     </para>
        /// </summary>
        /// <param name="clockwise">True for a quarter turn clockwise, false for counter-clockwise.</param>
        /// <returns>A new piece in the rotated orientation.</returns>
        public Tetromino Rotate(bool clockwise)
        {
            var size = Size;
            var rotated = new bool[size, size];

            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                if (clockwise)
                    rotated[x, size - 1 - y] = _cells[y, x];
                else
                    rotated[size - 1 - x, y] = _cells[y, x];
            }

            return new Tetromino(Kind, rotated);
        }
    }
}
