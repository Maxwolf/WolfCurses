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
            AddCommand(SelectFromList, ExampleCommands.SelectFromList);
            AddCommand(MultiSelectList, ExampleCommands.MultiSelectList);
            AddCommand(ShowMessageBox, ExampleCommands.MessageBoxDemo);
            AddCommand(TextInput, ExampleCommands.TextInputDemo);
            AddCommand(PasswordInput, ExampleCommands.PasswordDemo);
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
                MessageBoxButtons.YesNoCancel,
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