using WolfCurses.Window;
using WolfCurses.Window.Form;

namespace WolfCurses.Tests.TestDoubles
{
    /// <summary>
    ///     Carries the same [ParentWindow] attribute twice (AllowMultiple permits it); GetTypesWith must still yield
    ///     the type exactly once or FormFactory's dictionary Add would break every SimulationApp in this assembly.
    /// </summary>
    [ParentWindow(typeof(TestWindow))]
    [ParentWindow(typeof(TestWindow))]
    public sealed class DoubleRegisteredForm : Form<TestWindowData>
    {
        public DoubleRegisteredForm(IWindow window) : base(window)
        {
        }

        public override string OnRenderForm()
        {
            return "DOUBLE";
        }

        public override void OnInputBufferReturned(string input)
        {
        }
    }
}
