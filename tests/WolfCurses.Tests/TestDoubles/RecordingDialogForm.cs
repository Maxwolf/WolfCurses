using WolfCurses.Window;
using WolfCurses.Window.Form.Input;

namespace WolfCurses.Tests.TestDoubles
{
    /// <summary>
    ///     Base recording dialog; derived classes fix the DialogType. PROMPT_TEXT is what OnDialogPrompt supplies to
    ///     the form's internal prompt buffer during OnFormPostCreate.
    /// </summary>
    public abstract class RecordingDialogForm : InputForm<TestWindowData>
    {
        public const string PROMPT_TEXT = "PROMPT TEXT";

        protected RecordingDialogForm(IWindow window) : base(window)
        {
        }

        public DialogResponseEnum? LastResponse { get; private set; }

        public int ResponseCount { get; private set; }

        protected override string OnDialogPrompt()
        {
            return PROMPT_TEXT;
        }

        protected override void OnDialogResponse(DialogResponseEnum reponse)
        {
            LastResponse = reponse;
            ResponseCount++;
        }
    }
}
