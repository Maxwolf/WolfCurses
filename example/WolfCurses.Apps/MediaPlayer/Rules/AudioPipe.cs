// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;

namespace WolfCurses.Apps.MediaPlayer
{
    /// <summary>
    ///     Sound coming out of ffmpeg as plain numbers, for the visualizer to look at.
    ///     <para>
    ///         <b>This is not the sound you hear</b>, which is <see cref="AudioPlayer" /> a file away. Two programs
    ///         read the same file: one plays it, and this one turns it into samples nothing else could get at. They
    ///         are kept in step by both being told where the clock is rather than by talking to each other, which
    ///         is loose enough to drift by a frame and exact enough that nobody watching a set of bars would ever
    ///         see it.
    ///     </para>
    ///     <para>
    ///         <b>Mono, and eleven kilohertz, on purpose.</b> The bars want the shape of the sound rather than the
    ///         sound itself: one channel because two would be averaged anyway, and a low rate because it halves the
    ///         work and still reaches five and a half kilohertz, which is above everything a set of twenty bars
    ///         could tell apart.
    ///     </para>
    ///     <para>
    ///         <b>Blocks are handed out against the clock, not one per frame.</b> Ask for the block at a position
    ///         and whatever piled up behind it is thrown away, so a slow moment on screen does not put the picture
    ///         permanently behind the sound. The alternative - one block per look - drifts further out of step for
    ///         as long as the program runs and there is nothing to pull it back.
    ///     </para>
    /// </summary>
    internal sealed class AudioPipe : IDisposable
    {
        /// <summary>How many samples a second are asked for.</summary>
        public const int SampleRate = 11025;

        /// <summary>
        ///     How many samples make one block. A power of two because the spectrum needs one, and 512 of them at
        ///     this rate is a shade under fifty milliseconds, which is about twenty looks a second.
        /// </summary>
        public const int BlockSamples = 512;

        /// <summary>How many blocks may wait to be looked at, which is about a second of sound.</summary>
        private const int QueueDepth = 20;

        /// <summary>Blocks waiting to be looked at.</summary>
        private readonly BlockingCollection<short[]> _blocks = new(QueueDepth);

        /// <summary>Block-sized arrays that have been finished with.</summary>
        private readonly ConcurrentBag<short[]> _spare = new();

        /// <summary>Ends the read when the pipe is being shut down.</summary>
        private readonly CancellationTokenSource _cancel = new();

        /// <summary>ffmpeg itself.</summary>
        private readonly Process _process;

        /// <summary>Reads samples off the pipe.</summary>
        private readonly Thread _reader;

        /// <summary>Where in the file this pipe was started, so a block's own time can be worked out.</summary>
        private readonly TimeSpan _from;

        /// <summary>How many blocks have been handed out.</summary>
        private long _handed;

        /// <summary>Set once, when everything is being torn down.</summary>
        private volatile bool _closing;

        /// <summary>Starts ffmpeg turning a file into samples.</summary>
        /// <param name="path">The file, or a filter description when <paramref name="generated" /> is set.</param>
        /// <param name="from">Where in the file to start.</param>
        /// <param name="generated">Whether the path is a filter description rather than a file.</param>
        public AudioPipe(string path, TimeSpan from, bool generated = false)
        {
            _from = from;
            _process = FfmpegTools.Start("ffmpeg", Arguments(path, from, generated), true);

            if (_process == null)
                return;

            _reader = new Thread(Read) {IsBackground = true, Name = "wolfcurses-audio"};
            _reader.Start();

            var complaints = new Thread(Drain) {IsBackground = true, Name = "wolfcurses-audio-log"};
            complaints.Start();
        }

        /// <summary>
        ///     The block of samples belonging to a moment, throwing away everything that piled up before it.
        /// </summary>
        /// <param name="position">Where the clock is.</param>
        /// <param name="block">The samples, or null.</param>
        /// <returns>TRUE when a block came back.</returns>
        public bool TryReadAt(TimeSpan position, out short[] block)
        {
            block = null;

            var into = position - _from;
            var wanted = into <= TimeSpan.Zero
                ? 0L
                : (long) (into.TotalSeconds * SampleRate / BlockSamples);

            var taken = false;

            // Drains up to where the clock is. Nothing waits: a look that finds no sound ready simply keeps the
            // bars it had, which is what a moment of silence looks like anyway.
            while (_handed <= wanted && _blocks.TryTake(out var next))
            {
                if (taken)
                    Recycle(block);

                block = next;
                _handed++;
                taken = true;
            }

            return taken;
        }

