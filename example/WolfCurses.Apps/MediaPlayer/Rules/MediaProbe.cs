// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Globalization;
using System.IO;

namespace WolfCurses.Apps.MediaPlayer
{
    /// <summary>
    ///     Asks ffprobe what a file is.
    ///     <para>
    ///         <b>Its plain output is used rather than its JSON, on purpose.</b> ffprobe will emit JSON and there is
    ///         a reader for that in the base class library, but its default writer already produces exactly what is
    ///         wanted: <c>[STREAM]</c> and <c>[FORMAT]</c> sections of <c>key=value</c> lines. Parsing that is a
    ///         dozen lines with nothing to go wrong in it, and the sections are what make it parseable at all - a
    ///         flat list of keys could not say which stream a <c>codec_name</c> belonged to.
    ///     </para>
    ///     <para>
    ///         <b><see cref="Parse" /> takes text rather than a path</b>, which is the whole of how this is tested:
    ///         the awkward files are three lines of hand-written output rather than three files somebody has to find
    ///         and licence. The same split the spreadsheet and the card file make between reading a file and
    ///         understanding one.
    ///     </para>
    /// </summary>
    internal static class MediaProbe
    {
        /// <summary>How long to wait for ffprobe before giving up on the file.</summary>
        private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(15d);

        /// <summary>
        ///     Works out what a file is. Never throws: a file that cannot be probed still gets a
        ///     <see cref="MediaInfo" /> naming it, because ffprobe being absent is not a reason to refuse to try
        ///     playing something.
        /// </summary>
        /// <param name="path">The file.</param>
        /// <returns>What is in it, as far as anything could tell.</returns>
        public static MediaInfo Describe(string path)
        {
            var info = new MediaInfo {Path = path};

            if (!FfmpegTools.HasFfprobe || string.IsNullOrWhiteSpace(path))
                return Guess(info);

            var process = FfmpegTools.Start("ffprobe",
                new[] {"-hide_banner", "-v", "error", "-show_streams", "-show_format", path}, true);

            if (process == null)
                return Guess(info);

            try
            {
                var text = process.StandardOutput.ReadToEnd();

                if (!process.WaitForExit(_timeout))
                    process.Kill(true);

                return Parse(text, path);
            }
            catch (Exception exception) when (exception is IOException or InvalidOperationException)
            {
                return Guess(info);
            }
            finally
            {
                process.Dispose();
            }
        }

