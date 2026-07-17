// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/17/2026

using System.IO;
using WolfCurses.Graphics;
using WolfCurses.Graphics.Decoding;
using WolfCurses.Tests.Support;
using Xunit;

namespace WolfCurses.Tests.Graphics
{
    /// <summary>
    ///     Covers the corners of GIF that real files do not reach, so cannot be settled by comparing against another
    ///     decoder on the fixtures in <c>media/</c>.
    ///     <para>
    ///         The canvas around a frame is the main one. Every encoder in practice writes its first frame at full
    ///         size, so nothing in a corpus of real GIFs ever shows the background at all — and the one decoder handy
    ///         to compare against has a bug exactly here (stb_image fills with the palette entry's red and blue
    ///         swapped, and only when the index is not zero), so agreeing with it would be a worse outcome than
    ///         disagreeing. These are built byte by byte and checked against values worked out by hand instead.
    ///     </para>
    /// </summary>
    /// <seealso cref="DecoderDifferentialTests" />
    public class GifDecoderTests
    {
        private static readonly Rgba32 _red = new(255, 0, 0, 255);
        private static readonly Rgba32 _blue = new(0, 0, 255, 255);

        /// <summary>
        ///     A 4x4 GIF whose 2x2 frame sits at (1,1), leaving a one-pixel border of canvas all the way round —
        ///     the shape no real encoder produces and the only one that shows what the background rule does.
        ///     <para>
        ///         The LZW payload is four literal zeroes, each preceded by a clear code. Clearing between every
        ///         pixel is pointless compression and deliberate here: it holds the dictionary at its opening size so
        ///         the code width never steps up from three bits, which is what makes the bitstream short enough to
        ///         work out by hand and be sure of.
        ///     </para>
        /// </summary>
        /// <param name="backgroundIndex">Palette entry the canvas should be filled from.</param>
        /// <param name="transparentIndex">Transparent palette entry, or -1 to declare none.</param>
        private static byte[] CroppedFrameGif(int backgroundIndex, int transparentIndex)
        {
            var gif = new MemoryStream();
            gif.Write("GIF89a"u8.ToArray());
            gif.Write([0x04, 0x00, 0x04, 0x00]); // 4x4 logical screen
            gif.Write([0x80, (byte) backgroundIndex, 0x00]); // global table of 2, background index, aspect
            gif.Write([0xFF, 0x00, 0x00]); // entry 0: red
            gif.Write([0x00, 0x00, 0xFF]); // entry 1: blue

            if (transparentIndex >= 0)
                gif.Write([0x21, 0xF9, 0x04, 0x01, 0x00, 0x00, (byte) transparentIndex, 0x00]);

            gif.Write([0x2C]); // image descriptor
            gif.Write([0x01, 0x00, 0x01, 0x00]); // at (1,1)
            gif.Write([0x02, 0x00, 0x02, 0x00]); // 2x2
            gif.Write([0x00]); // no local table, not interlaced
            gif.Write([0x02]); // LZW minimum code size
            gif.Write([0x04, 0x04, 0x41, 0x10, 0x05]); // clear,0,clear,0,clear,0,clear,0,end
            gif.Write([0x00, 0x3B]); // block terminator, trailer
            return gif.ToArray();
        }

        [Fact]
        public void CroppedFrame_WithNoTransparency_PaintsTheCanvasWithTheBackgroundColour()
        {
            // The background index names an entry of the global table and the canvas is filled from it — what the
            // specification says the field is for, and what ffmpeg and GDI+ both do. Index 1 is blue, so a decoder
            // that read the entry backwards would hand back red here and be caught.
            var image = new GifDecoder().DecodeBytes(CroppedFrameGif(1, -1));

            Assert.Equal(4, image.Width);
            Assert.Equal(4, image.Height);

            for (var y = 0; y < 4; y++)
            for (var x = 0; x < 4; x++)
            {
                var expected = x is 1 or 2 && y is 1 or 2 ? _red : _blue;
                var actual = image.GetPixel(x, y);
                Assert.True(
                    expected.R == actual.R && expected.G == actual.G && expected.B == actual.B && actual.A == 255,
                    $"({x},{y}): expected {expected.R},{expected.G},{expected.B} got {actual.R},{actual.G},{actual.B},{actual.A}");
            }
        }

