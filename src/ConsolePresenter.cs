// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/15/2026

using System;
using System.IO;
using System.Text;
using WolfCurses.Graphics;

namespace WolfCurses
{
    /// <summary>
    ///     Writes <see cref="Core.SceneGraph" /> frames to the system console without flicker. Subscribe
    ///     <see cref="Present" /> to <see cref="Core.SceneGraph.ScreenBufferDirtyEvent" /> instead of clearing and
    ///     rewriting the console yourself.
    ///     <para>
    ///         Flicker comes from letting the terminal show an intermediate state: clearing the screen (or blanking a
    ///         row) before writing the new text makes the blank state visible for a moment, and writing the frame as
    ///         many small console calls lets the terminal repaint while the update is half done — which ANSI image
    ///         rows make very noticeable, because each one is a long string of escape sequences that takes a while to
    ///         rewrite. This class avoids both: it never blanks anything first (rows are overwritten in place and the
    ///         leftover tail of the old row is erased with the ANSI "erase to end of line" sequence), it only rewrites
    ///         the rows that actually changed since the previous frame, and it emits the whole update as one buffered
    ///         <see cref="Console.Out" /> write. The update is additionally wrapped in DEC private mode 2026
    ///         ("synchronized output") markers so terminals that understand them (Windows Terminal, iTerm2, kitty,
    ///         WezTerm, tmux, and others) apply it atomically; terminals that do not simply ignore the markers.
    ///     </para>
    ///     <para>
    ///         Everything is done with standard ANSI escape sequences over <see cref="Console.Out" />, so it works on
    ///         every platform .NET supports with a VT-capable terminal — which is the same requirement the ANSI image
    ///         feature already has, and which <see cref="AnsiConsole.Enable" /> switches on for Windows. On a console
    ///         where virtual-terminal processing cannot be enabled (for example a legacy Windows console host), the
    ///         presenter falls back to a plain-text, cursor-positioned overwrite that still avoids the blank-then-write
    ///         flash. Not thread-safe; call it from the same thread that pumps <see cref="SimulationApp.OnTick" />.
    ///     </para>
    /// </summary>
    public sealed class ConsolePresenter
    {
        /// <summary>The ASCII escape control character (0x1B) that begins every ANSI control sequence.</summary>
        private const char Escape = (char) 27;

        /// <summary>Control Sequence Introducer that begins every escape sequence this class emits.</summary>
        private static readonly string Csi = Escape + "[";

        /// <summary>Begin synchronized update (DEC 2026): buffer everything until the matching end marker.</summary>
        private static readonly string SyncBegin = Csi + "?2026h";

        /// <summary>End synchronized update (DEC 2026): apply the buffered update in one repaint.</summary>
        private static readonly string SyncEnd = Csi + "?2026l";

        /// <summary>Disable auto-wrap (DECAWM) so an over-long row clips at the right edge instead of corrupting the next row.</summary>
        private static readonly string WrapOff = Csi + "?7l";

        /// <summary>Re-enable auto-wrap (DECAWM), restoring the terminal's default behavior after the frame.</summary>
        private static readonly string WrapOn = Csi + "?7h";

        /// <summary>Reset colors/attributes so an erase that follows fills with the default background.</summary>
        private static readonly string SgrReset = Csi + "0m";

        /// <summary>Erase from the cursor (inclusive) to the end of the line.</summary>
        private static readonly string EraseToLineEnd = Csi + "K";

        /// <summary>Erase from the cursor (inclusive) to the end of the screen.</summary>
        private static readonly string EraseBelow = Csi + "J";

        /// <summary>
        ///     A full redraw is forced after this many diffed frames so the screen self-heals from anything the size
        ///     sampling cannot see — e.g. a maximize-and-restore that garbles the buffer but lands on the same
        ///     dimensions, or another process writing to the console. Full redraws still overwrite in place (nothing
        ///     is blanked first), so the refresh itself cannot flicker.
        /// </summary>
        private const int FullRedrawInterval = 10;

