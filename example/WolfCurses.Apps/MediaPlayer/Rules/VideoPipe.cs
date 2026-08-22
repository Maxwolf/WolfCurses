// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using WolfCurses.Graphics;

namespace WolfCurses.Apps.MediaPlayer
{
    /// <summary>
    ///     Pictures coming out of ffmpeg, one <see cref="PixelBuffer" /> at a time.
    ///     <para>
    ///         <b>Raw RGBA is asked for, so nothing has to be decoded on this side.</b> ffmpeg's <c>rgba</c> pixel
    ///         format is red, green, blue, alpha in memory order, which is byte for byte what
    ///         <see cref="PixelBuffer" /> wraps - so a frame is a read into an array and a constructor call, with no
    ///         copy, no conversion and none of the library's own decoders involved.
    ///     </para>
    ///     <para>
    ///         <b>ffmpeg is told the exact size to produce, and that is the whole performance story.</b> Resampling
    ///         is the dominant cost in the rendering stack, and asking a renderer to draw a 1920x1080 frame into a
    ///         seventy-column window means resizing two million pixels down to a few thousand, every frame, in
    ///         managed code. Scaling in ffmpeg instead is free in comparison and it letterboxes at the same time, so
    ///         what arrives here is already the shape of the window.
    ///     </para>
    ///     <para>
    ///         <b>The bounded queue is also the throttle, and pausing needs no code at all.</b> When the queue is
    ///         full the reading thread blocks, then ffmpeg's own pipe fills and ffmpeg blocks, and the whole chain
    ///         stops using processor time until somebody takes a frame. Stop taking frames and it stops decoding;
    ///         start again and it wakes up.
    ///     </para>
    ///     <para>
    ///         <b>Standard error is drained on its own thread and that is not optional.</b> A pipe nobody reads
    ///         fills after a few kilobytes and blocks the program writing into it, which presents as a decoder that
    ///         mysteriously stops part way through a damaged file. It is also where the reason for a failure is, so
    ///         the last few lines are kept for the status strip.
    ///     </para>
    /// </summary>
    internal sealed class VideoPipe : IDisposable
    {
        /// <summary>
        ///     How many decoded frames may wait to be shown, which is about a third of a second of slack. Enough to
        ///     ride out a slow moment without being so much that a seek has a wall of stale pictures to throw away.
        /// </summary>
        private const int QueueDepth = 8;

        /// <summary>How many lines of ffmpeg's complaints to keep for the status strip.</summary>
        private const int ErrorLines = 4;

        /// <summary>Frames waiting to be shown.</summary>
        private readonly BlockingCollection<PixelBuffer> _frames = new(QueueDepth);

        /// <summary>
        ///     Frame-sized arrays that have been finished with. A frame here is a megabyte at true-pixel sizes, so
        ///     thirty a second is thirty megabytes a second of large-object allocation; handing the arrays back
        ///     instead keeps the whole thing to the handful that are actually in flight.
        /// </summary>
        private readonly ConcurrentBag<byte[]> _spare = new();

        /// <summary>What ffmpeg had to say for itself, newest last.</summary>
        private readonly Queue<string> _errors = new();

        /// <summary>Ends the read when the pipe is being shut down.</summary>
        private readonly CancellationTokenSource _cancel = new();

        /// <summary>How wide a frame is.</summary>
        private readonly int _width;

        /// <summary>How tall a frame is.</summary>
        private readonly int _height;

        /// <summary>ffmpeg itself.</summary>
        private readonly Process _process;

        /// <summary>Reads frames off the pipe.</summary>
        private readonly Thread _reader;

        /// <summary>Set once, when everything is being torn down.</summary>
        private volatile bool _closing;

