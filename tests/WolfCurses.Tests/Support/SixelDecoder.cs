using System;
using System.Collections.Generic;
using System.Globalization;
using WolfCurses.Graphics;

namespace WolfCurses.Tests.Support
{
    /// <summary>
    ///     A minimal sixel reader used only by tests, to check that what
    ///     <see cref="WolfCurses.Graphics.SixelImageRenderer" /> writes really does describe the picture it was given.
    ///     Unit tests pin the encoder's output character by character, which proves it emits what was intended; this
    ///     proves what was intended actually reconstructs, catching whole-format mistakes — a wrong bit order, bands
    ///     stacked in the wrong direction, a run-length count off by one, one color's pass wiping another's — that
    ///     pinning cannot see.
    ///     <para>
    ///         It understands only the subset the renderer emits and is deliberately strict: anything unexpected throws
    ///         rather than being skipped, so a malformed sequence fails the test instead of quietly decoding to
    ///         something plausible.
    ///     </para>
    /// </summary>
    internal static class SixelDecoder
    {
        private const char Escape = (char) 27;

        /// <summary>Decodes a complete sixel sequence into straight-alpha RGBA pixels.</summary>
        /// <param name="payload">A sixel sequence, starting at its DCS introducer and ending with the terminator.</param>
        /// <returns>
        ///     The reconstructed picture. Pixels the sequence never draws are left fully transparent, which is exactly
        ///     what a terminal shows for them.
        /// </returns>
        public static PixelBuffer Decode(string payload)
        {
            var reader = new Reader(payload);

            reader.Expect(Escape);
            reader.Expect('P');
            reader.ReadWhile(c => c != 'q'); // Aspect/background/grid parameters: not needed to reconstruct.
            reader.Expect('q');

            var (width, height) = ReadRasterAttributes(reader);
            var image = new PixelBuffer(width, height);
            var palette = new Dictionary<int, Rgb24>();

            var color = 0;
            var x = 0;
            var bandTop = 0;

            while (!reader.AtEnd)
            {
                var c = reader.Peek();

                if (c == Escape)
                {
                    reader.Expect(Escape);
                    reader.Expect('\\');
                    break;
                }

                switch (c)
                {
                    case '#':
                        reader.Expect('#');
                        color = reader.ReadInt();
                        if (reader.TryTake(';'))
                            palette[color] = ReadColor(reader);

                        // Selecting a color always restarts the band at the left margin in this encoder's output.
                        x = 0;
                        break;

                    case '$': // Graphics carriage return: back to the left margin, same band, for the next color.
                        reader.Expect('$');
                        x = 0;
                        break;

                    case '-': // Graphics newline: down to the next band.
                        reader.Expect('-');
                        bandTop += 6;
                        x = 0;
                        break;

                    case '!': // Run length: the next data character repeats this many times.
                        reader.Expect('!');
                        var run = reader.ReadInt();
                        x = DrawRun(image, palette, color, reader.ReadData(), run, x, bandTop);
                        break;

                    default:
                        x = DrawRun(image, palette, color, reader.ReadData(), 1, x, bandTop);
                        break;
                }
            }

            return image;
        }

        /// <summary>Reads the optional <c>"Pan;Pad;Ph;Pv</c> raster attributes that give the picture's pixel size.</summary>
        private static (int Width, int Height) ReadRasterAttributes(Reader reader)
        {
            if (!reader.TryTake('"'))
                throw new FormatException("Expected raster attributes; the renderer always emits them.");

            reader.ReadInt(); // Pan: aspect numerator.
            reader.Expect(';');
            reader.ReadInt(); // Pad: aspect denominator.
            reader.Expect(';');
            var width = reader.ReadInt();
            reader.Expect(';');
            var height = reader.ReadInt();
            return (width, height);
        }

        /// <summary>Reads a <c>;2;r;g;b</c> color definition, converting sixel's 0-100 scale back to bytes.</summary>
        private static Rgb24 ReadColor(Reader reader)
        {
            var space = reader.ReadInt();
            if (space != 2)
                throw new FormatException($"Expected an RGB color definition (2), got {space}.");

            reader.Expect(';');
            var r = reader.ReadInt();
            reader.Expect(';');
            var g = reader.ReadInt();
            reader.Expect(';');
            var b = reader.ReadInt();

            return new Rgb24(FromPercent(r), FromPercent(g), FromPercent(b));
        }

        /// <summary>Paints one repeated data character: each of its six bits is a pixel of the current color.</summary>
        private static int DrawRun(PixelBuffer image, Dictionary<int, Rgb24> palette, int color, int mask, int run,
            int x, int bandTop)
        {
            for (var i = 0; i < run; i++, x++)
            {
                for (var row = 0; row < 6; row++)
                {
                    if ((mask & (1 << row)) == 0)
                        continue;

                    var y = bandTop + row;
                    if (x >= image.Width || y >= image.Height)
                        continue; // A band may overhang the bottom of a picture whose height is not a multiple of six.

                    if (!palette.TryGetValue(color, out var rgb))
                        throw new FormatException($"Color {color} was selected before it was defined.");

                    image.SetPixel(x, y, new Rgba32(rgb.R, rgb.G, rgb.B, 255));
                }
            }

            return x;
        }

        private static byte FromPercent(int percent)
        {
            return (byte) Math.Round(percent * 255.0 / 100.0, MidpointRounding.AwayFromZero);
        }

        /// <summary>A tiny cursor over the sequence, so the parser above reads as the grammar it implements.</summary>
        private sealed class Reader
        {
            private readonly string _text;
            private int _index;

            public Reader(string text)
            {
                _text = text;
            }

            public bool AtEnd => _index >= _text.Length;

            public char Peek()
            {
                return _text[_index];
            }

            public void Expect(char expected)
            {
                if (AtEnd || _text[_index] != expected)
                    throw new FormatException(
                        $"Expected '{expected}' at offset {_index}, found '{(AtEnd ? "<end>" : _text[_index].ToString())}'.");
                _index++;
            }

            public bool TryTake(char candidate)
            {
                if (AtEnd || _text[_index] != candidate)
                    return false;
                _index++;
                return true;
            }

            public void ReadWhile(Func<char, bool> predicate)
            {
                while (!AtEnd && predicate(_text[_index]))
                    _index++;
            }

            public int ReadInt()
            {
                var start = _index;
                ReadWhile(char.IsDigit);
                if (start == _index)
                    throw new FormatException($"Expected a number at offset {start}.");
                return int.Parse(_text.Substring(start, _index - start), CultureInfo.InvariantCulture);
            }

            /// <summary>Reads one sixel data character and returns its six-pixel bitmask.</summary>
            public int ReadData()
            {
                var c = _text[_index];
                if (c < '?' || c > '~')
                    throw new FormatException($"Expected a sixel data character at offset {_index}, found '{c}'.");
                _index++;
                return c - '?';
            }
        }
    }
}
