// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 01/16/2016@5:33 PM

using System;
using WolfCurses.Window;
using WolfCurses.Window.Form;

namespace WolfCurses.Demo.CustomInput
{
    /// <summary>
    ///     Asks for user name and then accepts the input from the input buffer.
    /// </summary>
    [ParentWindow(typeof (DemoWindow))]
    public sealed class DialogCustomInput : Form<DemoWindowInfo>
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="Form{TData}" /> class.
        ///     This constructor will be used by the other one
        /// </summary>
        /// <param name="window">The window.</param>
        // ReSharper disable once UnusedMember.Global
        public DialogCustomInput(IWindow window) : base(window)
        {
        }

        /// <summary>
        ///     Ask the question on the prompt line so the name the user types echoes right after it, the same way the
        ///     menu shows the typed number after "What is your choice?", instead of on a separate line below a second,
        ///     redundant prompt.
        ///     <para>
        ///         Set here and not in <see cref="OnRenderForm" />, which is the rule this file exists to show:
        ///         <see cref="OnRenderForm" /> is a pure read of state something else decided, called on every system
        ///         tick at roughly a thousand times a second. Anything that changes state belongs on a tick or, when
        ///         it is decided once like this prompt, in <see cref="OnFormPostCreate" />. Assigning from the render
        ///         costs a thousand writes a second to show one string, and hides who owns the value: the window's own
        ///         prompt is restored by <see cref="DemoWindow.OnFormChange" /> when this form goes away, which only
        ///         reads correctly if nothing is quietly rewriting it every frame.
        ///     </para>
        /// </summary>
        public override void OnFormPostCreate()
        {
            base.OnFormPostCreate();

            ParentWindow.PromptText = "What is your name?";
        }

        /// <summary>
        ///     Returns a text only representation of the current game Windows state. Could be a statement, information, question
        ///     waiting input, etc.
        /// </summary>
        /// <returns>
        ///     The text user interface.<see cref="string" />.
        /// </returns>
        public override string OnRenderForm()
        {
            return $"{Environment.NewLine}Dialog Custom Input{Environment.NewLine}";
        }

        /// <summary>Fired when the game Windows current state is not null and input buffer does not match any known command.</summary>
        /// <param name="input">Contents of the input buffer which didn't match any known command in parent game Windows.</param>
        public override void OnInputBufferReturned(string input)
        {
            // Do not allow empty names.
            if (string.IsNullOrEmpty(input) || string.IsNullOrWhiteSpace(input))
                return;

            // Copy name into user name and show form.
            UserData.PlayerName = input;
            SetForm(typeof (ShowName));
        }
    }
}