// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System;
using WolfCurses.Window;
using WolfCurses.Window.Control;
using WolfCurses.Window.Form;
using WolfCurses.Window.Form.Input;

namespace WolfCurses.Example.Demos
{
    /// <summary>
    ///     A small shared dialog that shows the outcome of a standard-control demo (what the user selected, which
    ///     button they pressed, the text they entered) framed with a <see cref="Box" />. The control demos set
    ///     <see cref="ExampleWindowInfo.LastResult" /> and switch to this form; pressing ENTER returns to the menu.
    /// </summary>
    [ParentWindow(typeof (ExampleWindow))]
    public sealed class ControlResultDialog : InputForm<ExampleWindowInfo>
    {
        /// <summary>Initializes a new instance of the <see cref="ControlResultDialog" /> class.</summary>
        /// <param name="window">The parent window.</param>
        // ReSharper disable once UnusedMember.Global
        public ControlResultDialog(IWindow window) : base(window)
        {
        }

        /// <inheritdoc />
        protected override string OnDialogPrompt()
        {
            var text = string.IsNullOrEmpty(UserData.LastResult) ? "(no result)" : UserData.LastResult;
            return Environment.NewLine + new Box {Title = "Result", Padding = 1}.Render(text) + Environment.NewLine;
        }

        /// <inheritdoc />
        protected override void OnDialogResponse(DialogResponse reponse)
        {
            ClearForm();
        }
    }
}
