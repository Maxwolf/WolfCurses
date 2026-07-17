// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/17/2026

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WolfCurses.Graphics;
using WolfCurses.Graphics.Decoding;
using WolfCurses.Tests.Support;
using Xunit;

namespace WolfCurses.Tests.Graphics
{
    /// <summary>
    ///     Covers decoding an animated GIF as an animation rather than as its first frame.
    ///     <para>
    ///         None of this can be settled by comparing against StbImageSharp the way every other decoder here is
    ///         (<see cref="DecoderDifferentialTests" />): stb decodes one frame and has no concept of a second, so on
    ///         the only question that matters here it has no opinion to differ from. Nor do the repository's two real
    ///         GIFs help — both use disposal method 1 for every frame, so the file corpus exercises one of the four
    ///         methods. What is left is fixtures built byte by byte, small enough that the expected pixels can be worked
    ///         out by hand and be sure of, which is what these are.
    ///     </para>
    ///     <para>
    ///         The palette throughout is four entries: 0 red, 1 blue, 2 green, 3 white.
    ///     </para>
    /// </summary>
    public class GifAnimationTests
    {
        private const int Red = 0;
        private const int Blue = 1;
        private const int Green = 2;
        private const int White = 3;

        /// <summary>Disposal method 1: leave the frame showing and draw the next one over it.</summary>
        private const int Keep = 1;

        /// <summary>Disposal method 2: take the frame's rectangle back to the canvas colour.</summary>
        private const int RestoreBackground = 2;

        /// <summary>Disposal method 3: put back whatever the frame covered up.</summary>
        private const int RestorePrevious = 3;

        /// <summary>No transparent index declared for this frame.</summary>
        private const int Opaque = -1;

        [Fact]
        public void LaterFrames_AreCompositedOntoTheOnesBeforeThem()
        {
            // The single fact the whole feature rests on. A GIF encoder writes the first frame in full and then stores
            // only a rectangle around whatever moved, so a decoder that starts each frame on a fresh canvas hands back
            // one picture followed by a series of fragments floating in nothing. media/animated.gif is 91 frames with
            // 83 distinct rectangles, so it is almost entirely made of this.
            //
            // The background is green and nothing in this file ever paints green, which is what gives the test teeth: a
            // decoder starting each frame afresh would fill the canvas from the background index, leaving green where
            // the assertions below want the first frame's red. A red background would let that bug straight through,
            // since the wrong answer and the right one would be the same colour.
            var gif = Begin(4, 4, Green);
            WriteFrame(gif, 0, 0, 4, 4, Keep, 0, Opaque, Fill(16, Red));
            WriteFrame(gif, 1, 1, 2, 2, Keep, 0, Opaque, Fill(4, Blue));
            End(gif);

            var frames = new GifDecoder().DecodeFramesBytes(gif.ToArray()).ToList();

            Assert.Equal(2, frames.Count);
            AssertPixel(frames[1].Image, 1, 1, Blue);
            AssertPixel(frames[1].Image, 2, 2, Blue);

            // Outside the second frame's rectangle the first frame is still there, which is the point.
            AssertPixel(frames[1].Image, 0, 0, Red);
            AssertPixel(frames[1].Image, 3, 3, Red);
        }

        [Fact]
        public void TransparentPixelsInALaterFrame_LeaveWhatWasUnderneathAlone()
        {
            // The other half of the same mechanism: within its rectangle, a frame marks the pixels that did not change
            // with the transparent index rather than repeating them. So transparency in an animation is not really
            // transparency at all — it means "keep", and treating it as "erase to nothing" punches holes through the
            // animation. Green background again, for the reason given above.
            var gif = Begin(4, 4, Green);
            WriteFrame(gif, 0, 0, 4, 4, Keep, 0, Opaque, Fill(16, Red));
            WriteFrame(gif, 1, 1, 2, 2, Keep, 0, White, Blue, White, White, Blue);
            End(gif);

            var frames = new GifDecoder().DecodeFramesBytes(gif.ToArray()).ToList();

            AssertPixel(frames[1].Image, 1, 1, Blue);
            AssertPixel(frames[1].Image, 2, 2, Blue);
            AssertPixel(frames[1].Image, 2, 1, Red); // transparent: the first frame shows through
            AssertPixel(frames[1].Image, 1, 2, Red);
        }

