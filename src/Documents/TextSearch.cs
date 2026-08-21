// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/20/2026

using System;
using System.Collections.Generic;

namespace WolfCurses.Documents
{
    /// <summary>
    ///     Finding text in a document. Pure arithmetic over a list of lines, like everything else in this namespace:
    ///     it takes no buffer, holds no state and remembers nothing between calls, so a caller can search a
    ///     <see cref="TextBuffer" />, a log it read off disk or a list it built itself.
    ///     <para>
    ///         <b>The two directions are deliberately not symmetrical</b>, and the asymmetry is what makes a Find
    ///         Next work at all. Forward returns the first match starting <i>at or after</i> the position it is
    ///         given; backward returns the last match starting <i>strictly before</i> it. Search forward from a
    ///         caret that is sitting on a match and you would find that same match forever, which is the classic
    ///         bug; with these rules the caller passes the end of the current match going forward and its start
    ///         going back, and both land on the neighbour.
    ///     </para>
    ///     <para>
    ///         <b>Wrapping is one extra pass, not an endless loop.</b> Having run out of document it starts again
    ///         from the other end and stops when it reaches where it began, so a needle that occurs once is found
    ///         once however many times Find Next is pressed, and a needle that occurs never returns null rather than
    ///         spinning.
    ///     </para>
    ///     <para>
    ///         <b>A needle spanning a line break is deliberately not supported.</b> Nothing here joins lines, so a
    ///         search for text containing a newline finds nothing. That is a different algorithm, and it is not what
    ///         a Find box is for.
    ///     </para>
    /// </summary>
    public static class TextSearch
    {
        /// <summary>
        ///     The next occurrence of <paramref name="needle" />, or null when there is not one.
        /// </summary>
        /// <param name="lines">The document, one string per line.</param>
        /// <param name="needle">What to look for; empty finds nothing, since it would otherwise match everywhere.</param>
        /// <param name="from">Where to start looking, in stored columns rather than screen ones.</param>
        /// <param name="matchCase">TRUE to tell case apart. Comparison is ordinal either way.</param>
        /// <param name="wholeWord">TRUE to refuse a match with a word character against either end of it.</param>
        /// <param name="backwards">TRUE to look towards the start of the document instead.</param>
        /// <param name="wrap">TRUE to continue from the far end after running out of document.</param>
        /// <returns>Where the match starts, or null.</returns>
        public static TextPosition? Next(IReadOnlyList<string> lines, string needle, TextPosition from,
            bool matchCase = false, bool wholeWord = false, bool backwards = false, bool wrap = true)
        {
            if (lines == null || lines.Count == 0 || string.IsNullOrEmpty(needle))
                return null;

            // Ordinal rather than culture-aware, because a document is bytes somebody typed rather than prose in a
            // known language, and a caller who searches for what is on screen expects to find exactly that.
            var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            var line = Math.Clamp(from.Line, 0, lines.Count - 1);
            var column = Math.Max(0, from.Column);

            return backwards
                ? Backward(lines, needle, line, column, comparison, wholeWord, wrap)
                : Forward(lines, needle, line, column, comparison, wholeWord, wrap);
        }

        /// <summary>Whether every occurrence would be found, which is what a Replace All needs to know first.</summary>
        /// <param name="lines">The document, one string per line.</param>
        /// <param name="needle">What to look for.</param>
        /// <param name="matchCase">TRUE to tell case apart.</param>
        /// <param name="wholeWord">TRUE to require word boundaries.</param>
        /// <returns>How many times it occurs.</returns>
        public static int Count(IReadOnlyList<string> lines, string needle, bool matchCase = false,
            bool wholeWord = false)
        {
            if (lines == null || lines.Count == 0 || string.IsNullOrEmpty(needle))
                return 0;

            var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            var found = 0;

            for (var i = 0; i < lines.Count; i++)
            {
                var at = 0;
                while (true)
                {
                    var hit = FirstIn(lines[i], needle, at, int.MaxValue, comparison, wholeWord);
                    if (hit < 0)
                        break;

                    // Occurrences do not overlap, which is the same rule replacing them has to follow: counting
                    // overlaps would promise a Replace All more work than it can actually do.
                    found++;
                    at = hit + needle.Length;
                }
            }

            return found;
        }

