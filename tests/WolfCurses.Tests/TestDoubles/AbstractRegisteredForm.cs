using WolfCurses.Window;
using WolfCurses.Window.Form;

namespace WolfCurses.Tests.TestDoubles
{
    /// <summary>
    ///     Abstract form registered via [ParentWindow]; FormFactory must refuse to instantiate it with a descriptive
    ///     exception rather than returning null for SetForm to dereference.
    /// </summary>
    [ParentWindow(typeof(TestWindow))]
    public abstract class AbstractRegisteredForm : Form<TestWindowData>
    {
        protected AbstractRegisteredForm(IWindow window) : base(window)
        {
        }
    }
}
