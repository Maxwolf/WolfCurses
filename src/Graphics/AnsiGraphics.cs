// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/16/2026

using System;
using System.Text;

namespace WolfCurses.Graphics
{
    /// <summary>
    ///     The contract between an <see cref="IImageRenderer" /> that draws with a true-pixel protocol (sixel, kitty)
    ///     and the <see cref="ConsolePresenter" /> that puts its output on screen.
    ///     <para>
    ///         The whole library speaks in whole rows: a frame is one string, one line per screen row, and the presenter
    ///         only rewrites the lines that changed. Half-block images fit that perfectly — a row of image is a row of
    ///         text. Sixel and kitty do not. The entire picture is <em>one</em> escape sequence that paints across many
    ///         rows at once, yet as text it is a single line of zero visible width. That breaks the presenter's model
    ///         twice over: it would think the rows under the picture were empty and erase straight through them, and it
    ///         would erase to end-of-line after the payload itself — which, because a terminal leaves the cursor below a
    ///         sixel it has just drawn, wipes part of the picture.
    ///     </para>
    ///     <para>
    ///         So a true-pixel renderer marks its rows with <see cref="Marker" />, a character no real text contains:
    ///         the payload line is <see cref="Marker" /> followed by the escape sequence, and every further row the
    ///         picture covers is a bare <see cref="RowPlaceholder" /> line. The line count therefore stays honest — the
    ///         frame still has exactly one line per screen row, so the scene graph's diff and everything downstream keep
    ///         working unchanged — while the presenter learns the two things it cannot infer: "write this row but do not
    ///         erase after it", and "this row belongs to the picture above; leave it alone".
    ///     </para>
    ///     <para>
    ///         Markers are stripped before anything reaches the terminal, and are only produced by renderers a host opts
    ///         into, so an application using the default <see cref="HalfBlockImageRenderer" /> never encounters them. A
    ///         host that draws frames itself instead of using <see cref="ConsolePresenter" /> must call
    ///         <see cref="StripMarkers" /> on the frame before writing it.
    ///     </para>
    /// </summary>
    /// <seealso cref="IImageRenderer" />
    public static class AnsiGraphics
    {
        /// <summary>
        ///     The character that marks a row as belonging to a true-pixel picture rather than to text. U+E000, the
        ///     first code point of the Unicode private use area, is used because no content or font assigns it meaning,
        ///     so it cannot collide with anything a window legitimately renders. It is never sent to the terminal.
        /// </summary>
        public const char Marker = '\uE000';

        /// <summary>
        ///     A line meaning "this screen row is covered by the picture whose payload appeared on an earlier row": the
        ///     presenter neither writes nor erases it, and never parks the cursor on it.
        /// </summary>
        public const string RowPlaceholder = "\uE000";

        /// <summary>
        ///     Wraps a true-pixel escape payload into the complete multi-line block a renderer should return: the marked
        ///     payload row, followed by one <see cref="RowPlaceholder" /> line for every further row the picture covers.
        /// </summary>
        /// <param name="payload">The raw escape sequence that draws the whole picture.</param>
        /// <param name="rows">Total screen rows the picture occupies, its payload row included.</param>
        /// <returns>The block, rows separated by <see cref="Environment.NewLine" /> and with no trailing newline.</returns>
        internal static string PayloadBlock(string payload, int rows)
        {
            var builder = new StringBuilder();
            builder.Append(Marker).Append(payload);

            for (var row = 1; row < rows; row++)
                builder.Append(Environment.NewLine).Append(RowPlaceholder);

            return builder.ToString();
        }

        /// <summary>
        ///     True when <paramref name="line" /> is a row covered by a picture drawn above it rather than one holding
        ///     content of its own.
        /// </summary>
        internal static bool IsRowPlaceholder(string line)
        {
            return line != null && line.Length == 1 && line[0] == Marker;
        }

        /// <summary>
        ///     True when <paramref name="line" /> carries a true-pixel escape payload, which must be written to the
        ///     terminal but never erased after. Use <see cref="PayloadOf" /> to get the escape sequence itself.
        /// </summary>
        internal static bool IsPayloadRow(string line)
        {
            return line != null && line.Length > 1 && line[0] == Marker;
        }

        /// <summary>The escape sequence carried by a payload row, without its <see cref="Marker" />.</summary>
        internal static string PayloadOf(string line)
        {
            return line.Substring(1);
        }

        /// <summary>
        ///     Removes every graphics marker from a rendered frame, turning payload rows back into bare escape sequences
        ///     and placeholder rows into empty lines. A host that writes frames to the terminal itself, rather than
        ///     through <see cref="ConsolePresenter" />, must do this first — otherwise the private-use marker characters
        ///     are printed as garbage. Frames containing no images pass through unchanged.
        /// </summary>
        /// <param name="frame">A rendered frame, or null.</param>
        /// <returns>The frame with markers removed.</returns>
        public static string StripMarkers(string frame)
        {
            if (string.IsNullOrEmpty(frame) || frame.IndexOf(Marker) < 0)
                return frame;

            var builder = new StringBuilder(frame.Length);
            foreach (var character in frame)
            {
                if (character != Marker)
                    builder.Append(character);
            }

            return builder.ToString();
        }
    }
}
