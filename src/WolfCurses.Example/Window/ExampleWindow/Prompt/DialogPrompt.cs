// Created by Ron 'Maxwolf' McDowell (ron.mcdowell@gmail.com) 
// Timestamp 01/07/2016@8:16 PM

namespace WolfCurses.Example
{
    using System;
    using System.Text;
    using Form;
    using Form.Input;

    /// <summary>
    ///     Shows a piece of information to the user, pressing any key will close the form.
    /// </summary>
    [ParentWindow(typeof (ExampleWindow))]
    public sealed class DialogPrompt : InputForm<ExampleWindowInfo>
    {
        /// <summary>
        ///     Holds all the text so we only need to render it once.
        /// </summary>
        private StringBuilder dialogPrompt = new StringBuilder();

        /// <summary>
        ///     Initializes a new instance of the <see cref="InputForm{T}" /> class.
        ///     This constructor will be used by the other one
        /// </summary>
        /// <param name="window">The window.</param>
        public DialogPrompt(IWindow window) : base(window)
        {
        }

        /// <summary>
        ///     Fired when dialog prompt is attached to active game Windows and would like to have a string returned.
        /// </summary>
        /// <returns>
        ///     The dialog prompt text.<see cref="string" />.
        /// </returns>
        protected override string OnDialogPrompt()
        {
            dialogPrompt.Clear();

            dialogPrompt.AppendLine($"{Environment.NewLine}Dialog Prompt Example{Environment.NewLine}");
            dialogPrompt.Append("This is some very important information the user should know about!");

            return dialogPrompt.ToString();
        }

        /// <summary>
        ///     Fired when the dialog receives favorable input and determines a response based on this. From this method it is
        ///     common to attach another state, or remove the current state based on the response.
        /// </summary>
        /// <param name="reponse">The response the dialog parsed from simulation input buffer.</param>
        protected override void OnDialogResponse(DialogResponse reponse)
        {
            ClearForm();
        }
    }
}