// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

namespace WolfCurses.Controls
{
    /// <summary>
    ///     What the <see cref="FileDialog" /> lets the user pick.
    /// </summary>
    public enum FileDialogModeEnum
    {
        /// <summary>
        ///     Navigate drives and folders and choose a single file (optionally restricted to certain extensions).
        /// </summary>
        OpenFile = 0,

        /// <summary>
        ///     Navigate drives and folders and choose a folder. Files are not shown; the user browses to the folder
        ///     they want and confirms it with the "select this folder" command.
        /// </summary>
        SelectFolder = 1
    }
}
