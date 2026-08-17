// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

namespace WolfCurses.Graphics
{
    /// <summary>
    ///     An opaque 24-bit color value in red, green, blue channel order. Used for the colors emitted into the ANSI
    ///     escape sequences where there is no alpha channel to speak of.
    /// </summary>
    public readonly struct Rgb24
    {
        /// <summary>Red channel, 0-255.</summary>
        public readonly byte R;

        /// <summary>Green channel, 0-255.</summary>
        public readonly byte G;

        /// <summary>Blue channel, 0-255.</summary>
        public readonly byte B;

        /// <summary>Initializes a new instance of the <see cref="Rgb24" /> struct.</summary>
        public Rgb24(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
        }

        /// <summary>
        ///     This colour as an <see cref="Rgba32" /> at the given opacity — the bridge from the colour vocabulary to
        ///     the pixel one, since <see cref="ColorRamp.Sample" /> hands back an <see cref="Rgb24" /> and everything
        ///     on <see cref="PixelBuffer" /> takes an <see cref="Rgba32" />.
        ///     <para>
        ///         The alpha is required rather than defaulted, and there is deliberately <b>no implicit conversion</b>
        ///         in either direction. A widening that invented opacity would silently turn a fade into a solid, and a
        ///         narrowing would silently drop the alpha — and a conversion that looks obviously right while quietly
        ///         recolouring everything is a mistake this library has already made once, in the hand-written table
        ///         that maps <see cref="ConsoleColor" /> to its ANSI number.
        ///     </para>
        /// </summary>
        /// <param name="a">Alpha channel, 0 (transparent) to 255 (opaque).</param>
        /// <returns>The same colour carrying that alpha.</returns>
        public Rgba32 WithAlpha(byte a)
        {
            return new Rgba32(R, G, B, a);
        }
    }
}
