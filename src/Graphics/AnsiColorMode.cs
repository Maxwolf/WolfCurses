// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

namespace WolfCurses.Graphics
{
    /// <summary>
    ///     Selects how much color fidelity the <see cref="AnsiImageRenderer" /> commits to when it emits escape
    ///     sequences. Richer modes look better but assume the destination terminal understands the corresponding escape
    ///     codes; the fallbacks let an image still be recognizable on a limited or colorless terminal.
    /// </summary>
    public enum AnsiColorMode
    {
        /// <summary>
        ///     Inspect the environment at render time (via <see cref="AnsiConsole.DetectColorMode" />) and pick the best
        ///     of the concrete modes below. This is the default.
        /// </summary>
        Auto = 0,

        /// <summary>
        ///     24-bit "true color". Each cell carries an exact red/green/blue foreground and background. Supported by
        ///     Windows Terminal and essentially every modern terminal emulator.
        /// </summary>
        TrueColor = 1,

        /// <summary>
        ///     The 256-color xterm palette (the 6x6x6 color cube plus the grayscale ramp). A good match for older
        ///     terminals that advertise <c>256color</c> but not true color.
        /// </summary>
        Palette256 = 2,

        /// <summary>
        ///     The 256-color palette restricted to its gray shades. Useful when color would be distracting or the
        ///     terminal renders color poorly, while still conveying the image's luminance.
        /// </summary>
        Grayscale = 3,

        /// <summary>
        ///     No color at all: the image is approximated as shaded ASCII characters chosen by brightness. The last
        ///     resort for a monochrome terminal or when the <c>NO_COLOR</c> convention is in effect.
        /// </summary>
        None = 4
    }
}