        /// <summary>Whether escape sequences can be used, decided once at construction.</summary>
        private readonly bool _useAnsi;

        /// <summary>The rows believed to be on screen right now (always exactly the visible row count), or null before the first frame.</summary>
        private string[] _shownLines;

        /// <summary>Console width in columns when <see cref="_shownLines" /> was drawn; a change forces a full redraw.</summary>
        private int _shownWidth;

        /// <summary>Console height in rows when <see cref="_shownLines" /> was drawn; a change forces a full redraw.</summary>
        private int _shownHeight;

        /// <summary>Diffed frames drawn since the last full redraw; see <see cref="FullRedrawInterval" />.</summary>
        private int _framesSinceFullRedraw;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ConsolePresenter" /> class.
        /// </summary>
        /// <param name="useAnsi">
        ///     True to force escape-sequence output, false to force the legacy plain-text fallback, or null (the
        ///     default) to call <see cref="AnsiConsole.Enable" /> and use whatever it reports — which also readies the
        ///     console for ANSI graphics, so a host using this class does not need to call it separately.
        /// </param>
        public ConsolePresenter(bool? useAnsi = null)
        {
            _useAnsi = useAnsi ?? AnsiConsole.Enable();
        }

        /// <summary>
        ///     Draws the given text user interface content, replacing whatever this presenter drew before. Only rows
        ///     that changed since the previous call are written; the first call (and any call after the console was
        ///     resized or <see cref="Reset" />) redraws everything and erases the rest of the screen.
        /// </summary>
        /// <param name="tuiContent">The complete frame, rows separated by newlines (both LF and CRLF are understood).</param>
        public void Present(string tuiContent)
        {
            var width = AnsiConsole.SafeWindowWidth();
            var height = AnsiConsole.SafeWindowHeight();

            // The bottom row is left alone so nothing is ever printed into the cell that makes terminals scroll.
            var visibleRows = Math.Max(1, height - 1);
            var lines = SplitLines(tuiContent ?? string.Empty, visibleRows);

            // Diff against the previous frame only while the console still has the size that frame was drawn for;
            // otherwise the screen no longer matches what we remember and everything must be redrawn. Every
            // FullRedrawInterval frames the diff is skipped on purpose so the screen heals from console transients
            // (e.g. a resize-and-restore between frames) that the size comparison cannot detect.
            var shownLines = _framesSinceFullRedraw < FullRedrawInterval &&
                             _shownLines != null && _shownLines.Length == visibleRows &&
                             _shownWidth == width && _shownHeight == height
                ? _shownLines
                : null;
            _framesSinceFullRedraw = shownLines == null ? 0 : _framesSinceFullRedraw + 1;

            var drawn = true;
            if (_useAnsi)
            {
                var update = BuildAnsiUpdate(lines, shownLines, width, height);
                if (update.Length > 0)
                    Console.Out.Write(update);
            }
            else
            {
                drawn = PresentLegacy(lines, shownLines, width, height);
            }

            // Only remember rows that really made it to the screen; a failed draw must not poison the diff, or the
            // rows it "remembered" would never be repainted.
            _shownLines = drawn ? lines : null;
            _shownWidth = width;
            _shownHeight = height;
        }

        /// <summary>
        ///     Forgets what is on screen so the next <see cref="Present" /> redraws every row. Call this if something
        ///     else wrote to the console behind the presenter's back.
        /// </summary>
        public void Reset()
        {
            _shownLines = null;
        }

