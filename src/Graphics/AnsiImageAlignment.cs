// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

namespace WolfCurses.Graphics
{
    /// <summary>
    ///     Horizontal anchor used when an image does not occupy the full width — chiefly to decide which part of the
    ///     picture is kept when <see cref="AnsiImageFit.Cover" /> crops it.
    /// </summary>
    public enum AnsiHorizontalAlignment
    {
        /// <summary>Anchor to the left edge (keep the left part when cropping).</summary>
        Left = 0,

        /// <summary>Anchor to the horizontal center (crop equally from both sides). The default.</summary>
        Center = 1,

        /// <summary>Anchor to the right edge (keep the right part when cropping).</summary>
        Right = 2
    }

    /// <summary>
    ///     Vertical anchor used when an image does not occupy the full height — chiefly to decide which part of the
    ///     picture is kept when <see cref="AnsiImageFit.Cover" /> crops it.
    /// </summary>
    public enum AnsiVerticalAlignment
    {
        /// <summary>Anchor to the top edge (keep the top part when cropping).</summary>
        Top = 0,

        /// <summary>Anchor to the vertical middle (crop equally from top and bottom). The default.</summary>
        Middle = 1,

        /// <summary>Anchor to the bottom edge (keep the bottom part when cropping).</summary>
        Bottom = 2
    }
}
