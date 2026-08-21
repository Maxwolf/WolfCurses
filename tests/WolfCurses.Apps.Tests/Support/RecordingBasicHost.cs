using System;
using System.Collections.Generic;
using System.Text;
using WolfCurses.Apps.Basic;

namespace WolfCurses.Apps.Tests.Support
{
    /// <summary>
    ///     A screen that is really a string. Everything a BASIC program can say goes into a buffer a test can read,
    ///     which is the entire point of the interpreter talking to <see cref="IBasicHost" /> rather than to a
    ///     console: a whole program can be run and checked with no application, no window and no terminal.
    /// </summary>
    public sealed class RecordingBasicHost : IBasicHost
    {
        /// <summary>What INPUT will be given, in order.</summary>
        private readonly Queue<string> _answers = new();

        /// <summary>What the program has written.</summary>
        private readonly StringBuilder _output = new();

        /// <summary>The keys INKEY$ will find, in order.</summary>
        private readonly Queue<string> _keys = new();

        /// <summary>Everything written so far.</summary>
        public string Output => _output.ToString();

        /// <summary>Every line written, with the trailing empty one dropped.</summary>
        public string[] Lines => _output.ToString().TrimEnd('\n').Split('\n');

        /// <summary>How many times the screen was cleared.</summary>
        public int Clears { get; private set; }

        /// <summary>How many times BEEP happened.</summary>
        public int Beeps { get; private set; }

        /// <summary>Where LOCATE last put the cursor.</summary>
        public (int Row, int Column) Cursor { get; private set; }

        /// <summary>What COLOR last asked for.</summary>
        public (int Foreground, int Background) Colors { get; private set; } = (7, -1);

        /// <summary>The prompts INPUT showed.</summary>
        public List<string> Prompts { get; } = new();

        /// <summary>Queues an answer for INPUT.</summary>
        /// <param name="answer">What the user would type.</param>
        public void Answer(string answer)
        {
            _answers.Enqueue(answer);
        }

        /// <summary>Queues a key for INKEY$.</summary>
        /// <param name="key">The key.</param>
        public void PressKey(string key)
        {
            _keys.Enqueue(key);
        }

        /// <inheritdoc />
        public void Write(string text)
        {
            _output.Append(text);
        }

        /// <inheritdoc />
        public void WriteLine()
        {
            _output.Append('\n');
        }

        /// <inheritdoc />
        public void Clear()
        {
            Clears++;
        }

        /// <inheritdoc />
        public void Locate(int row, int column)
        {
            Cursor = (row, column);
        }

        /// <inheritdoc />
        public void SetColor(int foreground, int background)
        {
            Colors = (foreground, background);
        }

        /// <inheritdoc />
        public string ReadLine(string prompt)
        {
            Prompts.Add(prompt);

            // An empty queue answers with nothing rather than throwing, so a test that forgot to queue an answer
            // fails on what the program did with it rather than inside the harness.
            return _answers.Count > 0 ? _answers.Dequeue() : string.Empty;
        }

        /// <inheritdoc />
        public string ReadKey()
        {
            return _keys.Count > 0 ? _keys.Dequeue() : string.Empty;
        }

        /// <inheritdoc />
        public void Beep()
        {
            Beeps++;
        }

        /// <summary>
        ///     Every drawing statement, written down as it was asked for. Recorded rather than drawn, because what
        ///     is worth checking here is that the interpreter passed the right numbers along; whether a line of
        ///     pixels comes out straight is BasicScreen's business and is tested against a real one.
        /// </summary>
        public List<string> Drawing { get; } = new();

        /// <summary>The screen mode a program last asked for.</summary>
        public int ScreenMode { get; private set; }

        /// <inheritdoc />
        public int LastX { get; private set; }

        /// <inheritdoc />
        public int LastY { get; private set; }

        /// <inheritdoc />
        public int ScreenWidth { get; private set; } = 80;

        /// <inheritdoc />
        public int ScreenHeight { get; private set; } = 25;

        /// <inheritdoc />
        public void SetScreenMode(int mode)
        {
            ScreenMode = mode;
            ScreenWidth = mode == 0 ? 80 : 320;
            ScreenHeight = mode == 0 ? 25 : 200;

            Drawing.Add("SCREEN " + mode);
        }

        /// <inheritdoc />
        public void Plot(int x, int y, int color)
        {
            LastX = x;
            LastY = y;

            Drawing.Add("PSET " + x + "," + y + "," + color);
        }

        /// <inheritdoc />
        public void DrawLine(int x0, int y0, int x1, int y1, int color, string box)
        {
            LastX = x1;
            LastY = y1;

            Drawing.Add("LINE " + x0 + "," + y0 + "-" + x1 + "," + y1 + "," + color +
                        (string.IsNullOrEmpty(box) ? string.Empty : "," + box));
        }

        /// <inheritdoc />
        public void DrawCircle(int x, int y, int radius, int color)
        {
            LastX = x;
            LastY = y;

            Drawing.Add("CIRCLE " + x + "," + y + "," + radius + "," + color);
        }

        /// <summary>The notes a program asked for, in order.</summary>
        public List<(double Frequency, double Milliseconds)> Notes { get; } = new();

        /// <summary>The pixels a test has arranged for GET to find.</summary>
        public Dictionary<(int X, int Y), int> Pixels { get; } = new();

        /// <inheritdoc />
        public int PixelAt(int x, int y)
        {
            return Pixels.TryGetValue((x, y), out var color) ? color : 0;
        }

        /// <inheritdoc />
        public void Sound(double frequency, double milliseconds)
        {
            Notes.Add((frequency, milliseconds));
        }

        /// <inheritdoc />
        public void Paint(int x, int y, int fill, int border)
        {
            Drawing.Add("PAINT " + x + "," + y + "," + fill + "," + border);
        }

        /// <summary>Runs a program against a fresh host and hands both back.</summary>
        /// <param name="source">The program.</param>
        /// <param name="seed">A fixed seed so RND is predictable.</param>
        /// <returns>The host it wrote to.</returns>
        public static RecordingBasicHost Run(string source, int? seed = 1)
        {
            var host = new RecordingBasicHost();
            BasicProgram.Compile(source).Run(new BasicRuntime(host, seed));

            return host;
        }

        /// <summary>Runs a program and hands back what it printed, trimmed of the trailing newline.</summary>
        /// <param name="source">The program.</param>
        /// <returns>The output.</returns>
        public static string Printed(string source)
        {
            return Run(source).Output.TrimEnd('\n');
        }

        /// <summary>The error a program produced, or a failure when it did not produce one.</summary>
        /// <param name="source">The program.</param>
        /// <returns>The error.</returns>
        public static BasicError Fails(string source)
        {
            try
            {
                Run(source);
            }
            catch (BasicError error)
            {
                return error;
            }

            throw new InvalidOperationException("The program was expected to fail and did not.");
        }
    }
}
