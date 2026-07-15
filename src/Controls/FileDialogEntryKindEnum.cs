// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

namespace WolfCurses.Controls
{
    /// <summary>
    ///     The kind of thing a <see cref="FileDialogEntry" /> points at, which decides what happens when the user
    ///     selects it.
    /// </summary>
    public enum FileDialogEntryKindEnum
    {
        /// <summary>The ".." entry that moves up to the containing folder (or the drive list at a drive root).</summary>
        ParentDirectory = 0,

        /// <summary>A drive root (shown in the drive-selection view).</summary>
        Drive = 1,

        /// <summary>A sub-folder that can be navigated into.</summary>
        Directory = 2,

        /// <summary>A file that can be chosen (open-file mode only).</summary>
        File = 3
    }
}
