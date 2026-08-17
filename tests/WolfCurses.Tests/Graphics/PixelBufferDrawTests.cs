using System;
using WolfCurses.Graphics;
using Xunit;

namespace WolfCurses.Tests.Graphics
{
    /// <summary>
    ///     The two shape primitives, and the one property that is the whole reason they are in the library rather
    ///     than left to callers: <b>every pixel inside a shape is blended exactly once</b>.
    ///     <para>
    ///         Almost every assertion here is made with a <i>translucent</i> colour, and that is not decoration. Both
    ///         of the textbook constructions these methods reject — a midpoint circle mirrored into eight octants, a
    ///         thick line built by stamping a square at each step — revisit pixels, and revisiting a pixel with an
    ///         opaque colour writes the identical byte the second time. An opaque test therefore passes against both
    ///         the correct implementation and the broken one, which is exactly how this class of bug survives being
    ///         written, reviewed and eyeballed.
    ///     </para>
    /// </summary>
    public class PixelBufferDrawTests
    {
        private static readonly Rgba32 _background = new(20, 40, 60, 255);
        private static readonly Rgba32 _opaque = new(255, 128, 0, 255);
        private static readonly Rgba32 _translucent = new(255, 128, 0, 100);

        /// <summary>
        ///     What one source-over blend of <paramref name="color" /> over <paramref name="over" /> produces, asked
        ///     of <see cref="PixelBuffer.DrawImage" /> rather than recomputed — so these tests cannot drift from the
        ///     compositing the rest of the library does, and a rounding change in the blend fails here too.
        /// </summary>
        private static Rgba32 SingleBlend(Rgba32 over, Rgba32 color)
        {
            var target = new PixelBuffer(1, 1);
            target.Fill(over);

            var stamp = new PixelBuffer(1, 1);
            stamp.Fill(color);

            target.DrawImage(stamp, 0, 0);
            return target.GetPixel(0, 0);
        }

        /// <summary>Every pixel that is not still the background colour.</summary>
        private static int CountPainted(PixelBuffer buffer)
        {
            var painted = 0;
            for (var y = 0; y < buffer.Height; y++)
            for (var x = 0; x < buffer.Width; x++)
            {
                if (!buffer.GetPixel(x, y).Equals(_background))
                    painted++;
            }

            return painted;
        }

        /// <summary>Asserts that every painted pixel carries the value one blend would have produced.</summary>
        private static void AssertEveryPaintedPixelWasBlendedOnce(PixelBuffer buffer)
        {
            var expected = SingleBlend(_background, _translucent);

            for (var y = 0; y < buffer.Height; y++)
            for (var x = 0; x < buffer.Width; x++)
            {
                var pixel = buffer.GetPixel(x, y);
                if (pixel.Equals(_background))
                    continue;

                Assert.True(pixel.Equals(expected),
                    $"({x},{y}) is {pixel.R},{pixel.G},{pixel.B},{pixel.A} where one blend gives " +
                    $"{expected.R},{expected.G},{expected.B},{expected.A} - so that pixel was drawn twice");
            }
        }

        private static PixelBuffer Painted(int width, int height)
        {
            var buffer = new PixelBuffer(width, height);
            buffer.Fill(_background);
            return buffer;
        }

        // ---------------------------------------------------------------- lines

        [Fact]
        public void AnOpaqueLineMatchesTheEquivalentStackOfSinglePixelFills()
        {
            // The primitive is a short cut, not a different picture: it must land on exactly the pixels the obvious
            // per-step plot would have, so a caller can reason about it as one.
            var byPrimitive = Painted(40, 24);
            var byHand = Painted(40, 24);

            byPrimitive.DrawLine(3, 2, 37, 21, _opaque);

            const int steps = 34; // max(|dx|, |dy|) = max(34, 19)
            for (var step = 0; step <= steps; step++)
            {
                var x = 3 + step;
                var y = 2 + (int) Math.Round(19.0*step/steps, MidpointRounding.AwayFromZero);
                byHand.Fill(x, y, 1, 1, _opaque);
            }

            Assert.Equal(byHand.Data, byPrimitive.Data);
        }

        [Fact]
        public void ATranslucentLineTouchesEveryPixelExactlyOnce()
        {
            var buffer = Painted(40, 24);

            buffer.DrawLine(3, 2, 37, 21, _translucent);

            AssertEveryPaintedPixelWasBlendedOnce(buffer);
            Assert.Equal(35, CountPainted(buffer)); // one pixel per step, both endpoints included
        }

