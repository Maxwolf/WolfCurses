// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

namespace WolfCurses.Controls
{
    /// <summary>
    ///     One selectable row in the <see cref="FileDialog" /> — a drive, folder, the parent folder, or a file.
    /// </summary>
    public readonly struct FileDialogEntry
    {
        /// <summary>Initializes a new instance of the <see cref="FileDialogEntry" /> struct.</summary>
        /// <param name="name">The text shown to the user for this entry.</param>
        /// <param name="fullPath">The full path this entry points at, or null for the "up to drives" parent entry.</param>
        /// <param name="kind">What sort of entry this is.</param>
        public FileDialogEntry(string name, string fullPath, FileDialogEntryKindEnum kind)
        {
            Name = name;
            FullPath = fullPath;
            Kind = kind;
        }

        /// <summary>The text shown to the user for this entry.</summary>
        public string Name { get; }

        /// <summary>The full path this entry points at; null only for a parent entry at a drive root (go to drives).</summary>
        public string FullPath { get; }

        /// <summary>What sort of entry this is.</summary>
        public FileDialogEntryKindEnum Kind { get; }

        /// <summary>True when selecting this entry navigates (parent, drive, folder) rather than picking a file.</summary>
        public bool IsNavigable => Kind != FileDialogEntryKindEnum.File;
    }
}