        /// <summary>Starts ffmpeg decoding a file into frames of an exact size.</summary>
        /// <param name="path">The file, or a lavfi source when <paramref name="generated" /> is set.</param>
        /// <param name="from">Where in the file to start.</param>
        /// <param name="width">How wide the frames should arrive.</param>
        /// <param name="height">How tall the frames should arrive.</param>
        /// <param name="fps">How many frames a second to produce.</param>
        /// <param name="generated">Whether the path is a filter description rather than a file.</param>
        public VideoPipe(string path, TimeSpan from, int width, int height, double fps, bool generated = false)
        {
            _width = Math.Max(2, width);
            _height = Math.Max(2, height);

            _process = FfmpegTools.Start("ffmpeg", Arguments(path, from, _width, _height, fps, generated), true);

            if (_process == null)
            {
                Failed = true;
                Error = "ffmpeg would not start.";
                return;
            }

            _reader = new Thread(Read) {IsBackground = true, Name = "wolfcurses-video"};
            _reader.Start();

            var complaints = new Thread(Drain) {IsBackground = true, Name = "wolfcurses-video-log"};
            complaints.Start();
        }

        /// <summary>
        ///     How many frames this pipe should have handed over by a given moment.
        ///     <para>
        ///         <b>Counted from where the pipe was started, not from the beginning of the file.</b> ffmpeg was
        ///         told to begin at <paramref name="from" />, so the first frame it produces is that moment rather
        ///         than frame zero of the film. Comparing a count of frames taken against the clock's own frame
        ///         number instead means that seeking twenty seconds into a thirty-a-second film makes the player
        ///         believe it is six hundred frames behind, and the catching-up loop then drains the pipe as fast
        ///         as ffmpeg can fill it - six hundred pictures decoded, scaled and thrown away, which presents as
        ///         the player locking up for a moment after every seek.
        ///     </para>
        /// </summary>
        /// <param name="position">Where the clock is.</param>
        /// <param name="from">Where the pipe was started.</param>
        /// <param name="fps">How many frames a second it was asked for.</param>
        /// <returns>The number of frames due, which is at least one.</returns>
        public static long FramesDue(TimeSpan position, TimeSpan from, double fps)
        {
            var into = position - from;

            if (into <= TimeSpan.Zero || fps <= 0d)
                return 1L;

            return (long) (into.TotalSeconds * fps) + 1L;
        }

        /// <summary>How wide the frames are.</summary>
        public int Width => _width;

        /// <summary>How tall the frames are.</summary>
        public int Height => _height;

        /// <summary>Whether ffmpeg has run out of pictures and every one of them has been taken.</summary>
        public bool IsFinished { get; private set; }

        /// <summary>Whether this never got going at all.</summary>
        public bool Failed { get; private set; }

        /// <summary>What went wrong, when something did.</summary>
        public string Error { get; private set; }

        /// <summary>Takes the next frame if one is ready, and never waits for one.</summary>
        /// <param name="frame">The frame, or null.</param>
        /// <returns>TRUE when a frame came back.</returns>
        public bool TryRead(out PixelBuffer frame)
        {
            frame = null;

            if (_frames.TryTake(out frame))
                return true;

            if (_frames.IsCompleted)
                IsFinished = true;

            return false;
        }

        /// <summary>
        ///     Hands a frame's storage back once it has been drawn, so the next one can be read into it. Passing
        ///     null, or never calling this at all, is safe: the reader simply allocates instead.
        /// </summary>
        /// <param name="frame">The frame that is finished with.</param>
        public void Recycle(PixelBuffer frame)
        {
            if (frame == null || _closing || frame.Width != _width || frame.Height != _height)
                return;

            // Bounded by the queue plus the one being drawn, so this can never grow without limit.
            if (_spare.Count <= QueueDepth + 1)
                _spare.Add(frame.Data);
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
                // Already gone, which is the only way this throws.
            }

            Kill();

            // The reader is blocked either on the pipe, which the kill above closes, or on a full queue, which the
            // cancel above releases. Joined rather than abandoned so its frames are not still arriving afterwards.
            _reader?.Join(TimeSpan.FromSeconds(2d));

            _frames.Dispose();
            _cancel.Dispose();
            _process?.Dispose();
        }

