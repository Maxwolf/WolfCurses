// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/20/2026

using System;
using System.Text;

namespace WolfCurses.Graphics
{
    /// <summary>
    ///     How a run of text should look: an optional foreground color, an optional background color, and whether it
    ///     is bold. This is what the <see cref="WolfCurses.Window.Control" /> widgets expose so a progress bar, a
    ///     chart or a box border can be colored without any of them growing their own idea of what a color is.
    ///     <para>
    ///         <b>The empty style is the whole compatibility story.</b> <see cref="None" /> is
    ///         <c>default(TextStyle)</c>, which is what every widget's new style properties start as, and every
    ///         method here returns its input completely untouched for an empty style — <em>no</em> escape, not even
    ///         a reset. So a widget that nobody has colored emits byte-for-byte what it emitted before this type
    ///         existed, and the existing pinned tests keep passing without being edited. Any implementation that
    ///         "helpfully" appends a trailing reset breaks that, so it does not.
    ///     </para>
    ///     <para>
    ///         The second rule is the same stance <see cref="WolfCurses.Window.Control.ListNavigator.Emphasize" />
    ///         takes: a resolved mode of <see cref="AnsiColorModeEnum.None" /> emits nothing at all, even for a
    ///         style the caller explicitly set, and even for bold. Bold is an attribute rather than a color, but
    ///         someone who set <c>NO_COLOR</c> asked for no escape sequences, not for a subset of them.
    ///     </para>
    ///     <para>
    ///         Colors go out as one escape with every parameter joined by <c>';'</c> — bold plus a red foreground on
    ///         white in true color is a single <c>ESC[1;38;2;255;0;0;48;2;255;255;255m</c>. Widgets that color cell
    ///         by cell use <see cref="OpenSequence" /> directly to coalesce equal neighbours into one run instead of
    ///         wrapping every single character, which is the difference between a rainbow bar costing a few dozen
    ///         bytes and costing a few hundred.
    ///     </para>
    /// </summary>
    public readonly struct TextStyle : IEquatable<TextStyle>
    {
        /// <summary>The ASCII escape control character (0x1B) that begins every ANSI control sequence.</summary>
        private const char Escape = (char) 27;

        /// <summary>
        ///     The style that changes nothing. Identical to <c>default(TextStyle)</c>, which is deliberate: it is
        ///     what an untouched widget property holds, so "nobody colored this" and "explicitly no color" are the
        ///     same value and take the same do-nothing path.
        /// </summary>
        public static readonly TextStyle None = default;

        /// <summary>
        ///     Initializes a new instance of the <see cref="TextStyle" /> struct.
        /// </summary>
        /// <param name="foreground">The text color, or null to leave the terminal's foreground alone.</param>
        /// <param name="background">The color behind the text, or null to leave the terminal's background alone.</param>
        /// <param name="bold">Whether to request bold (SGR 1).</param>
        public TextStyle(TextColor? foreground = null, TextColor? background = null, bool bold = false)
        {
            Foreground = foreground;
            Background = background;
            Bold = bold;
        }

        /// <summary>The text color, or null to leave the terminal's foreground alone.</summary>
        public TextColor? Foreground { get; }

        /// <summary>The color behind the text, or null to leave the terminal's background alone.</summary>
        public TextColor? Background { get; }

        /// <summary>Whether bold (SGR 1) is requested. Emitted in every mode except <see cref="AnsiColorModeEnum.None" />.</summary>
        public bool Bold { get; }

        /// <summary>
        ///     True when this style asks for nothing at all — no foreground, no background, not bold. The fast path
        ///     every widget checks before it does any work: an empty style is a guaranteed no-op.
        /// </summary>
        public bool IsEmpty => !Foreground.HasValue && !Background.HasValue && !Bold;

        /// <summary>
        ///     The escape sequence that closes any style this type opened. Public because a caller composing its own
        ///     runs from <see cref="OpenSequence" /> needs something to close them with, and because a reset must be
        ///     the <em>same</em> reset the widgets use or the two will not cancel cleanly when interleaved.
        /// </summary>
        public static string ResetSequence => Escape + "[0m";

        /// <summary>Builds a style whose foreground is a named console color, so <c>bar.FilledStyle = ConsoleColor.Green</c> compiles.</summary>
        /// <param name="foreground">The named foreground color.</param>
        public static implicit operator TextStyle(ConsoleColor foreground)
        {
            return new TextStyle(new TextColor(foreground));
        }

        /// <summary>Builds a style whose foreground is an exact color, so <c>bar.FilledStyle = new Rgb24(...)</c> compiles.</summary>
        /// <param name="foreground">The exact foreground color.</param>
        public static implicit operator TextStyle(Rgb24 foreground)
        {
            return new TextStyle(new TextColor(foreground));
        }

        /// <summary>
        ///     Builds a style whose foreground is the given color.
        ///     <para>
        ///         All three of these operators are spelled out rather than leaning on
        ///         <see cref="TextColor" />'s own conversions, because <b>C# never chains two user-defined implicit
        ///         conversions</b>. Without the <see cref="ConsoleColor" /> and <see cref="Rgb24" /> overloads
        ///         directly on this type, the obvious <c>bar.FilledStyle = ConsoleColor.Green;</c> simply fails to
        ///         compile even though both halves of the trip exist.
        ///     </para>
        /// </summary>
        /// <param name="foreground">The foreground color.</param>
        public static implicit operator TextStyle(TextColor foreground)
        {
            return new TextStyle(foreground);
        }

        /// <summary>Whether two styles ask for exactly the same thing.</summary>
        public static bool operator ==(TextStyle left, TextStyle right)
        {
            return left.Equals(right);
        }

        /// <summary>Whether two styles differ in any way.</summary>
        public static bool operator !=(TextStyle left, TextStyle right)
        {
            return !left.Equals(right);
        }

        /// <summary>Returns a copy of this style with a different foreground.</summary>
        /// <param name="foreground">The new foreground color, or null to leave the terminal's foreground alone.</param>
        public TextStyle WithForeground(TextColor? foreground)
        {
            return new TextStyle(foreground, Background, Bold);
        }

        /// <summary>Returns a copy of this style with a different background.</summary>
        /// <param name="background">The new background color, or null to leave the terminal's background alone.</param>
        public TextStyle WithBackground(TextColor? background)
        {
            return new TextStyle(Foreground, background, Bold);
        }

        /// <summary>Returns a copy of this style with bold turned on or off.</summary>
        /// <param name="bold">Whether to request bold.</param>
        public TextStyle WithBold(bool bold)
        {
            return new TextStyle(Foreground, Background, bold);
        }

        /// <summary>
        ///     Wraps text in this style and closes it again.
        ///     <para>
        ///         Returns <paramref name="text" /> completely untouched — the same reference, no escapes appended or
        ///         prepended — when the style is empty, when the resolved mode is
        ///         <see cref="AnsiColorModeEnum.None" />, or when the text itself is null or empty. That last case
        ///         matters more than it looks: widgets routinely draw zero-length runs (a progress bar at 0% has an
        ///         empty filled run, a bar chart row for a negative value has an empty bar), and an open/close pair
        ///         around nothing would land escapes between two spaces that the pinned layout tests measure.
        ///     </para>
        /// </summary>
        /// <param name="text">The text to style.</param>
        /// <param name="mode">The color mode; <see cref="AnsiColorModeEnum.Auto" /> asks the environment.</param>
        /// <returns>The styled text, or the input unchanged when nothing would be emitted.</returns>
        public string Apply(string text, AnsiColorModeEnum mode = AnsiColorModeEnum.Auto)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var open = OpenSequence(mode);
            return open.Length == 0 ? text : open + text + ResetSequence;
        }

