// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

namespace WolfCurses.Games.PacMan
{
    /// <summary>
    ///     What every hunting ghost is doing at once, on a schedule the player cannot see but can feel.
    ///     <para>
    ///         The alternation is the reason the original game is playable at all: without the scatter periods the
    ///         ghosts converge and never let go, and the game becomes a short one. It is also why the maze corners
    ///         are safe for a few seconds at a time, which is the rhythm a good player is really reading.
    ///     </para>
    /// </summary>
    public enum GhostModeEnum
    {
        /// <summary>Everyone heads for their own corner and leaves the player alone.</summary>
        Scatter = 0,

        /// <summary>Everyone hunts, each in their own way.</summary>
        Chase = 1
    }
}