        /// <summary>
        ///     What to tell ffmpeg.
        ///     <para>
        ///         <b><c>-ss</c> goes before <c>-i</c></b>, which is the difference between seeking to a keyframe
        ///         and decoding the whole file up to that point and throwing it away. On a long film that is the
        ///         difference between a scrub bar and a program that appears to have hung.
        ///     </para>
        /// </summary>
        /// <param name="path">The file or filter description.</param>
        /// <param name="from">Where to start.</param>
        /// <param name="width">How wide the frames should be.</param>
        /// <param name="height">How tall the frames should be.</param>
        /// <param name="fps">How many frames a second.</param>
        /// <param name="generated">Whether the path is a filter description.</param>
        /// <returns>The arguments.</returns>
        private static IEnumerable<string> Arguments(string path, TimeSpan from, int width, int height, double fps,
            bool generated)
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

            // No sound, no subtitles, no data streams: ffplay is doing the listening and everything else here is
            // pictures. Asking for them anyway would have ffmpeg decode them to throw them away.
            list.Add("-an");
            list.Add("-sn");
            list.Add("-dn");

            // The rate limit goes FIRST in the chain, not as an output option. -r drops frames after the filters
            // have run, so a sixty-a-second source has every one of its frames scaled down from 4K and then half of
            // them thrown away; fps= drops them before, so the scaler only ever touches a frame that will be shown.
            //
            // Then scaled to fit and padded back out to exactly the size asked for, so every frame is the same
            // shape and the letterboxing costs nothing on this side. The scaler is area averaging, which is both
            // the right answer for a large reduction and cheaper than the default.
            list.Add("-vf");
            list.Add(string.Format(CultureInfo.InvariantCulture,
                "{0}scale={1}:{2}:force_original_aspect_ratio=decrease:flags=area," +
                "pad={1}:{2}:(ow-iw)/2:(oh-ih)/2:black",
                fps > 0d ? "fps=" + fps.ToString("0.####", CultureInfo.InvariantCulture) + "," : string.Empty,
                width, height));

            list.Add("-f");
            list.Add("rawvideo");
            list.Add("-pix_fmt");
            list.Add("rgba");
            list.Add("-");

            return list;
        }

        /// <summary>Reads frames until the pictures run out or somebody closes this.</summary>
        private void Read()
        {
            var size = _width * _height * 4;
            var stream = _process.StandardOutput.BaseStream;

            try
            {
                while (!_closing)
                {
                    if (!_spare.TryTake(out var buffer) || buffer.Length != size)
                        buffer = new byte[size];

                    if (!ReadFully(stream, buffer))
                        break;

                    _frames.Add(new PixelBuffer(_width, _height, buffer), _cancel.Token);
                }
            }
            catch (Exception exception) when (exception is OperationCanceledException
                                                  or InvalidOperationException
                                                  or ObjectDisposedException
                                                  or IOException)
            {
                // Every one of these is a normal way for a pipe being shut down to end.
            }
            finally
            {
                try
                {
                    _frames.CompleteAdding();
                }
                catch (ObjectDisposedException)
                {
                    // Disposed underneath us, which is the shutdown case again.
                }
            }
        }

        /// <summary>
        ///     Fills a buffer completely, since a pipe read returns whatever happens to have arrived. A frame that
        ///     arrives half read is a picture torn in two, which is the classic way this goes wrong.
        /// </summary>
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

        /// <summary>Keeps ffmpeg's standard error moving, and keeps the last few lines of it.</summary>
        private void Drain()
        {
            try
            {
                string line;

                while ((line = _process.StandardError.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    lock (_errors)
                    {
                        _errors.Enqueue(line.Trim());

                        while (_errors.Count > ErrorLines)
                            _errors.Dequeue();

                        Error = string.Join(" ", _errors);
                    }
                }
            }
            catch (Exception exception) when (exception is IOException
                                                  or InvalidOperationException
                                                  or ObjectDisposedException)
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
