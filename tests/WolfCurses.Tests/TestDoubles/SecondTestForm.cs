using WolfCurses.Window;
using WolfCurses.Window.Form;

namespace WolfCurses.Tests.TestDoubles
{
    /// <summary>
    ///     Same recording behavior registered for SecondTestWindow; ParentWindowAttribute is Inherited=false, so this
    ///     subclass carries exactly one registration.
    /// </summary>
    [ParentWindow(typeof(SecondTestWindow))]
    public class SecondTestForm : TestForm
    {
        public SecondTestForm(IWindow window) : base(window)
        {
        }
    }
}
