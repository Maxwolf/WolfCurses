// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System;
using System.Text;
using WolfCurses.Window;
using WolfCurses.Window.Control;
using WolfCurses.Window.Form;

namespace WolfCurses.Controls
{
    /// <summary>
    ///     Draws the prompt (framed with a <see cref="Box" />) and turns the submitted line into a result. The value
    ///     the user is typing is echoed by the renderer on the prompt line (as asterisks when the dialog is masked). A
    ///     blank submission cancels; otherwise the value is run through the optional validator — a returned message
    ///     keeps the dialog open and is shown, and a clean result is handed back to the caller.
    /// </summary>
    [ParentWindow(typeof (TextInputWindow))]
    public sealed class TextInputForm : Form<TextInputData>
    {
        /// <summary>Initializes a new instance of the <see cref="TextInputForm" /> class.</summary>
        /// <param name="window">The parent window.</param>
        // ReSharper disable once UnusedMember.Global
        public TextInputForm(IWindow window) : base(window)
        {
        }

        private TextInputData Data => UserData;

        /// <inheritdoc />
        public override string OnRenderForm()
        {
            var data = Data;
            if (data == null || !data.Initialized)
            {
                ParentWindow.PromptText = "Opening...";
                return Environment.NewLine + "Opening...";
            }

            var body = new StringBuilder();
            body.Append(data.Message);
            if (!string.IsNullOrEmpty(data.Error))
            {
                body.AppendLine();
                body.AppendLine();
                body.Append("! " + data.Error);
            }

            var boxed = new Box {Title = "Input", Padding = 1}.Render(body.ToString());

            ParentWindow.PromptText = data.Masked
                ? "Enter value (hidden), or blank to cancel:"
                : "Enter value, or blank to cancel:";

            return Environment.NewLine + boxed;
        }

        /// <inheritdoc />
        public override void OnInputBufferReturned(string input)
        {
            var data = Data;
            if (data == null || !data.Initialized)
                return;

            // The input manager already trims the submitted line, so a blank line means the user just pressed ENTER.
            var value = input ?? string.Empty;
            if (value.Length == 0)
            {
                Cancel(data);
                return;
            }

            if (data.Validator != null)
            {
                var error = data.Validator(value);
                if (!string.IsNullOrEmpty(error))
                {
                    // Reject: keep the dialog open and show why.
                    data.Error = error;
                    return;
                }
            }

            Submit(data, value);
        }

        private void Submit(TextInputData data, string value)
        {
            var callback = data.OnSubmit;
            ParentWindow.RemoveWindowNextTick();
            callback?.Invoke(value);
        }

        private void Cancel(TextInputData data)
        {
            var callback = data.OnCancelled;
            ParentWindow.RemoveWindowNextTick();
            callback?.Invoke();
        }
    }
}