        /// <summary>
        ///     Wraps a run of one repeated character in this style. The convenience form of
        ///     <see cref="Apply(string, AnsiColorModeEnum)" /> for widgets built out of runs of block glyphs, and it
        ///     builds the run only once it knows there is a run to build.
        /// </summary>
        /// <param name="glyph">The character to repeat.</param>
        /// <param name="count">How many times to repeat it; zero or less produces nothing at all.</param>
        /// <param name="mode">The color mode; <see cref="AnsiColorModeEnum.Auto" /> asks the environment.</param>
        /// <returns>The styled run, or an empty string when <paramref name="count" /> is not positive.</returns>
        public string Apply(char glyph, int count, AnsiColorModeEnum mode = AnsiColorModeEnum.Auto)
        {
            if (count <= 0)
                return string.Empty;

            return Apply(new string(glyph, count), mode);
        }

        /// <summary>
        ///     The single escape sequence that turns this style on, with every parameter joined by <c>';'</c> — bold
        ///     first, then foreground, then background. Close it with <see cref="ResetSequence" />.
        ///     <para>
        ///         This is the seam for widgets that color cell by cell: emit an open only where the style actually
        ///         changes from the previous cell and one reset at the end of the line, rather than wrapping every
        ///         character. Returns an empty string when the style is empty or the resolved mode is
        ///         <see cref="AnsiColorModeEnum.None" />, which doubles as the "should I bother?" test — an empty
        ///         open means there is nothing to close either.
        ///     </para>
        /// </summary>
        /// <param name="mode">The color mode; <see cref="AnsiColorModeEnum.Auto" /> asks the environment.</param>
        /// <returns>One <c>ESC[...m</c> sequence, or an empty string when nothing should be emitted.</returns>
        public string OpenSequence(AnsiColorModeEnum mode = AnsiColorModeEnum.Auto)
        {
            if (IsEmpty)
                return string.Empty;

            var resolved = mode == AnsiColorModeEnum.Auto ? AnsiConsole.DetectColorMode() : mode;
            if (resolved == AnsiColorModeEnum.None)
                return string.Empty;

            var sb = new StringBuilder(32);
            sb.Append(Escape).Append('[');

            var written = false;
            if (Bold)
            {
                sb.Append('1');
                written = true;
            }

            if (Foreground.HasValue)
            {
                var body = Foreground.Value.ForegroundSequence(resolved);
                if (body.Length > 0)
                {
                    if (written)
                        sb.Append(';');
                    sb.Append(body);
                    written = true;
                }
            }

            if (Background.HasValue)
            {
                var body = Background.Value.BackgroundSequence(resolved);
                if (body.Length > 0)
                {
                    if (written)
                        sb.Append(';');
                    sb.Append(body);
                    written = true;
                }
            }

            // Every parameter turned out to be nothing; "ESC[m" would be a reset, which is not what was asked for.
            if (!written)
                return string.Empty;

            sb.Append('m');
            return sb.ToString();
        }

