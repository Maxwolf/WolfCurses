using System;
using WolfCurses.Graphics;
using Xunit;

namespace WolfCurses.Tests.Graphics
{
    /// <summary>
    ///     Exercises the decoder-agnostic pixel container and, most importantly, its area-averaging resize — including
    ///     the premultiplied-alpha behavior that stops transparent pixels from tinting their opaque neighbours.
    /// </summary>
    public class PixelBufferTests
    {
        [Fact]
        public void Constructor_WrongDataLength_Throws()
        {
            Assert.Throws<ArgumentException>(() => new PixelBuffer(2, 2, new byte[8]));
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 0)]
        [InlineData(-1, 4)]
        public void Constructor_NonPositiveDimensions_Throws(int width, int height)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PixelBuffer(width, height, new byte[16]));
        }

        [Fact]
        public void SetPixel_ThenGetPixel_RoundTrips()
        {
            var buffer = new PixelBuffer(2, 2);
            var color = new Rgba32(10, 20, 30, 40);
            buffer.SetPixel(1, 1, color);

            var read = buffer.GetPixel(1, 1);
            Assert.Equal(10, read.R);
            Assert.Equal(20, read.G);
            Assert.Equal(30, read.B);
            Assert.Equal(40, read.A);
        }

        [Fact]
        public void GetPixel_OutOfBounds_Throws()
        {
            var buffer = new PixelBuffer(2, 2);
            Assert.Throws<ArgumentOutOfRangeException>(() => buffer.GetPixel(2, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => buffer.GetPixel(0, 2));
        }

        [Fact]
        public void Resize_SameDimensions_ReturnsIndependentCopy()
        {
            var buffer = new PixelBuffer(1, 1);
            buffer.SetPixel(0, 0, new Rgba32(1, 2, 3, 4));

            var resized = buffer.Resize(1, 1);

            Assert.NotSame(buffer.Data, resized.Data);
            Assert.Equal(buffer.Data, resized.Data);
        }

        [Fact]
        public void Resize_SolidColorDownscale_PreservesColor()
        {
            // A 4x4 block of one opaque color must average down to exactly that color.
            var buffer = new PixelBuffer(4, 4);
            for (var y = 0; y < 4; y++)
            for (var x = 0; x < 4; x++)
                buffer.SetPixel(x, y, new Rgba32(60, 120, 180, 255));

            var small = buffer.Resize(1, 1);
            var pixel = small.GetPixel(0, 0);

            Assert.Equal(60, pixel.R);
            Assert.Equal(120, pixel.G);
            Assert.Equal(180, pixel.B);
            Assert.Equal(255, pixel.A);
        }

        [Fact]
        public void Resize_TransparentNeighbour_DoesNotBleedColorButAveragesAlpha()
        {
            // Left pixel opaque red, right pixel fully transparent blue. Averaging to a single pixel must keep the hue
            // pure red (the transparent blue contributes no color) while the alpha averages to half.
            var buffer = new PixelBuffer(2, 1);
            buffer.SetPixel(0, 0, new Rgba32(255, 0, 0, 255));
            buffer.SetPixel(1, 0, new Rgba32(0, 0, 255, 0));

            var merged = buffer.Resize(1, 1).GetPixel(0, 0);

            Assert.Equal(255, merged.R);
            Assert.Equal(0, merged.G);
            Assert.Equal(0, merged.B); // no blue bleed from the transparent pixel
            Assert.Equal(128, merged.A); // (255 + 0) / 2, rounded away from zero
        }

        [Fact]
        public void Resize_FullyTransparentRegion_StaysTransparent()
        {
            var buffer = new PixelBuffer(2, 2); // all zero => transparent black
            var merged = buffer.Resize(1, 1).GetPixel(0, 0);
            Assert.Equal(0, merged.A);
        }

        [Fact]
        public void Resize_Upscale_ProducesRequestedSize()
        {
            var buffer = new PixelBuffer(1, 1);
            buffer.SetPixel(0, 0, new Rgba32(200, 100, 50, 255));

            var big = buffer.Resize(3, 2);

            Assert.Equal(3, big.Width);
            Assert.Equal(2, big.Height);
            // Upscaling a solid pixel keeps the color everywhere.
            Assert.Equal(200, big.GetPixel(2, 1).R);
            Assert.Equal(100, big.GetPixel(2, 1).G);
        }

        [Fact]
        public void Constructor_DimensionsOverflowByteCount_ThrowsInsteadOfWrapping()
        {
            // 40000 * 40000 * 4 = 6.4e9 overflows a 32-bit byte count; it must be rejected, not silently wrapped into
            // a small, plausible-looking length that would later crash or corrupt memory.
            Assert.Throws<ArgumentOutOfRangeException>(() => new PixelBuffer(40000, 40000));
        }

        [Fact]
        public void Resize_DimensionsOverflowByteCount_ThrowsInsteadOfWrapping()
        {
            var buffer = new PixelBuffer(2, 2);
            Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Resize(40000, 40000));
        }

        [Fact]
        public void Crop_ExtractsRequestedRegion()
        {
            // Fill each pixel with a color encoding its (x,y) so we can verify the exact region copied.
            var buffer = new PixelBuffer(4, 4);
            for (var y = 0; y < 4; y++)
            for (var x = 0; x < 4; x++)
                buffer.SetPixel(x, y, new Rgba32((byte) (x * 10), (byte) (y * 10), 0, 255));

            var region = buffer.Crop(1, 2, 2, 2);

            Assert.Equal(2, region.Width);
            Assert.Equal(2, region.Height);
            Assert.Equal(10, region.GetPixel(0, 0).R); // source x=1
            Assert.Equal(20, region.GetPixel(0, 0).G); // source y=2
            Assert.Equal(20, region.GetPixel(1, 1).R); // source x=2
            Assert.Equal(30, region.GetPixel(1, 1).G); // source y=3
        }

        [Fact]
        public void Crop_OutsideBounds_Throws()
        {
            var buffer = new PixelBuffer(4, 4);
            Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Crop(3, 0, 2, 2));
        }

        [Fact]
        public void DrawImage_OpaqueOverlay_ReplacesPixels()
        {
            var background = new PixelBuffer(2, 2);
            for (var y = 0; y < 2; y++)
            for (var x = 0; x < 2; x++)
                background.SetPixel(x, y, new Rgba32(255, 0, 0, 255));

            var overlay = new PixelBuffer(1, 1);
            overlay.SetPixel(0, 0, new Rgba32(0, 0, 255, 255));

            background.DrawImage(overlay, 0, 0);

            Assert.Equal(0, background.GetPixel(0, 0).R);
            Assert.Equal(255, background.GetPixel(0, 0).B); // replaced with blue
            Assert.Equal(255, background.GetPixel(1, 1).R); // untouched red
        }

        [Fact]
        public void DrawImage_FullyTransparentOverlay_LeavesBackgroundUntouched()
        {
            var background = new PixelBuffer(1, 1);
            background.SetPixel(0, 0, new Rgba32(10, 20, 30, 255));

            var overlay = new PixelBuffer(1, 1);
            overlay.SetPixel(0, 0, new Rgba32(0, 0, 255, 0)); // transparent

            background.DrawImage(overlay, 0, 0);

            var pixel = background.GetPixel(0, 0);
            Assert.Equal(10, pixel.R);
            Assert.Equal(20, pixel.G);
            Assert.Equal(30, pixel.B);
            Assert.Equal(255, pixel.A);
        }

        [Fact]
        public void DrawImage_SemiTransparentOverOpaque_Blends()
        {
            var background = new PixelBuffer(1, 1);
            background.SetPixel(0, 0, new Rgba32(0, 0, 0, 255)); // black

            var overlay = new PixelBuffer(1, 1);
            overlay.SetPixel(0, 0, new Rgba32(255, 255, 255, 128)); // 50% white

            background.DrawImage(overlay, 0, 0);

            var pixel = background.GetPixel(0, 0);
            Assert.Equal(128, pixel.R); // halfway between black and white
            Assert.Equal(255, pixel.A); // stays fully opaque
        }

        [Fact]
        public void DrawImage_OverTransparentBackground_KeepsOverlayTranslucency()
        {
            // Compositing a translucent overlay onto a transparent background must keep the overlay's own translucency
            // (so you can still see through it / stack another image under it) rather than forcing it opaque.
            var background = new PixelBuffer(1, 1); // transparent black
            var overlay = new PixelBuffer(1, 1);
            overlay.SetPixel(0, 0, new Rgba32(100, 100, 100, 128));

            background.DrawImage(overlay, 0, 0);

            var pixel = background.GetPixel(0, 0);
            Assert.Equal(100, pixel.R);
            Assert.Equal(128, pixel.A); // still half transparent
        }

        [Fact]
        public void DrawImage_NegativeOffset_ClipsWithoutThrowing()
        {
            var background = new PixelBuffer(2, 2);
            var overlay = new PixelBuffer(2, 2);
            for (var y = 0; y < 2; y++)
            for (var x = 0; x < 2; x++)
                overlay.SetPixel(x, y, new Rgba32(0, 0, 255, 255));

            // Overlay shifted up-left so only its bottom-right pixel lands on the background at (0,0).
            background.DrawImage(overlay, -1, -1);

            Assert.Equal(255, background.GetPixel(0, 0).B);
            Assert.Equal(0, background.GetPixel(1, 1).A); // still untouched/transparent
        }

        [Theory]
        [InlineData(900, 620)] // enlarge: the sixel/kitty canvas case
        [InlineData(300, 150)] // shrink: the photograph-into-a-small-terminal case
        public void Resize_AboveTheParallelThreshold_MatchesTheSequentialReference(int newWidth, int newHeight)
        {
            // Big resizes fan out across threads, one destination row each. Rows write disjoint slices of the
            // destination and read only the immutable source, so the bytes must come out identical to the plain
            // sequential algorithm — this compares against a verbatim copy of it, so a race, a mis-hoisted capture,
            // or a wrong row boundary shows up as a byte difference rather than as a rare on-screen shimmer.
            var source = NoisyBuffer(640, 400);

            var actual = source.Resize(newWidth, newHeight);
            var expected = ReferenceResize(source, newWidth, newHeight);

            Assert.Equal(expected, actual.Data);
        }

        [Fact]
        public void Resize_AboveTheParallelThreshold_IsDeterministicAcrossRuns()
        {
            // A scheduling race would be intermittent, so the identity check gets several chances to catch it.
            var source = NoisyBuffer(640, 400);
            var first = source.Resize(900, 620).Data;

            for (var run = 0; run < 5; run++)
                Assert.Equal(first, source.Resize(900, 620).Data);
        }

        /// <summary>
        ///     A deterministic pseudorandom image big enough (256K pixels) to cross the parallel threshold, with the
        ///     alpha variety the resize arithmetic cares about: opaque, fully transparent and part-transparent pixels.
        /// </summary>
        private static PixelBuffer NoisyBuffer(int width, int height)
        {
            var data = new byte[width * height * 4];
            var state = 12345u;
            for (var i = 0; i < data.Length; i += 4)
            {
                state = state * 1664525u + 1013904223u;
                data[i] = (byte) (state >> 8);
                data[i + 1] = (byte) (state >> 16);
                data[i + 2] = (byte) (state >> 24);
                data[i + 3] = (state & 3) switch
                {
                    0 => (byte) 0,
                    1 => (byte) 255,
                    _ => (byte) (state >> 4)
                };
            }

            return new PixelBuffer(width, height, data);
        }

        /// <summary>
        ///     The sequential area-averaging resize, kept verbatim from before the row loop went parallel, so the
        ///     production code has an independent answer to be equal to. Any deliberate change to the resize
        ///     arithmetic must be made in both places — that is this copy's entire job.
        /// </summary>
        private static byte[] ReferenceResize(PixelBuffer image, int newWidth, int newHeight)
        {
            var dst = new byte[newWidth * newHeight * 4];
            var scaleX = (double) image.Width / newWidth;
            var scaleY = (double) image.Height / newHeight;
            var data = image.Data;

            var columnLeft = new double[newWidth];
            var columnRight = new double[newWidth];
            var columnStart = new int[newWidth];
            var columnEnd = new int[newWidth];
            for (var dx = 0; dx < newWidth; dx++)
            {
                var left = dx * scaleX;
                var right = (dx + 1) * scaleX;
                var end = (int) Math.Ceiling(right);

                columnLeft[dx] = left;
                columnRight[dx] = right;
                columnStart[dx] = (int) Math.Floor(left);
                columnEnd[dx] = end > image.Width ? image.Width : end;
            }

            for (var dy = 0; dy < newHeight; dy++)
            {
                var srcTop = dy * scaleY;
                var srcBottom = (dy + 1) * scaleY;
                var y0 = (int) Math.Floor(srcTop);
                var y1 = (int) Math.Ceiling(srcBottom);
                if (y1 > image.Height) y1 = image.Height;

                var singleRow = y1 - y0 == 1;
                var rowBase = dy * newWidth * 4;
                var sourceRowBase = y0 * image.Width * 4;

                for (var dx = 0; dx < newWidth; dx++)
                {
                    var srcLeft = columnLeft[dx];
                    var srcRight = columnRight[dx];
                    var x0 = columnStart[dx];
                    var x1 = columnEnd[dx];

                    var di = rowBase + dx * 4;

                    if (singleRow && x1 - x0 == 1)
                    {
                        var single = sourceRowBase + x0 * 4;
                        if (data[single + 3] != 0)
                        {
                            dst[di] = data[single];
                            dst[di + 1] = data[single + 1];
                            dst[di + 2] = data[single + 2];
                            dst[di + 3] = data[single + 3];
                        }

                        continue;
                    }

                    double sumR = 0, sumG = 0, sumB = 0;
                    double sumAlphaWeighted = 0;
                    double sumColorWeight = 0;
                    double sumCoverage = 0;

                    for (var sy = y0; sy < y1; sy++)
                    {
                        var yOverlap = Math.Min(srcBottom, sy + 1) - Math.Max(srcTop, sy);
                        if (yOverlap <= 0) continue;

                        var rowOffset = sy * image.Width * 4;
                        for (var sx = x0; sx < x1; sx++)
                        {
                            var xOverlap = Math.Min(srcRight, sx + 1) - Math.Max(srcLeft, sx);
                            if (xOverlap <= 0) continue;

                            var coverage = xOverlap * yOverlap;
                            var i = rowOffset + sx * 4;
                            double a = data[i + 3];
                            var colorWeight = coverage * (a / 255.0);

                            sumR += data[i] * colorWeight;
                            sumG += data[i + 1] * colorWeight;
                            sumB += data[i + 2] * colorWeight;
                            sumAlphaWeighted += coverage * a;
                            sumColorWeight += colorWeight;
                            sumCoverage += coverage;
                        }
                    }

                    if (sumCoverage <= 0)
                        continue;

                    var outA = sumAlphaWeighted / sumCoverage;

                    if (sumColorWeight > 0)
                    {
                        dst[di] = ReferenceClamp(sumR / sumColorWeight);
                        dst[di + 1] = ReferenceClamp(sumG / sumColorWeight);
                        dst[di + 2] = ReferenceClamp(sumB / sumColorWeight);
                    }

                    dst[di + 3] = ReferenceClamp(outA);
                }
            }

            return dst;
        }

        private static byte ReferenceClamp(double value)
        {
            var rounded = (int) Math.Round(value, MidpointRounding.AwayFromZero);
            if (rounded < 0) return 0;
            if (rounded > 255) return 255;
            return (byte) rounded;
        }
    }
}
