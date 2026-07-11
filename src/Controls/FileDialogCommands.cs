// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

namespace WolfCurses.Controls
{
    /// <summary>
    ///     Menu command enumeration required by the <see cref="WolfCurses.Window.Window{TCommands,TData}" /> base of
    ///     <see cref="FileDialogWindow" />. The file dialog does not use the numbered enum menu — it renders its own
    ///     dynamic list of drives, folders, and files through <see cref="FileDialogForm" /> — so this has no real
    ///     commands.
    /// </summary>
    public enum FileDialogCommands
    {
        /// <summary>Unused placeholder; the dialog never registers this as a menu choice.</summary>
        None = 0
    }
}