        /// <summary>Whether this style asks for exactly the same thing as another.</summary>
        /// <param name="other">The style to compare against.</param>
        public bool Equals(TextStyle other)
        {
            // Nullable.Equals rather than Foreground.Equals(other.Foreground): the instance overload is
            // Nullable<T>.Equals(object), which boxes its argument on every call. The data widgets compare styles
            // per cell (Sparkline) and per column (LineGraph's palette dictionary) while rebuilding their string
            // every frame, so that boxing is real gen0 churn on the render path. The static form compares HasValue
            // and the underlying value through the non-boxing typed comparer, and the result is byte-for-byte the
            // same decision.
            return Nullable.Equals(Foreground, other.Foreground) &&
                   Nullable.Equals(Background, other.Background) &&
                   Bold == other.Bold;
        }

        /// <summary>Whether this style asks for exactly the same thing as another object.</summary>
        /// <param name="obj">The object to compare against.</param>
        public override bool Equals(object obj)
        {
            return obj is TextStyle other && Equals(other);
        }

        /// <summary>A hash consistent with <see cref="Equals(TextStyle)" />.</summary>
        public override int GetHashCode()
        {
            return HashCode.Combine(Foreground, Background, Bold);
        }

        /// <summary>A short description of the style, for debugging and test failure messages.</summary>
        public override string ToString()
        {
            if (IsEmpty)
                return "none";

            var sb = new StringBuilder(24);
            if (Bold)
                sb.Append("bold");
            if (Foreground.HasValue)
                sb.Append(sb.Length > 0 ? " fg=" : "fg=").Append(Foreground.Value);
            if (Background.HasValue)
                sb.Append(sb.Length > 0 ? " bg=" : "bg=").Append(Background.Value);
            return sb.ToString();
        }
    }
}
