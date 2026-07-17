// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System;

namespace WolfCurses.Graphics
{
    /// <summary>
    ///     Holds the process-wide <see cref="IImageDecoder" /> used whenever an <see cref="AnsiImage" /> is created
    ///     without an explicit decoder. An application that loads encoded images (PNG, JPEG, and so on) must assign one
    ///     here once at start-up.
    ///     <para>
    ///         There is deliberately no built-in decoder: every real one is a third-party dependency, and the library
    ///         does not impose that choice on applications that do not need it (or that already have an image library
    ///         of their own). Until something is assigned, the default is a stand-in that throws an explanatory error
    ///         rather than decoding — see <see cref="UnconfiguredImageDecoder" />. The example app assigns
    ///         StbImageSharp in <c>Program.Main</c> and is worth copying.
    ///     </para>
    /// </summary>
    public static class ImageDecoders
    {
        private static IImageDecoder _default = new UnconfiguredImageDecoder();

        /// <summary>
        ///     The decoder used by <see cref="AnsiImage" /> when the caller does not pass one explicitly. Never null;
        ///     assigning null throws so a mis-configured start-up fails loudly rather than at first image load. Until
        ///     assigned it is a stand-in whose only behavior is to throw an <see cref="InvalidOperationException" />
        ///     explaining that a decoder has to be chosen.
        /// </summary>
        public static IImageDecoder Default
        {
            get => _default;
            set => _default = value ?? throw new ArgumentNullException(nameof(value),
                "The default image decoder cannot be null; assign a real IImageDecoder implementation.");
        }
    }
}
