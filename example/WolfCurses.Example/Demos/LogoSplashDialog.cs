// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System.IO;
using System.Text;
using WolfCurses.Graphics;
using WolfCurses.Window;
using WolfCurses.Window.Form;
using WolfCurses.Window.Form.Input;

namespace WolfCurses.Example.Demos
{
    /// <summary>
    ///     Startup splash that shows the WolfCurses logo in the terminal with ANSI graphics before the main menu
    ///     appears. Pressing ENTER dismisses it and reveals the menu. Because a splash never changes, it is a simple
    ///     press-a-key <see cref="InputForm{T}" /> rendered once.
    /// </summary>
    [ParentWindow(typeof (ExampleWindow))]
    public sealed class LogoSplashDialog : InputForm<ExampleWindowInfo>
    {
        private readonly string _rendered;

        /// <summary>Initializes a new instance of the <see cref="LogoSplashDialog" /> class and renders the logo once.</summary>
        /// <param name="window">The parent window.</param>
        // ReSharper disable once UnusedMember.Global
        public LogoSplashDialog(IWindow window) : base(window)
        {
            _rendered = BuildSplash();
        }

        private static string BuildSplash()
        {
            var body = new StringBuilder();
            body.AppendLine();
            body.AppendLine("Welcome to WolfCurses");
            body.AppendLine();

            body.AppendLine(File.Exists(DemoImages.LogoPath)
                ? AnsiImage.RenderFile(DemoImages.LogoPath, DemoImages.FitOptions())
                : "(logo image not found)");

            return body.ToString();
        }

        /// <inheritdoc />
        protected override string OnDialogPrompt()
        {
            // The "Press ENTER" hint is already in the body; blank the bottom prompt so the menu's question does not
            // show over the splash.
            ParentWindow.PromptText = string.Empty;
            return _rendered;
        }

        /// <inheritdoc />
        protected override void OnDialogResponse(DialogResponseEnum reponse)
        {
            ClearForm();
        }
    }
}