        [Fact]
        public void CroppedFrame_DeclaringATransparentIndex_LeavesTheCanvasTransparent()
        {
            // The one condition on the rule. A file with a transparent index is meant to be composited over something
            // it cannot see, so the canvas outside its frame is the same nothing its transparent pixels are —
            // painting a colour there would leave the border round a cropped sticker more opaque than the sticker.
            // ffmpeg and Pillow both read it this way. (The transparent index here is 1, which the frame never uses,
            // so the frame itself still paints.)
            var image = new GifDecoder().DecodeBytes(CroppedFrameGif(1, 1));

            Assert.Equal(0, image.GetPixel(0, 0).A);
            Assert.Equal(0, image.GetPixel(3, 3).A);
            Assert.Equal(255, image.GetPixel(1, 1).A);
            Assert.Equal(255, image.GetPixel(1, 1).R);
        }

        [Fact]
        public void FrameCoveringTheScreen_IgnoresTheBackgroundEntirely()
        {
            // The common case, and the one that must cost nothing: a full-size frame leaves no canvas showing, so the
            // background colour is irrelevant however hostile the index is. Index 200 does not exist in a two-entry
            // table; reading it would be an out-of-bounds access on a file that is not even malformed.
            var gif = new MemoryStream();
            gif.Write("GIF89a"u8.ToArray());
            gif.Write([0x02, 0x00, 0x02, 0x00]); // 2x2 logical screen
            gif.Write([0x80, 200, 0x00]); // background index far past the table
            gif.Write([0xFF, 0x00, 0x00]);
            gif.Write([0x00, 0x00, 0xFF]);
            gif.Write([0x2C, 0x00, 0x00, 0x00, 0x00, 0x02, 0x00, 0x02, 0x00, 0x00]); // frame fills the screen
            gif.Write([0x02, 0x04, 0x04, 0x41, 0x10, 0x05, 0x00, 0x3B]);

            var image = new GifDecoder().DecodeBytes(gif.ToArray());

            Assert.Equal(255, image.GetPixel(0, 0).R);
            Assert.Equal(255, image.GetPixel(1, 1).A);
        }

        [Fact]
        public void Gif87a_IsAcceptedAsReadilyAs89a()
        {
            var gif = CroppedFrameGif(1, -1);
            gif[4] = (byte) '7';

            var image = new GifDecoder().DecodeBytes(gif);

            Assert.Equal(4, image.Width);
        }

        [Fact]
        public void AnimatedGif_DecodesToItsFirstFrameRatherThanRefusing()
        {
            // The seam's answer, pinned: IImageDecoder.Decode returns one PixelBuffer, so this is where an animation
            // has to become a still. The first frame is what a browser shows before it starts and what a thumbnailer
            // picks, which makes it the least surprising choice — but it is a choice, so it is asserted rather than
            // left to be discovered. GifDecoder.DecodeFrames is the way in for a caller that wants the rest; see
            // GifAnimationTests, which also pins that this frame and that one's first are the same picture.
            Assert.SkipUnless(File.Exists(TestImages.Media("cool.gif") ?? ""), "media/cool.gif is not present.");

            using var stream = File.OpenRead(TestImages.Media("cool.gif"));
            var image = new GifDecoder().Decode(stream);

            Assert.Equal(426, image.Width);
            Assert.Equal(318, image.Height);
        }

        [Fact]
        public void Truncated_HandsBackWhatArrivedRatherThanThrowing()
        {
            // Truncation is the ordinary way for a GIF to be damaged, and half a picture is worth more than an
            // exception to something whose only job is to show it. Tk has shipped a demo image in this state for
            // years and every other decoder draws it.
            var full = CroppedFrameGif(1, -1);
            var cut = new byte[full.Length - 4];
            System.Array.Copy(full, cut, cut.Length);

            var image = new GifDecoder().DecodeBytes(cut);

            Assert.Equal(4, image.Width);
        }

        [Fact]
        public void MinimumCodeSizeOutsideTheFormat_IsRefusedWithTheValue()
        {
            // Counting back: trailer, terminator, four payload bytes, the sub-block length, then the code size.
            var gif = CroppedFrameGif(1, -1);
            gif[^8] = 9;

            var ex = Assert.Throws<InvalidDataException>(() => new GifDecoder().DecodeBytes(gif));

            Assert.Contains("9", ex.Message, System.StringComparison.Ordinal);
        }
    }
}
