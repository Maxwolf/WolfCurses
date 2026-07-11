// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 01/07/2016@7:31 PM

using System;
using System.Text;
using WolfCurses.Controls;
using WolfCurses.Example.CustomInput;
using WolfCurses.Example.Demos;
using WolfCurses.Example.Prompt;
using WolfCurses.Example.Question;
using WolfCurses.Window;

namespace WolfCurses.Example
{
    /// <summary>
    ///     Example window implementation that is attached to wolf curses list of active windows during runtime.
    /// </summary>
    public sealed class ExampleWindow : Window<ExampleCommands, ExampleWindowInfo>
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="Window{TCommands,TData}" /> class.
        /// </summary>
        /// <param name="simUnit">Core simulation which is controlling the form factory.</param>
        // ReSharper disable once UnusedMember.Global
        public ExampleWindow(SimulationApp simUnit) : base(simUnit)
        {
        }

        /// <summary>
        ///     Called after the Windows has been added to list of modes and made active.
        /// </summary>
        public override void OnWindowPostCreate()
        {
            base.OnWindowPostCreate();

            var headerText = new StringBuilder();
            headerText.Append(
                $"{Environment.NewLine}Example Console Application{Environment.NewLine}{Environment.NewLine}");
            headerText.AppendLine("Example UserData: " + UserData.ExampleUserData);
            headerText.Append("You may:");
            MenuHeader = headerText.ToString();

            AddCommand(TextPrompt, ExampleCommands.TextPrompt);
            AddCommand(YesNoPrompt, ExampleCommands.YesNoPrompt);
            AddCommand(CustomPrompt, ExampleCommands.CustomPrompt);
            AddCommand(ShowSlideshow, ExampleCommands.Slideshow);
            AddCommand(ShowCompositeSlideshow, ExampleCommands.CompositeSlideshow);
            AddCommand(OpenImageFile, ExampleCommands.OpenImageFile);
            AddCommand(SelectFolder, ExampleCommands.SelectFolder);
            AddCommand(ShowProgressAndGraphs, ExampleCommands.ProgressAndGraphs);
            AddCommand(CloseSimulation, ExampleCommands.CloseSimulation);

            // Flex the WolfCurses logo as an ANSI graphics splash before the menu; pressing ENTER reveals it.
            SetForm(typeof (LogoSplashDialog));
        }

        /// <summary>
        ///     Restores the menu's own prompt whenever control returns to it. Each form sets a prompt suited to its
        ///     own context, so without this the last form's prompt would linger on the menu.
        /// </summary>
        protected override void OnFormChange()
        {
            base.OnFormChange();

            if (CurrentForm == null)
                PromptText = "What is your choice?";
        }

        private void CloseSimulation()
        {
            Program.Destroy();
        }

        private void ShowSlideshow()
        {
            SetForm(typeof (SlideshowDialog));
        }

        private void ShowCompositeSlideshow()
        {
            SetForm(typeof (CompositeSlideshowDialog));
        }

        private void ShowProgressAndGraphs()
        {
            SetForm(typeof (ProgressGraphsDialog));
        }

        private void OpenImageFile()
        {
            // Browse for an image file, then display the one the user picks with ANSI graphics. SimUnit is the
            // running simulation, exposed to windows so they can push the file dialog window.
            FileDialog.OpenFile(
                SimUnit,
                AppContext.BaseDirectory,
                new[] {".jpg", ".jpeg", ".png", ".bmp", ".gif"},
                path =>
                {
                    UserData.SelectedPath = path;
                    SetForm(typeof (SelectedImageDialog));
                });
        }

        private void SelectFolder()
        {
            // Browse for a folder, then show the one the user picks.
            FileDialog.SelectFolder(
                SimUnit,
                AppContext.BaseDirectory,
                path =>
                {
                    UserData.SelectedPath = path;
                    SetForm(typeof (SelectedFolderDialog));
                });
        }

        private void CustomPrompt()
        {
            SetForm(typeof (DialogCustomInput));
        }

        private void YesNoPrompt()
        {
            SetForm(typeof (QuestionDialog));
        }

        private void TextPrompt()
        {
            SetForm(typeof (DialogPrompt));
        }
    }
}