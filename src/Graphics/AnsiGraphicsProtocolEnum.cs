// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/16/2026

namespace WolfCurses.Graphics
{
    /// <summary>
    ///     A way of getting real pixels onto a terminal, as opposed to drawing a picture out of colored characters.
    ///     Which one (if any) a terminal understands is what <see cref="AnsiConsole.DetectGraphicsProtocol()" /> reports
    ///     and <see cref="ImageRenderers.For" /> turns into a renderer.
    /// </summary>
    public enum AnsiGraphicsProtocolEnum
    {
        /// <summary>
        ///     No true-pixel protocol is known to be available, so pictures should be drawn out of character cells.
        ///     This is the safe answer and the one given whenever there is any doubt: half-block text works in every
        ///     terminal that can show color at all, and degrades further on its own through
        ///     <see cref="AnsiColorModeEnum" />, whereas guessing wrong about a protocol dumps raw escape sequences on
        ///     screen as garbage.
        /// </summary>
        None = 0,

        /// <summary>
        ///     The DEC sixel protocol, understood by xterm (when built with sixel support), foot, WezTerm, mlterm,
        ///     contour, recent Konsole and VTE, iTerm2, and Windows Terminal from 1.22. Indexed color, so pictures are
        ///     reduced to a palette.
        /// </summary>
        Sixel = 1,

        /// <summary>
        ///     The kitty graphics protocol, understood by kitty, WezTerm, Ghostty and recent Konsole. Preferred over
        ///     <see cref="Sixel" /> wherever both are available: it carries full 24-bit color and a real alpha channel
        ///     with no palette reduction.
        /// </summary>
        Kitty = 2
    }
}
