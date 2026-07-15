// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

namespace WolfCurses.Graphics
{
    /// <summary>
    ///     How an image is scaled to the target area (the console window, or the <see cref="AnsiImageOptions.MaxColumns" />
    ///     / <see cref="AnsiImageOptions.MaxRows" /> you supply). These mirror the familiar CSS <c>object-fit</c> values.
    ///     For the modes that crop (<see cref="Cover" />), which part of the image is kept is controlled by
    ///     <see cref="AnsiImageOptions.HorizontalAlignment" /> and <see cref="AnsiImageOptions.VerticalAlignment" />.
    /// </summary>
    public enum AnsiImageFitEnum
    {
        /// <summary>
        ///     Scale so the whole image is visible inside the area, preserving aspect ratio. The image is as large as it
        ///     can be while still showing all of itself, so one dimension may not fill the area (letterboxing). This is
        ///     the default — an image "wants to show all of itself".
        /// </summary>
        Contain = 0,

        /// <summary>
        ///     Scale so the image completely fills the area, preserving aspect ratio. Because aspect is kept, whatever
        ///     spills past the area is cropped away (the crop is anchored by the alignment options). Nothing is
        ///     letterboxed; the whole scene is covered.
        /// </summary>
        Cover = 1,

        /// <summary>
        ///     Scale to exactly fill the area, ignoring aspect ratio. The entire image shows and the whole scene is
        ///     filled, but the picture is stretched/squashed to match the area's proportions.
        /// </summary>
        Stretch = 2,

        /// <summary>
        ///     Like <see cref="Contain" />, but never enlarge the image beyond its native pixel size. A picture smaller
        ///     than the area is shown at (up to) its real size instead of being blown up and blurred.
        /// </summary>
        ScaleDown = 3
    }
}
