using System;
using System.Collections.Generic;
using WolfCurses.Window;
using WolfCurses.Window.Form;

namespace WolfCurses.Tests.TestDoubles
{
    /// <summary>
    ///     A form for <see cref="EscapeReturnsToMenuWindow" /> that records the keys forwarded to it, so a test can
    ///     show ESC is intercepted by the window above (this form is cleared and never sees it) while every other key
    ///     still arrives here through the window's <c>base.OnKeyPressed</c> call.
    /// </summary>
    [ParentWindow(typeof(EscapeReturnsToMenuWindow))]
    public class EscapeDismissableForm : Form<TestWindowData>
    {
        public EscapeDismissableForm(IWindow window) : base(window)
        {
        }

        public List<ConsoleKey> ReceivedKeys { get; } = new();

        public override string OnRenderForm()
        {
            return "ESCAPEDISMISSABLEFORM RENDER";
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
