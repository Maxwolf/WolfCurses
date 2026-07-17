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
            AddCommand(ForceSlideshowRenderType, ExampleCommandsEnum.ForceRenderType);
            AddCommand(ShowAnimatedGif, ExampleCommandsEnum.ShowAnimatedGif);
            AddCommand(ShowSpriteTest, ExampleCommandsEnum.SpriteTestBasic);
            AddCommand(ShowAdvancedSpriteTest, ExampleCommandsEnum.SpriteTestAdvanced);
            AddCommand(ShowCollisionSpriteTest, ExampleCommandsEnum.SpriteTestCollision);
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

        private void ShowAnimatedGif()
        {
            SetForm(typeof (AnimatedGifDialog));
        }

        private void ShowSpriteTest()
        {
            SetForm(typeof (SpriteTestDialog));
        }

        private void ShowAdvancedSpriteTest()
        {
            SetForm(typeof (SpriteTestAdvancedDialog));
        }

        private void ShowCollisionSpriteTest()
        {
            SetForm(typeof (SpriteTestCollisionDialog));
        }

        /// <summary>
        ///     Runs the slideshow through a render type the user forces, so the one detection picked can be compared by
        ///     eye against every other rung of the ladder. Program.Main already asked this terminal what it supports and
        ///     installed the answer as <see cref="ImageRenderers.Default" />, which is the first choice here.
        ///     <para>
        ///         The rest are graded best to worst, and each is a genuinely different code path rather than the same
        ///         picture with a knob turned: real pixels (kitty, then sixel), then two pixels per character cell with
        ///         steadily less color to spend, ending at the ASCII fallback that emits no color escapes whatsoever.
        ///         Forcing a type the terminal does not speak is the point of having them all here — a protocol that
        ///         goes unanswered spills escape-sequence garbage across the screen, which is exactly what detection
        ///         exists to prevent and worth seeing once.
        ///     </para>
        /// </summary>
        private void ForceSlideshowRenderType()
        {
            var detected = DescribeDefaultRenderer();
            var choices = new[]
            {
                $"Auto - what this terminal answered: {detected}",
                "Kitty - real pixels, 24-bit color and transparency",
                "Sixel - real pixels, quantized to a 256-color palette",
                "Half blocks - two pixels per cell, 24-bit color",
                "Half blocks - two pixels per cell, 256-color palette",
                "Half blocks - two pixels per cell, grayscale",
                "Fallback - shaded ASCII, no color escapes at all"
            };

            SelectList.Choose(
                SimUnit,
                "Force the slideshow to draw with which render type?",
                choices,
                index =>
                {
                    (UserData.SelectedImageRenderer, UserData.SelectedImageColorMode,
                        UserData.SelectedImageRendererName) = index switch
                    {
                        1 => ((IImageRenderer) new KittyImageRenderer(), AnsiColorModeEnum.Auto,
                            "Kitty: real pixels (forced)"),
                        2 => (new SixelImageRenderer(), AnsiColorModeEnum.Auto,
                            "Sixel: real pixels (forced)"),
                        3 => (new HalfBlockImageRenderer(), AnsiColorModeEnum.TrueColor,
                            "Half blocks: true color (forced)"),
                        4 => (new HalfBlockImageRenderer(), AnsiColorModeEnum.Palette256,
                            "Half blocks: 256 colors (forced)"),
                        5 => (new HalfBlockImageRenderer(), AnsiColorModeEnum.Grayscale,
                            "Half blocks: grayscale (forced)"),
                        6 => (new HalfBlockImageRenderer(), AnsiColorModeEnum.None,
                            "Fallback: shaded ASCII (forced)"),
                        _ => (ImageRenderers.Default, AnsiColorModeEnum.Auto,
                            $"Auto-detected render type: {detected}")
                    };

                    SetForm(typeof (ForcedRenderSlideshowDialog));
                },
                () => ShowResult("Slideshow cancelled."));
        }

        /// <summary>
        ///     Names the renderer that Program.Main's probe installed as <see cref="ImageRenderers.Default" />, so the
        ///     "Auto" choice reports what will actually draw.
        ///     <para>
        ///         Asking <see cref="AnsiConsole.DetectGraphicsProtocol" /> again here would be a different question
        ///         with a possibly different answer: that one reads environment variables only, and on Windows Terminal
        ///         it deliberately reports None because no version is published to key off. The probe asks the terminal
        ///         itself and may well have found sixel. Reporting the guess while the probe's answer does the drawing
        ///         would misattribute exactly what this screen exists to show.
        ///     </para>
        /// </summary>
        private static string DescribeDefaultRenderer()
        {
            return ImageRenderers.Default switch
            {
                KittyImageRenderer => "kitty (real pixels)",
                SixelImageRenderer => "sixel (real pixels)",
                HalfBlockImageRenderer => "half blocks",
                var other => other.GetType().Name
            };
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