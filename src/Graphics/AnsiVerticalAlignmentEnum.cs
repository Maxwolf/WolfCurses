// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

namespace WolfCurses.Graphics
{
    /// <summary>
    ///     Vertical anchor used when an image does not occupy the full height — chiefly to decide which part of the
    ///     picture is kept when <see cref="AnsiImageFitEnum.Cover" /> crops it.
    /// </summary>
    public enum AnsiVerticalAlignmentEnum
    {
        /// <summary>Anchor to the top edge (keep the top part when cropping).</summary>
        Top = 0,

        /// <summary>Anchor to the vertical middle (crop equally from top and bottom). The default.</summary>
        Middle = 1,

        /// <summary>Anchor to the bottom edge (keep the bottom part when cropping).</summary>
        Bottom = 2
    }
}
