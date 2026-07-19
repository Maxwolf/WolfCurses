using System;
using System.Collections.Generic;
using WolfCurses.Window;
using WolfCurses.Window.Form;

namespace WolfCurses.Tests.TestDoubles
{
    /// <summary>
    ///     Records the whole <see cref="ConsoleKeyInfo" /> of every key press reported to it — the form a port binding
    ///     shifted keys actually writes. The bare-key sibling (<see cref="KeyRecordingForm" />) cannot stand in for it:
    ///     ',' and '&lt;' both arrive there as <see cref="ConsoleKey.OemComma" />, and telling them apart is the entire
    ///     reason the full info travels.
    /// </summary>
    [ParentWindow(typeof(TestWindow))]
    public class KeyInfoRecordingForm : Form<TestWindowData>
    {
        public KeyInfoRecordingForm(IWindow window) : base(window)
        {
        }

        public List<ConsoleKeyInfo> ReceivedKeyInfos { get; } = new();

        public override string OnRenderForm()
        {
            return "KEYINFORECORDINGFORM RENDER";
        }

        public override void OnInputBufferReturned(string input)
        {
        }

        public override void OnKeyPressed(ConsoleKeyInfo keyInfo)
        {
            ReceivedKeyInfos.Add(keyInfo);
        }
    }
}
