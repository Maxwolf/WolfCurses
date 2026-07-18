// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using WolfCurses.Window;

namespace WolfCurses.Controls
{
    /// <summary>
    ///     The window that hosts the file/folder browser. It carries no numbered menu of its own; on creation it
    ///     attaches <see cref="FileDialogForm" />, which draws the dynamic listing and handles navigation. Push it with
    ///     <see cref="FileDialog.OpenFile" /> / <see cref="FileDialog.SelectFolder" /> rather than adding it directly.
    ///     The default <see cref="SimulationApp.AllowedWindows" /> discovers it automatically; an app that overrides
    ///     <c>AllowedWindows</c> must include it in the list.
    /// </summary>
    public sealed class FileDialogWindow : Window<FileDialogCommandsEnum, FileDialogData>
    {
        /// <summary>Initializes a new instance of the <see cref="FileDialogWindow" /> class.</summary>
        /// <param name="simUnit">Core simulation which is controlling the window.</param>
        // ReSharper disable once UnusedMember.Global
        public FileDialogWindow(SimulationApp simUnit) : base(simUnit)
        {
        }

        /// <inheritdoc />
        public override void OnWindowPostCreate()
        {
            base.OnWindowPostCreate();

            // The form does all of the rendering and input handling for the dialog.
            SetForm(typeof (FileDialogForm));
        }
    }
}
