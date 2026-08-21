// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;
using System.Threading;
using WolfCurses.Graphics;
using WolfCurses.Window.Control;

namespace WolfCurses.Apps.Basic
{
    /// <summary>
    ///     The screen a running BASIC program writes on: a character grid with a cursor and a colour, which is
    ///     exactly what the machines this imitates gave a program.
    ///     <para>
    ///         Built on the library's <see cref="TextGrid" /> rather than on a list of lines, because LOCATE means a
    ///         program can write anywhere at any time. A list of lines can only be appended to, and half of what a
    ///         BASIC program does to its screen is go back and change part of it.
    ///     </para>
    ///     <para>
    ///         <b>It is an <see cref="IBasicHost" /> and nothing else knows that.</b> The interpreter cannot tell
    ///         this from the recording host the tests use, which is the whole point of the seam.
    ///     </para>
    /// </summary>
    internal sealed class BasicScreen : IBasicHost
    {
        /// <summary>The sixteen colours a BASIC program can name, in the order it names them.</summary>
        private static readonly ConsoleColor[] _palette =
        {
            ConsoleColor.Black, ConsoleColor.DarkBlue, ConsoleColor.DarkGreen, ConsoleColor.DarkCyan,
            ConsoleColor.DarkRed, ConsoleColor.DarkMagenta, ConsoleColor.DarkYellow, ConsoleColor.Gray,
            ConsoleColor.DarkGray, ConsoleColor.Blue, ConsoleColor.Green, ConsoleColor.Cyan,
            ConsoleColor.Red, ConsoleColor.Magenta, ConsoleColor.Yellow, ConsoleColor.White
        };

        /// <summary>
        ///     What each colour number looks like as real pixels. These are the EGA values the machines actually
        ///     produced, not an approximation: a program that draws in colour 6 means brown, and picking a prettier
        ///     orange would make its pictures wrong.
        /// </summary>
        private static readonly Rgba32[] _pixelPalette =
        {
            new(0x00, 0x00, 0x00, 0xFF), new(0x00, 0x00, 0xAA, 0xFF), new(0x00, 0xAA, 0x00, 0xFF),
            new(0x00, 0xAA, 0xAA, 0xFF), new(0xAA, 0x00, 0x00, 0xFF), new(0xAA, 0x00, 0xAA, 0xFF),
            new(0xAA, 0x55, 0x00, 0xFF), new(0xAA, 0xAA, 0xAA, 0xFF), new(0x55, 0x55, 0x55, 0xFF),
            new(0x55, 0x55, 0xFF, 0xFF), new(0x55, 0xFF, 0x55, 0xFF), new(0x55, 0xFF, 0xFF, 0xFF),
            new(0xFF, 0x55, 0x55, 0xFF), new(0xFF, 0x55, 0xFF, 0xFF), new(0xFF, 0xFF, 0x55, 0xFF),
            new(0xFF, 0xFF, 0xFF, 0xFF)
        };

        /// <summary>The cells.</summary>
        private readonly TextGrid _grid;

        /// <summary>The pixels, once a program has asked for a graphics mode.</summary>
        private PixelBuffer _pixels;

        /// <summary>Which colour number later drawing uses when a statement does not name one.</summary>
        private int _drawColor = 15;

        /// <summary>Where the next character goes, counting from zero.</summary>
        private int _column;

        /// <summary>Which row the next character goes on, counting from zero.</summary>
        private int _row;

        /// <summary>How later writing is painted.</summary>
        private TextStyle _style;

        /// <summary>Initializes a new instance of the <see cref="BasicScreen" /> class.</summary>
        /// <param name="width">How many columns.</param>
        /// <param name="height">How many rows.</param>
        /// <param name="audible">
        ///     Whether notes are actually played. <b>Off by default, and that default is load-bearing:</b> the test
        ///     suite runs the shipped programs, one of which is a hundred and fifty notes of Grieg, and a screen
        ///     that made noise by construction would turn a test run into several minutes of beeping.
        /// </param>
        public BasicScreen(int width, int height, bool audible = false)
        {
            _grid = new TextGrid(Math.Max(1, width), Math.Max(1, height));
            _style = new TextStyle(ConsoleColor.Gray, ConsoleColor.Black);

            // Non-Windows has no Console.Beep to speak of, so it stays quiet rather than throwing per note.
            Audible = audible && OperatingSystem.IsWindows();

            Clear();
        }

        /// <summary>How many columns it has.</summary>
        public int Width => _grid.Width;

