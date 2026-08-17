// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

namespace WolfCurses.Games.PacMan
{
    /// <summary>What one ghost is doing, as distinct from what all of them are doing.</summary>
    public enum GhostStateEnum
    {
        /// <summary>Hunting normally, following whatever the board-wide mode says.</summary>
        Hunting = 0,

        /// <summary>Blue and edible, because the player ate a power pellet. Moves at half speed and picks at random.</summary>
        Frightened = 1,

        /// <summary>A pair of eyes going home. Passes through everything and moves at double speed.</summary>
        Eaten = 2
    }
}
