// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

namespace WolfCurses.Window.Control
{
    /// <summary>
    ///     Picks the box-drawing character that joins lines running in a given set of directions — the glyph for a
    ///     corner, a tee, a crossroads or a straight run, chosen from which of its four neighbours a cell connects to.
    ///     <para>
    ///         <b>This is the half of the line vocabulary <see cref="Box" /> never needed.</b> A box knows its six
    ///         glyphs up front because a rectangle's shape is fixed: four corners and two edges, decided by position.
    ///         Anything drawing a <i>network</i> of lines — a maze, a table with interior rules, a tree, a wiring
    ///         diagram — has to decide each cell from its neighbours instead, and there are sixteen answers rather
    ///         than six. Writing that table out is a five-minute job that everybody gets subtly wrong in the same two
    ///         places (see below), so it belongs here rather than in each caller.
    ///     </para>
    ///     <para>
    ///         <b>The same connections give the same shape in every border style</b>, which is why a cell with only
    ///         one connection takes the full line for its axis rather than one of Unicode's half-line stubs
    ///         (<c>╴╵╶╷</c>). Those stubs exist for the single-line style and have no double-line counterpart, so
    ///         using them would make <see cref="BoxBorderEnum.Double" /> silently draw a different picture from
    ///         <see cref="BoxBorderEnum.Single" /> for identical input. The vocabulary is deliberately limited to
    ///         what every style can spell.
    ///     </para>
    ///     <para>
    ///         A cell connecting to nothing at all takes the horizontal glyph, for the same reason: there is no
    ///         isolated-dot character in the double-line set either. A caller who wants an island drawn as something
    ///         else knows it is an island and can say so.
    ///     </para>
    /// </summary>
    /// <example>
    ///     <code>
    ///     // Walls of a maze, drawn as connected lines rather than blocks.
    ///     char glyph = BoxDrawing.Junction(
    ///         IsWall(x, y - 1), IsWall(x, y + 1), IsWall(x - 1, y), IsWall(x + 1, y),
    ///         BoxBorderEnum.Double);
    ///     </code>
    /// </example>
    public static class BoxDrawing
    {
        /// <summary>
        ///     The glyph for a cell whose lines run in the given directions.
        ///     <para>
        ///         The four flags are the cell's <i>connections</i>, not its walls — <paramref name="up" /> means a
        ///         line continues into the cell above, so a corner opening up and right is
        ///         <c>Junction(true, false, false, true)</c> and comes back as <c>└</c>.
        ///     </para>
        /// </summary>
        /// <param name="up">Whether a line continues into the cell above.</param>
        /// <param name="down">Whether a line continues into the cell below.</param>
        /// <param name="left">Whether a line continues into the cell to the left.</param>
        /// <param name="right">Whether a line continues into the cell to the right.</param>
        /// <param name="border">Which line style to draw in.</param>
        /// <returns>
        ///     One box-drawing character, or a space for <see cref="BoxBorderEnum.None" /> — which means "no border"
        ///     here exactly as it does on <see cref="Box" />.
        /// </returns>
        public static char Junction(bool up, bool down, bool left, bool right,
            BoxBorderEnum border = BoxBorderEnum.Single)
        {
            if (border == BoxBorderEnum.None)
                return ' ';

            // Ordered vertical-then-horizontal to match the parameter list, so a reader checking one case against the
            // table below is comparing the same thing in the same order.
            var mask = (up ? 8 : 0) | (down ? 4 : 0) | (left ? 2 : 0) | (right ? 1 : 0);

            return border switch
            {
                BoxBorderEnum.Double => _double[mask],
                BoxBorderEnum.Rounded => _rounded[mask],
                BoxBorderEnum.Ascii => _ascii[mask],
                _ => _single[mask]
            };
        }

        // Indexed by up*8 + down*4 + left*2 + right, so row order below is: nothing, right, left, horizontal, down,
        // down-right corner, down-left corner, top tee, up, up-right corner, up-left corner, bottom tee, vertical,
        // left tee, right tee, cross.
        //
        // Two entries in each table are the ones worth checking rather than skimming. The single-connection cases
        // (1, 2, 4, 8) take a full line rather than a stub, and the no-connection case (0) takes the horizontal - both
        // because the double-line set has no character for either, and one style quietly drawing a different picture
        // from another is worse than an island that looks like a dash.
        private static readonly char[] _single =
        {
            '─', '─', '─', '─',
            '│', '┌', '┐', '┬',
            '│', '└', '┘', '┴',
            '│', '├', '┤', '┼'
        };

        private static readonly char[] _double =
        {
            '═', '═', '═', '═',
            '║', '╔', '╗', '╦',
            '║', '╚', '╝', '╩',
            '║', '╠', '╣', '╬'
        };

        // Only the four corners differ from single; every tee, cross and straight run is shared, because rounding is
        // a property of a corner and there is nothing to round anywhere else.
        private static readonly char[] _rounded =
        {
            '─', '─', '─', '─',
            '│', '╭', '╮', '┬',
            '│', '╰', '╯', '┴',
            '│', '├', '┤', '┼'
        };

        private static readonly char[] _ascii =
        {
            '-', '-', '-', '-',
            '|', '+', '+', '+',
            '|', '+', '+', '+',
            '|', '+', '+', '+'
        };
    }
}