        /// <summary>How many rows it has.</summary>
        public int Height => _grid.Height;

        /// <summary>Whether a program has asked for a graphics mode.</summary>
        public bool IsGraphics => _pixels != null;

        /// <inheritdoc />
        public int ScreenWidth => _pixels?.Width ?? _grid.Width;

        /// <inheritdoc />
        public int ScreenHeight => _pixels?.Height ?? _grid.Height;

        /// <summary>Whether a program has put anything on it since it was last cleared.</summary>
        public bool HasOutput { get; private set; }

        /// <summary>
        ///     The screen, ready to be drawn.
        ///     <para>
        ///         <b>A graphics mode shows the picture and not the text</b>, which is a real limitation rather than
        ///         an oversight. On the machines this imitates the two shared one screen; here a picture may go out
        ///         as a single sixel payload that nothing is allowed to sit beside, so overlaying characters on it
        ///         is not something the presenter can express. Programs that draw and print at once lose the
        ///         printing, and the honest fix is a text renderer that draws into the pixels.
        ///     </para>
        /// </summary>
        /// <param name="columns">How many columns the picture may use.</param>
        /// <param name="rows">How many rows it may use.</param>
        /// <returns>The screen as text, newline separated.</returns>
        public string Render(int columns = 0, int rows = 0)
        {
            if (_pixels == null)
                return _grid.Render();

            var options = new AnsiImageOptions
            {
                MaxColumns = columns > 0 ? columns : _grid.Width,
                MaxRows = rows > 0 ? rows : _grid.Height,
                RowMargin = 0
            };

            return ImageRenderers.Default.Render(_pixels, options);
        }

        /// <inheritdoc />
        public void Write(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            HasOutput = true;

            foreach (var character in text)
            {
                // A control character would be obeyed by the terminal rather than drawn, exactly as it would in a
                // document; the library's substitution is what keeps a stray one from moving the real cursor.
                _grid.Set(_column, _row, ControlPictureOf(character), _style);
                _column++;

                if (_column < _grid.Width)
                    continue;

                _column = 0;
                NextRow();
            }
        }

        /// <inheritdoc />
        public void WriteLine()
        {
            HasOutput = true;
            _column = 0;
            NextRow();
        }

        /// <inheritdoc />
        public void Clear()
        {
            _pixels?.Fill(_pixelPalette[0]);
            _grid.Fill(' ', _style);
            _row = 0;
            _column = 0;
            HasOutput = false;
        }

        /// <inheritdoc />
        public void Locate(int row, int column)
        {
            // BASIC counts from one and the grid counts from zero, and clamping rather than throwing is what the
            // machines did: a program that locates off the edge gets put back on it.
            _row = Math.Clamp(row - 1, 0, _grid.Height - 1);
            _column = Math.Clamp(column - 1, 0, _grid.Width - 1);
        }

        /// <inheritdoc />
        public void SetColor(int foreground, int background)
        {
            TextColor front = _palette[Math.Clamp(foreground, 0, _palette.Length - 1)];

            // A missing background means "leave it as it was", which is how COLOR sets a foreground on its own.
            var back = background < 0
                ? _style.Background
                : (TextColor) _palette[Math.Clamp(background, 0, _palette.Length - 1)];

            _style = new TextStyle(front, back);

            // The same number the text is written in is the one drawing uses when a statement does not name one,
            // which is what COLOR means in a graphics mode.
            _drawColor = Math.Clamp(foreground, 0, _pixelPalette.Length - 1);
        }

        /// <summary>
        ///     Answers INPUT if somebody has typed something, and otherwise asks to be come back to.
        ///     <para>
        ///         <b>It deliberately does not write the prompt.</b> This is called twice for one INPUT, once to
        ///         signal and once to answer, so writing here would print the question twice.
        ///     </para>
        /// </summary>
        /// <param name="prompt">What the program wants to ask.</param>
        /// <returns>The answer.</returns>
        public string ReadLine(string prompt)
        {
            if (_answer == null)
                throw new BasicInputRequest(prompt);

            var answer = _answer;
            _answer = null;

            return answer;
        }

        /// <summary>Hands the waiting INPUT its answer, so that re-running the statement completes it.</summary>
        /// <param name="answer">What was typed.</param>
        public void SupplyAnswer(string answer)
        {
            _answer = answer ?? string.Empty;
        }

        /// <summary>Rubs out the character before the cursor, which is what echoing a backspace means.</summary>
        public void Backspace()
        {
            if (_column <= 0)
                return;

            _column--;
            _grid.Set(_column, _row, ' ', _style);
        }

