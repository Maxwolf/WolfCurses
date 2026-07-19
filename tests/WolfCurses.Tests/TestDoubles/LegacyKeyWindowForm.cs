using System;
using System.Collections.Generic;
using WolfCurses.Window;
using WolfCurses.Window.Form;

namespace WolfCurses.Tests.TestDoubles
{
    /// <summary>
    ///     The full-info recording form parented to <see cref="LegacyKeyWindow" />. A separate class rather than a
    ///     second attribute on <see cref="KeyInfoRecordingForm" /> because stacked <c>[ParentWindow]</c> attributes
    ///     register a form once, under the first attribute's parent only.
    /// </summary>
    [ParentWindow(typeof(LegacyKeyWindow))]
    public class LegacyKeyWindowForm : Form<TestWindowData>
    {
        public LegacyKeyWindowForm(IWindow window) : base(window)
        {
        }

        public List<ConsoleKeyInfo> ReceivedKeyInfos { get; } = new();

        public override string OnRenderForm()
        {
            return "LEGACYKEYWINDOWFORM RENDER";
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
