// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System;
using System.IO;
using System.Text;
using WolfCurses.Graphics;
using WolfCurses.Utility;
using WolfCurses.Window;
using WolfCurses.Window.Form;
using WolfCurses.Window.Form.Input;

namespace WolfCurses.Demo.Screens
{
    /// <summary>
    ///     Shown after the file browser picks an image: displays the chosen image with ANSI graphics. This ties the
    ///     file dialog and the ANSI image feature together. Pressing ENTER returns to the menu.
    ///     <para>
    ///         The file here is the one picture in the application nobody vetted, so this is also the screen most
    ///         likely to meet one that does not draw, and there are two unrelated ways for that to happen. The file
    ///         may not decode, which never throws: <see cref="AnsiImage.FromFile" /> hands back the checkerboard
    ///         with the reason waiting in <see cref="AnsiImage.Error" />. Or the console may not be able to carry a
    ///         picture at all, which <see cref="AnsiConsole.SupportsPictures" /> is the one place that decides.
    ///         Both used to end in something wordless: magenta squares, or an empty rectangle with a prompt under
    ///         it and nothing anywhere reporting a problem.
    ///     </para>
    /// </summary>
    [ParentWindow(typeof (DemoWindow))]
    public sealed class SelectedImageDialog : InputForm<DemoWindowInfo>
    {
        /// <summary>
        ///     What to say when a picture cannot reach this console, since saying it is the whole of what this
        ///     screen can honestly do. It names the two clauses because they fail for entirely different reasons and
        ///     the user can only act on one of them, and it names the way out that works either way.
        /// </summary>
        private const string NoPictureHere =
            "This console cannot carry a picture, so none was drawn. AnsiConsole.SupportsPictures() answers no " +
            "when virtual-terminal processing could not be enabled, where ConsolePresenter strips every escape " +
            "from a row before writing it (right, since the console would otherwise print the bytes as garbage " +
            "and miscount them as columns) and a picture therefore goes out blank; and, for a real-pixel " +
            "renderer, when color is switched off, since a sixel or kitty payload is nothing but color. Force " +
            "render type, then \"Text only\", draws the shaded ASCII ramp instead, which needs neither.";

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
            var width = Math.Max(20, AnsiConsole.SafeWindowWidth() - 2);
            var body = new StringBuilder();
            body.AppendLine();
            body.AppendLine($"You picked: {path}");
            body.AppendLine();

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                body.AppendLine("(No file was selected.)");
                return body.ToString();
            }

            // Loading is deliberately incapable of throwing, so the try/catch that used to wrap this line could
            // never fire, and a file that would not decode drew the magenta checkerboard with nothing whatsoever
            // saying why. The .bmp the file browser offers is the everyday way to get here, the built-in decoders
            // reading PNG, JPEG and GIF and nothing else. IsError is the question; Error is the answer.
            var image = AnsiImage.FromFile(path);
            var options = DemoImages.FitOptions();

            // Keep the reason on the screen beside the picture. A checkerboard teaches nothing extra past a few
            // rows, and a full-height one pushes the only words here off the bottom of the terminal.
            if (image.IsError)
                options.MaxRows = Math.Max(4, Math.Min(8, options.MaxRows ?? 8));

            if (PictureWouldReachTheScreen())
                body.AppendLine(image.ToAnsi(options));
            else
                body.Append(NoPictureHere.WordWrap(width));

            if (image.IsError)
                AppendReason(body, width, image.Error);

            return body.ToString();
        }

        /// <summary>
        ///     Whether a rendered picture would actually arrive, or whether this screen should say so instead.
        ///     <para>
        ///         <see cref="AnsiConsole.SupportsPictures" /> is both halves of that decision and is where it
        ///         lives, rather than being written out longhand here with a clause missing. The exemption is the
        ///         one case that blunt answer is wrong for on this screen: a character-cell renderer at a resolved
        ///         color mode of None draws the shaded ASCII ramp, which carries no escapes to lose and so survives
        ///         a presenter that strips them. That is exactly what the Force render type menu's "Text only"
        ///         choice asks for, and refusing it would turn that choice into a dead end on the one screen where
        ///         the user chose the file.
        ///     </para>
        /// </summary>
        private static bool PictureWouldReachTheScreen()
        {
            return AnsiConsole.SupportsPictures() ||
                   (!ImageRenderers.Default.DrawsTruePixels &&
                    AnsiConsole.DetectColorMode() == AnsiColorModeEnum.None);
        }

        /// <summary>
        ///     Reports the failure the checkerboard is standing in for. Every failed load looks identical on screen
        ///     by design, so this line is the only thing that tells a mistyped path from an unsupported format from
        ///     an interrupted download.
        /// </summary>
        private static void AppendReason(StringBuilder body, int width, Exception error)
        {
            body.AppendLine();
            body.Append(("That file did not decode, and nothing threw. A failed load renders as the magenta " +
                         "checkerboard and leaves the exception in AnsiImage.Error, which is where the difference " +
                         "between one failure and another is kept:").WordWrap(width));
            body.AppendLine();

            // Wrapped first and indented afterwards, or every line after the first loses the indent and the
            // exception stops reading as a quotation of something else's words.
            var reason = $"{error.GetType().Name}: {FirstSentence(error.Message)}";
            foreach (var line in reason.WordWrap(Math.Max(16, width - 4)).Split(Environment.NewLine))
            {
                if (line.Length > 0)
                    body.Append("    ").AppendLine(line);
            }

            body.AppendLine();
            body.Append(("Check IsError to handle a failure deliberately; that message is the opening of a longer " +
                         "one, all of which is in Error and on Trace. The image error demo on the menu is the " +
                         "screen that shows the convention at length.").WordWrap(width));
        }

        /// <summary>
        ///     The decoder's messages open with the diagnosis and then teach the fix at paragraph length. Here the
        ///     rows belong to the picture, so the opening sentence is all this screen spends on it.
        /// </summary>
        private static string FirstSentence(string message)
        {
            var firstLine = message.Split('\n')[0].TrimEnd('\r');
            var sentenceEnd = firstLine.IndexOf(". ", StringComparison.Ordinal);
            return sentenceEnd >= 0 ? firstLine[..(sentenceEnd + 1)] : firstLine;
        }

        /// <inheritdoc />
        protected override void OnDialogResponse(DialogResponseEnum reponse)
        {
            ClearForm();
        }
    }
}
