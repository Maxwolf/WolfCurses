// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace WolfCurses.Window.Control
{
    /// <summary>The line style used to draw a <see cref="Box" />.</summary>
    public enum BoxBorder
    {
        /// <summary>Single line: <c>┌─┐│└┘</c>.</summary>
        Single,

        /// <summary>Double line: <c>╔═╗║╚╝</c>.</summary>
        Double,

        /// <summary>Single line with rounded corners: <c>╭─╮│╰╯</c>.</summary>
        Rounded,

        /// <summary>Plain ASCII: <c>+-+|++</c>, for terminals without box-drawing glyphs.</summary>
        Ascii,

        /// <summary>No border at all — the content is just padded into a rectangular block.</summary>
        None
    }

    /// <summary>Where the title sits along the top border of a <see cref="Box" />.</summary>
    public enum BoxAlignment
    {
        /// <summary>Near the left corner.</summary>
        Left,

        /// <summary>Centered.</summary>
        Center,

        /// <summary>Near the right corner.</summary>
        Right
    }

    /// <summary>
    ///     Draws a border around a block of text, with an optional title in the top edge and interior padding. Like the
    ///     other <see cref="WolfCurses.Window.Control" /> widgets it is a pure string producer — pass it your content and
    ///     embed the framed result in a window or form's render. Column widths are measured ignoring ANSI color escape
    ///     sequences, so a box lines up correctly even around colored text or ANSI images (it does assume one column per
    ///     visible character — no double-width CJK/emoji handling).
    /// </summary>
    /// <example>
    ///     <code>
    ///     var box = new Box { Title = "Status", Border = BoxBorder.Double, Padding = 1 };
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
        private static readonly Regex AnsiEscape = new(@"\x1b\[[0-9;?=]*[A-Za-z]", RegexOptions.Compiled);

        /// <summary>The border line style. Defaults to a single line.</summary>
        public BoxBorder Border { get; set; } = BoxBorder.Single;

        /// <summary>Optional title drawn into the top border; null or empty for no title.</summary>
        public string Title { get; set; }

        /// <summary>Where the title sits along the top border.</summary>
        public BoxAlignment TitleAlignment { get; set; } = BoxAlignment.Left;

        /// <summary>Number of blank columns (and, top/bottom, blank rows) between the border and the content.</summary>
        public int Padding { get; set; }

        /// <summary>Minimum inner content width; the box grows past this to fit wider content or a longer title.</summary>
        public int MinimumWidth { get; set; }

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

            if (Border == BoxBorder.None)
            {
                var pad = new string(' ', padding);
                foreach (var line in lines)
                    outputLines.Add(pad + PadToVisible(line, bodyWidth) + pad);
                return string.Join(Environment.NewLine, outputLines);
            }

            outputLines.Add(BuildTop(glyphs, inner, hasTitle ? label : null));

            for (var top = 0; top < padding; top++)
                outputLines.Add(glyphs.Vertical + new string(' ', inner) + glyphs.Vertical);

            var sidePad = new string(' ', padding);
            foreach (var line in lines)
                outputLines.Add(glyphs.Vertical + sidePad + PadToVisible(line, bodyWidth) + sidePad + glyphs.Vertical);

            for (var bottom = 0; bottom < padding; bottom++)
                outputLines.Add(glyphs.Vertical + new string(' ', inner) + glyphs.Vertical);

            outputLines.Add(glyphs.BottomLeft + new string(glyphs.Horizontal, inner) + glyphs.BottomRight);

            return string.Join(Environment.NewLine, outputLines);
        }

        /// <summary>Builds the top border, embedding the (already space-wrapped) title per the alignment.</summary>
        private string BuildTop(BorderGlyphs glyphs, int inner, string label)
        {
            if (string.IsNullOrEmpty(label))
                return glyphs.TopLeft + new string(glyphs.Horizontal, inner) + glyphs.TopRight;

            var remaining = inner - VisibleWidth(label);
            if (remaining < 0)
                remaining = 0;

            int left;
            switch (TitleAlignment)
            {
                case BoxAlignment.Right:
                    left = remaining - Math.Min(1, remaining);
                    break;
                case BoxAlignment.Center:
                    left = remaining / 2;
                    break;
                default: // Left
                    left = Math.Min(1, remaining);
                    break;
            }

            var right = remaining - left;
            return glyphs.TopLeft + new string(glyphs.Horizontal, left) + label +
                   new string(glyphs.Horizontal, right) + glyphs.TopRight;
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
            return line.IndexOf('\x1b') < 0 ? line.Length : AnsiEscape.Replace(line, string.Empty).Length;
        }

        private static BorderGlyphs GlyphsFor(BoxBorder border)
        {
            switch (border)
            {
                case BoxBorder.Double:
                    return new BorderGlyphs('╔', '╗', '╚', '╝', '═', '║');
                case BoxBorder.Rounded:
                    return new BorderGlyphs('╭', '╮', '╰', '╯', '─', '│');
                case BoxBorder.Ascii:
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
