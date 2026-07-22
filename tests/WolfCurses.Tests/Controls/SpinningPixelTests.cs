using WolfCurses.Window.Control;
using Xunit;

namespace WolfCurses.Tests.Controls
{
    /// <summary>
    ///     SpinningPixel is a public sealed widget (it also drives SimulationApp.TickPhase internally). With its
    ///     GlyphStyle left at the default None it returns the bare cycling glyph, which is what these pin; the
    ///     coloring path is covered by <see cref="SpinningPixelColorTests" />.
    /// </summary>
    public class SpinningPixelTests
    {
        [Fact]
        public void Step_CyclesFourGlyphsThenWraps()
        {
            var pixel = new SpinningPixel();

            Assert.Equal("/", pixel.Step());
            Assert.Equal("-", pixel.Step());
            Assert.Equal(@"\", pixel.Step());
            Assert.Equal("|", pixel.Step());
            Assert.Equal("/", pixel.Step());
        }

        [Fact]
        public void IsPublic_AndStepReturnsOneBareGlyphWithNoTrailingNewline()
        {
            // The public surface a downstream string-producer consumer relies on: the type is reachable from outside
            // the library (the README bills it as a spinner), and each Step drops inline into a caller's text — one
            // glyph, no trailing newline to trim. InternalsVisibleTo would let this compile even if the type were
            // internal, so the publicness is asserted by reflection rather than merely by use.
            Assert.True(typeof(SpinningPixel).IsPublic);

            var step = new SpinningPixel().Step();
            Assert.Single(step);
            Assert.DoesNotContain('\n', step);
            Assert.DoesNotContain('\r', step);
        }
    }
}