        [Fact]
        public void AZeroLengthLineDrawsExactlyOnePixel()
        {
            // A trail from a missile's origin to a missile that has not moved yet. Drawing nothing would make the
            // first frame of every projectile invisible.
            var buffer = Painted(10, 10);

            buffer.DrawLine(5, 5, 5, 5, _opaque);

            Assert.Equal(1, CountPainted(buffer));
            Assert.Equal(_opaque, buffer.GetPixel(5, 5));
        }

        [Fact]
        public void BothEndpointsAreDrawn()
        {
            var buffer = Painted(20, 20);

            buffer.DrawLine(2, 3, 15, 17, _opaque);

            Assert.Equal(_opaque, buffer.GetPixel(2, 3));
            Assert.Equal(_opaque, buffer.GetPixel(15, 17));
        }

        [Fact]
        public void ClippingDoesNotChangeWhichPixelsLandInside()
        {
            // The loop range is clipped rather than the pixels, which is only safe because the position at each step
            // is recomputed from the original endpoints. An incremental error accumulator would draw a different
            // line depending on where the loop was entered, and this is the test that says so.
            var small = Painted(20, 20);
            var large = Painted(200, 200);

            small.DrawLine(-60, -30, 80, 55, _opaque);
            large.DrawLine(-60, -30, 80, 55, _opaque);

            for (var y = 0; y < 20; y++)
            for (var x = 0; x < 20; x++)
                Assert.Equal(large.GetPixel(x, y), small.GetPixel(x, y));
        }

        [Theory]
        [InlineData(-40, -40, -20, -30)] // entirely above and left
        [InlineData(50, 50, 90, 70)] // entirely below and right
        [InlineData(-10, 5, -1, 9)] // just off the left edge
        public void ALineWithNothingInsideThePictureDrawsNothing(int x0, int y0, int x1, int y1)
        {
            var buffer = Painted(20, 20);

            buffer.DrawLine(x0, y0, x1, y1, _opaque);

            Assert.Equal(0, CountPainted(buffer));
        }

        [Fact]
        public void EndpointsMillionsOfPixelsApartStillTerminate()
        {
            // Two billion steps if the loop is not clipped, which is minutes rather than microseconds. There is no
            // wall-clock assertion here on purpose: an unclipped implementation does not run slowly, it hangs the
            // test run, which is a louder failure than any timing bound and never flakes on a loaded machine.
            var buffer = Painted(16, 16);

            buffer.DrawLine(-1_000_000_000, -1_000_000_000, 1_000_000_000, 1_000_000_000, _opaque);

            // It really does pass through the picture: the midpoint of that line is the origin.
            Assert.Equal(_opaque, buffer.GetPixel(0, 0));
            Assert.Equal(_opaque, buffer.GetPixel(15, 15));
            Assert.Equal(16, CountPainted(buffer));
        }

        [Fact]
        public void ThicknessWidensTheSpanWithoutBlendingAnythingTwice()
        {
            // The mutation this exists for: implementing thickness by stamping a t-by-t square at every step. That
            // overlaps its own previous stamp by all but one row, so most of the line blends twice - invisibly for
            // an opaque colour, and as a blotchy dark streak for this one.
            var buffer = Painted(40, 20);

            buffer.DrawLine(4, 6, 34, 12, _translucent, 3);

            AssertEveryPaintedPixelWasBlendedOnce(buffer);

            // x-major, so every column the line crosses carries exactly one span of three.
            for (var x = 4; x <= 34; x++)
            {
                var inColumn = 0;
                for (var y = 0; y < 20; y++)
                {
                    if (!buffer.GetPixel(x, y).Equals(_background))
                        inColumn++;
                }

                Assert.True(inColumn == 3, $"column {x} holds {inColumn} pixels rather than the 3 asked for");
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-4)]
        public void AThicknessOfZeroOrLessDrawsNothing(int thickness)
        {
            var buffer = Painted(20, 20);

            buffer.DrawLine(2, 2, 17, 17, _opaque, thickness);

            Assert.Equal(0, CountPainted(buffer));
        }

        [Fact]
        public void AHorizontalLineCoversExactlyTheRectangleItImplies()
        {
            // Horizontal lines take a separate row-wise path, because drawing them a column at a time walks a whole
            // stride per pixel through a row-major array. It must produce the identical rectangle.
            var byLine = Painted(30, 12);
            var byFill = Painted(30, 12);

            byLine.DrawLine(5, 6, 24, 6, _opaque, 3);
            byFill.Fill(5, 5, 20, 3, _opaque); // half = (3-1)/2 = 1, so rows 5, 6, 7

            Assert.Equal(byFill.Data, byLine.Data);
        }

