// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System;
using System.IO;
using System.Text;
using WolfCurses.Graphics;
using WolfCurses.Window;
using WolfCurses.Window.Form;
using WolfCurses.Window.Form.Input;

namespace WolfCurses.Example.Demos
{
    /// <summary>
    ///     Shown after the file browser picks an image: displays the chosen image with ANSI graphics. This ties the
    ///     file dialog and the ANSI image feature together. Pressing ENTER returns to the menu.
    /// </summary>
    [ParentWindow(typeof (ExampleWindow))]
    public sealed class SelectedImageDialog : InputForm<ExampleWindowInfo>
    {
        /// <summary>Initializes a new instance of the <see cref="SelectedImageDialog" /> class.</summary>
        /// <param name="window">The parent window.</param>
        // ReSharper disable once UnusedMember.Global
        public SelectedImageDialog(IWindow window) : base(window)
        {
        }

        /// <inheritdoc />
        protected override string OnDialogPrompt()
        {
            ParentWindow.PromptText = string.Empty;

            var path = UserData.SelectedPath;
            var body = new StringBuilder();
            body.AppendLine();
            body.AppendLine($"You picked: {path}");
            body.AppendLine();

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                body.AppendLine("(No file was selected.)");
                return body.ToString();
            }

            try
            {
                body.AppendLine(AnsiImage.RenderFile(path, DemoImages.FitOptions()));
            }
            catch (Exception ex)
            {
                body.AppendLine($"(Could not display the image: {ex.Message})");
            }

            return body.ToString();
        }

        /// <inheritdoc />
        protected override void OnDialogResponse(DialogResponse reponse)
        {
            ClearForm();
        }
    }
}