        /// <summary>
        ///     Builds the escape-sequence payload that turns the previously shown rows into the new ones. Pure string
        ///     composition so it can be unit tested without a console.
        /// </summary>
        /// <param name="lines">The new frame, exactly one entry per visible row (see <see cref="SplitLines" />).</param>
        /// <param name="shownLines">
        ///     The rows currently on screen — must be the same length as <paramref name="lines" /> — or null to force a
        ///     full redraw that also erases the remainder of the screen.
        /// </param>
        /// <param name="consoleWidth">Console columns, used to decide whether a row leaves a tail worth erasing.</param>
        /// <param name="consoleHeight">Total console rows, used to know whether anything exists below the visible rows.</param>
        /// <returns>The complete update string, or an empty string when nothing changed.</returns>
        internal static string BuildAnsiUpdate(string[] lines, string[] shownLines, int consoleWidth, int consoleHeight)
        {
            var fullRedraw = shownLines == null;
            var body = new StringBuilder();

            for (var row = 0; row < lines.Length; row++)
            {
                if (!fullRedraw && string.Equals(lines[row], shownLines[row], StringComparison.Ordinal))
                    continue;

                // Move to the row, overwrite it in place, then reset attributes so a row without its own trailing
                // reset cannot leak color into the erase below (which some terminals fill with the current
                // background) or into the next frame.
                body.Append(Csi).Append(row + 1).Append(";1H");
                body.Append(lines[row]);
                body.Append(SgrReset);

                // Erase whatever the old (possibly longer) row left behind — but only when this row leaves room for
                // a leftover tail. A row that already fills (or overflows) the line ends with the cursor sitting ON
                // the last column, because auto-wrap is off, and erase-to-end-of-line includes the cursor cell — an
                // unconditional erase would blank the row's just-written rightmost character.
                if (VisibleLength(lines[row]) < consoleWidth)
                    body.Append(EraseToLineEnd);
            }

            // A full redraw cannot trust anything on screen, so also erase every row below the managed region (the
            // guard skips this on a one-row console, where "below" would clamp onto the row just drawn).
            if (fullRedraw && lines.Length < consoleHeight)
                body.Append(Csi).Append(lines.Length + 1).Append(";1H").Append(SgrReset).Append(EraseBelow);

            if (body.Length == 0)
                return string.Empty;

            // Park the cursor at the end of the last row with content, which is where the input prompt echoes typed
            // characters — so a host that keeps the cursor visible gets it blinking exactly where typing appears.
            var (parkRow, parkColumn) = ParkPosition(lines);
            body.Append(Csi).Append(parkRow).Append(';').Append(parkColumn).Append('H');

            // Auto-wrap is off while drawing so an over-long row clips instead of wrapping into the row below (which
            // could also scroll the screen); both it and the synchronized-update marker are restored afterwards.
            return SyncBegin + WrapOff + body + WrapOn + SyncEnd;
        }

        /// <summary>
        ///     Splits frame content into exactly <paramref name="rows" /> lines: understands LF and CRLF, pads with
        ///     empty rows when the content is short, and drops rows that would not fit on screen.
        /// </summary>
        internal static string[] SplitLines(string content, int rows)
        {
            var raw = content.Split('\n');
            var lines = new string[rows];

            for (var row = 0; row < rows; row++)
            {
                var line = row < raw.Length ? raw[row] : string.Empty;
                lines[row] = line.Length > 0 && line[line.Length - 1] == '\r'
                    ? line.Substring(0, line.Length - 1)
                    : line;
            }

            return lines;
        }

        /// <summary>
        ///     One-based row and column of the cell just after the last visible character of the last non-empty row,
        ///     or the home position for an empty frame.
        /// </summary>
        internal static (int Row, int Column) ParkPosition(string[] lines)
        {
            for (var row = lines.Length - 1; row >= 0; row--)
            {
                if (lines[row].Length == 0)
                    continue;
                return (row + 1, VisibleLength(lines[row]) + 1);
            }

            return (1, 1);
        }

