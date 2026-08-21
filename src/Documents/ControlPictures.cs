// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/20/2026

using System.Text;

namespace WolfCurses.Documents
{
    /// <summary>
    ///     Turns the control characters a real file contains into characters that can be <i>drawn</i>, because a
    ///     terminal does not draw them: it obeys them.
    ///     <para>
    ///         <b>This is the other half of making a stored line printable, and the half nobody remembers.</b>
    ///         <see cref="TabStops.Expand" /> handles the one control character everybody thinks of; a form feed, a
    ///         carriage return, a bell or an escape written straight to the console moves the cursor, rings, or
    ///         starts a sequence the terminal will act on. A form feed is the one that turns up first and in the
    ///         least suspicious place: text files have used one as a page break for fifty years, so the shipped
    ///         sample documents contain them and nothing anybody typed put them there. Written raw, it moves the
    ///         cursor down a row part way through writing a row, and everything after it in that row lands on the
    ///         line below, over whatever was already drawn there.
    ///     </para>
    ///     <para>
    ///         <b>Escape is the one with teeth.</b> A document holding <c>ESC[2J</c> would clear the screen it is
    ///         being read in, and a document holding anything else would repaint the interface around itself. That
    ///         is not an exotic file to meet: it is what any captured terminal session or coloured log looks like.
    ///     </para>
    ///     <para>
    ///         <b>One character in, one character out, always.</b> That is the whole contract and the reason this is
    ///         a substitution rather than a removal or an expansion. The caller has already worked its columns out
    ///         with <see cref="TabStops" />, and anything of a different width would move the caret, the selection
    ///         and every mouse hit test off by however many control characters happened to be earlier in the line.
    ///     </para>
    ///     <para>
    ///         <b>Drawing only. It never touches what is stored.</b> A page break has to survive being opened and
    ///         saved again, the same stance <see cref="TextBuffer" /> takes on remembering a file's line ending
    ///         rather than normalizing it. An editor that scrubbed control characters on load would be a
    ///         reformatter.
    ///     </para>
    /// </summary>
    public static class ControlPictures
    {
        /// <summary>
        ///     Unicode's pictures for the C0 controls, one per code point from NUL upward, so the glyph for a
        ///     character is found by adding rather than by a table.
        /// </summary>
        private const int PictureBase = 0x2400;

        /// <summary>The picture for DELETE, which sits past the run rather than inside it.</summary>
        private const char DeletePicture = '␡';

        /// <summary>
        ///     What stands in for a control character Unicode has no picture for, which is the C1 range. They are
        ///     worth replacing anyway: one of them is an eight-bit control sequence introducer, so a terminal in the
        ///     right mode acts on it exactly as it would on an escape.
        /// </summary>
        public const char Unknown = '␦';

        /// <summary>
        ///     The character to draw in place of one that would be obeyed. Anything that is not a control character
        ///     is handed back unchanged, spaces included: a space has a picture of its own in Unicode and is
        ///     emphatically not what anybody wants to see instead of a space.
        /// </summary>
        /// <param name="character">The character as it is stored.</param>
        /// <returns>The character as it can be drawn.</returns>
        public static char For(char character)
        {
            if (character < ' ')
                return (char) (PictureBase + character);

            if (character == '\u007F')
                return DeletePicture;

            return char.IsControl(character) ? Unknown : character;
        }

        /// <summary>
        ///     Rewrites text with every control character replaced by something visible of the same width. Returns
        ///     the same reference when there was nothing to replace, so the ordinary line costs a scan and no
        ///     allocation, exactly as <see cref="TabStops.Expand" /> does.
        ///     <para>
        ///         <b>Expand tabs before calling this, not after.</b> A tab is a control character, so one that
        ///         reaches here becomes a single picture rather than a run of spaces, which is right for a caller
        ///         that does not lay out tab stops and wrong for one that does.
        ///     </para>
        /// </summary>
        /// <param name="text">The text as it is stored.</param>
        /// <returns>The text as it can be drawn.</returns>
        public static string Replace(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var first = -1;
            for (var i = 0; i < text.Length; i++)
            {
                if (!char.IsControl(text[i]))
                    continue;

                first = i;
                break;
            }

            if (first < 0)
                return text;

            var sb = new StringBuilder(text, 0, first, text.Length);
            for (var i = first; i < text.Length; i++)
                sb.Append(For(text[i]));

            return sb.ToString();
        }
    }
}