        [Fact]
        public void DisposalRestoreBackground_ClearsOnlyThatFramesRectangle()
        {
            // Method 2 undoes the frame after its time is up, so the third frame here is composited onto a canvas with
            // a green hole where the second frame was — not onto the second frame, and not onto a blank screen. The
            // background index is what the hole is filled from, which is the one thing the logical screen descriptor's
            // background field is actually for.
            var gif = Begin(4, 4, Green);
            WriteFrame(gif, 0, 0, 4, 4, Keep, 0, Opaque, Fill(16, Red));
            WriteFrame(gif, 1, 1, 2, 2, RestoreBackground, 0, Opaque, Fill(4, Blue));
            WriteFrame(gif, 0, 0, 1, 1, Keep, 0, Opaque, White);
            End(gif);

            var frames = new GifDecoder().DecodeFramesBytes(gif.ToArray()).ToList();

            Assert.Equal(3, frames.Count);

            // While it is showing, the second frame is simply drawn on top; disposal has not happened yet.
            AssertPixel(frames[1].Image, 1, 1, Blue);

            AssertPixel(frames[2].Image, 0, 0, White); // the third frame itself
            AssertPixel(frames[2].Image, 1, 1, Green); // disposed back to the background
            AssertPixel(frames[2].Image, 2, 2, Green);
            AssertPixel(frames[2].Image, 3, 3, Red); // untouched by any of it
        }

        [Fact]
        public void DisposalRestoreBackground_ClearsToNothingWhenTheFileDeclaresTransparency()
        {
            // The condition on the background colour, applied to disposal rather than to the first frame: a file that
            // declares a transparent index is meant to be composited over something the decoder cannot see, so its
            // background is that same nothing. This is also what browsers do, and since every animation worth the name
            // declares transparency it is what method 2 means in practice.
            var gif = Begin(4, 4, Green);
            WriteFrame(gif, 0, 0, 4, 4, Keep, 0, Opaque, Fill(16, Red));
            WriteFrame(gif, 1, 1, 2, 2, RestoreBackground, 0, White, Fill(4, Blue));
            WriteFrame(gif, 0, 0, 1, 1, Keep, 0, Opaque, White);
            End(gif);

            var frames = new GifDecoder().DecodeFramesBytes(gif.ToArray()).ToList();

            Assert.Equal(0, frames[2].Image.GetPixel(1, 1).A);
            Assert.Equal(0, frames[2].Image.GetPixel(2, 2).A);
            AssertPixel(frames[2].Image, 3, 3, Red);
        }

        [Fact]
        public void DisposalRestorePrevious_PutsBackWhatTheFrameCoveredUp()
        {
            // Method 3 is the awkward one: it needs the canvas as it stood *before* the frame was drawn, so the copy has
            // to be taken ahead of drawing, since drawing is what destroys the thing being kept. Get the order wrong and
            // this restores the frame it was supposed to undo.
            var gif = Begin(4, 4, Green);
            WriteFrame(gif, 0, 0, 4, 4, Keep, 0, Opaque, Fill(16, Red));
            WriteFrame(gif, 1, 1, 2, 2, RestorePrevious, 0, Opaque, Fill(4, Blue));
            WriteFrame(gif, 0, 0, 1, 1, Keep, 0, Opaque, White);
            End(gif);

            var frames = new GifDecoder().DecodeFramesBytes(gif.ToArray()).ToList();

            AssertPixel(frames[1].Image, 1, 1, Blue); // shown, then undone

            AssertPixel(frames[2].Image, 0, 0, White);
            AssertPixel(frames[2].Image, 1, 1, Red); // back to the first frame, not to the background
            AssertPixel(frames[2].Image, 2, 2, Red);
        }