        /// <inheritdoc />
        public string ReadKey()
        {
            if (PendingKey.Length == 0)
                return string.Empty;

            var key = PendingKey;
            PendingKey = string.Empty;

            return key;
        }

        /// <inheritdoc />
        public void Beep()
        {
            Beeps++;
        }

        /// <inheritdoc />
        public int PixelAt(int x, int y)
        {
            return ColorAt(x, y);
        }

        /// <summary>What a program asked to hear, and how long for, whether or not anybody heard it.</summary>
        public List<(double Frequency, double Milliseconds)> Notes { get; } = new();

        /// <summary>Whether notes are actually played.</summary>
        public bool Audible { get; }

        /// <summary>
        ///     How many notes may be waiting before further ones are dropped.
        ///     <para>
        ///         A cap rather than an unbounded queue, because a program can ask for notes far faster than they
        ///         can be played: a SOUND inside a loop would otherwise pile up a tune that goes on playing minutes
        ///         after the program has stopped, which is worse than missing a few notes.
        ///     </para>
        /// </summary>
        private const int MaxQueuedNotes = 64;

        /// <summary>The notes waiting to be played.</summary>
        private readonly Queue<(double Frequency, double Milliseconds)> _pending = new();

        /// <summary>Guards the queue, which two threads reach.</summary>
        private readonly object _gate = new();

        /// <summary>The thread doing the beeping, started when the first note arrives.</summary>
        private Thread _player;

        /// <summary>Whether the player should give up and go home.</summary>
        private bool _finished;

        /// <summary>
        ///     Hands a note to the speaker.
        ///     <para>
        ///         <b>Queued for another thread rather than played here, because playing a note blocks for its
        ///         whole length.</b> This is called from the middle of running a program, and a program is run in
        ///         slices precisely so that the screen stays alive and ESC keeps working: beeping on this thread
        ///         would freeze the interface for the length of the tune.
        ///     </para>
        /// </summary>
        /// <param name="frequency">The pitch in hertz; zero is a rest.</param>
        /// <param name="milliseconds">How long it lasts.</param>
        public void Sound(double frequency, double milliseconds)
        {
            Notes.Add((frequency, milliseconds));

            if (!Audible)
                return;

            lock (_gate)
            {
                if (_pending.Count >= MaxQueuedNotes)
                    return;

                _pending.Enqueue((frequency, milliseconds));
                Monitor.Pulse(_gate);
            }

            StartPlayer();
        }

        /// <summary>
        ///     Throws away whatever has not been played yet, which is what stopping a program has to do: a tune
        ///     going on after ESC would be the clearest possible sign that ESC had not worked.
        /// </summary>
        public void Silence()
        {
            lock (_gate)
            {
                _pending.Clear();
                _finished = true;
                Monitor.PulseAll(_gate);
            }

            _player = null;
        }

        /// <summary>Starts the player thread if it is not already going.</summary>
        private void StartPlayer()
        {
            if (_player != null)
                return;

            _finished = false;

            // A background thread so that it can never hold the process open: a half-played tune must not be the
            // reason the program will not close.
            _player = new Thread(Play) {IsBackground = true, Name = "BASIC notes"};
            _player.Start();
        }

        /// <summary>Plays whatever turns up, one note at a time, until told to stop.</summary>
        private void Play()
        {
            while (true)
            {
                (double Frequency, double Milliseconds) note;

                lock (_gate)
                {
                    while (_pending.Count == 0 && !_finished)
                        Monitor.Wait(_gate);

                    if (_finished)
                        return;

                    note = _pending.Dequeue();
                }

                Emit(note.Frequency, note.Milliseconds);
            }
        }

        /// <summary>Makes one note, or waits out a rest.</summary>
        private static void Emit(double frequency, double milliseconds)
        {
            var length = (int) Math.Clamp(milliseconds, 1d, 10000d);

            // Thirty-seven hertz is the lowest the speaker will take, and below it there is nothing to play: a rest
            // arrives as a frequency of zero and is simply waited out.
            if (frequency < 37d)
            {
                Thread.Sleep(length);
                return;
            }

            try
            {
                if (OperatingSystem.IsWindows())
                    Console.Beep((int) Math.Clamp(frequency, 37d, 32767d), length);
            }
            catch (PlatformNotSupportedException)
            {
                // No speaker to talk to, which is not worth stopping a program over.
            }
        }

