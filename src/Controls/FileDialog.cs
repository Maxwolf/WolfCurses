// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System;
using System.Collections.Generic;

namespace WolfCurses.Controls
{
    /// <summary>
    ///     A ready-made file/folder browser control for WolfCurses applications. Call <see cref="OpenFile" /> to let
    ///     the user pick a file (optionally restricted to certain extensions) or <see cref="SelectFolder" /> to let
    ///     them pick a folder; the dialog pushes itself on top of the current screen, lets the user navigate drives and
    ///     folders, and invokes your callback with the chosen path (or your cancel callback) before closing itself.
    /// </summary>
    /// <remarks>
    ///     The dialog is a window shipped in the library, so the default <see cref="SimulationApp.AllowedWindows" />
    ///     discovers it (and its form) automatically; an application that overrides <c>AllowedWindows</c> must
    ///     include <c>typeof(FileDialogWindow)</c> in its list. From inside a
    ///     <see cref="WolfCurses.Window.Window{TCommands,TData}" /> the simulation is available as <c>SimUnit</c>.
    /// </remarks>
    /// <example>
    ///     <code>
    ///     FileDialog.OpenFile(SimUnit, startDirectory: "C:\\", extensions: new[] { ".jpg", ".png" },
    ///         onFileSelected: path => { /* do something with the file */ });
    ///     </code>
    /// </example>
    public static class FileDialog
    {
        /// <summary>
        ///     Opens the browser to let the user pick a single file.
        /// </summary>
        /// <param name="simulation">The running simulation (available as <c>SimUnit</c> inside a window).</param>
        /// <param name="startDirectory">Folder to start in; if missing or unreadable the drive list is shown.</param>
        /// <param name="extensions">Extensions to show (e.g. ".jpg", "png"); null or empty shows every file.</param>
        /// <param name="onFileSelected">Called with the full path of the chosen file. Required.</param>
        /// <param name="onCancelled">Optional; called if the user cancels.</param>
        public static void OpenFile(SimulationApp simulation, string startDirectory, IEnumerable<string> extensions,
            Action<string> onFileSelected, Action onCancelled = null)
        {
            Show(simulation, FileDialogModeEnum.OpenFile, startDirectory, extensions, onFileSelected, onCancelled);
        }

        /// <summary>
        ///     Opens the browser to let the user pick a folder (no files are shown; the user browses to a folder and
        ///     confirms it with the "select this folder" command).
        /// </summary>
        /// <param name="simulation">The running simulation (available as <c>SimUnit</c> inside a window).</param>
        /// <param name="startDirectory">Folder to start in; if missing or unreadable the drive list is shown.</param>
        /// <param name="onFolderSelected">Called with the full path of the chosen folder. Required.</param>
        /// <param name="onCancelled">Optional; called if the user cancels.</param>
        public static void SelectFolder(SimulationApp simulation, string startDirectory,
            Action<string> onFolderSelected, Action onCancelled = null)
        {
            Show(simulation, FileDialogModeEnum.SelectFolder, startDirectory, null, onFolderSelected, onCancelled);
        }

        private static void Show(SimulationApp simulation, FileDialogModeEnum mode, string startDirectory,
            IEnumerable<string> extensions, Action<string> onPathSelected, Action onCancelled)
        {
            if (simulation == null)
                throw new ArgumentNullException(nameof(simulation));
            if (onPathSelected == null)
                throw new ArgumentNullException(nameof(onPathSelected));

            // The dialog is a window, and the window factory can only create window types the app opted into.
            if (!IsAllowed(simulation))
                throw new InvalidOperationException(
                    "To use FileDialog, include typeof(WolfCurses.Controls.FileDialogWindow) in your " +
                    "SimulationApp.AllowedWindows override.");

            simulation.WindowManager.Add(typeof (FileDialogWindow));

            // Add normally makes the FileDialogWindow the fresh, focused window. But windows are single-instance per
            // type, so if one is already open (Initialized) or is mid-close (ShouldRemoveMode, e.g. opening a second
            // dialog from within the first's callback) Add re-activates that existing/doomed window instead. Detect
            // that and fail loudly rather than silently reconfiguring a window and dropping the first callback.
            var focused = simulation.WindowManager.FocusedWindow;
            if (focused is not FileDialogWindow window || window.ShouldRemoveMode ||
                focused.UserData is not FileDialogData data || data.Initialized)
                throw new InvalidOperationException(
                    "A file dialog is already open or closing. Open only one at a time and wait for its callback " +
                    "before opening another.");

            data.Initialize(mode, startDirectory, extensions, onPathSelected, onCancelled);
        }

        private static bool IsAllowed(SimulationApp simulation)
        {
            if (simulation.AllowedWindows == null)
                return false;

            foreach (var window in simulation.AllowedWindows)
                if (window == typeof (FileDialogWindow))
                    return true;

            return false;
        }
    }
}
