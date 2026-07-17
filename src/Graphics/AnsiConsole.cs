// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

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

        /// <summary>The ASCII escape control character (0x1B) that begins every ANSI control sequence.</summary>
        private const char Escape = (char) 27;

        /// <summary>
        ///     What <see cref="DetectColorMode" /> worked out, kept because it cannot change; null until first asked.
        /// </summary>
        private static AnsiColorModeEnum? _colorMode;

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
        ///     <see cref="AnsiColorModeEnum.Auto" />.
        ///     <para>
        ///         Worked out once and remembered. <see cref="AnsiColorModeEnum.Auto" /> is the default, so this is on
        ///         the path of every render, and a render can happen many times a second — where the answer is read from
        ///         environment variables that were fixed when the process started and cannot sensibly change under it.
        ///         Re-deciding it per frame was reading five environment variables and lower-casing two strings to reach
        ///         the same conclusion as last time, forever. Call <see cref="ResetColorModeCache" /> if you genuinely
        ///         change the environment underneath it.
        ///     </para>
        ///     <para>
        ///         Two threads arriving at once may both work it out, which costs a little and settles the same way:
        ///         the answer depends on nothing but the environment, so there is no lock worth taking to prevent it.
        ///     </para>
        /// </summary>
        /// <returns>A concrete (never <see cref="AnsiColorModeEnum.Auto" />) color mode.</returns>
        public static AnsiColorModeEnum DetectColorMode()
        {
            return _colorMode ??= DetectColorModeCore();
        }

        /// <summary>
        ///     Forgets the remembered answer from <see cref="DetectColorMode" />, so the next call works it out again.
        ///     For a host that changes <c>NO_COLOR</c> or its terminal underneath a running process, and for tests.
        /// </summary>
        public static void ResetColorModeCache()
        {
            _colorMode = null;
        }

        /// <summary>Works out the color mode from the environment, with nothing remembered.</summary>
        private static AnsiColorModeEnum DetectColorModeCore()
        {
            // The NO_COLOR convention: any non-empty value means "do not emit color".
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR")))
                return AnsiColorModeEnum.None;

            var colorTerm = Environment.GetEnvironmentVariable("COLORTERM");
            if (!string.IsNullOrEmpty(colorTerm))
            {
                var ct = colorTerm.ToLowerInvariant();
                if (ct.Contains("truecolor") || ct.Contains("24bit"))
                    return AnsiColorModeEnum.TrueColor;
            }

            // Windows Terminal advertises itself here and speaks true color.
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WT_SESSION")))
                return AnsiColorModeEnum.TrueColor;

            var termProgram = Environment.GetEnvironmentVariable("TERM_PROGRAM");
            if (!string.IsNullOrEmpty(termProgram))
            {
                // macOS Terminal.app is a notable holdout that only does 256 colors; most other host programs
                // (VS Code, iTerm2, WezTerm, Hyper) handle true color.
                if (string.Equals(termProgram, "Apple_Terminal", StringComparison.OrdinalIgnoreCase))
                    return AnsiColorModeEnum.Palette256;
                return AnsiColorModeEnum.TrueColor;
            }

            var term = Environment.GetEnvironmentVariable("TERM");
            if (!string.IsNullOrEmpty(term))
            {
                var t = term.ToLowerInvariant();
                if (t == "dumb")
                    return AnsiColorModeEnum.None;
                if (t.Contains("256"))
                    return AnsiColorModeEnum.Palette256;
                // A bare "xterm"/"screen"/"linux" with no color hint: 256-color support is a safe assumption.
                if (t.Contains("color") || t.Contains("xterm") || t.Contains("screen") || t.Contains("vt100"))
                    return AnsiColorModeEnum.Palette256;
            }

            // Modern Windows console hosts support true color once virtual-terminal processing is on.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return AnsiColorModeEnum.TrueColor;

            // Nothing told us otherwise; true color is near-universal on current terminals.
            return AnsiColorModeEnum.TrueColor;
        }

        /// <summary>
        ///     Best-effort guess at which true-pixel graphics protocol, if any, the destination terminal understands,
        ///     from the environment it advertises itself through. Use it to pick a renderer at start-up:
        ///     <code>
        ///         ImageRenderers.Default = ImageRenderers.ForCurrentTerminal();
        ///     </code>
        ///     <para>
        ///         This asks the terminal nothing — it only reads environment variables — so it cannot hang, cannot
        ///         consume a keystroke, and is safe to call at any time from anywhere. The trade is accuracy: it is
        ///         deliberately biased towards <see cref="AnsiGraphicsProtocolEnum.None" />, because being wrong the
        ///         other way prints raw escape sequences on screen as garbage, while <see cref="None" /> merely means a
        ///         picture is drawn out of characters instead of pixels. Two common terminals cannot be settled this
        ///         way at all — xterm only has sixel when built and started for it, and Windows Terminal publishes no
        ///         version to say whether it is 1.22 or later — so both are reported as
        ///         <see cref="AnsiGraphicsProtocolEnum.None" /> here. <see cref="ProbeGraphicsProtocol" /> settles them
        ///         by asking the terminal directly.
        ///     </para>
        /// </summary>
        /// <returns>The protocol to use, or <see cref="AnsiGraphicsProtocolEnum.None" /> to draw with characters.</returns>
        public static AnsiGraphicsProtocolEnum DetectGraphicsProtocol()
        {
            return DetectGraphicsProtocol(Environment.GetEnvironmentVariable, SafeIsOutputRedirected());
        }

        /// <summary>
        ///     The environment reasoning behind <see cref="DetectGraphicsProtocol()" />, with its inputs passed in so it
        ///     can be tested without touching the real process environment.
        /// </summary>
        /// <param name="environment">Reads an environment variable by name, returning null when it is not set.</param>
        /// <param name="outputRedirected">True when standard output is not a terminal.</param>
        internal static AnsiGraphicsProtocolEnum DetectGraphicsProtocol(Func<string, string> environment,
            bool outputRedirected)
        {
            // Output going to a file or a pipe has no terminal behind it to draw anything at all.
            if (outputRedirected)
                return AnsiGraphicsProtocolEnum.None;

            var term = (environment("TERM") ?? string.Empty).ToLowerInvariant();
            if (term == "dumb")
                return AnsiGraphicsProtocolEnum.None;

            // Inside tmux or screen the terminal a picture would reach is the multiplexer, not the one underneath.
            // Both rewrite escape sequences and need explicit per-user passthrough configuration to let graphics by,
            // so whatever the outer terminal supports cannot be relied on from in here.
            if (InsideMultiplexer(environment, term))
                return AnsiGraphicsProtocolEnum.None;

            var termProgram = environment("TERM_PROGRAM") ?? string.Empty;

            // Kitty first: where a terminal speaks both, kitty is the better protocol (no palette reduction).
            if (!string.IsNullOrEmpty(environment("KITTY_WINDOW_ID")) || term.Contains("kitty"))
                return AnsiGraphicsProtocolEnum.Kitty;

            if (term.Contains("ghostty") || !string.IsNullOrEmpty(environment("GHOSTTY_RESOURCES_DIR")) ||
                string.Equals(termProgram, "ghostty", StringComparison.OrdinalIgnoreCase))
                return AnsiGraphicsProtocolEnum.Kitty;

            if (string.Equals(termProgram, "WezTerm", StringComparison.OrdinalIgnoreCase) ||
                !string.IsNullOrEmpty(environment("WEZTERM_PANE")))
                return AnsiGraphicsProtocolEnum.Kitty;

            // Sixel-only terminals, identified by a TERM they set themselves rather than a generic one.
            if (term.Contains("foot") || term.Contains("mlterm") || term.Contains("yaft") || term.Contains("contour") ||
                string.Equals(termProgram, "contour", StringComparison.OrdinalIgnoreCase))
                return AnsiGraphicsProtocolEnum.Sixel;

            // iTerm2 has had sixel since its version 3.
            if (string.Equals(termProgram, "iTerm.app", StringComparison.OrdinalIgnoreCase) &&
                MajorVersionAtLeast(environment("TERM_PROGRAM_VERSION"), 3))
                return AnsiGraphicsProtocolEnum.Sixel;

            // VTE (GNOME Terminal, Tilix, Terminator, ...) turned sixel on in 0.78; VTE_VERSION reads like "7800".
            if (VersionAtLeast(environment("VTE_VERSION"), 7800))
                return AnsiGraphicsProtocolEnum.Sixel;

            // Konsole has had sixel since 22.04; KONSOLE_VERSION reads like "220400".
            if (VersionAtLeast(environment("KONSOLE_VERSION"), 220400))
                return AnsiGraphicsProtocolEnum.Sixel;

            // Everything else, including the two that genuinely cannot be settled from the environment (xterm, which
            // only has sixel when compiled and started for it, and Windows Terminal, which does not publish a version).
            return AnsiGraphicsProtocolEnum.None;
        }

        /// <summary>
        ///     Asks the terminal itself which graphics protocol it understands, rather than inferring it from the
        ///     environment, and falls back to <see cref="DetectGraphicsProtocol()" /> if it does not get a usable answer.
        ///     This is what settles the terminals the environment cannot: xterm, and Windows Terminal before/after 1.22.
        ///     <para>
        ///         <b>Call this once at start-up, before the application begins reading keys.</b> It works by writing a
        ///         query to the terminal and reading the reply back off standard input — so if the host's input loop is
        ///         already running, the two will steal each other's characters: the reply would be typed into the
        ///         simulation and a real keystroke could be swallowed here. Nothing in the library reads input on its
        ///         own (<see cref="Core.InputManager" /> is fed by the host), so "before the host's loop starts" is the
        ///         only requirement. Constructing a <see cref="SimulationApp" /> does exactly this automatically (via
        ///         <see cref="ImageRenderers.AutoDetect()" />) and installs the answer as the default renderer, so most
        ///         applications never call it themselves.
        ///     </para>
        ///     <para>
        ///         It never throws and is bounded by <paramref name="timeoutMilliseconds" />: every terminal worth
        ///         probing answers a device-attributes request, which is sent last precisely so its reply marks the end
        ///         of the conversation, and anything unexpected or silent simply falls back to the environment guess.
        ///     </para>
        /// </summary>
        /// <param name="timeoutMilliseconds">How long to wait for the terminal to answer before giving up.</param>
        /// <returns>The protocol to use, or <see cref="AnsiGraphicsProtocolEnum.None" /> to draw with characters.</returns>
        public static AnsiGraphicsProtocolEnum ProbeGraphicsProtocol(int timeoutMilliseconds = 250)
        {
            var guess = DetectGraphicsProtocol();

            try
            {
                // Probing needs a real terminal on both ends: something to answer, and somewhere for it to answer to.
                if (SafeIsOutputRedirected() || Console.IsInputRedirected)
                    return guess;

                // A multiplexer would answer for itself, not for the terminal the picture has to survive.
                if (InsideMultiplexer(Environment.GetEnvironmentVariable,
                        (Environment.GetEnvironmentVariable("TERM") ?? string.Empty).ToLowerInvariant()))
                    return guess;

                var reply = Ask(GraphicsQuery, timeoutMilliseconds);
                return reply == null ? guess : InterpretGraphicsReply(reply, guess);
            }
            catch
            {
                // A console that will not co-operate (no input buffer, an unusual host) is not a reason to fail
                // start-up; the environment guess is still a perfectly good answer.
                return guess;
            }
        }

        /// <summary>
        ///     Reads what the terminal makes of a graphics probe.
        /// </summary>
        /// <param name="reply">Everything the terminal sent back.</param>
        /// <param name="fallback">The environment's guess, used when the reply settles nothing.</param>
        internal static AnsiGraphicsProtocolEnum InterpretGraphicsReply(string reply, AnsiGraphicsProtocolEnum fallback)
        {
            // Kitty answers the graphics query with the id it was given and an OK; nothing else produces this.
            if (reply.IndexOf("i=31;OK", StringComparison.Ordinal) >= 0)
                return AnsiGraphicsProtocolEnum.Kitty;

            // The device-attributes reply lists what the terminal can do as numbers between "ESC[?" and "c";
            // attribute 4 is sixel.
            var start = reply.IndexOf("[?", StringComparison.Ordinal);
            if (start < 0)
                return fallback;

            var end = reply.IndexOf('c', start);
            if (end < 0)
                return fallback;

            var attributes = reply.Substring(start + 2, end - start - 2).Split(';');
            foreach (var attribute in attributes)
            {
                if (attribute == "4")
                    return AnsiGraphicsProtocolEnum.Sixel;
            }

            // The terminal answered and did not claim sixel, which is a real answer rather than a missing one — but
            // only about sixel, so a kitty guess from the environment still stands.
            return fallback == AnsiGraphicsProtocolEnum.Kitty ? fallback : AnsiGraphicsProtocolEnum.None;
        }

        /// <summary>
        ///     A kitty graphics query for a one-pixel image (which kitty answers and others ignore), followed by a
        ///     primary device-attributes request (which every terminal answers). The order matters: the second reply is
        ///     guaranteed, so it marks the end of the conversation and there is never a need to wait out the timeout.
        /// </summary>
        private static string GraphicsQuery =>
            Escape + "_Gi=31,s=1,v=1,a=q,t=d,f=24;AAAA" + Escape + "\\" + Escape + "[c";

        /// <summary>Writes a query to the terminal and collects the reply until it ends or the time runs out.</summary>
        /// <returns>The reply, or null if the terminal said nothing at all.</returns>
        private static string Ask(string query, int timeoutMilliseconds)
        {
            // Anything the user typed before start-up would otherwise be read as part of the reply.
            while (Console.KeyAvailable)
                Console.ReadKey(true);

            Console.Out.Write(query);
            Console.Out.Flush();

            var reply = new StringBuilder();
            var clock = Stopwatch.StartNew();

            while (clock.ElapsedMilliseconds < timeoutMilliseconds)
            {
                if (!Console.KeyAvailable)
                {
                    Thread.Sleep(1);
                    continue;
                }

                var key = Console.ReadKey(true);
                if (key.KeyChar != '\0')
                    reply.Append(key.KeyChar);

                // The device-attributes reply ends in 'c' and was sent last, so this is the whole conversation.
                if (key.KeyChar == 'c' && reply.ToString().IndexOf("[?", StringComparison.Ordinal) >= 0)
                    break;
            }

            return reply.Length == 0 ? null : reply.ToString();
        }

        /// <summary>
        ///     True when running inside a terminal multiplexer, whose escape-sequence rewriting stands between the
        ///     application and whatever terminal is actually drawing.
        /// </summary>
        private static bool InsideMultiplexer(Func<string, string> environment, string term)
        {
            return !string.IsNullOrEmpty(environment("TMUX")) ||
                   !string.IsNullOrEmpty(environment("STY")) ||
                   term.StartsWith("screen", StringComparison.Ordinal) ||
                   term.StartsWith("tmux", StringComparison.Ordinal);
        }

        /// <summary>True when a packed version variable (for example "220400") is at least the given value.</summary>
        private static bool VersionAtLeast(string value, int minimum)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var version) &&
                   version >= minimum;
        }

        /// <summary>True when a dotted version string (for example "3.4.19") has at least the given major version.</summary>
        private static bool MajorVersionAtLeast(string value, int minimum)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            var firstPart = value.Split('.')[0];
            return int.TryParse(firstPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var major) &&
                   major >= minimum;
        }

        /// <summary>Whether standard output is redirected, reported as "yes" if the question itself fails.</summary>
        private static bool SafeIsOutputRedirected()
        {
            try
            {
                return Console.IsOutputRedirected;
            }
            catch
            {
                return true;
            }
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
