// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

namespace WolfCurses.Games.Battlezone
{
    /// <summary>
    ///     What a line in the scene <i>means</i>, which is all the two views are allowed to disagree about.
    ///     <para>
    ///         <see cref="BattleScene" /> walks the world once and hands out screen-space segments tagged with one
    ///         of these; <see cref="BattlezoneArt" /> turns each into a colour and <see cref="BattlezoneText" />
    ///         turns each into a character. Everything else — the projection, the clipping, where the horizon sits,
    ///         which way the radar sweep is pointing — happens in exactly one place, so the picture and the
    ///         character view cannot drift apart. Two separate scene walks would be two chances to draw a different
    ///         game.
    ///     </para>
    /// </summary>
    public enum BattleInkEnum
    {
        /// <summary>The line where the ground meets the sky, and the mountains standing on it.</summary>
        Horizon = 0,

        /// <summary>Blocks and pyramids: the furniture, and the cover.</summary>
        Scenery = 1,

        /// <summary>Something that wants the player dead.</summary>
        Enemy = 2,

        /// <summary>The saucer, which does not.</summary>
        Saucer = 3,

        /// <summary>A shell in the air, whoever fired it.</summary>
        Shell = 4,

        /// <summary>Wreckage flying apart.</summary>
        Explosion = 5,

        /// <summary>The gunsight, which never moves.</summary>
        Reticle = 6,

        /// <summary>The radar bezel and its sweep.</summary>
        Radar = 7,

        /// <summary>A contact on the radar.</summary>
        Blip = 8,

        /// <summary>The broken viewport, which is what being shot looks like.</summary>
        Crack = 9
    }
}