        /// <summary>Hands a block's storage back once its numbers have been used.</summary>
        /// <param name="block">The block that is finished with.</param>
        public void Recycle(short[] block)
        {
            if (block == null || _closing || block.Length != BlockSamples)
                return;

            if (_spare.Count <= QueueDepth + 1)
                _spare.Add(block);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _closing = true;

            try
            {
                _cancel.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Already gone.
            }

            Kill();

            _reader?.Join(TimeSpan.FromSeconds(2d));

            _blocks.Dispose();
            _cancel.Dispose();
            _process?.Dispose();
        }

        /// <summary>What to tell ffmpeg.</summary>
        /// <param name="path">The file or filter description.</param>
        /// <param name="from">Where to start.</param>
        /// <param name="generated">Whether the path is a filter description.</param>
        /// <returns>The arguments.</returns>
        private static IEnumerable<string> Arguments(string path, TimeSpan from, bool generated)
        {
            var list = new List<string> {"-hide_banner", "-loglevel", "error"};

            if (from > TimeSpan.Zero)
            {
                list.Add("-ss");
                list.Add(from.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture));
            }

            if (generated)
            {
                list.Add("-f");
                list.Add("lavfi");
            }

            list.Add("-i");
            list.Add(path);

            list.Add("-vn");
            list.Add("-f");
            list.Add("s16le");
            list.Add("-acodec");
            list.Add("pcm_s16le");
            list.Add("-ac");
            list.Add("1");
            list.Add("-ar");
            list.Add(SampleRate.ToString(CultureInfo.InvariantCulture));
            list.Add("-");

            return list;
        }

        /// <summary>Reads blocks until the sound runs out or somebody closes this.</summary>
        private void Read()
        {
            var bytes = new byte[BlockSamples * 2];
            var stream = _process.StandardOutput.BaseStream;

            try
            {
                while (!_closing)
                {
                    if (!ReadFully(stream, bytes))
                        break;

                    if (!_spare.TryTake(out var block) || block.Length != BlockSamples)
                        block = new short[BlockSamples];

                    // Little-endian signed sixteen-bit, which is what s16le means and what every desktop uses.
                    for (var i = 0; i < BlockSamples; i++)
                        block[i] = (short) (bytes[i * 2] | (bytes[i * 2 + 1] << 8));

                    _blocks.Add(block, _cancel.Token);
                }
            }
            catch (Exception exception) when (exception is OperationCanceledException
                                                  or InvalidOperationException
                                                  or IOException)
            {
                // Every one of these is a normal way for a pipe being shut down to end.
            }
            finally
            {
                try
                {
                    _blocks.CompleteAdding();
                }
                catch (ObjectDisposedException)
                {
                    // Disposed underneath us.
                }
            }
        }

        /// <summary>Fills a buffer completely, since a pipe read returns whatever happens to have arrived.</summary>
        /// <param name="stream">The pipe.</param>
        /// <param name="buffer">Where to put the bytes.</param>
        /// <returns>FALSE when the stream ended before the buffer was full.</returns>
        private bool ReadFully(Stream stream, byte[] buffer)
        {
            var filled = 0;

            while (filled < buffer.Length)
            {
                var read = stream.Read(buffer, filled, buffer.Length - filled);

                if (read <= 0)
                    return false;

                filled += read;

                if (_closing)
                    return false;
            }

            return true;
        }

        /// <summary>Keeps ffmpeg's standard error moving, for the reason <see cref="VideoPipe" /> does.</summary>
        private void Drain()
        {
            try
            {
                while (_process.StandardError.ReadLine() != null)
                {
                    // Read and dropped. What matters here is that the pipe keeps moving; the picture pipe is
                    // where a reason worth showing would come from.
                }
            }
            catch (Exception exception) when (exception is IOException or InvalidOperationException)
            {
                // The pipe closing is how this ends.
            }
        }

        /// <summary>Ends ffmpeg, whatever state it is in.</summary>
        private void Kill()
        {
            if (_process == null)
                return;

            try
            {
                if (!_process.HasExited)
                    _process.Kill(true);
            }
            catch (Exception exception) when (exception is InvalidOperationException
                                                  or NotSupportedException
                                                  or System.ComponentModel.Win32Exception)
            {
                // Already gone, or never really started.
            }
        }
    }
}
