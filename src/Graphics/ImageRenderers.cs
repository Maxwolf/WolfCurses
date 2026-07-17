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

        /// <summary>
        ///     The best renderer for the terminal the application is actually running in, worked out from the
        ///     environment by <see cref="AnsiConsole.DetectGraphicsProtocol()" />. Assign it once at start-up and every
        ///     image drawn afterwards uses it:
        ///     <code>
        ///         ImageRenderers.Default = ImageRenderers.ForCurrentTerminal();
        ///     </code>
        ///     <para>
        ///         Where nothing better is known to be available this is <see cref="HalfBlockImageRenderer" />, which
        ///         works in any terminal that can show color at all — so this is always safe to call, including with
        ///         output redirected or on a console that has never heard of a graphics protocol.
        ///     </para>
        /// </summary>
        /// <seealso cref="AnsiConsole.ProbeGraphicsProtocol" />
        public static IImageRenderer ForCurrentTerminal()
        {
            return For(AnsiConsole.DetectGraphicsProtocol());
        }

        /// <summary>
        ///     The renderer that draws with a given protocol. Pass the result of
        ///     <see cref="AnsiConsole.ProbeGraphicsProtocol" /> to use the answer the terminal itself gave rather than
        ///     the environment's guess:
        ///     <code>
        ///         ImageRenderers.Default = ImageRenderers.For(AnsiConsole.ProbeGraphicsProtocol());
        ///     </code>
        /// </summary>
        /// <param name="protocol">
        ///     The protocol to draw with. <see cref="AnsiGraphicsProtocolEnum.None" /> — and any value that is not a
        ///     protocol this library knows — yields <see cref="HalfBlockImageRenderer" />, so a caller can hand this
        ///     whatever detection returned without checking it first.
        /// </param>
        public static IImageRenderer For(AnsiGraphicsProtocolEnum protocol)
        {
            switch (protocol)
            {
                case AnsiGraphicsProtocolEnum.Sixel:
                    return new SixelImageRenderer();
                case AnsiGraphicsProtocolEnum.Kitty:
                    return new KittyImageRenderer();
                default:
                    return new HalfBlockImageRenderer();
            }
        }
    }
}
