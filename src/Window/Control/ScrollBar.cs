// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/20/2026

using System;
using System.Text;
using WolfCurses.Graphics;

namespace WolfCurses.Window.Control
{
    /// <summary>
    ///     The bar down the side of anything bigger than its window: two arrow caps, a dithered track, and a thumb
    ///     whose size says how much of the whole you are looking at and whose position says where.
    ///     <para>
    ///         A pure widget like the rest of this namespace: told how big the thing is, how much of it fits and
    ///         where the window currently starts, it returns cells. It does not scroll anything and does not know
    ///         what it is beside, so a document, a list and a log all use the same one.
    ///     </para>
    ///     <para>
    ///         <b>The thumb says two things at once and both have an off-by-one waiting.</b> Its <i>length</i> is the
    ///         fraction visible, and must never round down to nothing or a long document has an invisible thumb. Its
    ///         <i>position</i> must reach the bottom of the track exactly when the last line is on screen and not one
    ///         cell before, which is why it is scaled against the furthest the window can start rather than against
    ///         the item count. Getting that second one wrong leaves a bar that never quite fills, which looks like
    ///         the document has more in it than it does.
    ///     </para>
    /// </summary>
    public sealed class ScrollBar
    {
        /// <summary>Initializes a new instance of the <see cref="ScrollBar" /> class.</summary>
        /// <param name="horizontal">TRUE for a bar that runs left to right.</param>
        public ScrollBar(bool horizontal = false)
        {
            Horizontal = horizontal;
        }

        /// <summary>Whether this bar runs left to right rather than top to bottom.</summary>
        public bool Horizontal { get; }

        /// <summary>How many cells the whole bar occupies, arrow caps included.</summary>
        public int Length { get; set; } = 3;

        /// <summary>How many items there are in total, such as the document's line count.</summary>
        public int Total { get; set; }

        /// <summary>How many of them fit on screen at once.</summary>
        public int Visible { get; set; } = 1;

        /// <summary>Which item is at the top or left of the window.</summary>
        public int Position { get; set; }

        /// <summary>The glyph the empty part of the track is drawn with.</summary>
        public char TrackGlyph { get; set; } = '░';

        /// <summary>The glyph the thumb is drawn with.</summary>
        public char ThumbGlyph { get; set; } = '█';

        /// <summary>How the arrow caps are drawn.</summary>
        public TextStyle ArrowStyle { get; set; } = TextStyle.None;

        /// <summary>How the empty track is drawn.</summary>
        public TextStyle TrackStyle { get; set; } = TextStyle.None;

        /// <summary>How the thumb is drawn.</summary>
        public TextStyle ThumbStyle { get; set; } = TextStyle.None;

        /// <summary>
        ///     Which colour vocabulary the styles resolve through. <see cref="AnsiColorModeEnum.Auto" /> asks the
        ///     environment, which is right for a running program and useless for a test: the answer is cached
        ///     process-wide, so a test that moved it would race every other test in the assembly. Pinning it per
        ///     widget is how the rest of this namespace stays testable without a non-parallel collection.
        /// </summary>
        public AnsiColorModeEnum ColorMode { get; set; } = AnsiColorModeEnum.Auto;

        /// <summary>How many cells lie between the two arrow caps.</summary>
        public int TrackLength => Math.Max(0, Length - 2);

        /// <summary>
        ///     How long the thumb is, in track cells. Never zero while there is a track at all: a thumb that rounds
        ///     away is a scrollbar with nothing in it.
        /// </summary>
        public int ThumbLength
        {
            get
            {
                var track = TrackLength;
                if (track <= 0)
                    return 0;

                if (Total <= 0 || Visible <= 0 || Visible >= Total)
                    return track;

                return Math.Clamp(track * Visible / Total, 1, track);
            }
        }

        /// <summary>
        ///     Where the thumb starts, in track cells. Scaled against the furthest the window can start rather than
        ///     against the item count, so it reaches the end of the track exactly when the last item is on screen.
        /// </summary>
        public int ThumbStart
        {
            get
            {
                var slack = TrackLength - ThumbLength;
                if (slack <= 0)
                    return 0;

                var furthest = Math.Max(1, Total - Visible);
                return Math.Clamp(slack * Math.Clamp(Position, 0, furthest) / furthest, 0, slack);
            }
        }

        /// <summary>
        ///     The bar as one cell per element: the leading arrow, the track with the thumb in it, then the trailing
        ///     arrow. A vertical bar's owner appends one of these to each of its rows; a horizontal one's joins them.
        /// </summary>
        /// <returns>Exactly <see cref="Length" /> cells, each a styled single glyph.</returns>
        public string[] Cells()
        {
            var length = Math.Max(2, Length);
            var cells = new string[length];

            cells[0] = ArrowStyle.Apply(Horizontal ? "←" : "↑", ColorMode);
            cells[length - 1] = ArrowStyle.Apply(Horizontal ? "→" : "↓", ColorMode);

            var thumbStart = ThumbStart;
            var thumbEnd = thumbStart + ThumbLength;

            for (var i = 0; i < TrackLength; i++)
            {
                var inThumb = i >= thumbStart && i < thumbEnd;
                cells[i + 1] = inThumb
                    ? ThumbStyle.Apply(ThumbGlyph.ToString(), ColorMode)
                    : TrackStyle.Apply(TrackGlyph.ToString(), ColorMode);
            }

            return cells;
        }

        /// <summary>The bar as a single run of cells, which is what a horizontal one is drawn from.</summary>
        /// <returns>The whole bar.</returns>
        public string Render()
        {
            var sb = new StringBuilder();
            foreach (var cell in Cells())
                sb.Append(cell);

            return sb.ToString();
        }

        /// <summary>
        ///     Which item a press on the bar means, or -1 for a press that is not a scroll. An arrow cap steps one
        ///     item, the track above or below the thumb jumps a windowful, and the thumb itself is left alone because
        ///     dragging it needs a pointer this library does not report.
        /// </summary>
        /// <param name="cell">Which cell of the bar was pressed, counting from the leading arrow.</param>
        /// <returns>The position to scroll to, or -1 to do nothing.</returns>
        public int PositionForPress(int cell)
        {
            var length = Math.Max(2, Length);
            if (cell < 0 || cell >= length)
                return -1;

            var furthest = Math.Max(0, Total - Visible);

            if (cell == 0)
                return Math.Clamp(Position - 1, 0, furthest);

            if (cell == length - 1)
                return Math.Clamp(Position + 1, 0, furthest);

            var track = cell - 1;
            if (track < ThumbStart)
                return Math.Clamp(Position - Visible, 0, furthest);

            if (track >= ThumbStart + ThumbLength)
                return Math.Clamp(Position + Visible, 0, furthest);

            return -1;
        }
    }
}
