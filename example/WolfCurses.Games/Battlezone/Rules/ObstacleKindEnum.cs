// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

namespace WolfCurses.Games.Battlezone
{
    /// <summary>The scenery. Both kinds stop a tank and stop a shell; they differ only in what they look like.</summary>
    public enum ObstacleKindEnum
    {
        /// <summary>A block.</summary>
        Cube = 0,

        /// <summary>A four-sided pyramid, which reads differently at a distance and so helps as a landmark.</summary>
        Pyramid = 1
    }
}
