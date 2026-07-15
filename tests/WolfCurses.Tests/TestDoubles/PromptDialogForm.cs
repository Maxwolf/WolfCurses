using WolfCurses.Window;
using WolfCurses.Window.Form;

namespace WolfCurses.Tests.TestDoubles
{
    /// <summary>Uses the base InputForm DialogType, which defaults to Prompt.</summary>
    [ParentWindow(typeof(TestWindow))]
    public sealed class PromptDialogForm : RecordingDialogForm
    {
        public PromptDialogForm(IWindow window) : base(window)
        {
        }
    }
}
