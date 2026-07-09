using WolfCurses.Window;
using WolfCurses.Window.Form;

namespace WolfCurses.Tests.TestDoubles
{
    /// <summary>
    ///     Deliberately lacks [ParentWindow], so FormFactory never registers it; SetForm(typeof(OrphanForm)) must
    ///     fail. Constructed directly in tests that need a form without factory involvement.
    /// </summary>
    public sealed class OrphanForm : Form<TestWindowData>
    {
        public OrphanForm(IWindow window) : base(window)
        {
        }

        public override string OnRenderForm()
        {
            return "ORPHAN";
        }

        public override void OnInputBufferReturned(string input)
        {
        }
    }
}
