// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WolfCurses.Controls
{
    /// <summary>
    ///     The filesystem logic behind the <see cref="FileDialog" />, kept free of any UI so it can be unit tested on
    ///     its own: normalizing an extension filter, deciding whether a file matches it, and listing the entries of a
    ///     folder or of the machine's drives.
    /// </summary>
    public static class FileDialogListing
    {
        /// <summary>
        ///     Cleans up a caller-supplied set of extensions into a canonical form: lower-cased, each starting with a
        ///     dot, whitespace and blanks removed, duplicates collapsed. A caller can pass "jpg", ".JPG", or " .jpg "
        ///     and they all become ".jpg".
        /// </summary>
        /// <param name="extensions">Raw extensions, or null.</param>
        /// <returns>Normalized extensions; an empty array means "no filter" (all files).</returns>
        public static string[] NormalizeExtensions(IEnumerable<string> extensions)
        {
            if (extensions == null)
                return Array.Empty<string>();

            return extensions
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Select(e =>
                {
                    var trimmed = e.Trim().ToLowerInvariant();
                    return trimmed.StartsWith(".", StringComparison.Ordinal) ? trimmed : "." + trimmed;
                })
                .Distinct()
                .ToArray();
        }

        /// <summary>
        ///     Determines whether a file name passes the (already normalized) extension filter. An empty or null filter
        ///     matches everything.
        /// </summary>
        public static bool MatchesExtension(string fileName, string[] normalizedExtensions)
        {
            if (normalizedExtensions == null || normalizedExtensions.Length == 0)
                return true;

            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            foreach (var candidate in normalizedExtensions)
            {
                // A simple ".ext" matches the file's final extension; a compound one like ".tar.gz" (which
                // Path.GetExtension would only report as ".gz") is matched by suffix. The leading dot on every
                // normalized extension keeps the suffix test from matching a bare name ending in the same letters.
                if (ext == candidate || fileName.EndsWith(candidate, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        ///     Lists the entries of a folder: a ".." entry first, then sub-folders, then (when
        ///     <paramref name="includeFiles" /> is true) the files that pass the extension filter — each group sorted
        ///     alphabetically and case-insensitively. Access errors are allowed to propagate so the caller can report
        ///     them and stay put.
        /// </summary>
        /// <param name="directory">Absolute path of the folder to list.</param>
        /// <param name="normalizedExtensions">Extension filter from <see cref="NormalizeExtensions" />.</param>
        /// <param name="includeFiles">True to include files (open-file mode); false to show only folders (folder mode).</param>
        public static IReadOnlyList<FileDialogEntry> BuildEntries(string directory, string[] normalizedExtensions,
            bool includeFiles)
        {
            var entries = new List<FileDialogEntry>();

            // The parent entry always exists; at a drive root its path is null, which the dialog reads as "go back to
            // the drive list".
            var parent = Directory.GetParent(directory);
            entries.Add(new FileDialogEntry("..", parent?.FullName, FileDialogEntryKindEnum.ParentDirectory));

            foreach (var dir in Directory.GetDirectories(directory)
                         .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
                entries.Add(new FileDialogEntry(Path.GetFileName(dir), dir, FileDialogEntryKindEnum.Directory));

            if (includeFiles)
                foreach (var file in Directory.GetFiles(directory)
                             .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
                    if (MatchesExtension(file, normalizedExtensions))
                        entries.Add(new FileDialogEntry(Path.GetFileName(file), file, FileDialogEntryKindEnum.File));

            return entries;
        }

        /// <summary>
        ///     Lists the machine's ready drives as selectable entries (the drive-selection view).
        /// </summary>
        public static IReadOnlyList<FileDialogEntry> BuildDriveEntries()
        {
            var entries = new List<FileDialogEntry>();

            DriveInfo[] drives;
            try
            {
                drives = DriveInfo.GetDrives();
            }
            catch (IOException)
            {
                return entries;
            }
            catch (UnauthorizedAccessException)
            {
                return entries;
            }

            foreach (var drive in drives)
            {
                bool ready;
                try
                {
                    ready = drive.IsReady;
                }
                catch (Exception)
                {
                    // A drive that throws just querying readiness (e.g. a disconnected network mount) is skipped.
                    ready = false;
                }

                if (ready)
                    entries.Add(new FileDialogEntry(drive.Name, drive.RootDirectory.FullName,
                        FileDialogEntryKindEnum.Drive));
            }

            return entries;
        }
    }
}
