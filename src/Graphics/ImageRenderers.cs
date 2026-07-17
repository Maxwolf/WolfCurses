// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/16/2026

using System;

namespace WolfCurses.Graphics
{
    /// <summary>
    ///     Holds the process-wide <see cref="IImageRenderer" /> used whenever an <see cref="AnsiImage" /> is drawn
    ///     without an explicit renderer. It starts out as <see cref="HalfBlockImageRenderer" /> — the safe choice that
    ///     works in any color terminal — but a consuming application can replace it once at start-up to route every
    ///     image through a different one, which is how an app opts into a true-pixel protocol:
    ///     <code>
    ///         ImageRenderers.Default = new SixelImageRenderer();
    ///     </code>
    ///     <para>
    ///         This mirrors <see cref="ImageDecoders" /> deliberately: the two seams of the graphics feature are
    ///         configured the same way, so knowing one teaches you the other.
    ///     </para>
    /// </summary>
    /// <seealso cref="ImageDecoders" />
    public static class ImageRenderers
    {
        private static IImageRenderer _default = new HalfBlockImageRenderer();

        /// <summary>
        ///     The renderer used by <see cref="AnsiImage" /> when the caller does not pass one explicitly. Never null;
        ///     assigning null throws so a mis-configured start-up fails loudly rather than at the first image drawn.
        /// </summary>
        public static IImageRenderer Default
        {
            get => _default;
            set => _default = value ?? throw new ArgumentNullException(nameof(value),
                "The default image renderer cannot be null; assign a real IImageRenderer implementation.");
        }
    }
}