        /// <summary>
        ///     The number of character cells a line occupies on screen, counting escape sequences (CSI, OSC/DCS-style
        ///     strings, and short ESC sequences) as zero width. Cells are counted per UTF-16 code unit, the same
        ///     measure the rest of the library uses.
        /// </summary>
        internal static int VisibleLength(string line)
        {
            var length = 0;
            var i = 0;
            while (i < line.Length)
            {
                if (line[i] != Escape)
                {
                    length++;
                    i++;
                    continue;
                }

                if (i + 1 >= line.Length)
                    break;

                var kind = line[i + 1];
                if (kind == '[')
                {
                    // CSI: parameter/intermediate bytes (0x20-0x3F) up to a final byte in 0x40-0x7E.
                    i += 2;
                    while (i < line.Length && (line[i] < '@' || line[i] > '~'))
                        i++;
                    if (i < line.Length)
                        i++;
                }
                else if (kind == ']' || kind == 'P' || kind == '^' || kind == '_' || kind == 'X')
                {
                    // OSC/DCS/PM/APC/SOS string (e.g. an OSC 8 hyperlink): runs until BEL or the ST terminator
                    // "ESC \"; any other escape means the string was left unterminated and a new sequence begins.
                    i += 2;
                    while (i < line.Length)
                    {
                        if (line[i] == (char) 7)
                        {
                            i++;
                            break;
                        }

                        if (line[i] == Escape)
                        {
                            if (i + 1 < line.Length && line[i + 1] == '\\')
                                i += 2;
                            break;
                        }

                        i++;
                    }
                }
                else
                {
                    // A short ESC sequence: optional intermediate bytes (0x20-0x2F, e.g. the "(" of a charset
                    // designation like "ESC ( B") followed by one final byte.
                    i += 2;
                    if (kind >= ' ' && kind <= '/')
                    {
                        while (i < line.Length && line[i] >= ' ' && line[i] <= '/')
                            i++;
                        if (i < line.Length)
                            i++;
                    }
                }
            }

            return length;
        }

        /// <summary>
        ///     Fallback for consoles without virtual-terminal support: overwrite changed rows in a single pass, padding
        ///     each to the console width so the old row's tail disappears without a separate blanking write. Plain text
        ///     only — a console that cannot interpret escape sequences cannot show ANSI graphics either.
        /// </summary>
        private static bool PresentLegacy(string[] lines, string[] shownLines, int width, int height)
        {
            try
            {
                // SetCursorPosition addresses the console *buffer*, but the frame is sized to the *window*. On a
                // classic Windows console the buffer is much taller and may be scrolled, so offset every row by the
                // window's top; Unix-style consoles report zero here.
                var windowTop = Console.WindowTop;

                for (var row = 0; row < lines.Length; row++)
                {
                    if (shownLines != null && string.Equals(lines[row], shownLines[row], StringComparison.Ordinal))
                        continue;

                    // Writing into the very last cell of the bottom row makes a classic console wrap and scroll, so
                    // a row that sits on the console's last line (only possible on a one-row console) stops one
                    // column short.
                    var maxLength = row == height - 1 ? width - 1 : width;
                    if (maxLength < 1)
                        continue;

                    Console.SetCursorPosition(0, windowTop + row);
                    var line = lines[row];
                    Console.Write(line.Length >= maxLength ? line.Substring(0, maxLength) : line.PadRight(maxLength));
                }

                // Mirror the ANSI path's erase-below: a full redraw cannot trust anything on screen, so also blank
                // the deliberately unmanaged bottom row (again stopping short of the scroll-triggering last cell).
                if (shownLines == null && lines.Length < height && width > 1)
                {
                    Console.SetCursorPosition(0, windowTop + height - 1);
                    Console.Write(new string(' ', width - 1));
                }

                // Mirror the ANSI path: leave the cursor where the input prompt echoes typing.
                var (parkRow, parkColumn) = ParkPosition(lines);
                Console.SetCursorPosition(Math.Min(parkColumn - 1, Math.Max(0, width - 1)), windowTop + parkRow - 1);
                return true;
            }
            catch (IOException)
            {
                // No interactive console at all (output is a pipe or file): positioned drawing is impossible, so
                // report failure and let the caller forget the screen state — there is none to diff against.
                return false;
            }
            catch (ArgumentOutOfRangeException)
            {
                // The console shrank between measuring and drawing; redraw everything on the next frame.
                return false;
            }
        }
    }
}
