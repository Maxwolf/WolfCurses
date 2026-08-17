using WolfCurses.Graphics;
using Xunit;

namespace WolfCurses.Tests.Graphics
{
    /// <summary>
    ///     The one crossing between the colour vocabulary and the pixel one. <see cref="ColorRamp" /> speaks
    ///     <see cref="Rgb24" /> because an ANSI escape has no alpha to carry; <see cref="PixelBuffer" /> speaks
    ///     <see cref="Rgba32" /> because a picture does. Anything that wants to draw a ramp has to cross, so the
    ///     crossing may not lose or invent anything on the way.
    /// </summary>
    public class Rgb24Tests
    {
        [Theory]
        [InlineData(0, 0, 0, 0)]
        [InlineData(255, 255, 255, 255)]
        [InlineData(18, 200, 7, 128)]
        public void WithAlphaCarriesEveryChannelThrough(byte r, byte g, byte b, byte a)
        {
            var crossed = new Rgb24(r, g, b).WithAlpha(a);

            Assert.Equal(new Rgba32(r, g, b, a), crossed);
        }

        [Fact]
        public void WithAlphaZeroProducesATransparentColourRatherThanABlackOne()
        {
            // The distinction that matters for a fade: at the end of it the pixel must vanish, not turn black. A
            // conversion that zeroed the channels along with the alpha would look right over a black background and
            // wrong over every other one.
            var faded = new Rgb24(255, 128, 0).WithAlpha(0);

            Assert.Equal(0, faded.A);
            Assert.Equal(255, faded.R);
            Assert.Equal(128, faded.G);
        }

        [Fact]
        public void ARampSampleSurvivesTheCrossingUnchanged()
        {
            // The actual use: sample a ramp for the colour, decide the opacity separately, draw. If the crossing
            // altered anything the whole ramp would be off by a shade at every stop.
            foreach (var position in new[] {0.0, 0.25, 0.5, 0.75, 1.0})
            {
                var sampled = ColorRamp.Heat.Sample(position);

                var drawable = sampled.WithAlpha(200);

                Assert.Equal(sampled.R, drawable.R);
                Assert.Equal(sampled.G, drawable.G);
                Assert.Equal(sampled.B, drawable.B);
                Assert.Equal(200, drawable.A);
            }
        }

        [Fact]
        public void AColourCrossedAtFullAlphaDrawsAsItself()
        {
            // Ties the crossing to what actually happens downstream: an opaque draw replaces, so the pixel that lands
            // must be the exact colour the ramp gave, not a blend of it with whatever the canvas was.
            var color = ColorRamp.Rainbow.Sample(0.4);
            var buffer = new PixelBuffer(3, 3);
            buffer.Fill(new Rgba32(1, 2, 3, 255));

            buffer.DrawDisc(1, 1, 1, color.WithAlpha(255));

            var drawn = buffer.GetPixel(1, 1);
            Assert.Equal(color.R, drawn.R);
            Assert.Equal(color.G, drawn.G);
            Assert.Equal(color.B, drawn.B);
            Assert.Equal(255, drawn.A);
        }
    }
}
