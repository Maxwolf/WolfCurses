using WolfCurses.Window;
using WolfCurses.Window.Form;
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

        public DialogResponse? LastResponse { get; private set; }

        public int ResponseCount { get; private set; }

        protected override string OnDialogPrompt()
        {
            return PROMPT_TEXT;
        }

        protected override void OnDialogResponse(DialogResponse reponse)
        {
            LastResponse = reponse;
            ResponseCount++;
        }
    }

    /// <summary>Uses the base InputForm DialogType, which defaults to Prompt.</summary>
    [ParentWindow(typeof(TestWindow))]
    public sealed class PromptDialogForm : RecordingDialogForm
    {
        public PromptDialogForm(IWindow window) : base(window)
        {
        }
    }

    [ParentWindow(typeof(TestWindow))]
    public sealed class YesNoDialogForm : RecordingDialogForm
    {
        public YesNoDialogForm(IWindow window) : base(window)
        {
        }

        protected override DialogType DialogType => DialogType.YesNo;
    }

    [ParentWindow(typeof(TestWindow))]
    public sealed class CustomDialogForm : RecordingDialogForm
    {
        public CustomDialogForm(IWindow window) : base(window)
        {
        }

        protected override DialogType DialogType => DialogType.Custom;
    }
}