        /// <inheritdoc />
        public void SetScreenMode(int mode)
        {
            if (mode == 0)
            {
                _pixels = null;
                Clear();
                return;
            }

            var size = SizeOf(mode);
            if (size.Width == 0)
                throw new BasicError("Unsupported screen mode " + mode);

            _pixels = new PixelBuffer(size.Width, size.Height);
            _pixels.Fill(_pixelPalette[0]);

            HasOutput = true;
            LastX = 0;
            LastY = 0;
        }

        /// <inheritdoc />
        public void Plot(int x, int y, int color)
        {
            Remember(x, y);

            // Clipped rather than refused, which is what the machines did: a program that draws off the edge of the
            // screen loses the part that is off it and carries on.
            if (_pixels == null || x < 0 || y < 0 || x >= _pixels.Width || y >= _pixels.Height)
                return;

            _pixels.SetPixel(x, y, Ink(color));
        }

        /// <inheritdoc />
        public void DrawLine(int x0, int y0, int x1, int y1, int color, string box)
        {
            Remember(x1, y1);

            if (_pixels == null)
                return;

            var ink = Ink(color);

            if (string.Equals(box, "BF", StringComparison.Ordinal))
            {
                var left = Math.Min(x0, x1);
                var top = Math.Min(y0, y1);

                _pixels.Fill(left, top, Math.Abs(x1 - x0) + 1, Math.Abs(y1 - y0) + 1, ink);
                return;
            }

            if (string.Equals(box, "B", StringComparison.Ordinal))
            {
                _pixels.DrawLine(x0, y0, x1, y0, ink);
                _pixels.DrawLine(x1, y0, x1, y1, ink);
                _pixels.DrawLine(x1, y1, x0, y1, ink);
                _pixels.DrawLine(x0, y1, x0, y0, ink);
                return;
            }

            _pixels.DrawLine(x0, y0, x1, y1, ink);
        }

        /// <inheritdoc />
        public void DrawCircle(int x, int y, int radius, int color)
        {
            Remember(x, y);

            if (_pixels == null || radius < 0)
                return;

            var ink = Ink(color);

            if (radius == 0)
            {
                Plot(x, y, color);
                return;
            }

            // A midpoint circle, with the mirrored points skipped where two octants meet. The library's own
            // DrawDisc keeps a single-visit rule for the same reason and outline circles were left out of it
            // precisely because of these seams: without the guards the four axis points and both diagonals are
            // plotted twice, which is invisible in an opaque colour and wrong the moment one is not.
            var dx = radius;
            var dy = 0;
            var error = 1 - radius;

            while (dx >= dy)
            {
                PlotOctants(x, y, dx, dy, ink);
                dy++;

                if (error < 0)
                {
                    error += 2 * dy + 1;
                    continue;
                }

                dx--;
                error += 2 * (dy - dx) + 1;
            }
        }

        /// <inheritdoc />
        public void Paint(int x, int y, int fill, int border)
        {
            if (_pixels == null || x < 0 || y < 0 || x >= _pixels.Width || y >= _pixels.Height)
                return;

            var ink = Ink(fill);

            // A border of its own colour is what BASIC means by leaving it out: flood until the fill colour is met,
            // which is also what stops this running forever once an area is done.
            var edge = border < 0 ? ink : Ink(border);

            var pending = new Stack<(int X, int Y)>();
            pending.Push((x, y));

            while (pending.Count > 0)
            {
                var (px, py) = pending.Pop();

                if (px < 0 || py < 0 || px >= _pixels.Width || py >= _pixels.Height)
                    continue;

                var here = _pixels.GetPixel(px, py);
                if (Same(here, edge) || Same(here, ink))
                    continue;

                _pixels.SetPixel(px, py, ink);

                pending.Push((px + 1, py));
                pending.Push((px - 1, py));
                pending.Push((px, py + 1));
                pending.Push((px, py - 1));
            }
        }

        /// <summary>Where the last drawing statement finished, which is what LINE with no first point means.</summary>
        public int LastX { get; private set; }

        /// <summary>Where the last drawing statement finished, down the screen.</summary>
        public int LastY { get; private set; }

