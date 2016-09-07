// Created by Ron 'Maxwolf' McDowell (ron.mcdowell@gmail.com) 
// Timestamp 01/16/2016@5:32 PM

using System;
using System.Text;
using WolfCurses.Window;
using WolfCurses.Window.Form;
using WolfCurses.Window.Form.Input;

namespace WolfCurses.Example.Question
{
    /// <summary>
    ///     Asks the user a yes/no based question, they can only reply with those predetermined answers.
    /// </summary>
    [ParentWindow(typeof (ExampleWindow))]
    public sealed class QuestionDialog : InputForm<ExampleWindowInfo>
    {
        /// <summary>
        ///     Holds all the text so we only need to render it once.
        /// </summary>
        private StringBuilder dialogYesNo = new StringBuilder();

        /// <summary>
        ///     Initializes a new instance of the <see cref="InputForm{T}" /> class.
        ///     This constructor will be used by the other one
        /// </summary>
        /// <param name="window">The window.</param>
        public QuestionDialog(IWindow window) : base(window)
        {
        }

        /// <summary>
        ///     Defines what type of dialog this will act like depending on this enumeration value. Up to implementation to define
        ///     desired behavior.
        /// </summary>
        protected override DialogType DialogType
        {
            get { return DialogType.YesNo; }
        }

        /// <summary>
        ///     Fired when dialog prompt is attached to active game Windows and would like to have a string returned.
        /// </summary>
        /// <returns>
        ///     The dialog prompt text.<see cref="string" />.
        /// </returns>
        protected override string OnDialogPrompt()
        {
            dialogYesNo.Clear();

            dialogYesNo.AppendLine($"{Environment.NewLine}Question Dialog Example{Environment.NewLine}");
            dialogYesNo.Append("Do you like wolves? Y/N");

            return dialogYesNo.ToString();
        }

        /// <summary>
        ///     Fired when the dialog receives favorable input and determines a response based on this. From this method it is
        ///     common to attach another state, or remove the current state based on the response.
        /// </summary>
        /// <param name="reponse">The response the dialog parsed from simulation input buffer.</param>
        protected override void OnDialogResponse(DialogResponse reponse)
        {
            switch (reponse)
            {
                case DialogResponse.Custom:
                case DialogResponse.No:
                    SetForm(typeof (NoWolves));
                    break;
                case DialogResponse.Yes:
                    SetForm(typeof (YesWolves));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(reponse), reponse, null);
            }
        }
    }
}