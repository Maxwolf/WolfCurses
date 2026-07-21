// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using WolfCurses.Graphics;

namespace WolfCurses.Window.Control
{
    /// <summary>
    ///     Draws a border around a block of text, with an optional title in the top edge and interior padding. Like the
    ///     other <see cref="WolfCurses.Window.Control" /> widgets it is a pure string producer — pass it your content and
    ///     embed the framed result in a window or form's render. Column widths are measured ignoring ANSI color escape
    ///     sequences, so a box lines up correctly even around colored text or ANSI images (it does assume one column per
    ///     visible character — no double-width CJK/emoji handling).
    ///     <para>
    ///         The frame itself can be colored via <see cref="BorderStyle" /> and <see cref="TitleStyle" />; that same
    ///         escape-blind measurement is what lets it, since a box drawn in color is still measured — by this class
    ///         and by any outer box it is nested inside — as the plain glyphs it wraps.
    ///     </para>
    /// </summary>
    /// <example>
    ///     <code>
    ///     var box = new Box { Title = "Status", Border = BoxBorderEnum.Double, Padding = 1 };
    ///     string framed = box.Render("All systems nominal.");
    ///     // ╔═ Status ═════════════╗
    ///     // ║                      ║
    ///     // ║  All systems nominal. ║
    ///     // ║                      ║
    ///     // ╚══════════════════════╝
    ///     </code>
    /// </example>
    public sealed class Box
    {
        private static readonly Regex _ansiEscape = new(@"\x1b\[[0-9;?=]*[A-Za-z]", RegexOptions.Compiled);

        /// <summary>The border line style. Defaults to a single line.</summary>
        public BoxBorderEnum Border { get; set; } = BoxBorderEnum.Single;

        /// <summary>Optional title drawn into the top border; null or empty for no title.</summary>
        public string Title { get; set; }

        /// <summary>Where the title sits along the top border.</summary>
        public BoxAlignmentEnum TitleAlignment { get; set; } = BoxAlignmentEnum.Left;

        /// <summary>Number of blank columns (and, top/bottom, blank rows) between the border and the content.</summary>
        public int Padding { get; set; }

        /// <summary>Minimum inner content width; the box grows past this to fit wider content or a longer title.</summary>
        public int MinimumWidth { get; set; }

        /// <summary>
        ///     How much color the frame is allowed to use. <see cref="AnsiColorModeEnum.Auto" /> asks the environment,
        ///     which is what a running application wants; a concrete mode is how a test pins one answer without
        ///     touching process-wide state such as <c>NO_COLOR</c>. <see cref="AnsiColorModeEnum.None" /> emits no
        ///     escape sequences whatsoever, even for styles that were explicitly set.
        /// </summary>
        public AnsiColorModeEnum ColorMode { get; set; } = AnsiColorModeEnum.Auto;

        /// <summary>
        ///     How the border glyphs look — corners, edges and the horizontal runs either side of a title. Empty by
        ///     default, so an uncolored box is byte-for-byte the box this class always drew.
        ///     <para>
        ///         Only the glyphs are painted, never the interior spaces or the caller's content, so a background
        ///         color here draws a frame rather than filling the box. Entirely inert for
        ///         <see cref="BoxBorderEnum.None" />, which by definition has no glyphs to paint.
        ///     </para>
        /// </summary>
        public TextStyle BorderStyle { get; set; } = TextStyle.None;

        /// <summary>
        ///     How the title text sitting in the top border looks, independently of
        ///     <see cref="BorderStyle" /> — the top edge goes out as three runs (left glyphs, title, right glyphs) so
        ///     the two never have to share one style.
        ///     <para>
        ///         The layout arithmetic runs on the <em>plain</em> title and is finished before any escape is added,
        ///         which is what keeps the frame square: the horizontal runs are sized from
        ///         <c>VisibleWidth(label)</c>, and escapes are zero-width by that measure anyway. A title the caller
        ///         already colored is styled around, not re-colored — worth knowing, because the caller's own reset
        ///         will close this style early and the rest of the title reverts to the terminal default.
        ///     </para>
        /// </summary>
        public TextStyle TitleStyle { get; set; } = TextStyle.None;

