// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Globalization;
using System.Text;

namespace WolfCurses.Apps.MediaPlayer
{
    /// <summary>
    ///     What a file turned out to be: how long, what is in it, and how big the pictures are. Pure facts with no
    ///     console anywhere near them, so everything that reads them can be driven from a test.
    ///     <para>
    ///         <b>Every field has an honest "not known" value and the screen uses them.</b> A length of zero means
    ///         the length is not known rather than that the file is empty, which is what a stream, a pipe, and any
    ///         file at all when ffprobe is missing all look like. The scrub bar draws no playhead for it rather
    ///         than putting one at a position it invented.
    ///     </para>
    /// </summary>
    public sealed class MediaInfo
    {
        /// <summary>Where the file is.</summary>
        public string Path { get; set; }

        /// <summary>How long it runs for, or <see cref="TimeSpan.Zero" /> when that is not known.</summary>
        public TimeSpan Duration { get; set; }

        /// <summary>Whether there are pictures in it.</summary>
        public bool HasVideo { get; set; }

        /// <summary>Whether there is sound in it.</summary>
        public bool HasAudio { get; set; }

        /// <summary>How wide the pictures are, or zero.</summary>
        public int Width { get; set; }

        /// <summary>How tall the pictures are, or zero.</summary>
        public int Height { get; set; }

        /// <summary>
        ///     How many pictures a second, or zero when that is not known.
        ///     <para>
        ///         Kept as the number ffprobe gives rather than rounded, because the ones that matter are not whole:
        ///         24000/1001 is 23.976 and treating it as 24 puts the picture a second ahead of the sound after
        ///         forty minutes.
        ///     </para>
        /// </summary>
        public double FrameRate { get; set; }

        /// <summary>What the pictures are encoded with, or null.</summary>
        public string VideoCodec { get; set; }

        /// <summary>What the sound is encoded with, or null.</summary>
        public string AudioCodec { get; set; }

        /// <summary>How many channels of sound, or zero.</summary>
        public int Channels { get; set; }

        /// <summary>How many samples a second of sound, or zero.</summary>
        public int SampleRate { get; set; }

        /// <summary>Whether this is worth trying to play at all.</summary>
        public bool IsPlayable => HasVideo || HasAudio;

        /// <summary>
        ///     What to call this on screen, when the path is not something to show anybody. A generated source's
        ///     path is a filter graph forty characters wide, which is a thing to hand ffmpeg rather than a title.
        /// </summary>
        public string Title { get; set; }

        /// <summary>What to call this: whatever it was given, or the file's own name.</summary>
        public string Name
        {
            get
            {
                if (!string.IsNullOrEmpty(Title))
                    return Title;

                return string.IsNullOrEmpty(Path) ? "Nothing open" : System.IO.Path.GetFileName(Path);
            }
        }

        /// <summary>
        ///     One line saying what is in the file, for the strip above the picture. Leaves out whatever is not
        ///     known rather than writing zeroes, since a zero here is an absence and reads as a fault.
        /// </summary>
        /// <returns>Something like <c>h264 1280x720 23.98fps + aac 2ch</c>.</returns>
        public string Summary()
        {
            var sb = new StringBuilder();

            if (HasVideo)
            {
                sb.Append(VideoCodec ?? "video");

                if (Width > 0 && Height > 0)
                    sb.Append(' ').Append(Width).Append('x').Append(Height);

                if (FrameRate > 0d)
                    sb.Append(' ').Append(FrameRate.ToString("0.##", CultureInfo.InvariantCulture)).Append("fps");
            }

            if (HasAudio)
            {
                if (sb.Length > 0)
                    sb.Append(" + ");

                sb.Append(AudioCodec ?? "audio");

                if (Channels > 0)
                    sb.Append(' ').Append(Channels).Append("ch");
            }

            return sb.Length == 0 ? "nothing playable in it" : sb.ToString();
        }
    }
}
