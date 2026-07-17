// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 01/07/2016@7:31 PM

using System;
using System.Text;
using WolfCurses.Controls;
using WolfCurses.Example.CustomInput;
using WolfCurses.Example.Demos;
using WolfCurses.Graphics;
using WolfCurses.Example.Prompt;
using WolfCurses.Example.Question;
using WolfCurses.Window;

namespace WolfCurses.Example
{
    /// <summary>
    ///     Example window implementation that is attached to wolf curses list of active windows during runtime.
    /// </summary>
    public sealed class ExampleWindow : Window<ExampleCommandsEnum, ExampleWindowInfo>
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

            AddCommand(TextPrompt, ExampleCommandsEnum.TextPrompt);
            AddCommand(YesNoPrompt, ExampleCommandsEnum.YesNoPrompt);
            AddCommand(CustomPrompt, ExampleCommandsEnum.CustomPrompt);
            AddCommand(ShowSlideshow, ExampleCommandsEnum.Slideshow);
            AddCommand(ShowCompositeSlideshow, ExampleCommandsEnum.CompositeSlideshow);
            AddCommand(OpenImageFile, ExampleCommandsEnum.OpenImageFile);
            AddCommand(SelectFolder, ExampleCommandsEnum.SelectFolder);
            AddCommand(ShowProgressAndGraphs, ExampleCommandsEnum.ProgressAndGraphs);
            AddCommand(SelectFromList, ExampleCommandsEnum.SelectFromList);
            AddCommand(MultiSelectList, ExampleCommandsEnum.MultiSelectList);
            AddCommand(ShowMessageBox, ExampleCommandsEnum.MessageBoxDemo);
            AddCommand(TextInput, ExampleCommandsEnum.TextInputDemo);
            AddCommand(PasswordInput, ExampleCommandsEnum.PasswordDemo);
            AddCommand(ShowTruePixelSlideshow, ExampleCommandsEnum.TruePixelSlideshow);
            AddCommand(CloseSimulation, ExampleCommandsEnum.CloseSimulation);

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

        /// <summary>
        ///     Runs the slideshow through a renderer of the user's choosing. The protocol is picked rather than detected
        ///     on purpose: asking a terminal what it supports means reading back an escape-sequence reply, which would
        ///     race the simulation's own input handling — so a terminal that does not understand sixel or kitty prints
        ///     the sequence as garbage instead of degrading. Letting the user choose keeps that an informed decision.
        /// </summary>
        private void ShowTruePixelSlideshow()
        {
            var choices = new[]
            {
                "Half blocks - works in any color terminal",
                "Sixel - xterm, foot, WezTerm, mlterm, Windows Terminal 1.22+",
                "Kitty - kitty, WezTerm, Ghostty, Konsole"
            };

            SelectList.Choose(
                SimUnit,
                "Draw the slideshow with which renderer?",
                choices,
                index =>
                {
                    (UserData.SelectedImageRenderer, UserData.SelectedImageRendererName) = index switch
                    {
                        1 => ((IImageRenderer) new SixelImageRenderer(), "Sixel slideshow"),
                        2 => (new KittyImageRenderer(), "Kitty slideshow"),
                        _ => (new HalfBlockImageRenderer(), "Half-block slideshow")
                    };

                    SetForm(typeof (TruePixelSlideshowDialog));
                },
                () => ShowResult("Slideshow cancelled."));
        }

        private void SelectFromList()
        {
            var colors = new[] {"Crimson", "Emerald", "Sapphire", "Goldenrod", "Violet", "Amber", "Teal"};

            SelectList.Choose(
                SimUnit,
                "Pick a color",
                colors,
                index => ShowResult($"You chose: {colors[index]}"),
                () => ShowResult("Selection cancelled."));
        }

        private void MultiSelectList()
        {
            var toppings = new[]
                {"Cheese", "Pepperoni", "Mushroom", "Onion", "Olives", "Bacon", "Pineapple", "Peppers"};

            // Open with the user's last picks already checked (initiallySelected) so they edit from the current state
            // rather than starting blank; the confirmed set replaces it, so reopening reflects the change.
            SelectList.ChooseMany(
                SimUnit,
                "Pick your toppings",
                toppings,
                topping => topping,
                chosen =>
                {
                    UserData.SelectedToppings.Clear();
                    UserData.SelectedToppings.AddRange(chosen);
                    ShowResult(chosen.Count == 0
                        ? "No toppings selected."
                        : "Toppings: " + string.Join(", ", chosen));
                },
                () => ShowResult("Selection cancelled."),
                initiallySelected: UserData.SelectedToppings);
        }

        private void ShowMessageBox()
        {
            MessageBox.Show(
                SimUnit,
                "Enable hard mode?" + Environment.NewLine + "This makes the journey much tougher.",
                MessageBoxButtonsEnum.YesNoCancel,
                result => ShowResult($"You picked: {result}"));
        }

        private void TextInput()
        {
            TextInputDialog.Prompt(
                SimUnit,
                "What is your name?",
                name => ShowResult($"Hello, {name}!"),
                () => ShowResult("No name entered."),
                defaultValue: string.IsNullOrEmpty(UserData.PlayerName) ? "Traveler" : UserData.PlayerName,
                validator: value => value.Length < 2 ? "Name must be at least 2 characters." : null);
        }

        private void PasswordInput()
        {
            TextInputDialog.Prompt(
                SimUnit,
                "Choose a passphrase (at least 4 characters):",
                _ => ShowResult("Passphrase accepted."),
                () => ShowResult("No passphrase entered."),
                masked: true,
                validator: value => value.Length < 4 ? "Passphrase must be at least 4 characters." : null);
        }

        /// <summary>Stores a demo's outcome and switches to the shared result dialog to show it.</summary>
        private void ShowResult(string result)
        {
            UserData.LastResult = result;
            SetForm(typeof (ControlResultDialog));
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