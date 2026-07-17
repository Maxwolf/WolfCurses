using System;
using System.Collections.Generic;
using WolfCurses.Window;
using WolfCurses.Window.Form;

namespace WolfCurses.Tests.TestDoubles
{
    /// <summary>
    ///     Records the key presses reported to it, so tests can see that a key the host saw reached the form that cared
    ///     about it. A key press is the only input that reaches a form without going through the buffer, so nothing else
    ///     here can stand in for it.
    /// </summary>
    [ParentWindow(typeof(TestWindow))]
    public class KeyRecordingForm : Form<TestWindowData>
    {
        public KeyRecordingForm(IWindow window) : base(window)
        {
        }

        public List<ConsoleKey> ReceivedKeys { get; } = new();

        public override string OnRenderForm()
        {
            return "KEYRECORDINGFORM RENDER";
        }

        public override void OnInputBufferReturned(string input)
        {
        }

        public override void OnKeyPressed(ConsoleKey key)
        {
            base.OnKeyPressed(key);
            ReceivedKeys.Add(key);
        }
    }
}
