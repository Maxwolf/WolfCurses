// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System;
using System.Collections.Generic;
using System.IO;
using WolfCurses.Window;

namespace WolfCurses.Controls
{
    /// <summary>
    ///     Holds the configuration and current navigation state of a <see cref="FileDialog" />. It is the
    ///     <see cref="WindowData" /> of the <see cref="FileDialogWindow" />, which is how <see cref="FileDialog" />
    ///     hands the start folder, filter, and result callbacks to the window after creating it.
    /// </summary>
    public sealed class FileDialogData : WindowData
    {
        /// <summary>Whether the dialog is picking a file or a folder.</summary>
        public FileDialogMode Mode { get; private set; }

        /// <summary>Normalized extension filter (open-file mode); empty means all files.</summary>
        public string[] Extensions { get; private set; } = Array.Empty<string>();

        /// <summary>Invoked with the chosen file path (open-file) or folder path (folder mode) when the user confirms.</summary>
        public Action<string> OnPathSelected { get; private set; }

        /// <summary>Invoked if the user cancels the dialog. Optional.</summary>
        public Action OnCancelled { get; private set; }

        /// <summary>The folder currently being shown, or null when showing the drive list.</summary>
        public string CurrentDirectory { get; private set; }

        /// <summary>The entries of the current view (parent, folders, and — in open-file mode — files).</summary>
        public IReadOnlyList<FileDialogEntry> Entries { get; private set; } = Array.Empty<FileDialogEntry>();

        /// <summary>Zero-based index of the page currently displayed.</summary>
        public int PageIndex { get; set; }

        /// <summary>A message shown when a folder could not be opened (e.g. access denied); null when all is well.</summary>
        public string ErrorMessage { get; private set; }

        /// <summary>True once <see cref="Initialize" /> has run and the dialog has something to show.</summary>
        public bool Initialized { get; private set; }

        /// <summary>
        ///     Configures the dialog and navigates to its starting view. If the start folder is missing or unreadable,
        ///     the drive list is shown instead.
        /// </summary>
        public void Initialize(FileDialogMode mode, string startDirectory, IEnumerable<string> extensions,
            Action<string> onPathSelected, Action onCancelled)
        {
            Mode = mode;
            Extensions = FileDialogListing.NormalizeExtensions(extensions);
            OnPathSelected = onPathSelected;
            OnCancelled = onCancelled;
            Initialized = true;

            if (!string.IsNullOrWhiteSpace(startDirectory) && Directory.Exists(startDirectory))
            {
                LoadDirectory(startDirectory);

                // If the start folder exists but could not be listed (e.g. access denied), fall back to the drive
                // list as documented, keeping the error message so the user sees why.
                if (CurrentDirectory == null)
                {
                    var error = ErrorMessage;
                    LoadDrives();
                    ErrorMessage = error;
                }
            }
            else
            {
                LoadDrives();
            }
        }

        /// <summary>
        ///     Navigates to a folder, rebuilding the entry list. On failure the current view is kept and
        ///     <see cref="ErrorMessage" /> is set.
        /// </summary>
        public void LoadDirectory(string directory)
        {
            try
            {
                // Strip any trailing separator so the ".." entry and GoUp resolve to the real parent — without this,
                // Directory.GetParent("C:\\X\\") returns "C:\\X" (the same folder) and navigating up is a no-op on the
                // first press. TrimEndingDirectorySeparator leaves a bare root like "C:\\" alone.
                var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
                var entries = FileDialogListing.BuildEntries(full, Extensions, Mode == FileDialogMode.OpenFile);

                CurrentDirectory = full;
                Entries = entries;
                PageIndex = 0;
                ErrorMessage = null;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Cannot open that folder ({ex.GetType().Name}: {ex.Message}).";
            }
        }

        /// <summary>Switches to the drive-selection view.</summary>
        public void LoadDrives()
        {
            CurrentDirectory = null;
            Entries = FileDialogListing.BuildDriveEntries();
            PageIndex = 0;
            ErrorMessage = null;
        }

        /// <summary>
        ///     Moves up to the parent folder, or to the drive list when already at a drive root. Does nothing on the
        ///     drive list itself.
        /// </summary>
        public void GoUp()
        {
            if (CurrentDirectory == null)
                return;

            var parent = Directory.GetParent(CurrentDirectory);
            if (parent != null)
                LoadDirectory(parent.FullName);
            else
                LoadDrives();
        }
    }
}
