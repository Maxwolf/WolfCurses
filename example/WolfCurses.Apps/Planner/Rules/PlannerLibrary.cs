// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;
using System.IO;
using WolfCurses.Documents;

namespace WolfCurses.Apps.Planner
{
    /// <summary>
    ///     Where the sample planner lives and how one is read and written. No console anywhere in here, exactly as
    ///     the word processor's document library and the spreadsheet's sheet library are.
    /// </summary>
    internal static class PlannerLibrary
    {
        /// <summary>The planner opened at start-up.</summary>
        public const string DefaultPlannerName = "planner.csv";

        /// <summary>The folder the samples are copied into, beside the executable.</summary>
        public static string Folder => Path.Combine(AppContext.BaseDirectory, "planner");

        /// <summary>The full path of the planner opened at start-up.</summary>
        public static string DefaultPlannerPath => Path.Combine(Folder, DefaultPlannerName);

        /// <summary>Where an Open dialog should start, falling back to somewhere that certainly exists.</summary>
        public static string BrowseFolder => Directory.Exists(Folder) ? Folder : AppContext.BaseDirectory;

        /// <summary>The file extensions the Open dialog offers.</summary>
        public static string[] Extensions { get; } = {".csv", ".txt"};

        /// <summary>
        ///     Reads a planner, or returns null when it cannot be read for any reason. Null rather than an
        ///     exception and caught broadly, for the reason every file reader in this suite is: an Open dialog can
        ///     be pointed at anything on the machine, and "that is a directory" belongs on the status line.
        /// </summary>
        /// <param name="path">The file to read.</param>
        /// <param name="error">What went wrong, when the read failed.</param>
        /// <returns>The planner, or null.</returns>
        public static PlannerDiary TryLoad(string path, out string error)
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

        /// <summary>Writes a planner, reporting failure the same way <see cref="TryLoad" /> does.</summary>
        /// <param name="diary">The planner to write.</param>
        /// <param name="path">Where to write it.</param>
        /// <param name="error">What went wrong, when the write failed.</param>
        /// <returns>TRUE when the file was written.</returns>
        public static bool TrySave(PlannerDiary diary, string path, out string error)
        {
            error = null;

            if (diary == null || string.IsNullOrWhiteSpace(path))
            {
                error = "No file name was given.";
                return false;
            }

            try
            {
                var rows = new List<string[]> {new[] {"Date", "Time", "What"}};

                foreach (var entry in diary.Events)
                    rows.Add(new[] {entry.DateText(), entry.Time, entry.Title});

                File.WriteAllText(path, DelimitedText.Write(rows, DelimitedText.DefaultDelimiter, diary.NewLine));
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
        ///     Turns the file's text into a planner. Separate from reading a file so the interesting half can be
        ///     tested without one.
        ///     <para>
        ///         <b>A row whose date does not parse is skipped rather than refused.</b> The header row is exactly
        ///         such a row, which is the neatest reason to be lenient: the file gets a line saying what its
        ///         columns are, and nothing has to know that line is special.
        ///     </para>
        /// </summary>
        /// <param name="text">The file's contents.</param>
        /// <returns>The planner.</returns>
        public static PlannerDiary Parse(string text)
        {
            var diary = new PlannerDiary();

            foreach (var row in DelimitedText.Read(text))
            {
                if (row.Count < 2)
                    continue;

                if (!PlannerEvent.TryParseDate(row[0], out var year, out var month, out var day))
                    continue;

                var time = row.Count > 1 ? row[1] : string.Empty;
                var title = row.Count > 2 ? row[2] : string.Empty;

                diary.Add(new PlannerEvent(year, month, day, time, title));
            }

            diary.NewLine = text != null && text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            diary.MarkSaved();

            return diary;
        }
    }
}
