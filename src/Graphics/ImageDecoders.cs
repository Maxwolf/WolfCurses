// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System;

namespace WolfCurses.Graphics
{
    /// <summary>
    ///     Holds the process-wide <see cref="IImageDecoder" /> used whenever an <see cref="AnsiImage" /> is created
    ///     without an explicit decoder. It starts out as the built-in <see cref="StbImageDecoder" /> (which handles PNG
    ///     with transparency, baseline and progressive JPEG, BMP, GIF, TGA, and more) but a consuming application can
    ///     replace it once at start-up to route all image loading through its own library.
    /// </summary>
    public static class ImageDecoders
    {
        private static IImageDecoder _default = new StbImageDecoder();

        /// <summary>
        ///     The decoder used by <see cref="AnsiImage" /> when the caller does not pass one explicitly. Never null;
        ///     assigning null throws so a mis-configured start-up fails loudly rather than at first image load.
        /// </summary>
        public static IImageDecoder Default
        {
            get => _default;
            set => _default = value ?? throw new ArgumentNullException(nameof(value),
                "The default image decoder cannot be null; assign a real IImageDecoder implementation.");
        }
    }
}
