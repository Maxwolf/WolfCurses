// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/20/2026

using System;
using System.Collections.Generic;
using System.IO;

namespace WolfCurses.Apps.WordProcessor
{
    /// <summary>
    ///     The word list the spell checker asks. Loaded from <c>dictionary/words.txt</c> beside the executable,
    ///     which is 370,105 public domain words; see <c>Assets/dictionary/README.md</c> for where they came from and
    ///     why a shorter list was rejected on licensing rather than on size.
    ///     <para>
    ///         <b>Loaded once per process and not once per editor.</b> It is 4 MB on disk and roughly 25 MB as a
    ///         set, so a form that reloaded it every time somebody reopened the word processor would be noticeably
    ///         worse for no reason. A static cache rather than something on <see cref="AppsWindowInfo" /> because
    ///         this is not shared <i>state</i>: nothing mutates it, nothing needs to see anybody else's changes to
    ///         it, and a suite-wide clipboard genuinely is a different thing from a file that is read once.
    ///     </para>
    ///     <para>
    ///         <b>Loaded lazily, on the first spell check.</b> Doing it at start-up would make the editor slower to
    ///         open for everybody who never spell checks, which is most people most of the time.
    ///     </para>
    /// </summary>
    internal sealed class SpellDictionary
    {
        /// <summary>The one instance, since the file never changes while the program runs.</summary>
        private static SpellDictionary _shared;

        /// <summary>The words, compared without regard to case so a capitalized word is not a misspelling.</summary>
        private readonly HashSet<string> _words = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Initializes a new instance of the <see cref="SpellDictionary" /> class.</summary>
        private SpellDictionary()
        {
        }

        /// <summary>Where the word list lives once the build has copied it beside the executable.</summary>
        public static string Path => System.IO.Path.Combine(AppContext.BaseDirectory, "dictionary", "words.txt");

        /// <summary>How many words were loaded.</summary>
        public int Count => _words.Count;

        /// <summary>Why the load failed, or null when it did not.</summary>
        public string Error { get; private set; }

        /// <summary>Whether there is a usable dictionary.</summary>
        public bool IsUsable => _words.Count > 0;

        /// <summary>
        ///     The shared dictionary, reading the file on the first call. A failed load is remembered rather than
        ///     retried, so a missing file costs one disk hit and reports the same thing every time afterwards.
        /// </summary>
        /// <returns>The dictionary, which may be unusable.</returns>
        public static SpellDictionary Shared()
        {
            if (_shared != null)
                return _shared;

            var dictionary = new SpellDictionary();
            dictionary.Load();
            _shared = dictionary;

            return _shared;
        }

        /// <summary>
        ///     A dictionary holding exactly the given words and reading no file, so what the checker does with a
        ///     word is decided by the test rather than by whatever a 370,105 word list happens to contain. That
        ///     matters more than it sounds: a near-exhaustive list has real entries for plenty of strings that look
        ///     like typos, so a test asserting that one is caught would be asserting a fact about the data file.
        /// </summary>
        /// <param name="words">The words it should know.</param>
        /// <returns>A dictionary that never touches the disk.</returns>
        internal static SpellDictionary ForTesting(params string[] words)
        {
            var dictionary = new SpellDictionary();
            foreach (var word in words ?? Array.Empty<string>())
                dictionary._words.Add(word);

            return dictionary;
        }

        /// <summary>Whether a word is in the list.</summary>
        /// <param name="word">The word to look up.</param>
        /// <returns>TRUE when it is a word.</returns>
        public bool Contains(string word)
        {
            return !string.IsNullOrEmpty(word) && _words.Contains(word);
        }

        /// <summary>Reads the file, or records why it could not.</summary>
        private void Load()
        {
            try
            {
                if (!File.Exists(Path))
                {
                    Error = "the word list was not found beside the program";
                    return;
                }

                // Read line by line rather than ReadAllLines: the whole file as an array of 370,105 strings is one
                // large allocation that is thrown away immediately afterwards, and the set is what is wanted.
                foreach (var line in File.ReadLines(Path))
                {
                    var word = line.Trim();
                    if (word.Length > 0)
                        _words.Add(word);
                }

                if (_words.Count == 0)
                    Error = "the word list was empty";
            }
            catch (IOException problem)
            {
                Error = problem.Message;
            }
            catch (UnauthorizedAccessException problem)
            {
                Error = problem.Message;
            }
        }
    }
}
