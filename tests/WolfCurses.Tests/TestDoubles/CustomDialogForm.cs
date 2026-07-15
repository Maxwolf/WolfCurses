using WolfCurses.Window;
using WolfCurses.Window.Form;
using WolfCurses.Window.Form.Input;

namespace WolfCurses.Tests.TestDoubles
{
    [ParentWindow(typeof(TestWindow))]
    public sealed class CustomDialogForm : RecordingDialogForm
    {
        public CustomDialogForm(IWindow window) : base(window)
        {
        }

        protected override DialogTypeEnum DialogType => DialogTypeEnum.Custom;
    }
}
