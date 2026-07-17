// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System;

namespace WolfCurses.Graphics
{
    /// <summary>
    ///     Holds the process-wide <see cref="IImageDecoder" /> used whenever an <see cref="AnsiImage" /> is created
    ///     without an explicit decoder. It starts as <see cref="BuiltInImageDecoder" />, so PNG, JPEG and GIF load with
    ///     no set-up at all and most applications never touch this class.
    ///     <para>
    ///         Assign here to replace that: a faster decoder, one that handles a format outside those three, or simply
    ///         the imaging library an application already has, so images are not decoded two different ways in one
    ///         process. Do it once at start-up, before the first image is loaded.
    ///     </para>
    ///     <para>
    ///         Having both a working default and a seam is the whole design. A default means the library can show a
    ///         picture out of the box without a dependency; the seam means nobody is stuck with it.
    ///     </para>
    /// </summary>
    /// <seealso cref="BuiltInImageDecoder" />
    public static class ImageDecoders
    {
        private static IImageDecoder _default = new BuiltInImageDecoder();

        /// <summary>
        ///     The decoder used by <see cref="AnsiImage" /> when the caller does not pass one explicitly. Never null;
        ///     assigning null throws, so a mis-configured start-up fails loudly at the assignment rather than as a
        ///     <see cref="NullReferenceException" /> at the first image load.
        /// </summary>
        public static IImageDecoder Default
        {
            get => _default;
            set => _default = value ?? throw new ArgumentNullException(nameof(value),
                "The default image decoder cannot be null; assign a real IImageDecoder implementation.");
        }
    }
}
