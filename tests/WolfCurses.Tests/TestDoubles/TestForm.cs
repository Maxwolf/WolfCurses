using System.Collections.Generic;
using WolfCurses.Window;
using WolfCurses.Window.Form;

namespace WolfCurses.Tests.TestDoubles
{
    /// <summary>
    ///     Recording form for TestWindow. Counts ticks since Window.OnTick forwards only to its form — the sole
    ///     per-window tick observable. GetTypesWith yields each type once regardless of how many [ParentWindow]
    ///     attributes it carries (see DoubleRegisteredForm), and FormFactory registers the first attribute's parent.
    /// </summary>
    [ParentWindow(typeof(TestWindow))]
    public class TestForm : Form<TestWindowData>
    {
        public const string RENDER_TEXT = "TESTFORM RENDER";

        public TestForm(IWindow window) : base(window)
        {
        }

        public List<string> ReceivedInputs { get; } = new();

        public int TickCount { get; private set; }

        public override string OnRenderForm()
        {
            return RENDER_TEXT;
        }

        public override void OnInputBufferReturned(string input)
        {
            ReceivedInputs.Add(input);
        }

        public override void OnTick(bool systemTick, bool skipDay)
        {
            base.OnTick(systemTick, skipDay);
            TickCount++;
        }
    }

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
