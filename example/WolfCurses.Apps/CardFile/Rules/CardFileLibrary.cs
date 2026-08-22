// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;
using System.IO;
using WolfCurses.Documents;

namespace WolfCurses.Apps.CardFile
{
    /// <summary>
    ///     Where the sample cards live, and how a deck is read and written. No console anywhere in here, exactly as
    ///     the word processor's document library and the planner's are.
    ///     <para>
    ///         <b>This is the screen that has to distrust a file it wrote itself</b>, and the distrust is all in
    ///         <see cref="Parse" />. Everything else in the suite reads a file somebody else made, or reads its own
    ///         and gets away with assuming the shape. A card file writes a file the user then opens in the word
    ///         processor three menu items away, moves a column in, deletes one, and hand-types a row that is a
    ///         field short. All three come back here.
    ///     </para>
    ///     <para>
    ///         The answer is that <b>nothing is read by position</b>. The header row says what the columns are and
    ///         every value is fetched by name through <see cref="DelimitedColumns" />, which answers empty for both
    ///         of the ragged cases rather than throwing on one and lying about the other.
    ///     </para>
    /// </summary>
    internal static class CardFileLibrary
    {
        /// <summary>The card file opened at start-up.</summary>
        public const string DefaultCardsName = "contacts.csv";

        /// <summary>The folder the samples are copied into, beside the executable.</summary>
        public static string Folder => Path.Combine(AppContext.BaseDirectory, "cards");

        /// <summary>The full path of the card file opened at start-up.</summary>
        public static string DefaultCardsPath => Path.Combine(Folder, DefaultCardsName);

        /// <summary>Where an Open dialog should start, falling back to somewhere that certainly exists.</summary>
        public static string BrowseFolder => Directory.Exists(Folder) ? Folder : AppContext.BaseDirectory;

        /// <summary>The file extensions the Open dialog offers.</summary>
        public static string[] Extensions { get; } = {".csv", ".txt"};

        /// <summary>
        ///     Reads a deck, or returns null when it cannot be read for any reason. Null rather than an exception
        ///     and caught broadly, for the reason every file reader in this suite is: an Open dialog can be pointed
        ///     at anything on the machine, and "that is a directory" belongs on the status line.
        /// </summary>
        /// <param name="path">The file to read.</param>
        /// <param name="error">What went wrong, when the read failed.</param>
        /// <returns>The deck, or null.</returns>
        public static CardDeck TryLoad(string path, out string error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(path))
            {
                error = "No file name was given.";
                return null;
            }

            try
            {
                return Parse(File.ReadAllText(path));
            }
            catch (Exception exception) when (exception is IOException
                                                  or UnauthorizedAccessException
                                                  or NotSupportedException
                                                  or ArgumentException)
            {
                error = exception.Message;
                return null;
            }
        }

        /// <summary>Writes a deck, reporting failure the same way <see cref="TryLoad" /> does.</summary>
        /// <param name="deck">The deck to write.</param>
        /// <param name="path">Where to write it.</param>
        /// <param name="error">What went wrong, when the write failed.</param>
        /// <returns>TRUE when the file was written.</returns>
        public static bool TrySave(CardDeck deck, string path, out string error)
        {
            error = null;

            if (deck == null || string.IsNullOrWhiteSpace(path))
            {
                error = "No file name was given.";
                return false;
            }

            try
            {
                File.WriteAllText(path, Format(deck));
                return true;
            }
            catch (Exception exception) when (exception is IOException
                                                  or UnauthorizedAccessException
                                                  or NotSupportedException
                                                  or ArgumentException)
            {
                error = exception.Message;
                return false;
            }
        }

        /// <summary>
        ///     Writes a deck out as delimited text, header row and all. Separate from writing a file so the round
        ///     trip that matters can be tested without one.
        /// </summary>
        /// <param name="deck">The deck.</param>
        /// <returns>The file's contents.</returns>
        public static string Format(CardDeck deck)
        {
            var rows = new List<IEnumerable<string>> {Card.FieldNames};

            foreach (var card in deck.Cards)
                rows.Add(card.ToRow());

            return DelimitedText.Write(rows, DelimitedText.DefaultDelimiter, deck.NewLine);
        }

        /// <summary>
        ///     Turns a file's text into a deck.
        ///     <para>
        ///         <b>A file with a header is read by name and one without is read by position</b>, and the two are
        ///         the same code: a headerless file is handed the field names as its header, so the loop below does
        ///         not know which kind it is looking at. What decides is whether the first row names the Name
        ///         column, since a file this program wrote always does.
        ///     </para>
        ///     <para>
        ///         The one ambiguity that cannot be resolved is a headerless file whose first card is somebody
        ///         called Name, which would be eaten as a header. That is why what this program writes always has
        ///         one: a file that declares its columns is never guessed about.
        ///     </para>
        ///     <para>
        ///         A row with nothing in its name field is skipped rather than filed, since the index is by name
        ///         and a nameless card is one nothing could reach. That also quietly disposes of the blank line at
        ///         the end of the file.
        ///     </para>
        /// </summary>
        /// <param name="text">The file's contents.</param>
        /// <returns>The deck.</returns>
        public static CardDeck Parse(string text)
        {
            var deck = new CardDeck();
            var rows = DelimitedText.Read(text);

            if (rows.Count > 0)
            {
                var declared = new DelimitedColumns(rows[0]);
                var headed = declared.HasAll(Card.FieldNames[Card.NameField]);

                // A file with no header is read as though it had the one this program writes, which is what makes
                // the two cases one loop instead of two.
                var columns = headed ? declared : new DelimitedColumns(Card.FieldNames);

                for (var i = headed ? 1 : 0; i < rows.Count; i++)
                {
                    var card = new Card();

                    for (var field = 0; field < card.Fields; field++)
                        card[field] = columns.Value(rows[i], Card.FieldNames[field]);

                    deck.Add(card);
                }
            }

            deck.NewLine = text != null && text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            deck.MarkSaved();

            return deck;
        }
    }
}
