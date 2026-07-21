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
    }
}
