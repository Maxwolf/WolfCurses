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
    }
}