        [Fact]
        public void FrameDelay_IsReportedInRealTimeRatherThanHundredths()
        {
            var gif = Begin(4, 4, Red);
            WriteFrame(gif, 0, 0, 4, 4, Keep, 7, Opaque, Fill(16, Red));
            WriteFrame(gif, 0, 0, 4, 4, Keep, 250, Opaque, Fill(16, Blue));
            End(gif);

            var frames = new GifDecoder().DecodeFramesBytes(gif.ToArray()).ToList();

            Assert.Equal(TimeSpan.FromMilliseconds(70), frames[0].Delay);
            Assert.Equal(TimeSpan.FromMilliseconds(2500), frames[1].Delay);
        }

        [Fact]
        public void ZeroDelay_IsPassedOnUntouched()
        {
            // Deliberately not clamped here. A great many real files say 0, and browsers each impose a different floor;
            // choosing one would be a display decision made somewhere that cannot see the display. The example app's
            // player is where that floor lives.
            var gif = Begin(4, 4, Red);
            WriteFrame(gif, 0, 0, 4, 4, Keep, 0, Opaque, Fill(16, Red));
            End(gif);

            var frames = new GifDecoder().DecodeFramesBytes(gif.ToArray()).ToList();

            Assert.Equal(TimeSpan.Zero, frames[0].Delay);
        }

        [Fact]
        public void AFrameWithNoGraphicControlExtension_GetsTheDefaults()
        {
            // Every GIF written before 89a looks like this, and a frame in an 89a file is not obliged to have one
            // either. No delay, no transparency, and leave the frame where it is.
            var gif = Begin(4, 4, Red);
            gif.Write(new byte[] {0x2C, 0x00, 0x00, 0x00, 0x00, 0x04, 0x00, 0x04, 0x00, 0x00});
            WriteRaster(gif, Fill(16, Blue));
            End(gif);

            var frames = new GifDecoder().DecodeFramesBytes(gif.ToArray()).ToList();

            Assert.Single(frames);
            Assert.Equal(TimeSpan.Zero, frames[0].Delay);
            AssertPixel(frames[0].Image, 0, 0, Blue);
        }

        [Fact]
        public void GraphicControlState_DoesNotLeakFromOneFrameToTheNext()
        {
            // A graphic control extension describes the one frame that follows it, so a frame arriving without one must
            // not inherit the last frame's delay or transparency. Keeping this state in a loop variable is exactly the
            // shape of mistake that survives every single-frame test.
            var gif = Begin(4, 4, Red);
            WriteFrame(gif, 0, 0, 4, 4, Keep, 50, White, Fill(16, Red));
            gif.Write(new byte[] {0x2C, 0x00, 0x00, 0x00, 0x00, 0x04, 0x00, 0x04, 0x00, 0x00});
            WriteRaster(gif, Fill(16, White));
            End(gif);

            var frames = new GifDecoder().DecodeFramesBytes(gif.ToArray()).ToList();

            Assert.Equal(TimeSpan.FromMilliseconds(500), frames[0].Delay);
            Assert.Equal(TimeSpan.Zero, frames[1].Delay);

            // White was the transparent index for the first frame only. Had it leaked, this frame would paint nothing
            // and still be red.
            AssertPixel(frames[1].Image, 0, 0, White);
        }

        [Fact]
        public void BadDataIsReportedFromTheCallRatherThanTheEnumeration()
        {
            // DecodeFrames hands back a lazy sequence, which puts an ordinary trap in reach: if validation were deferred
            // too, the exception for "this is not a GIF" would come out of a foreach somewhere else entirely, quite
            // possibly after the stream it was read from had been closed. Note there is no enumeration here at all.
            var decoder = new GifDecoder();

            Assert.Throws<InvalidDataException>(() => decoder.DecodeFramesBytes(new byte[] {1, 2, 3, 4, 5, 6, 7, 8}));
            Assert.Throws<ArgumentNullException>(() => decoder.DecodeFramesBytes(null));
        }

