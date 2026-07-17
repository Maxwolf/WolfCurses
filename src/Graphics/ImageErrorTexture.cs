// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/17/2026

using System;

namespace WolfCurses.Graphics
{
    /// <summary>
    ///     The magenta-and-black checkerboard an image turns into when it cannot be loaded — the convention Quake and
    ///     the Source engine made universal, borrowed here for the same reason it worked there.
    ///     <para>
    ///         The reason it works is that magenta appears in almost nothing real. A photograph, a logo, a screenshot:
    ///         none of them are that colour, so a developer who has ever opened a game with a missing asset recognizes
    ///         it across the room and without being told. It is doing a job an error message cannot, because the error
    ///         message is not where they are looking — the screen is.
    ///     </para>
    ///     <para>
    ///         Why <see cref="AnsiImage" /> shows this rather than throwing: the documented way to use this library is
    ///         <c>private readonly string _logo = AnsiImage.RenderFile("media/logo.jpg");</c> — a field initializer,
    ///         where an exception surfaces as a <see cref="TypeInitializationException" /> naming the wrong type, from
    ///         a stack that no longer mentions the image. And in a text UI there is nowhere for an exception to go: the
    ///         console is the screen, so a stack trace lands on top of the interface. A picture that is visibly wrong,
    ///         in an application that is still running, beats both. The reason is still there in
    ///         <see cref="AnsiImage.Error" />, and gets written to <see cref="System.Diagnostics.Trace" /> as well.
    ///     </para>
    ///     <para>
    ///         The checks are a fixed count rather than a fixed pixel size, deliberately. Images are area-average
    ///         resampled on the way to the terminal, which turns a fine pattern into flat mush — sizing the checks to
    ///         the texture instead of to the pixel keeps them coarse enough to still read as a checkerboard after
    ///         being scaled down to a few dozen rows.
    ///     </para>
    /// </summary>
    /// <seealso cref="AnsiImage.Error" />
    public static class ImageErrorTexture
    {
        /// <summary>The colour nothing real is: full red, full blue, no green.</summary>
        public static readonly Rgba32 Magenta = new(255, 0, 255, 255);

        /// <summary>The other half of the checkerboard.</summary>
        public static readonly Rgba32 Black = new(0, 0, 0, 255);

        /// <summary>How many checks across and down <see cref="Create" /> draws unless told otherwise.</summary>
        public const int DefaultChecks = 8;

        /// <summary>The size <see cref="Create" /> makes unless told otherwise; square, so it letterboxes evenly.</summary>
        public const int DefaultSize = 128;

        /// <summary>
        ///     Builds a magenta-and-black checkerboard.
        /// </summary>
        /// <param name="width">Texture width in pixels.</param>
        /// <param name="height">Texture height in pixels.</param>
        /// <param name="checks">
        ///     How many checks to draw across and down. Kept small on purpose: the texture is about to be scaled to
        ///     fit a terminal, and a busy pattern would average away into a flat magenta smear on the way.
        /// </param>
        /// <returns>An opaque checkerboard.</returns>
        public static PixelBuffer Create(int width = DefaultSize, int height = DefaultSize, int checks = DefaultChecks)
        {
            if (checks < 1)
                throw new ArgumentOutOfRangeException(nameof(checks), checks,
                    "A checkerboard needs at least one check.");

            // PixelBuffer validates the dimensions, so a nonsense size fails here rather than drawing nothing.
            var texture = new PixelBuffer(width, height);

            // Ceiling division, so the last check is allowed to be a partial one rather than the pattern running out
            // before the right or bottom edge does.
            var checkWidth = Math.Max(1, (width + checks - 1) / checks);
            var checkHeight = Math.Max(1, (height + checks - 1) / checks);

            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                texture.SetPixel(x, y, (x / checkWidth + y / checkHeight) % 2 == 0 ? Magenta : Black);

            return texture;
        }
    }
}
