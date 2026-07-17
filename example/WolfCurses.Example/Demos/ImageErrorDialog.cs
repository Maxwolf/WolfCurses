// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/17/2026

using System;
using System.IO;
using System.Text;
using WolfCurses.Graphics;
using WolfCurses.Utility;
using WolfCurses.Window;
using WolfCurses.Window.Form;
using WolfCurses.Window.Form.Input;

namespace WolfCurses.Example.Demos
{
    /// <summary>
    ///     Shows what an image that fails to load looks like: the magenta-and-black checkerboard, on purpose. Three
    ///     loads are attempted that each fail a different way — a mistyped path, a ".jpg" that is really a saved 404
    ///     page, and a real photograph cut off after 100 bytes — and none of them throws.
    ///     <see cref="AnsiImage.FromFile" /> and friends hand back a renderable <see cref="ImageErrorTexture" />
    ///     instead, with the exception that would have been thrown waiting in <see cref="AnsiImage.Error" />, which
    ///     this screen prints under the picture so the diagnostic side of the convention is visible too.
    ///     <para>
    ///         Only one checkerboard is drawn even though three loads failed, because that is the point: every
    ///         failure looks the same on screen by design (magenta appears in almost nothing real, so it reads as
    ///         "missing asset" across the room). The differences live in <see cref="AnsiImage.Error" />, and the
    ///         built-in decoder works to make them diagnosable — note how the 404 page is called out as HTML rather
    ///         than as random bytes.
    ///     </para>
    /// </summary>
    [ParentWindow(typeof (ExampleWindow))]
    public sealed class ImageErrorDialog : InputForm<ExampleWindowInfo>
    {
        /// <summary>How much of the real logo the "interrupted download" case keeps.</summary>
        private const int TruncateAfterBytes = 100;

        /// <summary>
        ///     What a download script leaves on disk when the server answered with an error page instead of the
        ///     picture — the classic way a file with an image extension comes to hold no image at all.
        /// </summary>
        private static readonly byte[] _savedErrorPage = Encoding.ASCII.GetBytes(
            "<!DOCTYPE html><html><head><title>404 Not Found</title></head>" +
            "<body><h1>404 Not Found</h1><p>The image you requested is no longer here.</p></body></html>");

        /// <summary>Initializes a new instance of the <see cref="ImageErrorDialog" /> class.</summary>
        /// <param name="window">The parent window.</param>
        // ReSharper disable once UnusedMember.Global
        public ImageErrorDialog(IWindow window) : base(window)
        {
        }

        /// <inheritdoc />
        protected override string OnDialogPrompt()
        {
            ParentWindow.PromptText = string.Empty;

            // Three loads that each fail differently. None of these calls throws; each returns an image that
            // renders as the error checkerboard and carries its reason in Error.
            var mistyped = AnsiImage.FromFile(Path.Combine(DemoImages.Folder, "logo_typo.jpg"));
            var imposter = AnsiImage.FromBytes(_savedErrorPage);
            var truncated = AnsiImage.FromBytes(TruncatedLogo());

            // Leave room under the picture for the diagnostics, and keep the checkerboard modest on tall
            // terminals — past a dozen rows it teaches nothing extra.
            var options = DemoImages.FitOptions();
            var width = Math.Max(20, options.MaxColumns ?? 78);
            options.MaxRows = Math.Max(4, Math.Min(12, (options.MaxRows ?? 16) - 12));

            var body = new StringBuilder();
            body.AppendLine();
            body.Append(("Loading an image never throws: a file that is missing, corrupt or not an image at all " +
                         "becomes this magenta-and-black checkerboard (the Quake/Source-engine convention), so one " +
                         "bad file cannot take the application down and the failure shows up where you are already " +
                         "looking - the screen.").WordWrap(width));
            body.AppendLine(mistyped.ToAnsi(options));
            body.AppendLine();
            body.Append("Three different failures just drew that same texture; each reason is in AnsiImage.Error:"
                .WordWrap(width));
            body.AppendLine();
            AppendCase(body, width, 1, "Mistyped path - images/logo_typo.jpg does not exist:", mistyped);
            AppendCase(body, width, 2, "A \".jpg\" that is really a saved 404 page:", imposter);
            AppendCase(body, width, 3, $"logo.jpg cut off after {TruncateAfterBytes} bytes (interrupted download):",
                truncated);
            body.Append(("Check IsError to handle a failure deliberately; the full exception is in Error and on " +
                         "Trace. Code that wants the throw instead calls the decoder directly: " +
                         "ImageDecoders.Default.Decode(stream) is the strict path.").WordWrap(width));

            return body.ToString();
        }

        /// <summary>One failed load: what was attempted, then the exception the checkerboard is standing in for.</summary>
        private static void AppendCase(StringBuilder body, int width, int number, string title, AnsiImage image)
        {
            body.AppendLine($" {number}. {title}");

            var reason = image.IsError
                ? $"{image.Error.GetType().Name}: {FirstSentence(image.Error.Message)}"
                : "(unexpectedly loaded fine - this case needs a new way to fail)";

            foreach (var line in reason.WordWrap(Math.Max(16, width - 4)).Split(Environment.NewLine))
            {
                if (line.Length > 0)
                    body.Append("    ").AppendLine(line);
            }

            body.AppendLine();
        }

        /// <summary>
        ///     The real logo's first <see cref="TruncateAfterBytes" /> bytes: enough to be recognized as a JPEG, not
        ///     enough to decode — exactly what an interrupted download leaves behind. Falls back to a bare JPEG
        ///     signature if the logo is not beside the executable, so the demo fails the intended way regardless.
        /// </summary>
        private static byte[] TruncatedLogo()
        {
            if (!File.Exists(DemoImages.LogoPath))
                return new byte[] {0xFF, 0xD8, 0xFF, 0xE0};

            var bytes = File.ReadAllBytes(DemoImages.LogoPath);
            var head = new byte[Math.Min(TruncateAfterBytes, bytes.Length)];
            Array.Copy(bytes, head, head.Length);
            return head;
        }

        /// <summary>
        ///     The decoder's messages open with the diagnosis and then teach the fix at paragraph length; on this
        ///     screen the opening sentence is the part worth the rows.
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
