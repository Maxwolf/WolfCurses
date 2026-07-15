// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

namespace WolfCurses.Graphics
{
    /// <summary>
    ///     Horizontal anchor used when an image does not occupy the full width — chiefly to decide which part of the
    ///     picture is kept when <see cref="AnsiImageFitEnum.Cover" /> crops it.
    /// </summary>
    public enum AnsiHorizontalAlignmentEnum
    {
        /// <summary>Anchor to the left edge (keep the left part when cropping).</summary>
        Left = 0,

        /// <summary>Anchor to the horizontal center (crop equally from both sides). The default.</summary>
        Center = 1,

        /// <summary>Anchor to the right edge (keep the right part when cropping).</summary>
        Right = 2
    }
}
