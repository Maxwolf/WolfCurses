// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/20/2026

using System;
using System.Globalization;

namespace WolfCurses.Graphics
{
    /// <summary>
    ///     One color a widget can paint text with, in either of the two forms a terminal understands: a
    ///     <em>named</em> <see cref="ConsoleColor" /> that the user's own theme decides the shade of, or an
    ///     <em>exact</em> <see cref="Rgb24" /> triple the terminal is told to reproduce.
    ///     <para>
    ///         Both forms are kept because they answer different questions. A named color is the polite one — a
    ///         progress bar asking for <see cref="ConsoleColor.Green" /> comes out in whatever green the user chose
    ///         for their terminal, so it sits with the rest of their palette and stays legible against their
    ///         background. An exact color is the precise one — a flag stripe or a heat ramp means a particular shade
    ///         and would be wrong in someone else's green. Neither is a good default for the other's job, so the
    ///         struct carries which one it is rather than converting one into the other.
    ///     </para>
    ///     <para>
    ///         <see cref="Rgb" /> is populated for both forms: for a named color it is the canonical shade from the
    ///         legacy console palette, which is what the ramp arithmetic and the grayscale downgrade need to work
    ///         with. It is a stand-in for the user's actual theme, which nothing can read, so treat it as "roughly
    ///         what this name looks like" and not as what will appear on screen.
    ///     </para>
    /// </summary>
    public readonly struct TextColor : IEquatable<TextColor>
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="TextColor" /> struct holding an exact 24-bit color.
        /// </summary>
        /// <param name="rgb">The exact color to reproduce.</param>
        public TextColor(Rgb24 rgb)
        {
            IsNamed = false;
            Name = default;
            Rgb = rgb;
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="TextColor" /> struct holding a theme-respecting named
        ///     console color.
        /// </summary>
        /// <param name="name">The named color; the terminal's own theme decides the shade.</param>
        public TextColor(ConsoleColor name)
        {
            IsNamed = true;
            Name = name;
            Rgb = CanonicalRgb(name);
        }

        /// <summary>
        ///     True when this is a named <see cref="ConsoleColor" /> (the terminal picks the shade), false when it is
        ///     an exact <see cref="Rgb" /> value.
        /// </summary>
        public bool IsNamed { get; }

        /// <summary>The named color. Only meaningful while <see cref="IsNamed" /> is true.</summary>
        public ConsoleColor Name { get; }

        /// <summary>
        ///     The exact color when <see cref="IsNamed" /> is false, or the canonical shade of <see cref="Name" />
        ///     when it is true. Always populated, so ramp math and the grayscale downgrade never have to branch.
        /// </summary>
        public Rgb24 Rgb { get; }

        /// <summary>Wraps an exact color, so a call site can pass an <see cref="Rgb24" /> wherever a color is wanted.</summary>
        /// <param name="rgb">The exact color to reproduce.</param>
        public static implicit operator TextColor(Rgb24 rgb)
        {
            return new TextColor(rgb);
        }

        /// <summary>Wraps a named color, so a call site can pass a <see cref="ConsoleColor" /> wherever a color is wanted.</summary>
        /// <param name="name">The named color; the terminal's own theme decides the shade.</param>
        public static implicit operator TextColor(ConsoleColor name)
        {
            return new TextColor(name);
        }

        /// <summary>Whether two colors are the same kind and the same value.</summary>
        public static bool operator ==(TextColor left, TextColor right)
        {
            return left.Equals(right);
        }

        /// <summary>Whether two colors differ in kind or in value.</summary>
        public static bool operator !=(TextColor left, TextColor right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        ///     Whether this is the same color as another. A named color and an exact color are never equal even when
        ///     their shades coincide, because they will not render the same way — the named one follows the theme.
        /// </summary>
        /// <param name="other">The color to compare against.</param>
        public bool Equals(TextColor other)
        {
            if (IsNamed != other.IsNamed)
                return false;

            return IsNamed
                ? Name == other.Name
                : Rgb.R == other.Rgb.R && Rgb.G == other.Rgb.G && Rgb.B == other.Rgb.B;
        }

        /// <summary>Whether this is the same color as another object.</summary>
        /// <param name="obj">The object to compare against.</param>
        public override bool Equals(object obj)
        {
            return obj is TextColor other && Equals(other);
        }

        /// <summary>A hash consistent with <see cref="Equals(TextColor)" />.</summary>
        public override int GetHashCode()
        {
            return IsNamed
                ? HashCode.Combine(true, (int) Name)
                : HashCode.Combine(false, Rgb.R, Rgb.G, Rgb.B);
        }

        /// <summary>A short description of the color, for debugging and test failure messages.</summary>
        public override string ToString()
        {
            return IsNamed
                ? Name.ToString()
                : string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}", Rgb.R, Rgb.G, Rgb.B);
        }

        /// <summary>
        ///     The SGR parameter body that sets this color as the foreground — <c>"32"</c>, <c>"38;5;120"</c> or
        ///     <c>"38;2;255;0;0"</c> depending on the mode and the kind of color. Deliberately <em>not</em> a
        ///     complete escape sequence: <see cref="TextStyle" /> joins several of these with <c>';'</c> into one
        ///     <c>ESC[...m</c>, which is both fewer bytes on the wire and one fewer thing for a terminal to
        ///     mis-parse than a run of adjacent escapes.
        /// </summary>
        /// <param name="mode">
        ///     The resolved color mode. <see cref="AnsiColorModeEnum.Auto" /> is resolved here as a courtesy;
        ///     <see cref="AnsiColorModeEnum.None" /> returns an empty body, though callers normally short-circuit
        ///     long before asking.
        /// </param>
        /// <returns>The parameter body, without <c>ESC[</c> or the trailing <c>m</c>.</returns>
        internal string ForegroundSequence(AnsiColorModeEnum mode)
        {
            var resolved = Resolve(mode);
            switch (resolved)
            {
                case AnsiColorModeEnum.None:
                    return string.Empty;

                case AnsiColorModeEnum.Grayscale:
                    // Grayscale means "the palette restricted to gray shades", so a named color is downgraded
                    // through its canonical shade rather than being allowed to sneak real color past the mode.
                    return "38;5;" + Index(Ansi256.GrayFromRgb(Rgb.R, Rgb.G, Rgb.B));

                case AnsiColorModeEnum.Palette256:
                    return IsNamed
                        ? Index(NamedForegroundCode(Name))
                        : "38;5;" + Index(Ansi256.FromRgb(Rgb.R, Rgb.G, Rgb.B));

                default: // TrueColor
                    return IsNamed
                        ? Index(NamedForegroundCode(Name))
                        : "38;2;" + Index(Rgb.R) + ";" + Index(Rgb.G) + ";" + Index(Rgb.B);
            }
        }

        /// <summary>
        ///     The SGR parameter body that sets this color as the background — the foreground body with the codes
        ///     shifted into the background range (named colors are foreground code plus ten, indexed and true colors
        ///     swap the leading <c>38</c> for <c>48</c>).
        /// </summary>
        /// <param name="mode">
        ///     The resolved color mode. <see cref="AnsiColorModeEnum.Auto" /> is resolved here as a courtesy;
        ///     <see cref="AnsiColorModeEnum.None" /> returns an empty body.
        /// </param>
        /// <returns>The parameter body, without <c>ESC[</c> or the trailing <c>m</c>.</returns>
        internal string BackgroundSequence(AnsiColorModeEnum mode)
        {
            var resolved = Resolve(mode);
            switch (resolved)
            {
                case AnsiColorModeEnum.None:
                    return string.Empty;

                case AnsiColorModeEnum.Grayscale:
                    return "48;5;" + Index(Ansi256.GrayFromRgb(Rgb.R, Rgb.G, Rgb.B));

                case AnsiColorModeEnum.Palette256:
                    return IsNamed
                        ? Index(NamedForegroundCode(Name) + 10)
                        : "48;5;" + Index(Ansi256.FromRgb(Rgb.R, Rgb.G, Rgb.B));

                default: // TrueColor
                    return IsNamed
                        ? Index(NamedForegroundCode(Name) + 10)
                        : "48;2;" + Index(Rgb.R) + ";" + Index(Rgb.G) + ";" + Index(Rgb.B);
            }
        }

        /// <summary>Turns <see cref="AnsiColorModeEnum.Auto" /> into whatever the environment supports.</summary>
        private static AnsiColorModeEnum Resolve(AnsiColorModeEnum mode)
        {
            return mode == AnsiColorModeEnum.Auto ? AnsiConsole.DetectColorMode() : mode;
        }

        /// <summary>Formats a number for an escape sequence, never at the mercy of the ambient culture.</summary>
        private static string Index(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        ///     The SGR foreground code for a named console color.
        ///     <para>
        ///         <b>This table is the classic trap and cannot be computed from the enum's numeric value.</b>
        ///         <see cref="ConsoleColor" /> orders its members blue-green-cyan-red (a DOS/CGA legacy where the
        ///         bits read blue-green-red), while ANSI orders them red-green-yellow-blue. The two agree on black,
        ///         green, cyan-vs-magenta by accident in places and disagree loudly elsewhere: blue and red are
        ///         swapped, and so are cyan and yellow. Anything clever here produces a bar that is red when it
        ///         claims to be blue, and it will look plausible enough in a screenshot to survive review.
        ///     </para>
        ///     <para>
        ///         The bright half (<c>90</c>-<c>97</c>) is the "aixterm" range, universally supported and far more
        ///         reliable than the alternative of emitting bold plus a dim code — bold is a font weight on a lot of
        ///         terminals now, not a brightener, and this struct lets the caller ask for bold separately anyway.
        ///     </para>
        /// </summary>
        /// <param name="name">The named color.</param>
        /// <returns>The SGR foreground code; add ten for the matching background code.</returns>
        private static int NamedForegroundCode(ConsoleColor name)
        {
            switch (name)
            {
                case ConsoleColor.Black: return 30;
                case ConsoleColor.DarkBlue: return 34;
                case ConsoleColor.DarkGreen: return 32;
                case ConsoleColor.DarkCyan: return 36;
                case ConsoleColor.DarkRed: return 31;
                case ConsoleColor.DarkMagenta: return 35;
                case ConsoleColor.DarkYellow: return 33;
                case ConsoleColor.Gray: return 37;
                case ConsoleColor.DarkGray: return 90;
                case ConsoleColor.Blue: return 94;
                case ConsoleColor.Green: return 92;
                case ConsoleColor.Cyan: return 96;
                case ConsoleColor.Red: return 91;
                case ConsoleColor.Magenta: return 95;
                case ConsoleColor.Yellow: return 93;
                case ConsoleColor.White: return 97;

                // A value cast in from outside the enum's range. 39 is "default foreground" (49 background), which
                // is the honest answer to "some color we do not know" and never paints an unreadable cell.
                default: return 39;
            }
        }

        /// <summary>
        ///     The canonical shade of a named console color.
        ///     <para>
        ///         Source: the legacy Windows console palette, which is byte-for-byte the same set as indices 0-15 of
        ///         the xterm 256-color palette (0/128/192/255 on each axis). It is only ever a stand-in — the real
        ///         shade is whatever the user's terminal theme says, and nothing here can read that — but it is the
        ///         shade both platforms ship with, so it is the least wrong guess available for the grayscale
        ///         downgrade and for ramp arithmetic that starts from a named color.
        ///     </para>
        /// </summary>
        /// <param name="name">The named color.</param>
        /// <returns>The canonical 24-bit shade.</returns>
        private static Rgb24 CanonicalRgb(ConsoleColor name)
        {
            switch (name)
            {
                case ConsoleColor.Black: return new Rgb24(0, 0, 0);
                case ConsoleColor.DarkBlue: return new Rgb24(0, 0, 128);
                case ConsoleColor.DarkGreen: return new Rgb24(0, 128, 0);
                case ConsoleColor.DarkCyan: return new Rgb24(0, 128, 128);
                case ConsoleColor.DarkRed: return new Rgb24(128, 0, 0);
                case ConsoleColor.DarkMagenta: return new Rgb24(128, 0, 128);
                case ConsoleColor.DarkYellow: return new Rgb24(128, 128, 0);
                case ConsoleColor.Gray: return new Rgb24(192, 192, 192);
                case ConsoleColor.DarkGray: return new Rgb24(128, 128, 128);
                case ConsoleColor.Blue: return new Rgb24(0, 0, 255);
                case ConsoleColor.Green: return new Rgb24(0, 255, 0);
                case ConsoleColor.Cyan: return new Rgb24(0, 255, 255);
                case ConsoleColor.Red: return new Rgb24(255, 0, 0);
                case ConsoleColor.Magenta: return new Rgb24(255, 0, 255);
                case ConsoleColor.Yellow: return new Rgb24(255, 255, 0);
                case ConsoleColor.White: return new Rgb24(255, 255, 255);

                // Out-of-range cast: mid gray, which downgrades to a middling shade rather than to invisible black.
                default: return new Rgb24(128, 128, 128);
            }
        }
    }
}
