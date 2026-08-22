// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/16/2026

using System;
using System.Text;

namespace WolfCurses.Graphics
{
    /// <summary>
    ///     Measures and removes ANSI escape sequences. The one answer in this library to "how wide is this row,
    ///     really?" — because an escape sequence has length but no width, and everything that lays text out beside
    ///     other text needs the second number rather than the first.
    ///     <para>
    ///         <b>This is public because keeping it private was a mistake with a name.</b> The rule it encodes is
    ///         that the two questions — how wide is this, and what is this with the colour taken off — must be
    ///         answered by <i>one</i> walk of <i>one</i> grammar, or they drift and the drift is invisible until a
    ///         layout tears. <see cref="ConsolePresenter" /> has documented that trap since it was written, and then
    ///         the library shipped without exposing the walk, so every consumer that framed or aligned styled text had
    ///         to write the second parser. Two of them ended up inside this package (the presenter's, and a regular
    ///         expression in <see cref="WolfCurses.Window.Control.Box" /> that understood <c>ESC[…m</c> and nothing
    ///         else) and a third downstream. There is now one, and it is reachable.
    ///     </para>
    ///     <para>
    ///         The grammar handled is the one a terminal actually receives: <b>CSI</b> (<c>ESC [</c> … final byte
    ///         0x40-0x7E, so <c>ESC[0m</c> and <c>ESC[38;2;255;0;0m</c> and also <c>ESC[200~</c>), <b>string
    ///         sequences</b> (OSC/DCS/PM/APC/SOS — an OSC 8 hyperlink, say — terminated by BEL or <c>ESC \</c>), and
    ///         <b>short escapes</b> such as the charset designation <c>ESC ( B</c>. A bare trailing <c>ESC</c> with
    ///         nothing after it consumes to the end of the string rather than counting as a column.
    ///     </para>
    ///     <para>
    ///         Two things it deliberately does <b>not</b> do. It counts one column per UTF-16 code unit, so
    ///         double-width CJK and emoji are measured as one — the same measure the rest of the library uses, and
    ///         changing it would move every pinned layout in the test suite. And it knows nothing about
    ///         <see cref="AnsiGraphics" />' marker convention: a sixel or kitty payload row is one escape blob of zero
    ///         visible width that paints across many rows, so this reports zero for it, which is true about the row
    ///         and useless as a layout width. Use <see cref="AnsiGraphics.IsPayloadRow" /> to spot those first.
    ///     </para>
    ///     <seealso cref="AnsiGraphics.StripMarkers" />
    /// </summary>
    public static class AnsiText
    {
        /// <summary>The ASCII escape control character (0x1B) that begins every ANSI control sequence.</summary>
        private const char Escape = (char) 27;

        /// <summary>
        ///     The number of character cells a string occupies on screen, counting every escape sequence as zero
        ///     width. This is the number to pad against, centre with, or compare to the console width — never
        ///     <c>string.Length</c>, which for a styled row is larger by however many bytes of colour it carries.
        /// </summary>
        /// <param name="text">The text to measure. Null and empty both measure zero.</param>
        /// <returns>The visible column count.</returns>
        public static int VisibleLength(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            // The overwhelmingly common case is a row with no styling in it at all, where the walk below would visit
            // every character to learn what Length already knows.
            if (text.IndexOf(Escape) < 0)
                return text.Length;

            var length = 0;
            var i = 0;
            while (i < text.Length)
            {
                if (text[i] != Escape)
                {
                    length++;
                    i++;
                    continue;
                }

                i = SkipEscape(text, i);
            }

            return length;
        }

        /// <summary>
        ///     Removes every escape sequence, keeping only the characters that occupy a visible cell.
        ///     <para>
        ///         Walks the exact same grammar <see cref="VisibleLength" /> measures, sharing
        ///         <see cref="SkipEscape" /> so the two can never disagree — which is the whole point of this type:
        ///         <c>StripEscapes(x).Length == VisibleLength(x)</c> holds for every input, and a codebase where it
        ///         stops holding has grown a second parser.
        ///     </para>
        /// </summary>
        /// <param name="text">The text to strip. Null, empty, and escape-free text are returned unchanged.</param>
        /// <returns>The text with every escape sequence removed.</returns>
        public static string StripEscapes(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf(Escape) < 0)
                return text;

            var sb = new StringBuilder(text.Length);
            var i = 0;
            while (i < text.Length)
            {
                if (text[i] != Escape)
                {
                    sb.Append(text[i]);
                    i++;
                    continue;
                }

                i = SkipEscape(text, i);
            }

            return sb.ToString();
        }

