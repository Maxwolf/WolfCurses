using WolfCurses.Window.Control;
using Xunit;

namespace WolfCurses.Tests.Controls
{
    /// <summary>
    ///     SpinningPixel is internal sealed; reachable via InternalsVisibleTo. It drives SimulationApp.TickPhase.
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
