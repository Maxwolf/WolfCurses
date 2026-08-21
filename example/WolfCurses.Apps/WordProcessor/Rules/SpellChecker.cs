// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/20/2026

using System;
using System.Collections.Generic;
using System.Text;

namespace WolfCurses.Apps.WordProcessor
{
    /// <summary>
    ///     Deciding whether a word is spelled correctly and, when it is not, what was probably meant. Pure logic
    ///     with no console and no document, so all of it is unit tested directly.
    ///     <para>
    ///         <b>Suggestions are generated rather than searched for.</b> The obvious implementation compares the
    ///         typed word against every word in the list, which is 370,105 edit-distance calculations for one
    ///         suggestion and takes long enough to be felt. This builds the few hundred strings that are one edit
    ///         away from what was typed and asks the set which of them are words, which is a few hundred hash
    ///         lookups instead. The idea is Peter Norvig's and it is the difference between a feature and a pause.
    ///     </para>
    /// </summary>
    internal static class SpellChecker
    {
        /// <summary>The letters an edit may insert or substitute.</summary>
        private const string Alphabet = "abcdefghijklmnopqrstuvwxyz";

        /// <summary>
        ///     What counts as part of a word when spell checking, which is <b>not</b> the library's rule.
        ///     <para>
        ///         The apostrophe is the whole difference. To a cursor it is a boundary, so CTRL+arrow rightly stops
        ///         at it; to a dictionary "don't" is one word, and splitting it hands the checker a fragment that no
        ///         word list contains. Half of Hamlet would be underlined.
        ///     </para>
        /// </summary>
        /// <param name="character">The character to test.</param>
        /// <returns>TRUE when it is part of a word.</returns>
        public static bool IsWordCharacter(char character)
        {
            return char.IsLetterOrDigit(character) || character == '\'';
        }

        /// <summary>
        ///     The part of a word worth looking up: everything before the first apostrophe. A word list of plain
        ///     words has no contractions in it, so "don't" is checked as "don" and "Hamlet's" as "Hamlet", both of
        ///     which are real words and neither of which would be found whole.
        /// </summary>
        /// <param name="word">The word as it appears in the document.</param>
        /// <returns>The part to look up, which may be empty.</returns>
        public static string Stem(string word)
        {
            if (string.IsNullOrEmpty(word))
                return string.Empty;

            var apostrophe = word.IndexOf('\'');
            return apostrophe < 0 ? word : word.Substring(0, apostrophe);
        }

        /// <summary>
        ///     Whether a word is worth checking at all. Three things are skipped, and each of them is a false
        ///     positive nobody would thank us for: anything with a digit in it (<c>rfc1149</c>), anything in capitals
        ///     (<c>IP</c>, <c>BBN</c>, and every other acronym), and single letters.
        /// </summary>
        /// <param name="word">The word as it appears in the document.</param>
        /// <returns>TRUE when it should be looked up.</returns>
        public static bool ShouldCheck(string word)
        {
            var stem = Stem(word);
            if (stem.Length < 2)
                return false;

            var capitals = 0;
            foreach (var character in stem)
            {
                if (!char.IsLetter(character))
                    return false;

                if (char.IsUpper(character))
                    capitals++;
            }

            return capitals != stem.Length;
        }

        /// <summary>Whether a word is spelled correctly, which anything not worth checking counts as.</summary>
        /// <param name="word">The word as it appears in the document.</param>
        /// <param name="dictionary">The word list to ask.</param>
        /// <returns>TRUE when the word is fine or is not our business.</returns>
        public static bool IsCorrect(string word, SpellDictionary dictionary)
        {
            if (dictionary == null || !dictionary.IsUsable)
                return true;

            return !ShouldCheck(word) || dictionary.Contains(Stem(word));
        }

        /// <summary>
        ///     What was probably meant, best first. Words one edit away are offered before words two edits away, and
        ///     the second tier is only built when the first found nothing, since it costs a few hundred times more.
        /// </summary>
        /// <param name="word">The misspelled word as it appears in the document.</param>
        /// <param name="dictionary">The word list to ask.</param>
        /// <param name="limit">How many to offer.</param>
        /// <returns>The suggestions, which may be none at all.</returns>
        public static IReadOnlyList<string> Suggest(string word, SpellDictionary dictionary, int limit = 6)
        {
            var found = new List<string>();
            if (dictionary == null || !dictionary.IsUsable || limit <= 0)
                return found;

            var stem = Stem(word);
            if (stem.Length == 0)
                return found;

            var lower = stem.ToLowerInvariant();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var oneEdit = Edits(lower);
            Collect(oneEdit, dictionary, seen, found, limit);

            if (found.Count == 0)
            {
                // Two edits away, built from the first tier. Only reached when nothing closer exists, because this
                // is a few hundred squared strings and it is the one path here that can be felt.
                foreach (var candidate in oneEdit)
                {
                    Collect(Edits(candidate), dictionary, seen, found, limit);
                    if (found.Count >= limit)
                        break;
                }
            }

            // Offered back in the shape of what was typed, so correcting a word at the start of a sentence does not
            // quietly lower-case it.
            for (var i = 0; i < found.Count; i++)
                found[i] = MatchCase(stem, found[i]);

            return found;
        }

        /// <summary>Keeps whichever candidates are real words, in the order they were generated.</summary>
        private static void Collect(IEnumerable<string> candidates, SpellDictionary dictionary,
            HashSet<string> seen, List<string> found, int limit)
        {
            foreach (var candidate in candidates)
            {
                if (found.Count >= limit)
                    return;

                if (!dictionary.Contains(candidate) || !seen.Add(candidate))
                    continue;

                found.Add(candidate);
            }
        }

        /// <summary>
        ///     Every string one edit away: a letter dropped, two letters swapped, a letter changed, a letter added.
        ///     Four kinds because those are the four ways a person mistypes a word, and leaving out transposition in
        ///     particular loses the single most common typing mistake there is.
        /// </summary>
        private static List<string> Edits(string word)
        {
            var edits = new List<string>();

            for (var i = 0; i < word.Length; i++)
                edits.Add(word.Remove(i, 1));

            for (var i = 0; i < word.Length - 1; i++)
            {
                var swapped = new StringBuilder(word);
                (swapped[i], swapped[i + 1]) = (swapped[i + 1], swapped[i]);
                edits.Add(swapped.ToString());
            }

            foreach (var letter in Alphabet)
            {
                for (var i = 0; i < word.Length; i++)
                {
                    if (word[i] != letter)
                        edits.Add(word.Substring(0, i) + letter + word.Substring(i + 1));
                }

                for (var i = 0; i <= word.Length; i++)
                    edits.Add(word.Substring(0, i) + letter + word.Substring(i));
            }

            return edits;
        }

        /// <summary>Dresses a suggestion in the capitalization of the word it is replacing.</summary>
        private static string MatchCase(string original, string suggestion)
        {
            if (original.Length == 0 || suggestion.Length == 0 || !char.IsUpper(original[0]))
                return suggestion;

            return char.ToUpperInvariant(suggestion[0]) + suggestion.Substring(1);
        }
    }
}