        /// <summary>
        ///     Pads or trims text so that it occupies exactly <paramref name="width" /> visible columns, measured
        ///     with <see cref="VisibleLength" /> rather than <c>string.Length</c>.
        ///     <para>
        ///         <b>This is the operation every laid-out screen performs and the one it performs wrongly.</b> A
        ///         styled row twenty columns wide is several hundred characters long, so <c>PadRight</c> pads it to
        ///         nothing and <c>Substring</c> cuts an escape in half, which spills the rest of the sequence into
        ///         the terminal as text and leaves the colour switched on for the remainder of the screen.
        ///     </para>
        ///     <para>
        ///         An earlier version of this class deliberately shipped without a padding helper, on the grounds
        ///         that it would have been public surface for a single internal caller. That reasoning has expired:
        ///         there are now several, inside the package and out, and each had privately re-derived a version
        ///         that only works while the text happens to carry no colour.
        ///     </para>
        ///     <para>
        ///         Trimming keeps every escape it passes, including ones sitting past the cut, so a run that was
        ///         opened is still closed by whatever reset the original ended with. Padding is spaces and is added
        ///         outside the text, which is the right order: style what has already been fitted, rather than
        ///         fitting something already styled, and the padding takes the background with it.
        ///     </para>
        /// </summary>
        /// <param name="width">The exact number of visible columns wanted. Zero or less gives an empty string.</param>
        /// <param name="text">The text to fit. Null is treated as empty and becomes that many spaces.</param>
        /// <param name="alignment">
        ///     Where the text sits when it is narrower than the width. When centring leaves an odd space over it
        ///     goes on the right.
        /// </param>
        /// <returns>Text exactly <paramref name="width" /> visible columns wide.</returns>
        public static string Fit(string text, int width,
            AnsiHorizontalAlignmentEnum alignment = AnsiHorizontalAlignmentEnum.Left)
        {
            if (width <= 0)
                return string.Empty;

            text ??= string.Empty;

            var visible = VisibleLength(text);

            // Already exactly right, which for a screen redrawn every frame is the overwhelmingly common case.
            if (visible == width)
                return text;

            if (visible > width)
                return Slice(text, 0, width);

            var missing = width - visible;

            switch (alignment)
            {
                case AnsiHorizontalAlignmentEnum.Right:
                    return new string(' ', missing) + text;

                case AnsiHorizontalAlignmentEnum.Center:
                    var before = missing / 2;
                    return new string(' ', before) + text + new string(' ', missing - before);

                default:
                    return text + new string(' ', missing);
            }
        }

        /// <summary>
        ///     Keeps a range of a styled string's visible columns, carrying every escape sequence through.
        ///     <para>
        ///         <b>This is the one safe way to cut a row somebody else has already styled.</b> A finished row is
        ///         several hundred characters long for twenty columns of width, so <c>Substring</c> by column lands
        ///         inside an escape and spills the remainder into the terminal as text. Walking the grammar instead
        ///         keeps the columns and the escapes separate, which is what makes the cut mean what it says.
        ///     </para>
        ///     <para>
        ///         <b>Every escape is kept, including those before the range and after it.</b> Both halves matter
        ///         and for opposite reasons: drop the ones before and a run that was opened earlier arrives
        ///         unstyled, drop the ones after and a run that was opened is never closed and its colour runs on
        ///         across everything drawn later. Some of the kept sequences paint nothing, which costs a few bytes
        ///         and is the price of not having to understand what any of them mean.
        ///     </para>
        ///     <para>
        ///         Prefer <see cref="WolfCurses.Window.Control.TextRow" /> when you are <i>building</i> the row:
        ///         keeping it as plain runs until it is drawn is cheaper and needs no parsing at all. This is for
        ///         when a row arrives already finished, which is what every widget's own render gives you.
        ///     </para>
        /// </summary>
        /// <param name="text">The text to cut. Null and empty give an empty string.</param>
        /// <param name="fromColumn">The first visible column to keep. Below zero counts as zero.</param>
        /// <param name="count">How many visible columns to keep. Zero or fewer gives an empty string.</param>
        /// <returns>That range of the text, styling intact.</returns>
        public static string Slice(string text, int fromColumn, int count)
        {
            if (string.IsNullOrEmpty(text) || count <= 0)
                return string.Empty;

            var from = Math.Max(0, fromColumn);
            var end = from + count;

            var sb = new StringBuilder(text.Length);
            var seen = 0;
            var i = 0;

            while (i < text.Length)
            {
                if (text[i] == Escape)
                {
                    var next = SkipEscape(text, i);
                    sb.Append(text, i, next - i);
                    i = next;
                    continue;
                }

                if (seen >= from && seen < end)
                    sb.Append(text[i]);

                seen++;
                i++;
            }

            return sb.ToString();
        }

        /// <summary>
        ///     Given a string and the index of an <see cref="Escape" /> character within it, returns the index of the
        ///     first character after the escape sequence that starts there — so both <see cref="VisibleLength" /> and
        ///     <see cref="StripEscapes" /> skip escapes identically. A bare trailing <see cref="Escape" /> with nothing
        ///     following it consumes to the end of the string.
        /// </summary>
        /// <param name="line">The string being scanned.</param>
        /// <param name="i">The index of the <see cref="Escape" /> character.</param>
        /// <returns>The index just past the escape sequence.</returns>
        private static int SkipEscape(string line, int i)
        {
            if (i + 1 >= line.Length)
                return line.Length;

            var kind = line[i + 1];
            if (kind == '[')
            {
                // CSI: parameter/intermediate bytes (0x20-0x3F) up to a final byte in 0x40-0x7E.
                i += 2;
                while (i < line.Length && (line[i] < '@' || line[i] > '~'))
                    i++;
                if (i < line.Length)
                    i++;
            }
            else if (kind == ']' || kind == 'P' || kind == '^' || kind == '_' || kind == 'X')
            {
                // OSC/DCS/PM/APC/SOS string (e.g. an OSC 8 hyperlink): runs until BEL or the ST terminator
                // "ESC \"; any other escape means the string was left unterminated and a new sequence begins.
                i += 2;
                while (i < line.Length)
                {
                    if (line[i] == (char) 7)
                    {
                        i++;
                        break;
                    }

                    if (line[i] == Escape)
                    {
                        if (i + 1 < line.Length && line[i + 1] == '\\')
                            i += 2;
                        break;
                    }

                    i++;
                }
            }
            else
            {
                // A short ESC sequence: optional intermediate bytes (0x20-0x2F, e.g. the "(" of a charset
                // designation like "ESC ( B") followed by one final byte.
                i += 2;
                if (kind >= ' ' && kind <= '/')
                {
                    while (i < line.Length && line[i] >= ' ' && line[i] <= '/')
                        i++;
                    if (i < line.Length)
                        i++;
                }
            }

            return i;
        }
    }
}
