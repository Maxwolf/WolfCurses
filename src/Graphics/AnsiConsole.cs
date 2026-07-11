// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace WolfCurses.Graphics
{
    /// <summary>
    ///     Small helper for getting a console ready to display ANSI graphics, and for guessing how much color that
    ///     console can show. None of this is required to <em>build</em> an ANSI string, but a Windows console will not
    ///     interpret the escape sequences (it prints them literally) and will render the half-block glyphs as question
    ///     marks unless virtual-terminal processing and a UTF-8 output encoding are turned on first.
    /// </summary>
    public static class AnsiConsole
    {
        private const int StdOutputHandle = -11;
        private const uint EnableVirtualTerminalProcessing = 0x0004;

        /// <summary>
        ///     Prepares the current process's console for ANSI graphics: switches standard output to UTF-8 (so the
        ///     <c>▀</c>/<c>▄</c> half-block glyphs render) and, on Windows, enables the
        ///     <c>ENABLE_VIRTUAL_TERMINAL_PROCESSING</c> console mode (so escape sequences are interpreted rather than
        ///     printed). Safe to call more than once and safe to call when output is redirected — it simply reports what
        ///     it managed to do. Non-Windows terminals interpret ANSI natively, so only the encoding is touched there.
        /// </summary>
        /// <returns>True if the console appears ready for ANSI graphics; false if a step could not be completed.</returns>
        public static bool Enable()
        {
            var ready = true;

            try
            {
                Console.OutputEncoding = Encoding.UTF8;
            }
            catch
            {
                // Setting the encoding can throw when output is redirected to a handle that is not a console; the
                // caller can still write UTF-8 bytes, so this is not fatal on its own.
                ready = false;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                ready &= TryEnableWindowsVirtualTerminal();

            return ready;
        }

        /// <summary>
        ///     Best-effort guess at how much color the destination terminal supports, honoring the common
        ///     <c>NO_COLOR</c>, <c>COLORTERM</c> and <c>TERM</c> environment conventions. Used to resolve
        ///     <see cref="AnsiColorMode.Auto" />.
        /// </summary>
        /// <returns>A concrete (never <see cref="AnsiColorMode.Auto" />) color mode.</returns>
        public static AnsiColorMode DetectColorMode()
        {
            // The NO_COLOR convention: any non-empty value means "do not emit color".
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR")))
                return AnsiColorMode.None;

            var colorTerm = Environment.GetEnvironmentVariable("COLORTERM");
            if (!string.IsNullOrEmpty(colorTerm))
            {
                var ct = colorTerm.ToLowerInvariant();
                if (ct.Contains("truecolor") || ct.Contains("24bit"))
                    return AnsiColorMode.TrueColor;
            }

            // Windows Terminal advertises itself here and speaks true color.
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WT_SESSION")))
                return AnsiColorMode.TrueColor;

            var termProgram = Environment.GetEnvironmentVariable("TERM_PROGRAM");
            if (!string.IsNullOrEmpty(termProgram))
            {
                // macOS Terminal.app is a notable holdout that only does 256 colors; most other host programs
                // (VS Code, iTerm2, WezTerm, Hyper) handle true color.
                if (string.Equals(termProgram, "Apple_Terminal", StringComparison.OrdinalIgnoreCase))
                    return AnsiColorMode.Palette256;
                return AnsiColorMode.TrueColor;
            }

            var term = Environment.GetEnvironmentVariable("TERM");
            if (!string.IsNullOrEmpty(term))
            {
                var t = term.ToLowerInvariant();
                if (t == "dumb")
                    return AnsiColorMode.None;
                if (t.Contains("256"))
                    return AnsiColorMode.Palette256;
                // A bare "xterm"/"screen"/"linux" with no color hint: 256-color support is a safe assumption.
                if (t.Contains("color") || t.Contains("xterm") || t.Contains("screen") || t.Contains("vt100"))
                    return AnsiColorMode.Palette256;
            }

            // Modern Windows console hosts support true color once virtual-terminal processing is on.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return AnsiColorMode.TrueColor;

            // Nothing told us otherwise; true color is near-universal on current terminals.
            return AnsiColorMode.TrueColor;
        }

        /// <summary>Console window width in columns, or <paramref name="fallback" /> when there is no console.</summary>
        internal static int SafeWindowWidth(int fallback = 80)
        {
            try
            {
                var width = Console.WindowWidth;
                return width > 0 ? width : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        /// <summary>Console window height in rows, or <paramref name="fallback" /> when there is no console.</summary>
        internal static int SafeWindowHeight(int fallback = 24)
        {
            try
            {
                var height = Console.WindowHeight;
                return height > 0 ? height : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        /// <summary>
        ///     Turns on <c>ENABLE_VIRTUAL_TERMINAL_PROCESSING</c> for the standard output handle. Returns false (rather
        ///     than throwing) when there is no real console attached, for example because output is a pipe or file.
        /// </summary>
        private static bool TryEnableWindowsVirtualTerminal()
        {
            try
            {
                var handle = GetStdHandle(StdOutputHandle);
                if (handle == IntPtr.Zero || handle == new IntPtr(-1))
                    return false;

                // GetConsoleMode fails for a redirected handle, which tells us there is nothing to configure.
                if (!GetConsoleMode(handle, out var mode))
                    return false;

                if ((mode & EnableVirtualTerminalProcessing) != 0)
                    return true;

                return SetConsoleMode(handle, mode | EnableVirtualTerminalProcessing);
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);
    }
}
