// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
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

        /// <summary>The cells.</summary>
        private readonly TextGrid _grid;

        /// <summary>Where the next character goes, counting from zero.</summary>
        private int _column;

        /// <summary>Which row the next character goes on, counting from zero.</summary>
        private int _row;

        /// <summary>How later writing is painted.</summary>
        private TextStyle _style;

        /// <summary>Initializes a new instance of the <see cref="BasicScreen" /> class.</summary>
        /// <param name="width">How many columns.</param>
        /// <param name="height">How many rows.</param>
        public BasicScreen(int width, int height)
        {
            _grid = new TextGrid(Math.Max(1, width), Math.Max(1, height));
            _style = new TextStyle(ConsoleColor.Gray, ConsoleColor.Black);

            Clear();
        }

        /// <summary>How many columns it has.</summary>
        public int Width => _grid.Width;

        /// <summary>How many rows it has.</summary>
        public int Height => _grid.Height;

        /// <summary>Whether a program has put anything on it since it was last cleared.</summary>
        public bool HasOutput { get; private set; }

        /// <summary>The screen as text, ready to be drawn.</summary>
        /// <returns>The rows, newline separated.</returns>
        public string Render()
        {
            return _grid.Render();
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
        }

        /// <inheritdoc />
        public string ReadLine(string prompt)
        {
            // Not yet: INPUT has to stop the program in the middle of a statement and wait, and the screen runs the
            // program in bounded slices so that it can stay alive. Answering with nothing is the honest stand-in
            // until that is built, and it is why the shipped samples do not ask questions.
            Write(prompt);
            WriteLine();

            return string.Empty;
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

        /// <summary>The key a running program will find next time it asks, which the screen above sets.</summary>
        public string PendingKey { get; set; } = string.Empty;

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
