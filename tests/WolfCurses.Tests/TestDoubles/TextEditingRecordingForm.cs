using System;
using System.Collections.Generic;
using WolfCurses.Window;
using WolfCurses.Window.Form;

namespace WolfCurses.Tests.TestDoubles
{
    /// <summary>
    ///     A form shaped like a text editor: it opts into <see cref="Form{T}.EditsText" />, so ENTER and BACKSPACE
    ///     are delivered to it as key presses instead of being spent on the input buffer, and it leaves
    ///     <see cref="InputFillsBuffer" /> false so typed characters do not pile up in a prompt nobody is reading.
    ///     Records every key it is handed, which is what the routing tests assert against.
    /// </summary>
    [ParentWindow(typeof(TestWindow))]
    public sealed class TextEditingRecordingForm : Form<TestWindowData>
    {
        public TextEditingRecordingForm(IWindow window) : base(window)
        {
        }

        /// <summary>Every key press this form was handed, in order.</summary>
        public List<ConsoleKey> ReceivedKeys { get; } = new();

        /// <summary>Every line submitted through the buffer, which for this form should be none at all.</summary>
        public List<string> ReceivedInputs { get; } = new();

        /// <inheritdoc />
        public override bool EditsText => true;

        /// <inheritdoc />
        public override bool InputFillsBuffer => false;

        /// <inheritdoc />
        public override string OnRenderForm()
        {
            return "EDITING";
        }

        /// <inheritdoc />
        public override void OnKeyPressed(ConsoleKeyInfo keyInfo)
        {
            base.OnKeyPressed(keyInfo);
            ReceivedKeys.Add(keyInfo.Key);
        }

        /// <inheritdoc />
        public override void OnInputBufferReturned(string input)
        {
            ReceivedInputs.Add(input);
        }
    }
}
