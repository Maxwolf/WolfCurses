// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/20/2026

using System;
using System.Text;

namespace WolfCurses.Documents
{
    /// <summary>
    ///     The translation between where a character <i>is</i> in a line and where it is <i>drawn</i>, which stop
    ///     being the same thing the moment the line contains a tab. One character of document, several columns of
    ///     screen.
    ///     <para>
    ///         <b>A tab advances to the next tab stop, it is not a fixed number of spaces.</b> That distinction is
    ///         the whole reason this type exists: replacing each tab with N spaces looks right on a line that begins
    ///         with one and is wrong everywhere else, because a tab in column 3 of an eight-wide grid advances five
    ///         columns rather than eight. The naive version passes a first glance and misaligns every table anyone
    ///         opens.
    ///     </para>
    ///     <para>
    ///         Three things need this and each needs a different direction of it: drawing a line wants
    ///         <see cref="Expand" />, drawing a caret or a selection wants <see cref="ToDisplayColumn" />, and a
    ///         mouse click wants <see cref="ToDocumentColumn" />. A renderer that expands the text but forgets to
    ///         move the caret column with it puts the cursor in the wrong place on exactly the lines a person is
    ///         most likely to be editing.
    ///     </para>
    ///     <para>
    ///         Tabs are the case handled here and the case that matters for a terminal document. Characters that are
    ///         two columns wide (most CJK) or zero (combining marks) are the same class of problem and are
    ///         deliberately <b>not</b> solved: doing them properly needs a width table, and pretending otherwise
    ///         would be worse than the honest limitation.
    ///     </para>
    /// </summary>
    public static class TabStops
    {
        /// <summary>The tab stop interval nearly everything uses, and what a file written elsewhere assumes.</summary>
        public const int DefaultWidth = 8;

        /// <summary>
        ///     Rewrites a line as it should be drawn, with each tab replaced by however many spaces reach the next
        ///     tab stop. Returns the same reference when there is nothing to do, so the common case of a line with no
        ///     tabs costs a scan and no allocation.
        /// </summary>
        /// <param name="line">The line as it is stored.</param>
        /// <param name="tabWidth">Columns between tab stops; anything below one is treated as one.</param>
        /// <returns>The line as it should appear on screen.</returns>
        public static string Expand(string line, int tabWidth = DefaultWidth)
        {
            if (string.IsNullOrEmpty(line) || line.IndexOf('\t') < 0)
                return line;

            tabWidth = Math.Max(1, tabWidth);

            var sb = new StringBuilder(line.Length + tabWidth);
            foreach (var character in line)
            {
                if (character != '\t')
                {
                    sb.Append(character);
                    continue;
                }

                sb.Append(' ', tabWidth - sb.Length % tabWidth);
            }

            return sb.ToString();
        }

        /// <summary>
        ///     Where a document column is drawn. Walks the line rather than multiplying, because every tab before the
        ///     position changes the answer by a different amount depending on where it sits.
        /// </summary>
        /// <param name="line">The line as it is stored.</param>
        /// <param name="documentColumn">A character index, which may be the line's length (just past the end).</param>
        /// <param name="tabWidth">Columns between tab stops.</param>
        /// <returns>The screen column that character is drawn at.</returns>
        public static int ToDisplayColumn(string line, int documentColumn, int tabWidth = DefaultWidth)
        {
            tabWidth = Math.Max(1, tabWidth);

            if (string.IsNullOrEmpty(line))
                return Math.Max(0, documentColumn);

            var limit = Math.Clamp(documentColumn, 0, line.Length);
            var column = 0;

            for (var i = 0; i < limit; i++)
                column = line[i] == '\t' ? column + (tabWidth - column % tabWidth) : column + 1;

            // Past the stored end of the line, one document column is one screen column: there is nothing there but
            // the caret, and it moves one cell at a time.
            return column + Math.Max(0, documentColumn - line.Length);
        }

        /// <summary>
        ///     Which character a screen column falls on, which is the whole of a mouse hit test on a line with tabs.
        ///     A column anywhere inside a tab's run of spaces lands on the tab itself, so clicking the middle of an
        ///     indent puts the caret somewhere a subsequent BACKSPACE removes in one press rather than somewhere
        ///     between two characters that do not exist.
        /// </summary>
        /// <param name="line">The line as it is stored.</param>
        /// <param name="displayColumn">A screen column.</param>
        /// <param name="tabWidth">Columns between tab stops.</param>
        /// <returns>The character index under that column, clamped to the line.</returns>
        public static int ToDocumentColumn(string line, int displayColumn, int tabWidth = DefaultWidth)
        {
            tabWidth = Math.Max(1, tabWidth);

            if (string.IsNullOrEmpty(line) || displayColumn <= 0)
                return Math.Max(0, Math.Min(displayColumn, line?.Length ?? 0));

            var column = 0;
            for (var i = 0; i < line.Length; i++)
            {
                var next = line[i] == '\t' ? column + (tabWidth - column % tabWidth) : column + 1;
                if (displayColumn < next)
                    return i;

                column = next;
            }

            // Clicking past the end of the text lands after the last character, and clicking far past it stays
            // there rather than inventing columns the line does not have.
            return line.Length;
        }

        /// <summary>How many screen columns a whole line occupies.</summary>
        /// <param name="line">The line as it is stored.</param>
        /// <param name="tabWidth">Columns between tab stops.</param>
        /// <returns>The line's drawn width.</returns>
        public static int DisplayWidth(string line, int tabWidth = DefaultWidth)
        {
            return string.IsNullOrEmpty(line) ? 0 : ToDisplayColumn(line, line.Length, tabWidth);
        }
    }
}
