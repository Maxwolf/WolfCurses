// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace WolfCurses.Apps.MediaPlayer
{
    /// <summary>
    ///     Whether ffmpeg is on this machine, and what that means for what the player can do.
    ///     <para>
    ///         <b>Three separate programs and three separate answers</b>, because a build can ship any subset of
    ///         them and the player degrades differently for each: without <c>ffmpeg</c> there is nothing to decode
    ///         at all, without <c>ffprobe</c> a file can still be played but its length is unknown so the scrub bar
    ///         has nothing to scrub, and without <c>ffplay</c> everything works silently. Answering one question
    ///         with one flag would make all three failures read as "ffmpeg missing", which is wrong twice.
    ///     </para>
    ///     <para>
    ///         <b>Asked once and remembered</b>, for the reason <see cref="Graphics.AnsiConsole" /> caches its own
    ///         answers: it costs three process launches, and what is installed cannot change under a running
    ///         program. That does mean the first frame after opening this application is a little late, which is
    ///         the honest price of not asking again on every render.
    ///     </para>
    ///     <para>
    ///         No <c>PackageReference</c> is involved and none could be: this talks to ffmpeg the way a shell does,
    ///         by running it and reading its output. That is also why the whole feature works with whatever build
    ///         the user already has rather than pinning a version.
    ///     </para>
    /// </summary>
    internal static class FfmpegTools
    {
        /// <summary>How long to wait for a version banner before deciding the program is not really there.</summary>
        private static readonly TimeSpan _probeTimeout = TimeSpan.FromSeconds(5d);

        /// <summary>What was found, or null until somebody asks.</summary>
        private static Found _found;

        /// <summary>Whether the decoder is available, which is the one everything else needs.</summary>
        public static bool HasFfmpeg => Detect().Ffmpeg != null;

        /// <summary>Whether the prober is available, which is what gives a file a known length.</summary>
        public static bool HasFfprobe => Detect().Ffprobe != null;

        /// <summary>Whether the player is available, which is the difference between sound and silence.</summary>
        public static bool HasFfplay => Detect().Ffplay != null;

        /// <summary>The version string of whichever program answered first, or null when none did.</summary>
        public static string Version => Detect().Version;

        /// <summary>
        ///     Runs one of the tools with its output on a pipe. The caller owns the process and must dispose it.
        ///     <para>
        ///         Arguments go through <see cref="ProcessStartInfo.ArgumentList" /> rather than being pasted into
        ///         one string, which is not tidiness: a filter graph is full of colons, commas and brackets, and a
        ///         file name is full of spaces and apostrophes. Quoting that by hand is a bug per platform.
        ///     </para>
        /// </summary>
        /// <param name="program">Which tool: <c>ffmpeg</c>, <c>ffprobe</c> or <c>ffplay</c>.</param>
        /// <param name="arguments">The arguments, one per element, unquoted.</param>
        /// <param name="readOutput">Whether standard output is wanted on a pipe.</param>
        /// <returns>The running process, or null when it could not be started at all.</returns>
        public static Process Start(string program, IEnumerable<string> arguments, bool readOutput)
        {
            var info = new ProcessStartInfo(program)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = readOutput,

                // Always redirected, and never optional. ffmpeg writes its progress to standard error even when
                // nothing has gone wrong, and a pipe nobody drains fills after a few kilobytes and blocks the
                // program writing to it - which looks exactly like a decoder that has hung.
                RedirectStandardError = true,

                // Also never optional. ffplay reads the keyboard, and sharing this console with it means it eats
                // keystrokes meant for the player. Its own pipe leaves our console alone.
                RedirectStandardInput = true
            };

            foreach (var argument in arguments)
                info.ArgumentList.Add(argument);

            try
            {
                // Adopted rather than merely started: a child that outlives this program is the user's problem
                // afterwards, and the ways a program can die without running any of its own code are the usual
                // ways it dies. See ChildProcesses.
                return ChildProcesses.Adopt(Process.Start(info));
            }
            catch (Exception exception) when (exception is System.ComponentModel.Win32Exception
                                                  or InvalidOperationException
                                                  or System.IO.IOException)
            {
                return null;
            }
        }

        /// <summary>What to tell the user about all this, one line per thing they might want to know.</summary>
        /// <returns>The report.</returns>
        public static IReadOnlyList<string> Report()
        {
            var found = Detect();
            var lines = new List<string>();

            lines.Add(found.Ffmpeg == null
                ? "ffmpeg    not found. Nothing can be decoded; install it and reopen this screen."
                : "ffmpeg    found" + (found.Version == null ? string.Empty : ", version " + found.Version));

            lines.Add(found.Ffprobe == null
                ? "ffprobe   not found. Files will play, but with no known length to scrub along."
                : "ffprobe   found. Lengths and stream details are known.");

            lines.Add(found.Ffplay == null
                ? "ffplay    not found. Pictures will play silently."
                : "ffplay    found. Sound will play.");

            return lines;
        }

        /// <summary>Finds the tools, once.</summary>
        /// <returns>What is on this machine.</returns>
        private static Found Detect()
        {
            return _found ??= new Found
            {
                Ffmpeg = Banner("ffmpeg"),
                Ffprobe = Banner("ffprobe"),
                Ffplay = Banner("ffplay")
            };
        }

        /// <summary>
        ///     Runs a program's version banner and hands back its first line, or null when it is not there.
        ///     <para>
        ///         Running it is the check, rather than searching the path: it settles the question the same way on
        ///         every platform, and it catches the case where something of the right name is on the path and
        ///         cannot actually run.
        ///     </para>
        /// </summary>
        /// <param name="program">The program to run.</param>
        /// <returns>Its first line of output, or null.</returns>
        private static string Banner(string program)
        {
            Process process = null;

            try
            {
                process = Start(program, new[] {"-hide_banner", "-version"}, true);

                if (process == null)
                    return null;

                var line = process.StandardOutput.ReadLine();

                if (!process.WaitForExit(_probeTimeout))
                    process.Kill(true);

                return string.IsNullOrWhiteSpace(line) ? program : line;
            }
            catch (Exception exception) when (exception is System.IO.IOException
                                                  or InvalidOperationException
                                                  or System.ComponentModel.Win32Exception)
            {
                return null;
            }
            finally
            {
                process?.Dispose();
            }
        }

        /// <summary>What was found, worked out once.</summary>
        private sealed class Found
        {
            /// <summary>The decoder's banner, or null.</summary>
            public string Ffmpeg { get; init; }

            /// <summary>The prober's banner, or null.</summary>
            public string Ffprobe { get; init; }

            /// <summary>The player's banner, or null.</summary>
            public string Ffplay { get; init; }

            /// <summary>The version number out of whichever banner there is.</summary>
            public string Version
            {
                get
                {
                    var banner = Ffmpeg ?? Ffprobe ?? Ffplay;

                    if (banner == null)
                        return null;

                    var match = Regex.Match(banner, @"version\s+(\S+)");

                    return match.Success ? match.Groups[1].Value : null;
                }
            }
        }
    }
}
