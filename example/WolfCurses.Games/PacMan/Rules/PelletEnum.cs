// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

namespace WolfCurses.Games.PacMan
{
    /// <summary>What was in a cell the player just walked into.</summary>
    public enum PelletEnum
    {
        /// <summary>Nothing; the cell was already empty.</summary>
        None = 0,

        /// <summary>An ordinary pellet.</summary>
        Pellet = 1,

        /// <summary>One of the four big ones, which turns the ghosts blue.</summary>
        Power = 2
    }
}
