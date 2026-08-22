using WolfCurses.Window;
using WolfCurses.Window.Form;

namespace WolfCurses.Tests.TestDoubles
{
    /// <summary>
    ///     Records that it was told it is going away, which is the only way to see a teardown hook fire: everything a
    ///     form would really be releasing there is a handle, a thread or a child process, none of which a test can
    ///     look at afterwards.
    /// </summary>
    [ParentWindow(typeof (TestWindow))]
    public class ClosingRecordingForm : Form<TestWindowData>
    {
        public ClosingRecordingForm(IWindow window) : base(window)
        {
        }

        /// <summary>How many times this form has been told it is closing.</summary>
        public int Closings { get; private set; }

        /// <summary>Whether it should clear itself from inside its own teardown, to prove that does not recurse.</summary>
        public bool ClearsItselfWhileClosing { get; set; }

        public override string OnRenderForm()
        {
            return "CLOSINGRECORDINGFORM RENDER";
        }

        public override void OnInputBufferReturned(string input)
        {
        }

        public override void OnFormClosing()
        {
            base.OnFormClosing();

            Closings++;

            if (ClearsItselfWhileClosing)
                ParentWindow.ClearForm();
        }
    }
}
