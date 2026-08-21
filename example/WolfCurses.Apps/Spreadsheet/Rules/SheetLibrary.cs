// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;
using System.IO;
using WolfCurses.Documents;

namespace WolfCurses.Apps.Spreadsheet
{
    /// <summary>
    ///     Where the sample sheet lives, and how a sheet is read and written. No console anywhere in here, so the
    ///     awkward half of a spreadsheet can be driven from a test without a screen, exactly as the word processor's
    ///     document library is.
    ///     <para>
    ///         The folder is resolved from <see cref="AppContext.BaseDirectory" /> rather than the working
    ///         directory, which is the one path property a single-file publish keeps honest.
    ///     </para>
    /// </summary>
    internal static class SheetLibrary
    {
        /// <summary>The sheet the spreadsheet opens on.</summary>
        public const string DefaultSheetName = "spreadsheet.csv";

        /// <summary>The folder the samples are copied into, beside the executable.</summary>
        public static string Folder => Path.Combine(AppContext.BaseDirectory, "sheets");

        /// <summary>The full path of the sheet opened at start-up.</summary>
        public static string DefaultSheetPath => Path.Combine(Folder, DefaultSheetName);

        /// <summary>
        ///     Where an Open dialog should start: among the samples when they are there, and beside the executable
        ///     when they are not, so a browser always opens somewhere that exists rather than throwing.
        /// </summary>
        public static string BrowseFolder => Directory.Exists(Folder) ? Folder : AppContext.BaseDirectory;

        /// <summary>The file extensions the Open dialog offers.</summary>
        public static string[] Extensions { get; } = {".csv", ".tsv", ".txt"};

        /// <summary>
        ///     Reads a sheet, or returns null when it cannot be read for any reason.
        ///     <para>
        ///         Null rather than an exception, and caught broadly on purpose: this is a demonstration program
        ///         whose Open dialog can be pointed at any file on the machine, so "that is a directory", "that is
        ///         locked" and "the drive went away" are ordinary answers that belong on the status line rather than
        ///         faults that belong in a stack trace across the interface.
        ///     </para>
        /// </summary>
        /// <param name="path">The file to read.</param>
        /// <param name="error">What went wrong, when the read failed.</param>
        /// <returns>The sheet, or null.</returns>
        public static Sheet TryLoad(string path, out string error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(path))
            {
                error = "No file name was given.";
                return null;
            }

            try
            {
                return Parse(File.ReadAllText(path), DelimiterFor(path));
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

        /// <summary>Writes a sheet, reporting failure the same way <see cref="TryLoad" /> does.</summary>
        /// <param name="sheet">The sheet to write.</param>
        /// <param name="path">Where to write it.</param>
        /// <param name="error">What went wrong, when the write failed.</param>
        /// <returns>TRUE when the file was written.</returns>
        public static bool TrySave(Sheet sheet, string path, out string error)
        {
            error = null;

            if (sheet == null || string.IsNullOrWhiteSpace(path))
            {
                error = "No file name was given.";
                return false;
            }

            try
            {
                File.WriteAllText(path, DelimitedText.Write(sheet.Rows(), DelimiterFor(path), sheet.NewLine));
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
        ///     Turns delimited text into a sheet. Separate from reading a file so that the interesting half can be
        ///     tested without one.
        /// </summary>
        /// <param name="text">The file's contents.</param>
        /// <param name="delimiter">What separates the fields.</param>
        /// <returns>The sheet.</returns>
        public static Sheet Parse(string text, char delimiter = DelimitedText.DefaultDelimiter)
        {
            var rows = DelimitedText.Read(text, delimiter);
            var sheet = new Sheet();

            var columns = 0;

            for (var row = 0; row < rows.Count && row < sheet.RowCount; row++)
            {
                var fields = rows[row];
                columns = Math.Max(columns, fields.Count);

                for (var column = 0; column < fields.Count && column < sheet.ColumnCount; column++)
                    sheet.SetText(row, column, fields[column]);
            }

            // Kept rather than normalized, so that opening a file and saving it back untouched produces the same
            // bytes it arrived as.
            sheet.NewLine = text != null && text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

            ApplyBannerMerges(sheet, rows, columns);
            sheet.MarkSaved();

            return sheet;
        }

        /// <summary>
        ///     Merges the rows that are obviously headings.
        ///     <para>
        ///         <b>A comma separated file cannot say that a cell is merged</b>, there being nowhere in the format
        ///         to put it, so the rule is stated instead: a row with something in its first cell and nothing in
        ///         any other is a banner, and is drawn across the whole width. That is exactly the shape a title or
        ///         a line of instructions has, and nothing else in a table looks like it.
        ///     </para>
        ///     <para>
        ///         It applies at load, and merging by hand from the menu afterwards is unaffected. A rule inferred
        ///         from the data is worth having only when it is written down, which is what this comment is for.
        ///     </para>
        /// </summary>
        /// <param name="sheet">The sheet being built.</param>
        /// <param name="rows">The rows as they were read.</param>
        /// <param name="columns">How wide the widest row was.</param>
        private static void ApplyBannerMerges(Sheet sheet, IReadOnlyList<IReadOnlyList<string>> rows, int columns)
        {
            if (columns < 2)
                return;

            for (var row = 0; row < rows.Count && row < sheet.RowCount; row++)
            {
                var fields = rows[row];

                if (fields.Count == 0 || string.IsNullOrEmpty(fields[0]))
                    continue;

                var alone = true;

                for (var column = 1; column < fields.Count; column++)
                {
                    if (string.IsNullOrEmpty(fields[column]))
                        continue;

                    alone = false;
                    break;
                }

                if (alone)
                    sheet.Merge(row, 0, columns);
            }
        }

        /// <summary>
        ///     What separates the fields of a file, taken from its name. A file called .tsv really is separated by
        ///     tabs, and reading it with commas would give one very wide column.
        /// </summary>
        /// <param name="path">The file's name.</param>
        /// <returns>The delimiter.</returns>
        private static char DelimiterFor(string path)
        {
            return string.Equals(Path.GetExtension(path), ".tsv", StringComparison.OrdinalIgnoreCase)
                ? '\t'
                : DelimitedText.DefaultDelimiter;
        }
    }
}
