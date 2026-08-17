// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/16/2026

namespace WolfCurses.Games
{
    /// <summary>
    ///     Which way the player just pushed. Lives at the root of the project rather than inside
    ///     <see cref="Snake.SnakeBoard" />, its only reader today, because an arrow key means the same thing to every
    ///     game that is steered rather than typed at, and the next one should not define this again.
    /// </summary>
    public enum DirectionEnum
    {
        /// <summary>No direction at all; a key that was not one of the four.</summary>
        None = 0,

        /// <summary>Toward row zero.</summary>
        Up = 1,

        /// <summary>Toward the last row.</summary>
        Down = 2,

        /// <summary>Toward column zero.</summary>
        Left = 3,

        /// <summary>Toward the last column.</summary>
        Right = 4
    }
}
