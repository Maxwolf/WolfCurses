// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

namespace WolfCurses.Games.PacMan
{
    /// <summary>
    ///     Which ghost this is, which is the same thing as saying how it hunts.
    ///     <para>
    ///         The four of them run <i>identical</i> movement code and differ only in the one line that answers
    ///         "which square am I heading for?" — that is the whole trick of the original game, and the reason four
    ///         ghosts with four one-line rules feel like they are cooperating when nothing in the program says they
    ///         should. See <see cref="Ghost.Target" />.
    ///     </para>
    /// </summary>
    public enum GhostKindEnum
    {
        /// <summary>Red. Goes straight for the player, and is the reason you can never simply stand still.</summary>
        Blinky = 0,

        /// <summary>Pink. Aims four squares <i>in front of</i> the player, so it cuts corners and heads you off.</summary>
        Pinky = 1,

        /// <summary>Cyan. Aims through Blinky, so where it goes depends on where the red one already is.</summary>
        Inky = 2,

        /// <summary>Orange. Chases from a distance and loses its nerve up close, wandering off to its corner.</summary>
        Clyde = 3
    }
}
