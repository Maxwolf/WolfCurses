// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/20/2026

using System;
using System.Collections.Generic;

namespace WolfCurses.Documents
{
    /// <summary>
    ///     Walking the words of a document. Pure arithmetic over strings, like everything else here: it holds no
    ///     state and knows nothing about what any word means, which is what keeps a spell checker's opinions out of
    ///     the library while the tedious half lives in one place.
    ///     <para>
    ///         <b>What counts as a word is the same rule the rest of the library uses</b>
    ///         (<see cref="TextBuffer.IsWordCharacter" />: letters, digits and the underscore), so a word here is
    ///         the same run that CTRL+arrow steps over, that a double-click selects and that a whole-word search
    ///         matches. Callers wanting a different rule pass their own, which is what a spell checker needs, since
    ///         an apostrophe is part of a word to a dictionary and is not part of one to a cursor.
    ///     </para>
    /// </summary>
    public static class TextWords
    {
        /// <summary>
        ///     The first word at or after a column, if there is one.
        ///     <para>
        ///         <b>A caller must resume past the word it was given</b> (<c>from = start + length</c>). Passing
        ///         the same start back hands back the same word forever, which is the same trap
        ///         <see cref="TextSearch" /> is shaped around and it costs a hung loop rather than a wrong answer.
        ///     </para>
        /// </summary>
        /// <param name="line">The line to walk.</param>
        /// <param name="from">The column to start looking at.</param>
        /// <param name="start">Where the word begins, or -1 when there is not one.</param>
        /// <param name="length">How many characters long it is.</param>
        /// <param name="isWordCharacter">What counts as part of a word; null uses the library's own rule.</param>
        /// <returns>TRUE when a word was found.</returns>
        public static bool TryNextWord(string line, int from, out int start, out int length,
            Func<char, bool> isWordCharacter = null)
        {
            start = -1;
            length = 0;

            if (string.IsNullOrEmpty(line))
                return false;

            var isWord = isWordCharacter ?? TextBuffer.IsWordCharacter;
            var at = Math.Max(0, from);

            while (at < line.Length && !isWord(line[at]))
                at++;

            if (at >= line.Length)
                return false;

            start = at;

            while (at < line.Length && isWord(line[at]))
                at++;

            length = at - start;
            return true;
        }

        /// <summary>How many words a document holds, which is the question every word processor is asked.</summary>
        /// <param name="lines">The document, one string per line.</param>
        /// <param name="isWordCharacter">What counts as part of a word; null uses the library's own rule.</param>
        /// <returns>The word count.</returns>
        public static int Count(IReadOnlyList<string> lines, Func<char, bool> isWordCharacter = null)
        {
            if (lines == null)
                return 0;

            var total = 0;

            foreach (var line in lines)
            {
                var at = 0;
                while (TryNextWord(line, at, out var start, out var length, isWordCharacter))
                {
                    total++;
                    at = start + length;
                }
            }

            return total;
        }
    }
}