        /// <summary>Looks towards the end of the document, then round from the start.</summary>
        private static TextPosition? Forward(IReadOnlyList<string> lines, string needle, int fromLine, int fromColumn,
            StringComparison comparison, bool wholeWord, bool wrap)
        {
            for (var i = fromLine; i < lines.Count; i++)
            {
                var at = FirstIn(lines[i], needle, i == fromLine ? fromColumn : 0, int.MaxValue, comparison,
                    wholeWord);

                if (at >= 0)
                    return new TextPosition(i, at);
            }

            if (!wrap)
                return null;

            for (var i = 0; i <= fromLine; i++)
            {
                // On the line it started from, only what lies before the starting column has not been looked at.
                var last = i == fromLine ? fromColumn - 1 : int.MaxValue;
                var at = FirstIn(lines[i], needle, 0, last, comparison, wholeWord);

                if (at >= 0)
                    return new TextPosition(i, at);
            }

            return null;
        }

        /// <summary>Looks towards the start of the document, then round from the end.</summary>
        private static TextPosition? Backward(IReadOnlyList<string> lines, string needle, int fromLine, int fromColumn,
            StringComparison comparison, bool wholeWord, bool wrap)
        {
            for (var i = fromLine; i >= 0; i--)
            {
                var last = i == fromLine ? fromColumn - 1 : int.MaxValue;
                var at = LastIn(lines[i], needle, 0, last, comparison, wholeWord);

                if (at >= 0)
                    return new TextPosition(i, at);
            }

            if (!wrap)
                return null;

            for (var i = lines.Count - 1; i >= fromLine; i--)
            {
                var first = i == fromLine ? fromColumn : 0;
                var at = LastIn(lines[i], needle, first, int.MaxValue, comparison, wholeWord);

                if (at >= 0)
                    return new TextPosition(i, at);
            }

            return null;
        }

        /// <summary>The first match in one line within a range of starting columns, or -1.</summary>
        private static int FirstIn(string line, string needle, int first, int last, StringComparison comparison,
            bool wholeWord)
        {
            first = Math.Max(0, first);

            // A match cannot start closer to the end than its own length, which is also what keeps IndexOf's start
            // index inside the string.
            last = Math.Min(last, line.Length - needle.Length);

            var at = first;
            while (at <= last)
            {
                var found = line.IndexOf(needle, at, comparison);
                if (found < 0 || found > last)
                    return -1;

                if (!wholeWord || IsWholeWord(line, found, needle.Length))
                    return found;

                at = found + 1;
            }

            return -1;
        }

        /// <summary>The last match in one line within a range of starting columns, or -1.</summary>
        private static int LastIn(string line, string needle, int first, int last, StringComparison comparison,
            bool wholeWord)
        {
            first = Math.Max(0, first);
            last = Math.Min(last, line.Length - needle.Length);

            for (var at = last; at >= first; at--)
            {
                if (string.Compare(line, at, needle, 0, needle.Length, comparison) != 0)
                    continue;

                if (!wholeWord || IsWholeWord(line, at, needle.Length))
                    return at;
            }

            return -1;
        }

        /// <summary>
        ///     Whether a match has a word boundary at both ends. Uses the same rule as word movement and
        ///     double-click selection, so "whole word" means the same thing everywhere in the library.
        /// </summary>
        private static bool IsWholeWord(string line, int index, int length)
        {
            if (index > 0 && TextBuffer.IsWordCharacter(line[index - 1]))
                return false;

            var after = index + length;
            return after >= line.Length || !TextBuffer.IsWordCharacter(line[after]);
        }
    }
}
