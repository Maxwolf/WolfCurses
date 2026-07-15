// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

namespace WolfCurses.Graphics
{
    /// <summary>
    ///     A straight-alpha (non-premultiplied) 32-bit color value in red, green, blue, alpha channel order. Alpha of
    ///     zero is fully transparent and 255 is fully opaque.
    /// </summary>
    public readonly struct Rgba32
    {
        /// <summary>Red channel, 0-255.</summary>
        public readonly byte R;

        /// <summary>Green channel, 0-255.</summary>
        public readonly byte G;

        /// <summary>Blue channel, 0-255.</summary>
        public readonly byte B;

        /// <summary>Alpha channel, 0 (transparent) to 255 (opaque).</summary>
        public readonly byte A;

        /// <summary>Initializes a new instance of the <see cref="Rgba32" /> struct.</summary>
        public Rgba32(byte r, byte g, byte b, byte a)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }
    }
}
