// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System;
using WolfCurses.Window;
using WolfCurses.Window.Form;
using WolfCurses.Window.Form.Input;

namespace WolfCurses.Example.Demos
{
    /// <summary>
    ///     Shown after the folder browser picks a folder: displays the chosen folder path. Pressing ENTER returns to
    ///     the menu.
    /// </summary>
    [ParentWindow(typeof (ExampleWindow))]
    public sealed class SelectedFolderDialog : InputForm<ExampleWindowInfo>
    {
        /// <summary>Initializes a new instance of the <see cref="SelectedFolderDialog" /> class.</summary>
        /// <param name="window">The parent window.</param>
        // ReSharper disable once UnusedMember.Global
        public SelectedFolderDialog(IWindow window) : base(window)
        {
        }

        /// <inheritdoc />
        protected override string OnDialogPrompt()
        {
            ParentWindow.PromptText = string.Empty;
            return $"{Environment.NewLine}You selected the folder:{Environment.NewLine}{Environment.NewLine}" +
                   $"{UserData.SelectedPath}{Environment.NewLine}";
        }

        /// <inheritdoc />
        protected override void OnDialogResponse(DialogResponseEnum reponse)
        {
            ClearForm();
        }
    }
}
