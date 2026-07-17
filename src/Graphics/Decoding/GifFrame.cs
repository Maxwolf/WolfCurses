// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/17/2026

using System;

namespace WolfCurses.Graphics.Decoding
{
    /// <summary>
    ///     One frame of an animated GIF: the whole picture as it stands at that moment, and how long it stands there.
    ///     <para>
    ///         <see cref="Image" /> is the entire logical screen, already composited — <b>not</b> the rectangle the file
    ///         stored for this frame. That distinction is the whole reason this type exists rather than a bare
    ///         <see cref="PixelBuffer" /> list being enough. A GIF encoder writes the first frame in full and then, for
    ///         every frame after it, stores only a rectangle around whatever changed, filling the parts that did not
    ///         change with the transparent index to mean "leave what is underneath alone". The repository's own
    ///         <c>media/animated.gif</c> is 540x540 with 91 frames and <b>83 different frame rectangles</b>: handed those
    ///         rectangles raw, a caller would draw one picture and then ninety smears. So a frame here is what you would
    ///         actually see if the animation were paused on it, which is the only form that is any use to something whose
    ///         next move is to paint it.
    ///     </para>
    /// </summary>
    /// <seealso cref="GifDecoder.DecodeFrames(System.IO.Stream)" />
    public sealed class GifFrame
    {
        /// <summary>Initializes a new instance of the <see cref="GifFrame" /> class.</summary>
        /// <param name="image">The composited logical screen for this frame.</param>
        /// <param name="delay">How long this frame is shown before the next one replaces it.</param>
        public GifFrame(PixelBuffer image, TimeSpan delay)
        {
            Image = image ?? throw new ArgumentNullException(nameof(image));
            Delay = delay;
        }

        /// <summary>The whole logical screen as it appears while this frame is showing.</summary>
        public PixelBuffer Image { get; }

        /// <summary>
        ///     How long this frame is shown before the next replaces it, exactly as the file states it.
        ///     <para>
        ///         Reported rather than corrected, which is worth knowing before using it as a timer. GIF stores this in
        ///         hundredths of a second, and a great many files in the wild state 0 — meaning "as fast as you can" to
        ///         the browsers of 1998 and meaning nothing at all to anything else. Browsers answer that by quietly
        ///         imposing a floor, and they do not agree on it. Deciding it here would be this class making a display
        ///         decision on behalf of a caller it cannot see, the same reason the error texture lives in
        ///         <see cref="AnsiImage" /> and not in a decoder; a player that cares should clamp, and the example
        ///         app's animated GIF demo does.
        ///     </para>
        /// </summary>
        public TimeSpan Delay { get; }
    }
}
