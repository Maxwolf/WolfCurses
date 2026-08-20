// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/20/2026

using System;
using System.IO;

namespace WolfCurses.Apps.WordProcessor
{
    /// <summary>
    ///     Where the sample documents live and how they are read and written. No console anywhere in here, so the
    ///     awkward half of a word processor (the filesystem) can be driven from a test without a screen.
    ///     <para>
    ///         The folder is resolved from <see cref="AppContext.BaseDirectory" /> rather than the current working
    ///         directory, which is the one path property a single-file publish keeps honest. The release workflow
    ///         publishes each app as one executable beside its own asset folder for exactly this reason, and the
    ///         arcade resolves its chess artwork the same way.
    ///     </para>
    /// </summary>
    internal static class DocumentLibrary
    {
        /// <summary>The document the word processor opens on: short, famous, and a joke.</summary>
        public const string DefaultDocumentName = "rfc1149.txt";

        /// <summary>The folder the samples are copied into, beside the executable.</summary>
        public static string Folder => Path.Combine(AppContext.BaseDirectory, "documents");

        /// <summary>The full path of the document opened at start-up.</summary>
        public static string DefaultDocumentPath => Path.Combine(Folder, DefaultDocumentName);

        /// <summary>
        ///     Where an Open dialog should start. The samples folder when it is there, and the executable's own
        ///     folder when it is not, so a browser always opens somewhere that exists rather than throwing.
        /// </summary>
        public static string BrowseFolder => Directory.Exists(Folder) ? Folder : AppContext.BaseDirectory;

        /// <summary>
        ///     Reads a document, or returns null when it cannot be read for any reason.
        ///     <para>
        ///         Null rather than an exception, and caught broadly on purpose. This is a demonstration program
        ///         whose Open dialog can be pointed at any file on the machine, so "that is a directory", "that is
        ///         locked", "you may not read that" and "the drive went away" are all ordinary answers rather than
        ///         faults, and every one of them should put a message on the status line instead of a stack trace
        ///         across the interface. The library takes the same stance for images, where a bad file becomes a
        ///         checkerboard rather than a crash.
        ///     </para>
        /// </summary>
        /// <param name="path">The file to read.</param>
        /// <param name="error">What went wrong, when the read failed.</param>
        /// <returns>The document's text, or null.</returns>
        public static string TryLoad(string path, out string error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(path))
            {
                error = "No file name was given.";
                return null;
            }

            try
            {
                return File.ReadAllText(path);
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

        /// <summary>Writes a document, reporting failure the same way <see cref="TryLoad" /> does.</summary>
        /// <param name="path">The file to write.</param>
        /// <param name="text">The document's text.</param>
        /// <param name="error">What went wrong, when the write failed.</param>
        /// <returns>TRUE when the file was written.</returns>
        public static bool TrySave(string path, string text, out string error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(path))
            {
                error = "No file name was given.";
                return false;
            }

            try
            {
                // The buffer already joined its lines with whatever ending the file arrived with, so this writes the
                // bytes back rather than letting the framework impose the platform's ending on every line.
                File.WriteAllText(path, text);
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
    }
}
