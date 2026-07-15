using WolfCurses.Window;
using WolfCurses.Window.Form;

namespace WolfCurses.Tests.TestDoubles
{
    /// <summary>
    ///     Destroys the owning simulation from inside input dispatch, which happens during the InputManager module
    ///     tick — the mid-tick teardown scenario. The app reference travels through the shared user data object.
    /// </summary>
    [ParentWindow(typeof(TestWindow))]
    public sealed class DestroyOnInputForm : Form<TestWindowData>
    {
        public DestroyOnInputForm(IWindow window) : base(window)
        {
        }

        public override string OnRenderForm()
        {
            return "DESTROYER";
        }

        public override void OnInputBufferReturned(string input)
        {
            UserData.App?.Destroy();
        }
    }
}