        [Fact]
        public void DamageFoundPartWayThrough_KeepsTheFramesThatArrivedFirst()
        {
            // The other side of the same coin: damage that can only be found once the walk reaches it has to surface
            // from the enumeration, by which time earlier frames have been handed over and cannot be taken back. This
            // also demonstrates the laziness — frame one is produced before the block that throws is ever looked at.
            var gif = Begin(4, 4, Red);
            WriteFrame(gif, 0, 0, 4, 4, Keep, 0, Opaque, Fill(16, Red));
            gif.WriteByte(0x99); // not an image, an extension, or the trailer
            End(gif);

            var frames = new GifDecoder().DecodeFramesBytes(gif.ToArray());
            var collected = new List<GifFrame>();

            var ex = Assert.Throws<InvalidDataException>(() =>
            {
                foreach (var frame in frames)
                    collected.Add(frame);
            });

            Assert.Single(collected);
            AssertPixel(collected[0].Image, 0, 0, Red);
            Assert.Contains("0x99", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void DecodeAgreesWithTheFirstFrameOfDecodeFrames()
        {
            // The seam's still answer and the animation's first frame are the same picture, and have to stay that way:
            // they are now the same code path, and this is what says so if someone splits them again.
            var gif = Begin(4, 4, Green);
            WriteFrame(gif, 1, 1, 2, 2, Keep, 0, Opaque, Fill(4, Blue));
            WriteFrame(gif, 0, 0, 4, 4, Keep, 0, Opaque, Fill(16, White));
            End(gif);

            var data = gif.ToArray();
            var still = new GifDecoder().DecodeBytes(data);
            var first = new GifDecoder().DecodeFramesBytes(data).First().Image;

            Assert.Equal(first.Data, still.Data);
        }

        [Fact]
        public void RealAnimation_HasEveryFrameTheFileCarries()
        {
            // media/animated.gif: 91 frames of 3 centiseconds on a 540x540 screen, each stored as a small rectangle of
            // whatever moved. The frame count is the assertion that the walk does not stop early or lose its place in
            // the middle of a real file, which no hand-built fixture of four pixels can show.
            Assert.SkipUnless(File.Exists(TestImages.Media("animated.gif") ?? ""), "media/animated.gif is not present.");

            using var stream = File.OpenRead(TestImages.Media("animated.gif"));
            var frames = new GifDecoder().DecodeFrames(stream).ToList();

            Assert.Equal(91, frames.Count);
            Assert.All(frames, frame =>
            {
                Assert.Equal(540, frame.Image.Width);
                Assert.Equal(540, frame.Image.Height);
                Assert.Equal(TimeSpan.FromMilliseconds(30), frame.Delay);
            });
        }

        [Fact]
        public void RealAnimation_ActuallyMovesFromFrameToFrame()
        {
            // A decoder that yielded the same canvas 91 times, or lost the compositing entirely and yielded 91 near-
            // empty ones, would still pass the frame count above. This asks whether the pictures differ from each other
            // and whether they are pictures at all.
            Assert.SkipUnless(File.Exists(TestImages.Media("animated.gif") ?? ""), "media/animated.gif is not present.");

            using var stream = File.OpenRead(TestImages.Media("animated.gif"));
            var frames = new GifDecoder().DecodeFrames(stream).Take(12).ToList();

            var moved = 0;
            for (var i = 1; i < frames.Count; i++)
                if (!frames[i].Image.Data.AsSpan().SequenceEqual(frames[i - 1].Image.Data))
                    moved++;

            Assert.True(moved >= 10, $"Only {moved} of 11 frame transitions changed anything; the animation is static.");

            // Every frame is a whole picture rather than a bare delta rectangle floating in transparency. The first
            // frame paints the screen and disposal method 1 never clears it, so coverage cannot fall away.
            Assert.All(frames, frame =>
            {
                var opaque = 0;
                for (var i = 3; i < frame.Image.Data.Length; i += 4)
                    if (frame.Image.Data[i] != 0)
                        opaque++;

                Assert.True(opaque > frame.Image.Data.Length / 4 / 2,
                    $"Only {opaque} pixels of {frame.Image.Data.Length / 4} are opaque; frames are not accumulating.");
            });
        }

        [Fact]
        public void RealTransparentAnimation_TakesEachFrameBackToNothingAsTheFileAsksFor()
        {
            // media/transparent_anim.gif is the only fixture that uses disposal method 2, and it uses it on seven of
            // its eight frames with transparency declared — so each frame's rectangle goes back to nothing when its
            // time is up rather than being left for the next frame to paint over.
            //
            // The assertion needs no oracle, which is the point of it: it is structural. Pixels that were opaque in one
            // frame are transparent in the next, and a decoder that ignored disposal and simply drew each frame over
            // the last could not produce that at any point in any file. Painting only ever adds opaque pixels, so the
            // covered area would climb towards the union of every rectangle and never once fall.
            Assert.SkipUnless(File.Exists(TestImages.Media("transparent_anim.gif") ?? ""),
                "media/transparent_anim.gif is not present.");

            using var stream = File.OpenRead(TestImages.Media("transparent_anim.gif"));
            var frames = new GifDecoder().DecodeFrames(stream).ToList();

            Assert.Equal(8, frames.Count);
            Assert.All(frames, frame =>
            {
                Assert.Equal(200, frame.Image.Width);
                Assert.Equal(197, frame.Image.Height);
                Assert.Equal(TimeSpan.FromMilliseconds(100), frame.Delay);
            });

            for (var i = 1; i < frames.Count; i++)
            {
                var cleared = CountCleared(frames[i - 1].Image, frames[i].Image);
                Assert.True(cleared > 0,
                    $"Frame {i} took nothing back that frame {i - 1} had painted, which painting alone cannot do: " +
                    "disposal method 2 is not being honoured.");
            }

            // The same fact from the other side: coverage holds steady instead of accumulating.
            var first = CountOpaque(frames[0].Image);
            var last = CountOpaque(frames[^1].Image);
            Assert.True(last < first * 1.2,
                $"Opaque pixels grew from {first} to {last} across the animation; frames are piling up.");
        }

        [Fact]
        public void AStillGif_IsAnAnimationOfOneFrame()
        {
            var gif = Begin(4, 4, Red);
            WriteFrame(gif, 0, 0, 4, 4, Keep, 0, Opaque, Fill(16, Blue));
            End(gif);

            Assert.Single(new GifDecoder().DecodeFramesBytes(gif.ToArray()));
        }

        /// <summary>Starts a GIF: header, 4x4-or-whatever logical screen, and the four-entry global palette.</summary>
        private static MemoryStream Begin(int screenWidth, int screenHeight, int backgroundIndex)
        {
            var gif = new MemoryStream();
            gif.Write("GIF89a"u8.ToArray());
            gif.Write(new[] {(byte) screenWidth, (byte) 0, (byte) screenHeight, (byte) 0});

            // 0x81: a global table is present, and its size field of 1 means 2 << 1 = 4 entries.
            gif.Write(new[] {(byte) 0x81, (byte) backgroundIndex, (byte) 0});
            gif.Write(new byte[] {0xFF, 0x00, 0x00}); // 0 red
            gif.Write(new byte[] {0x00, 0x00, 0xFF}); // 1 blue
            gif.Write(new byte[] {0x00, 0xFF, 0x00}); // 2 green
            gif.Write(new byte[] {0xFF, 0xFF, 0xFF}); // 3 white
            return gif;
        }

        /// <summary>Writes the trailer.</summary>
        private static void End(MemoryStream gif)
        {
            gif.WriteByte(0x3B);
        }

        /// <summary>Writes a graphic control extension, an image descriptor, and the frame's pixels.</summary>
        private static void WriteFrame(MemoryStream gif, int left, int top, int width, int height, int disposal,
            int delayCentiseconds, int transparentIndex, params int[] pixels)
        {
            Assert.Equal(width * height, pixels.Length);

            var packed = (byte) ((disposal << 2) | (transparentIndex >= 0 ? 1 : 0));
            gif.Write(new byte[] {0x21, 0xF9, 0x04});
            gif.Write(new[]
            {
                packed,
                (byte) (delayCentiseconds & 0xFF), (byte) (delayCentiseconds >> 8),
                (byte) Math.Max(transparentIndex, 0),
                (byte) 0
            });

            gif.WriteByte(0x2C);
            gif.Write(new[] {(byte) left, (byte) 0, (byte) top, (byte) 0});
            gif.Write(new[] {(byte) width, (byte) 0, (byte) height, (byte) 0});
            gif.WriteByte(0x00); // no local table, not interlaced
            WriteRaster(gif, pixels);
        }

        /// <summary>Writes the LZW minimum code size and the frame's compressed pixels as one sub-block chain.</summary>
        private static void WriteRaster(MemoryStream gif, params int[] pixels)
        {
            var payload = EncodePixels(pixels);
            Assert.True(payload.Length <= 255, "Fixture frame is too big for a single sub-block.");

            gif.WriteByte(0x02); // LZW minimum code size for a four-entry palette
            gif.WriteByte((byte) payload.Length);
            gif.Write(payload);
            gif.WriteByte(0x00); // block terminator
        }

        /// <summary>
        ///     Encodes pixels as an LZW stream that compresses nothing at all.
        ///     <para>
        ///         Every literal is preceded by a clear code, which resets the dictionary to its opening size and so
        ///         holds the code width at three bits for the whole stream. That is pointless as compression and the
        ///         entire point here: a fixed-width stream can be worked out on paper and checked, where a real
        ///         encoder's output could only be checked by the decoder under test.
        ///     </para>
        /// </summary>
        private static byte[] EncodePixels(int[] pixels)
        {
            const int minCodeSize = 2;
            const int width = minCodeSize + 1;
            const int clear = 1 << minCodeSize;
            const int endOfInformation = clear + 1;

            var bytes = new List<byte>();
            var accumulator = 0;
            var bits = 0;

            void Write(int code)
            {
                accumulator |= code << bits;
                bits += width;
                while (bits >= 8)
                {
                    bytes.Add((byte) (accumulator & 0xFF));
                    accumulator >>= 8;
                    bits -= 8;
                }
            }

            foreach (var pixel in pixels)
            {
                Write(clear);
                Write(pixel);
            }

            Write(endOfInformation);
            if (bits > 0)
                bytes.Add((byte) accumulator);

            return bytes.ToArray();
        }

        /// <summary>Counts pixels that <paramref name="before" /> had painted and <paramref name="after" /> does not.</summary>
        private static int CountCleared(PixelBuffer before, PixelBuffer after)
        {
            var cleared = 0;
            for (var i = 3; i < after.Data.Length; i += 4)
                if (before.Data[i] != 0 && after.Data[i] == 0)
                    cleared++;

            return cleared;
        }

        /// <summary>Counts pixels that are not fully transparent.</summary>
        private static int CountOpaque(PixelBuffer image)
        {
            var opaque = 0;
            for (var i = 3; i < image.Data.Length; i += 4)
                if (image.Data[i] != 0)
                    opaque++;

            return opaque;
        }

        /// <summary>A run of one palette index, for a frame of a single colour.</summary>
        private static int[] Fill(int count, int index)
        {
            return Enumerable.Repeat(index, count).ToArray();
        }

        private static void AssertPixel(PixelBuffer image, int x, int y, int paletteIndex)
        {
            var expected = paletteIndex switch
            {
                Red => new Rgba32(255, 0, 0, 255),
                Blue => new Rgba32(0, 0, 255, 255),
                Green => new Rgba32(0, 255, 0, 255),
                _ => new Rgba32(255, 255, 255, 255)
            };

            var actual = image.GetPixel(x, y);
            Assert.True(
                expected.R == actual.R && expected.G == actual.G && expected.B == actual.B && expected.A == actual.A,
                $"({x},{y}): expected {expected.R},{expected.G},{expected.B},{expected.A} " +
                $"got {actual.R},{actual.G},{actual.B},{actual.A}");
        }
    }
}
