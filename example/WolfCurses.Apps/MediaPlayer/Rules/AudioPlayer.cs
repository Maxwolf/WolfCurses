// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

namespace WolfCurses.Apps.MediaPlayer
{
    /// <summary>
    ///     The sound you actually hear, which is ffplay running beside us with nothing to look at.
    ///     <para>
    ///         <b>This library has no way to make a sound, and that is a deliberate absence rather than a gap.</b>
    ///         Everything in WolfCurses is the base class library plus characters on a terminal, and there is no
    ///         audio output in the base class library: getting one means platform interop, per platform. So the
    ///         sound is played by the program that already knows how, which came with the decoder we are already
    ///         using, and the player's own job is to keep it in step.
    ///     </para>
    ///     <para>
    ///         <b>Pause, resume and seek are all the same operation: stop it and start it again somewhere.</b>
    ///         ffplay has no way to be told anything once it is running, so there is nothing else on offer - and it
    ///         turns out to be enough, because starting it at a position is exactly what a seek is. What it costs
    ///         is a fraction of a second of silence at each one, which is the honest price of not writing an audio
    ///         stack.
    ///     </para>
    ///     <para>
    ///         <b>Its standard input is redirected and that is load-bearing.</b> ffplay reads the keyboard, and a
    ///         child sharing this console reads keys meant for the player: pressing SPACE would pause our clock and
    ///         its playback separately, and quitting it with Q would look like the terminal ignoring us.
    ///         <see cref="FfmpegTools.Start" /> gives it a pipe of its own so it never sees the console at all.
    ///     </para>
    /// </summary>
    internal sealed class AudioPlayer : IDisposable
    {
        /// <summary>ffplay, while it is running.</summary>
        private Process _process;

        /// <summary>Whether there is any sound to be had at all on this machine.</summary>
        public static bool IsAvailable => FfmpegTools.HasFfplay;

        /// <summary>Whether something is playing now.</summary>
        public bool IsPlaying => _process != null && !HasExited(_process);

        /// <summary>
        ///     Whether the sound is turned down to nothing.
        ///     <para>
        ///         <b>ffplay is still started, at zero volume, rather than not started at all.</b> Muting a player
        ///         is not the same as closing it: the sound has to be exactly where it was when it comes back, and
        ///         a process that was never running has no position to come back to. It also keeps everything else
        ///         about a muted player identical to a loud one, which is what makes the screen tests worth
        ///         anything - they run muted, and still drive the same three processes a person would.
        ///     </para>
        ///     <para>
        ///         Takes effect when the sound next starts, since ffplay cannot be told anything once it is
        ///         running; the caller restarts it, which is the same thing pausing and seeking already do.
        ///     </para>
        /// </summary>
        public bool IsMuted { get; set; }

        /// <summary>
        ///     Plays a file from a position, stopping whatever was playing before.
        /// </summary>
        /// <param name="path">The file, or a filter description when <paramref name="generated" /> is set.</param>
        /// <param name="from">Where to start.</param>
        /// <param name="generated">Whether the path is a filter description rather than a file.</param>
        public void PlayFrom(string path, TimeSpan from, bool generated = false)
        {
            Stop();

            if (!IsAvailable || string.IsNullOrWhiteSpace(path))
                return;

            var arguments = new List<string> {"-hide_banner", "-loglevel", "quiet", "-nodisp", "-autoexit"};

            if (IsMuted)
            {
                arguments.Add("-volume");
                arguments.Add("0");
            }

            if (from > TimeSpan.Zero)
            {
                arguments.Add("-ss");
                arguments.Add(from.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture));
            }

            if (generated)
            {
                arguments.Add("-f");
                arguments.Add("lavfi");
            }

            arguments.Add("-i");
            arguments.Add(path);

            _process = FfmpegTools.Start("ffplay", arguments, false);
        }

        /// <summary>Stops the sound. Safe to call when there is none.</summary>
        public void Stop()
        {
            var process = _process;
            _process = null;

            if (process == null)
                return;

            ChildProcesses.Release(process);

            try
            {
                if (!process.HasExited)
                    process.Kill(true);
            }
            catch (Exception exception) when (exception is InvalidOperationException
                                                  or NotSupportedException
                                                  or System.ComponentModel.Win32Exception)
            {
                // Already gone, which is the usual case when a file has played itself out.
            }
            finally
            {
                process.Dispose();
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Stop();
        }

        /// <summary>Whether a process has finished, treating "cannot tell" as finished.</summary>
        /// <param name="process">The process.</param>
        /// <returns>TRUE when it is over.</returns>
        private static bool HasExited(Process process)
        {
            try
            {
                return process.HasExited;
            }
            catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException)
            {
                return true;
            }
        }
    }
}