        /// <summary>Plots the eight mirrored points of a circle, skipping the ones two octants share.</summary>
        private void PlotOctants(int cx, int cy, int dx, int dy, Rgba32 ink)
        {
            // Mirrored across the vertical, then across the horizontal, and each mirror skipped when it would
            // land on the point it was mirroring. Getting this wrong the obvious way skips the top and bottom of
            // the circle entirely: at dy of zero the second group is not a repeat of the first, it IS the vertical
            // pair, and only the mirror WITHIN each group collapses.
            Put(cx + dx, cy + dy, ink);

            if (dx != 0)
                Put(cx - dx, cy + dy, ink);

            if (dy != 0)
            {
                Put(cx + dx, cy - dy, ink);

                if (dx != 0)
                    Put(cx - dx, cy - dy, ink);
            }

            // Only when the two are equal is the swapped group genuinely the same eight points.
            if (dx == dy)
                return;

            Put(cx + dy, cy + dx, ink);

            if (dy != 0)
                Put(cx - dy, cy + dx, ink);

            if (dx != 0)
            {
                Put(cx + dy, cy - dx, ink);

                if (dy != 0)
                    Put(cx - dy, cy - dx, ink);
            }
        }

        /// <summary>
        ///     Which colour number a pixel holds, or -1 for one that is off the screen or holds no palette colour.
        ///     A seam for tests: what a drawing statement actually put on the screen cannot be asked of the
        ///     rendered text, which is escape sequences and half blocks by then.
        /// </summary>
        /// <param name="x">Across.</param>
        /// <param name="y">Down.</param>
        /// <returns>The colour number.</returns>
        public int ColorAt(int x, int y)
        {
            if (_pixels == null || x < 0 || y < 0 || x >= _pixels.Width || y >= _pixels.Height)
                return -1;

            var pixel = _pixels.GetPixel(x, y);

            for (var i = 0; i < _pixelPalette.Length; i++)
            {
                if (Same(pixel, _pixelPalette[i]))
                    return i;
            }

            return -1;
        }

        /// <summary>Sets a pixel if it is on the screen at all.</summary>
        private void Put(int x, int y, Rgba32 ink)
        {
            if (x >= 0 && y >= 0 && x < _pixels.Width && y < _pixels.Height)
                _pixels.SetPixel(x, y, ink);
        }

        /// <summary>Remembers where drawing finished.</summary>
        private void Remember(int x, int y)
        {
            LastX = x;
            LastY = y;
        }

        /// <summary>The pixels a colour number means, falling back to what COLOR last set.</summary>
        private Rgba32 Ink(int color)
        {
            var index = color < 0 ? _drawColor : color;
            return _pixelPalette[Math.Clamp(index, 0, _pixelPalette.Length - 1)];
        }

        /// <summary>Whether two colours are the same, which a flood fill asks about constantly.</summary>
        private static bool Same(Rgba32 left, Rgba32 right)
        {
            return left.R == right.R && left.G == right.G && left.B == right.B && left.A == right.A;
        }

        /// <summary>
        ///     How big each screen mode is. The numbers are the ones the hardware had, because a program's
        ///     coordinates only mean anything against them.
        /// </summary>
        private static (int Width, int Height) SizeOf(int mode)
        {
            return mode switch
            {
                1 or 7 or 13 => (320, 200),
                2 or 8 => (640, 200),
                9 => (640, 350),
                11 or 12 => (640, 480),
                _ => (0, 0)
            };
        }

        /// <summary>The key a running program will find next time it asks, which the screen above sets.</summary>
        public string PendingKey { get; set; } = string.Empty;

        /// <summary>What a waiting INPUT will be given, or null while nothing has been typed.</summary>
        private string _answer;

        /// <summary>How many times the program has asked for a noise.</summary>
        public int Beeps { get; private set; }

        /// <summary>Moves down a row, scrolling the whole screen when there is nowhere further to go.</summary>
        private void NextRow()
        {
            if (_row + 1 < _grid.Height)
            {
                _row++;
                return;
            }

            Scroll();
        }

        /// <summary>
        ///     Moves everything up one row and blanks the last, which is what a screen does when a program writes
        ///     past the bottom of it. Copied cell by cell because a grid is a rectangle of cells and there is no
        ///     cheaper honest way to move them.
        /// </summary>
        private void Scroll()
        {
            for (var y = 1; y < _grid.Height; y++)
            {
                for (var x = 0; x < _grid.Width; x++)
                    _grid.Set(x, y - 1, _grid.GlyphAt(x, y), _grid.StyleAt(x, y));
            }

            _grid.Fill(0, _grid.Height - 1, _grid.Width, 1, ' ', _style);
        }

        /// <summary>Something drawable in place of a character a terminal would act on.</summary>
        private static char ControlPictureOf(char character)
        {
            return char.IsControl(character) ? Documents.ControlPictures.For(character) : character;
        }
    }
}
