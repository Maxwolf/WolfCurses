// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

namespace WolfCurses.Games.MissileCommand
{
    /// <summary>
    ///     What a <see cref="Missile" /> is, which decides how it is drawn, what it is worth and whether a blast can
    ///     touch it at all.
    /// </summary>
    public enum MissileKindEnum
    {
        /// <summary>The plain incoming warhead. Falls in a straight line from the sky to whatever it was aimed at.</summary>
        Icbm = 1,

        /// <summary>
        ///     An ICBM carrying more warheads, which it lets go of once on the way down. Identical to
        ///     <see cref="Icbm" /> in every other respect — the split is a flag rather than a type, because after it
        ///     has happened there is nothing left to tell them apart.
        /// </summary>
        Mirv = 2,

        /// <summary>
        ///     Slower, and it steers around a blast rather than flying into one, so it cannot be dealt with by
        ///     shooting where it is going to be.
        /// </summary>
        SmartBomb = 3,

        /// <summary>
        ///     The player's counter-missile. Flies to the point it was aimed at and detonates there, and is the one
        ///     kind a blast does not destroy — you may fire straight through your own cloud.
        /// </summary>
        Counter = 4
    }
}
