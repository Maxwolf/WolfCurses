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

            // Remember the probe's answer before anything can override it, then build the header — whose top line names
            // the render type the "Force render type" item last set (renderer plus color mode, for every demo at once).
            DemoImages.CaptureAutoDetectedRenderer();
            RefreshMenuHeader();

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
            AddCommand(ForceRenderType, ExampleCommandsEnum.ForceRenderType);
            AddCommand(ShowAnimatedGif, ExampleCommandsEnum.ShowAnimatedGif);
            AddCommand(ShowSpriteTest, ExampleCommandsEnum.SpriteTestBasic);
            AddCommand(ShowAdvancedSpriteTest, ExampleCommandsEnum.SpriteTestAdvanced);
            AddCommand(ShowCollisionSpriteTest, ExampleCommandsEnum.SpriteTestCollision);
            AddCommand(ShowImageErrors, ExampleCommandsEnum.ImageErrorDemo);
            AddCommand(ShowPrideFlags, ExampleCommandsEnum.PrideFlags);
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

        /// <summary>
        ///     ESC backs out of whichever example is showing and returns to the main menu. Every demo in this app is a
        ///     form attached to this one window, so intercepting the key here — before <see cref="Window{TCommands,TData}" />
        ///     forwards it down to the form — backs all of them out in a single place, no per-demo handling.
        ///     <para>
        ///         Only when a form is up: with the menu itself showing there is nothing to back out of (you are
        ///         already at the top), so ESC is handed to the base, which ignores it. Every other key is passed
        ///         straight through, so the arrow-driven demos (the sprite tests) still steer exactly as before.
        ///         The library's own modal controls — <see cref="SelectList" />, <see cref="MessageBox" />,
        ///         <see cref="TextInputDialog" />, <see cref="FileDialog" /> — are separate windows this override never
        ///         sees; they are dismissed by their own on-screen cancel affordance ([C]ancel, or a blank line).
        ///     </para>
        /// </summary>
        /// <param name="key">The key that was pressed.</param>
        public override void OnKeyPressed(ConsoleKey key)
        {
            if (key == ConsoleKey.Escape && CurrentForm != null)
            {
                ClearForm();
                return;
            }

            base.OnKeyPressed(key);
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

        private void ShowImageErrors()
        {
            SetForm(typeof (ImageErrorDialog));
        }

        private void ShowPrideFlags()
        {
            SetForm(typeof (PrideFlagDialog));
        }

        /// <summary>
        ///     Overrides the render method every image demo draws with, then snaps back to the menu — the whole point
        ///     being to change it from the auto-detected default to anything the library can produce and then open any
        ///     demo (slideshow, GIF, sprites, compositing, the logo) to see the result. The library already asked this
        ///     terminal what it supports at start-up and installed the answer as <see cref="ImageRenderers.Default" />,
        ///     which the <c>Auto</c> choice restores.
        ///     <para>
        ///         The choices are graded best to worst, and each is a genuinely different code path rather than the
        ///         same picture with a knob turned: real pixels (kitty, then sixel), then two pixels per character cell
        ///         with steadily less color to spend, ending at the text-only shaded ASCII that emits no color escapes
        ///         whatsoever. Forcing a type the terminal does not speak is the point of having them all here — a
        ///         protocol that goes unanswered spills escape-sequence garbage across the screen, which is exactly
        ///         what detection exists to prevent and worth seeing once.
        ///     </para>
        ///     <para>
        ///         It reaches every demo — images, colored widgets and styled prose alike — through two globals and
        ///         nothing else: the picked renderer becomes <see cref="ImageRenderers.Default" /> (which every image
        ///         draws through, directly or via <see cref="RendererSwitch" />), and the color mode becomes
        ///         <see cref="AnsiConsole.ForcedColorMode" />, which everything left at
        ///         <see cref="AnsiColorModeEnum.Auto" /> resolves through <see cref="AnsiConsole.DetectColorMode" /> —
        ///         so the pride flags and the graph dashboard grey out with the images. The real-pixel renderers
        ///         (kitty, sixel) carry their own color, so those choices force no color mode.
        ///     </para>
        /// </summary>
        private void ForceRenderType()
        {
            // Remember the probe's untouched answer so the "Auto" choice can restore it (idempotent; also captured at
            // window creation).
            DemoImages.CaptureAutoDetectedRenderer();

            var detected = DescribeRenderer(DemoImages.AutoDetectedRenderer);
            var choices = new[]
            {
                $"Auto - what this terminal answered: {detected}",
                "Kitty - real pixels, 24-bit color and transparency",
                "Sixel - real pixels, quantized to a 256-color palette",
                "Half blocks - two pixels per cell, 24-bit color",
                "Half blocks - two pixels per cell, 256-color palette",
                "Half blocks - two pixels per cell, grayscale",
                "Text only - shaded ASCII, no color escapes at all"
            };

            SelectList.Choose(
                SimUnit,
                "Force render type - which method should every demo draw with?",
                choices,
                index =>
                {
                    // The whole effect of the choice, and it reaches widgets and prose as much as images: the picked
                    // renderer becomes the global default, and the color mode becomes AnsiConsole.ForcedColorMode,
                    // which every Auto consumer resolves through. A real-pixel choice (kitty/sixel) forces no color
                    // mode — they carry their own — so widgets stay on the detected one; "Auto" clears both.
                    (ImageRenderers.Default, AnsiConsole.ForcedColorMode) = index switch
                    {
                        1 => ((IImageRenderer) new KittyImageRenderer(), (AnsiColorModeEnum?) null),
                        2 => (new SixelImageRenderer(), null),
                        3 => (new HalfBlockImageRenderer(), AnsiColorModeEnum.TrueColor),
                        4 => (new HalfBlockImageRenderer(), AnsiColorModeEnum.Palette256),
                        5 => (new HalfBlockImageRenderer(), AnsiColorModeEnum.Grayscale),
                        6 => (new HalfBlockImageRenderer(), AnsiColorModeEnum.None),
                        _ => (DemoImages.AutoDetectedRenderer, null)
                    };

                    // Reflect it on the menu header, then let the SelectList's own close snap us back there.
                    RefreshMenuHeader();
                },
                () => { });
        }

        /// <summary>
        ///     Rebuilds the menu header, whose top line names the render method every demo will draw with, so forcing
        ///     one shows its effect the moment you snap back to the menu — before you have even opened a demo.
        /// </summary>
        private void RefreshMenuHeader()
        {
            var headerText = new StringBuilder();
            headerText.Append(
                $"{Environment.NewLine}Example Console Application{Environment.NewLine}{Environment.NewLine}");
            headerText.AppendLine("Render type: " + DescribeCurrentRenderType());
            headerText.AppendLine("Example UserData: " + UserData.ExampleUserData);
            headerText.Append("You may (arrow keys + ENTER, or type a number):");
            MenuHeader = headerText.ToString();
        }

        /// <summary>
        ///     Describes the render method the demos will use right now, and whether it is one the user forced or the
        ///     probe's own answer. Reads the live <see cref="ImageRenderers.Default" /> and
        ///     <see cref="AnsiConsole.ForcedColorMode" /> rather than any stored copy, so it is always current.
        /// </summary>
        private static string DescribeCurrentRenderType()
        {
            var renderer = DescribeRenderer(ImageRenderers.Default);
            var rendererForced = !ReferenceEquals(ImageRenderers.Default, DemoImages.AutoDetectedRenderer);
            var forcedMode = AnsiConsole.ForcedColorMode;
            var modeForced = forcedMode.HasValue && forcedMode.Value != AnsiColorModeEnum.Auto;
            if (!rendererForced && !modeForced)
                return $"{renderer} (auto-detected)";

            var colorMode = forcedMode switch
            {
                AnsiColorModeEnum.TrueColor => ", true color",
                AnsiColorModeEnum.Palette256 => ", 256 colors",
                AnsiColorModeEnum.Grayscale => ", grayscale",
                AnsiColorModeEnum.None => ", text only",
                _ => string.Empty
            };
            return $"{renderer}{colorMode} (forced)";
        }

        /// <summary>
        ///     Names a renderer in a few words, for the <c>Auto</c> choice's label and the menu footer.
        ///     <para>
        ///         Asks the object what it is rather than re-calling <see cref="AnsiConsole.DetectGraphicsProtocol" />,
        ///         which would be a different question with a possibly different answer: that one reads environment
        ///         variables only, and on Windows Terminal it deliberately reports None because no version is published
        ///         to key off. The probe asked the terminal itself and may well have found sixel; reporting the guess
        ///         while the probe's answer does the drawing would misattribute exactly what this screen exists to show.
        ///     </para>
        /// </summary>
        /// <param name="renderer">The renderer to name.</param>
        private static string DescribeRenderer(IImageRenderer renderer)
        {
            return renderer switch
            {
                KittyImageRenderer => "kitty (real pixels)",
                SixelImageRenderer => "sixel (real pixels)",
                HalfBlockImageRenderer => "half blocks",
                var other => other?.GetType().Name ?? "none"
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