        /// <summary>
        ///     Reads ffprobe's sectioned output.
        ///     <para>
        ///         The <b>first</b> stream of each kind wins, matching what the decoder will pick when nothing tells
        ///         it otherwise. A file with three audio tracks is described by the one that will actually be heard.
        ///     </para>
        /// </summary>
        /// <param name="text">What ffprobe wrote.</param>
        /// <param name="path">The file it was asked about.</param>
        /// <returns>What is in it.</returns>
        public static MediaInfo Parse(string text, string path)
        {
            var info = new MediaInfo {Path = path};

            if (string.IsNullOrEmpty(text))
                return Guess(info);

            var section = string.Empty;
            var kind = string.Empty;
            var pending = new PendingStream();

            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();

                if (line.Length == 0)
                    continue;

                if (line[0] == '[')
                {
                    if (line.StartsWith("[/STREAM", StringComparison.OrdinalIgnoreCase))
                        Commit(info, kind, pending);

                    if (line.StartsWith("[STREAM", StringComparison.OrdinalIgnoreCase))
                    {
                        pending = new PendingStream();
                        kind = string.Empty;
                    }

                    section = line;
                    continue;
                }

                var split = line.IndexOf('=');

                if (split <= 0)
                    continue;

                var key = line.Substring(0, split);
                var value = line.Substring(split + 1);

                // "N/A" is ffprobe's own way of saying it does not know, and letting it reach a number parse would
                // turn every unknown into a zero that reads as a real measurement.
                if (string.Equals(value, "N/A", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (section.StartsWith("[FORMAT", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(key, "duration", StringComparison.Ordinal))
                        info.Duration = Seconds(value);

                    continue;
                }

                switch (key)
                {
                    case "codec_type":
                        kind = value;
                        break;
                    case "codec_name":
                        pending.Codec = value;
                        break;
                    case "width":
                        pending.Width = Number(value);
                        break;
                    case "height":
                        pending.Height = Number(value);
                        break;
                    case "r_frame_rate":
                        pending.FrameRate = Fraction(value);
                        break;
                    case "channels":
                        pending.Channels = Number(value);
                        break;
                    case "sample_rate":
                        pending.SampleRate = Number(value);
                        break;
                    case "duration":

                        // A stream's own length, kept only as a fallback: the format's is the one that counts,
                        // and it is written after every stream, so it overwrites this if it is there at all.
                        if (info.Duration <= TimeSpan.Zero)
                            info.Duration = Seconds(value);

                        break;
                }
            }

            // A file whose last section was never closed, which is what a killed ffprobe leaves behind.
            Commit(info, kind, pending);

            return info.IsPlayable ? info : Guess(info);
        }

        /// <summary>Files the stream just read under whichever kind it turned out to be.</summary>
        /// <param name="info">What is being built.</param>
        /// <param name="kind">The stream's <c>codec_type</c>.</param>
        /// <param name="stream">What was read about it.</param>
        private static void Commit(MediaInfo info, string kind, PendingStream stream)
        {
            if (string.Equals(kind, "video", StringComparison.OrdinalIgnoreCase) && !info.HasVideo)
            {
                // A cover picture is a video stream of one frame, and treating it as a film gives a player that
                // shows an album cover for two seconds and then reports that the file has finished.
                if (stream.Width <= 0 || stream.Height <= 0)
                    return;

                info.HasVideo = true;
                info.VideoCodec = stream.Codec;
                info.Width = stream.Width;
                info.Height = stream.Height;
                info.FrameRate = stream.FrameRate;
                return;
            }

            if (!string.Equals(kind, "audio", StringComparison.OrdinalIgnoreCase) || info.HasAudio)
                return;

            info.HasAudio = true;
            info.AudioCodec = stream.Codec;
            info.Channels = stream.Channels;
            info.SampleRate = stream.SampleRate;
        }

        /// <summary>
        ///     What to assume about a file nothing could tell us anything about: that it is worth trying. The
        ///     extension is the only clue left, and being wrong costs an error message rather than anything worse.
        /// </summary>
        /// <param name="info">What little is known.</param>
        /// <returns>The same object, with a guess in it.</returns>
        private static MediaInfo Guess(MediaInfo info)
        {
            if (info.IsPlayable)
                return info;

            info.HasAudio = true;
            info.HasVideo = MediaLibrary.LooksLikeVideo(info.Path);

            return info;
        }

        /// <summary>Reads a count of seconds, which ffprobe writes with a decimal point and no units.</summary>
        /// <param name="value">The text.</param>
        /// <returns>The length, or zero when it does not read as one.</returns>
        private static TimeSpan Seconds(string value)
        {
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) &&
                   seconds > 0d && !double.IsInfinity(seconds)
                ? TimeSpan.FromSeconds(seconds)
                : TimeSpan.Zero;
        }

        /// <summary>Reads a whole number.</summary>
        /// <param name="value">The text.</param>
        /// <returns>The number, or zero.</returns>
        private static int Number(string value)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) &&
                   number > 0
                ? number
                : 0;
        }

        /// <summary>
        ///     Reads a frame rate, which ffprobe writes as a fraction.
        ///     <para>
        ///         The fraction is the point of it: <c>30000/1001</c> is 29.97 and <c>24000/1001</c> is 23.976, and
        ///         those are the two rates most film and television actually use. Rounding either to a whole number
        ///         puts the picture a second ahead of the sound after about forty minutes.
        ///     </para>
        /// </summary>
        /// <param name="value">The text.</param>
        /// <returns>Frames per second, or zero.</returns>
        private static double Fraction(string value)
        {
            var slash = value.IndexOf('/');

            if (slash < 0)
            {
                return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var flat) &&
                       flat > 0d
                    ? flat
                    : 0d;
            }

            var top = value.Substring(0, slash);
            var bottom = value.Substring(slash + 1);

            if (!double.TryParse(top, NumberStyles.Float, CultureInfo.InvariantCulture, out var numerator) ||
                !double.TryParse(bottom, NumberStyles.Float, CultureInfo.InvariantCulture, out var denominator) ||
                denominator <= 0d || numerator <= 0d)
                return 0d;

            return numerator / denominator;
        }

        /// <summary>One stream's facts, held until its <c>codec_type</c> says where they belong.</summary>
        private sealed class PendingStream
        {
            /// <summary>What it is encoded with.</summary>
            public string Codec { get; set; }

            /// <summary>How wide, for a picture stream.</summary>
            public int Width { get; set; }

            /// <summary>How tall, for a picture stream.</summary>
            public int Height { get; set; }

            /// <summary>How many pictures a second.</summary>
            public double FrameRate { get; set; }

            /// <summary>How many channels, for a sound stream.</summary>
            public int Channels { get; set; }

            /// <summary>How many samples a second, for a sound stream.</summary>
            public int SampleRate { get; set; }
        }
    }
}
