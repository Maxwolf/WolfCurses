using System.Collections.Generic;
using WolfCurses.Window;
using WolfCurses.Window.Form;

namespace WolfCurses.Tests.TestDoubles
{
    /// <summary>
    ///     Records inputs like <see cref="TestForm" /> but reports InputFillsBuffer=false, so the simulation is NOT
    ///     accepting input while it is attached. Used to prove stale buffer text cannot leak through as a command.
    /// </summary>
    [ParentWindow(typeof(TestWindow))]
    public sealed class NonFillingRecordingForm : Form<TestWindowData>
    {
        public NonFillingRecordingForm(IWindow window) : base(window)
        {
        }

        public List<string> ReceivedInputs { get; } = new();

        public override bool InputFillsBuffer => false;

        public override string OnRenderForm()
        {
            return "NONFILLING";
        }

        public override void OnInputBufferReturned(string input)
        {
            ReceivedInputs.Add(input);
        }
    }
}
