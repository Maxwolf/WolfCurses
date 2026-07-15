using WolfCurses.Window;
using WolfCurses.Window.Form;
using WolfCurses.Window.Form.Input;

namespace WolfCurses.Tests.TestDoubles
{
    [ParentWindow(typeof(TestWindow))]
    public sealed class YesNoDialogForm : RecordingDialogForm
    {
        public YesNoDialogForm(IWindow window) : base(window)
        {
        }

        protected override DialogTypeEnum DialogType => DialogTypeEnum.YesNo;
    }
}