        [Fact]
        public void AHorizontalTranslucentLineIsStillBlendedOnlyOnce()
        {
            var buffer = Painted(30, 12);

            buffer.DrawLine(5, 6, 24, 6, _translucent, 3);

            AssertEveryPaintedPixelWasBlendedOnce(buffer);
            Assert.Equal(60, CountPainted(buffer)); // 20 columns x 3 rows
        }

        [Fact]
        public void AVerticalLineIsDrawnTheOtherWayRoundAndStillCoversItsRectangle()
        {
            var byLine = Painted(12, 30);
            var byFill = Painted(12, 30);

            byLine.DrawLine(6, 5, 6, 24, _opaque, 3);
            byFill.Fill(5, 5, 3, 20, _opaque);

            Assert.Equal(byFill.Data, byLine.Data);
        }

        // ---------------------------------------------------------------- discs

        [Fact]
        public void AnOpaqueDiscMatchesTheEquivalentScanlineFills()
        {
            var byPrimitive = Painted(41, 41);
            var byHand = Painted(41, 41);

            byPrimitive.DrawDisc(20, 20, 15, _opaque);

            for (var y = 5; y <= 35; y++)
            {
                var half = (int) Math.Sqrt(15.0*15.0 - (y - 20.0)*(y - 20.0));
                byHand.Fill(20 - half, y, half*2 + 1, 1, _opaque);
            }

            Assert.Equal(byHand.Data, byPrimitive.Data);
        }

        [Fact]
        public void ATranslucentDiscIsUniformRightAcrossItself()
        {
            // THE test. Rewrite DrawDisc as a midpoint circle mirrored into eight octants and this is the only
            // assertion in the suite that fails: the octant seams and the four axis extremes get plotted twice, so
            // a dark X grows through the middle of anything with alpha. Every opaque assertion above passes happily
            // against that implementation, because writing the same byte twice is writing the same byte.
            var buffer = Painted(61, 61);

            buffer.DrawDisc(30, 30, 25, _translucent);

            AssertEveryPaintedPixelWasBlendedOnce(buffer);
        }

        [Fact]
        public void ADiscIsRoundRatherThanSquare()
        {
            var buffer = Painted(41, 41);

            buffer.DrawDisc(20, 20, 15, _opaque);

            Assert.Equal(_opaque, buffer.GetPixel(20, 20)); // centre
            Assert.Equal(_opaque, buffer.GetPixel(5, 20)); // left extreme
            Assert.Equal(_opaque, buffer.GetPixel(20, 5)); // top extreme
            Assert.Equal(_background, buffer.GetPixel(6, 6)); // a corner the circle does not reach
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-9)]
        public void ARadiusOfZeroOrLessDrawsNothing(int radius)
        {
            // Not one pixel either: a second rule about what "no size" means would be one rule too many, and Fill
            // already answers "zero or less paints nothing".
            var buffer = Painted(20, 20);

            buffer.DrawDisc(10, 10, radius, _opaque);

            Assert.Equal(0, CountPainted(buffer));
        }

        [Theory]
        [InlineData(0, 0)] // centred on the top-left corner
        [InlineData(19, 0)] // top-right
        [InlineData(0, 19)] // bottom-left
        [InlineData(19, 19)] // bottom-right
        [InlineData(-8, 10)] // centre wholly off the left
        [InlineData(10, 26)] // centre wholly off the bottom
        public void ADiscHangingOffTheEdgeIsClippedRatherThanRefused(int centerX, int centerY)
        {
            var buffer = Painted(20, 20);

            buffer.DrawDisc(centerX, centerY, 9, _opaque);

            // Something landed, nothing threw, and nothing was written outside - which GetPixel would have caught
            // by throwing on the way past.
            Assert.True(CountPainted(buffer) > 0);
        }

        [Theory]
        [InlineData(-40, 10)]
        [InlineData(10, -40)]
        [InlineData(70, 10)]
        [InlineData(10, 70)]
        public void ADiscWithNothingInsideThePictureDrawsNothing(int centerX, int centerY)
        {
            var buffer = Painted(20, 20);

            buffer.DrawDisc(centerX, centerY, 9, _opaque);

            Assert.Equal(0, CountPainted(buffer));
        }

