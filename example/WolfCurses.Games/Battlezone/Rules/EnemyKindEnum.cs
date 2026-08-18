// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

namespace WolfCurses.Games.Battlezone
{
    /// <summary>What is out there on the plain, in the order it starts turning up.</summary>
    public enum EnemyKindEnum
    {
        /// <summary>The ordinary enemy tank: slow to turn, which is the whole of the game.</summary>
        Tank = 0,

        /// <summary>Faster, tougher to out-turn, and worth more. Starts appearing once the player can handle one.</summary>
        SuperTank = 1,

        /// <summary>
        ///     Drifts across the plain in a straight line and never fires a shot. It is worth a great deal, which
        ///     makes it a trap rather than a gift: taking it means turning away from something that <i>is</i> shooting.
        /// </summary>
        Saucer = 2
    }
}