        /// <summary>Frames the given content. Null is treated as empty. Rows are joined with the platform newline; no trailing newline.</summary>
        /// <param name="content">The text to frame (may be multiple lines and may contain ANSI color escapes).</param>
        public string Render(string content)
        {
            var padding = Math.Max(0, Padding);
            var minWidth = Math.Max(0, MinimumWidth);

            // Normalize newlines to a single list of content rows.
            var normalized = (content ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n");
            var lines = new List<string>(normalized.Split('\n'));

            // Body width fits the widest content row (measured without ANSI escapes) but at least the minimum.
            var bodyWidth = minWidth;
            foreach (var line in lines)
                bodyWidth = Math.Max(bodyWidth, VisibleWidth(line));

            var inner = bodyWidth + 2 * padding;

            // Collapse any newlines in the title so it cannot break the single-line top border, and measure it by
            // visible width (like the content) so ANSI escapes in the title do not distort the frame.
            var titleText = string.IsNullOrEmpty(Title)
                ? string.Empty
                : Title.Replace('\r', ' ').Replace('\n', ' ').Trim();
            var hasTitle = titleText.Length > 0;
            var label = hasTitle ? " " + titleText + " " : string.Empty;
            if (hasTitle)
            {
                // A title can force the box wider than its content; grow the interior (and body) to fit it.
                inner = Math.Max(inner, VisibleWidth(label));
                bodyWidth = inner - 2 * padding;
            }

            var glyphs = GlyphsFor(Border);
            var outputLines = new List<string>(lines.Count + 2);

            if (Border == BoxBorderEnum.None)
            {
                var pad = new string(' ', padding);
                foreach (var line in lines)
                    outputLines.Add(pad + PadToVisible(line, bodyWidth) + pad);
                return string.Join(Environment.NewLine, outputLines);
            }

            // Resolved once per render rather than per row, and only past the borderless early return above so a
            // BoxBorderEnum.None box never asks the environment anything. Both are empty strings when the style is
            // empty or the mode resolves to None, and every paint below degenerates to the identity function then —
            // that, and nothing else, is what makes the uncolored output byte-identical to the pre-color class.
            var borderOpen = BorderStyle.OpenSequence(ColorMode);
            var titleOpen = TitleStyle.OpenSequence(ColorMode);

            outputLines.Add(BuildTop(glyphs, inner, hasTitle ? label : null, borderOpen, titleOpen));

            // One painted vertical serves every row on both sides; the interior stays unpainted so the style closes
            // before the content and cannot bleed across it or across the newline the rows are joined with.
            var vertical = Paint(borderOpen, glyphs.Vertical.ToString());

            for (var top = 0; top < padding; top++)
                outputLines.Add(vertical + new string(' ', inner) + vertical);

            var sidePad = new string(' ', padding);
            foreach (var line in lines)
                outputLines.Add(vertical + sidePad + PadToVisible(line, bodyWidth) + sidePad + vertical);

            for (var bottom = 0; bottom < padding; bottom++)
                outputLines.Add(vertical + new string(' ', inner) + vertical);

            outputLines.Add(Paint(borderOpen,
                glyphs.BottomLeft + new string(glyphs.Horizontal, inner) + glyphs.BottomRight));

            return string.Join(Environment.NewLine, outputLines);
        }

        /// <summary>
        ///     Builds the top border, embedding the (already space-wrapped) title per the alignment. Measurement
        ///     happens on the plain label and the glyph runs are sized before a single escape is emitted — measure
        ///     first, paint last — so a colored title or a colored border cannot skew the frame.
        /// </summary>
        /// <param name="glyphs">The border characters for the chosen style.</param>
        /// <param name="inner">The interior width the top edge has to span.</param>
        /// <param name="label">The space-wrapped title, or null for an untitled edge.</param>
        /// <param name="borderOpen">The border style's opening sequence, or an empty string for no style.</param>
        /// <param name="titleOpen">The title style's opening sequence, or an empty string for no style.</param>
        private string BuildTop(BorderGlyphs glyphs, int inner, string label, string borderOpen, string titleOpen)
        {
            if (string.IsNullOrEmpty(label))
                return Paint(borderOpen, glyphs.TopLeft + new string(glyphs.Horizontal, inner) + glyphs.TopRight);

            var remaining = inner - VisibleWidth(label);
            if (remaining < 0)
                remaining = 0;

            int left;
            switch (TitleAlignment)
            {
                case BoxAlignmentEnum.Right:
                    left = remaining - Math.Min(1, remaining);
                    break;
                case BoxAlignmentEnum.Center:
                    left = remaining / 2;
                    break;
                default: // Left
                    left = Math.Min(1, remaining);
                    break;
            }

            var right = remaining - left;
            return Paint(borderOpen, glyphs.TopLeft + new string(glyphs.Horizontal, left)) +
                   Paint(titleOpen, label) +
                   Paint(borderOpen, new string(glyphs.Horizontal, right) + glyphs.TopRight);
        }

        /// <summary>
        ///     Wraps a run of the frame in a style and closes it again, or hands the run straight back when there is
        ///     nothing to open or nothing to wrap.
        ///     <para>
        ///         Every call site currently passes at least one corner or one vertical, so the empty-text half of the
        ///         guard is defensive rather than load-bearing — but a zero-width box (<c>┌┐</c>) already shortens the
        ///         horizontal runs to nothing, and the rule that an open/reset pair never surrounds an empty string is
        ///         cheaper to keep than to rediscover.
        ///     </para>
        /// </summary>
        /// <param name="open">The style's opening sequence, or an empty string for no style.</param>
        /// <param name="text">The run of frame characters to paint.</param>
        /// <returns>The painted run, or the input unchanged when nothing should be emitted.</returns>
        private static string Paint(string open, string text)
        {
            if (open.Length == 0 || string.IsNullOrEmpty(text))
                return text;

            return open + text + TextStyle.ResetSequence;
        }

        /// <summary>Right-pads a line with spaces to reach the given visible width (ANSI escapes are zero-width).</summary>
        private static string PadToVisible(string line, int width)
        {
            var needed = width - VisibleWidth(line);
            return needed > 0 ? line + new string(' ', needed) : line;
        }

        /// <summary>The number of visible columns in a line, i.e. its length after stripping ANSI escape sequences.</summary>
        private static int VisibleWidth(string line)
        {
            if (string.IsNullOrEmpty(line))
                return 0;
            return line.IndexOf('\x1b') < 0 ? line.Length : _ansiEscape.Replace(line, string.Empty).Length;
        }

        private static BorderGlyphs GlyphsFor(BoxBorderEnum border)
        {
            switch (border)
            {
                case BoxBorderEnum.Double:
                    return new BorderGlyphs('╔', '╗', '╚', '╝', '═', '║');
                case BoxBorderEnum.Rounded:
                    return new BorderGlyphs('╭', '╮', '╰', '╯', '─', '│');
                case BoxBorderEnum.Ascii:
                    return new BorderGlyphs('+', '+', '+', '+', '-', '|');
                default: // Single (None never draws glyphs)
                    return new BorderGlyphs('┌', '┐', '└', '┘', '─', '│');
            }
        }

        /// <summary>The six characters that make up a border style.</summary>
        private readonly struct BorderGlyphs
        {
            public BorderGlyphs(char topLeft, char topRight, char bottomLeft, char bottomRight, char horizontal,
                char vertical)
            {
                TopLeft = topLeft;
                TopRight = topRight;
                BottomLeft = bottomLeft;
                BottomRight = bottomRight;
                Horizontal = horizontal;
                Vertical = vertical;
            }

            public char TopLeft { get; }
            public char TopRight { get; }
            public char BottomLeft { get; }
            public char BottomRight { get; }
            public char Horizontal { get; }
            public char Vertical { get; }
        }
    }
}