        [Fact]
        public void ARadiusPastTheSquareRootOfIntMaxStillDrawsTheDisc()
        {
            // 50,000 squared is 2.5 billion, which does not fit in an int: computed there it wraps negative, the
            // radicand goes below zero, every half-width becomes the square root of a negative number cast to zero,
            // and the disc silently draws a one-pixel column. Promoting before the multiply is the fix and this is
            // the size that notices.
            var buffer = Painted(16, 16);

            buffer.DrawDisc(8, 8, 50_000, _opaque);

            Assert.Equal(16*16, CountPainted(buffer));
        }

        [Fact]
        public void ARadiusLargerThanThePictureCostsOnlyThePictureAndTerminates()
        {
            var buffer = Painted(16, 16);

            buffer.DrawDisc(8, 8, 1_000_000_000, _opaque);

            Assert.Equal(16*16, CountPainted(buffer));
        }

        // ---------------------------------------------------------------- the shared rule

        [Fact]
        public void ATranslucentDrawAgreesExactlyWithTheSameColourStampedByDrawImage()
        {
            // The one-blend pin, tying both primitives to the compositing the rest of the library does. If the shape
            // methods ever grow their own copy of the source-over arithmetic, it will differ here by a rounding step
            // and nothing else in the suite would see it.
            var byLine = Painted(9, 9);
            var byDisc = Painted(9, 9);
            var expected = SingleBlend(_background, _translucent);

            byLine.DrawLine(4, 4, 4, 4, _translucent);
            byDisc.DrawDisc(4, 4, 1, _translucent);

            Assert.Equal(expected, byLine.GetPixel(4, 4));
            Assert.Equal(expected, byDisc.GetPixel(4, 4));
        }

        [Theory]
        // destination                source                     expected after one source-over blend
        [InlineData(20, 40, 60, 255, 255, 128, 0, 100, 112, 75, 36, 255)]
        [InlineData(10, 20, 30, 200, 240, 200, 160, 90, 105, 94, 83, 219)]
        [InlineData(0, 0, 0, 0, 200, 100, 50, 128, 200, 100, 50, 128)]
        public void TheSharedBlendProducesTheseExactBytes(
            byte dr, byte dg, byte db, byte da,
            byte sr, byte sg, byte sb, byte sa,
            byte er, byte eg, byte eb, byte ea)
        {
            // AN ABSOLUTE ASSERTION, AND IT HAS TO BE. Every other test in this file that mentions blending compares
            // one drawing method against another - and since they all route through the same source-over arithmetic,
            // they all move together when that arithmetic changes. Dropping the "+ outA / 2" rounding term from the
            // blend passed all eighty-eight tests of this file plus AnsiImage's before this Theory existed: it is
            // exactly the "consistency where absolute was needed" trap that TextColumns' width test already records.
            //
            // The numbers are worked by hand, not copied from a run. The first row is the discriminating one - its
            // green channel comes to 19,127/255 with the rounding term and 19,000/255 without, which lands either
            // side of 75.
            var destination = new Rgba32(dr, dg, db, da);
            var source = new Rgba32(sr, sg, sb, sa);
            var expected = new Rgba32(er, eg, eb, ea);

            // Asserted through both entry points, because the whole point of extracting the blend was that DrawImage
            // and the shape primitives can never disagree about it.
            var byDisc = new PixelBuffer(3, 3);
            byDisc.Fill(destination);
            byDisc.DrawDisc(1, 1, 1, source);

            var byStamp = new PixelBuffer(3, 3);
            byStamp.Fill(destination);
            var stamp = new PixelBuffer(1, 1);
            stamp.Fill(source);
            byStamp.DrawImage(stamp, 1, 1);

            Assert.Equal(expected, byDisc.GetPixel(1, 1));
            Assert.Equal(expected, byStamp.GetPixel(1, 1));
        }

        [Fact]
        public void AFullyTransparentColourDrawsNothingAtAll()
        {
            var buffer = Painted(20, 20);
            var invisible = new Rgba32(255, 255, 255, 0);

            buffer.DrawLine(0, 0, 19, 19, invisible, 3);
            buffer.DrawDisc(10, 10, 8, invisible);

            Assert.Equal(0, CountPainted(buffer));
        }

        [Fact]
        public void AnOpaqueDrawReplacesRatherThanBlendsSoItCanStandInForAPaint()
        {
            // The half of "Fill paints, Draw composites" that keeps the split usable: a caller who wants replacement
            // does not need a different method, they need alpha 255.
            var buffer = Painted(20, 20);

            buffer.DrawDisc(10, 10, 6, _opaque);

            Assert.Equal(_opaque, buffer.GetPixel(10, 10));
        }
    }
}
