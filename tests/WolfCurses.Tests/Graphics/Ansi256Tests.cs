using WolfCurses.Graphics;
using Xunit;

namespace WolfCurses.Tests.Graphics
{
    /// <summary>
    ///     Pins the mapping from 24-bit RGB onto the 256-color xterm palette, including the deliberate preference for
    ///     the grayscale ramp over the coarse color cube when a color is close to gray.
    /// </summary>
    public class Ansi256Tests
    {
        [Fact]
        public void FromRgb_PureBlack_MapsToCubeBlack()
        {
            Assert.Equal(16, Ansi256.FromRgb(0, 0, 0));
        }

        [Fact]
        public void FromRgb_PureWhite_MapsToCubeWhite()
        {
            Assert.Equal(231, Ansi256.FromRgb(255, 255, 255));
        }

        [Fact]
        public void FromRgb_PureRed_MapsToCubeRed()
        {
            // Cube index for (255,0,0): 16 + 36*5 + 6*0 + 0 = 196.
            Assert.Equal(196, Ansi256.FromRgb(255, 0, 0));
        }

        [Fact]
        public void FromRgb_NearGray_PrefersGrayscaleRamp()
        {
            // (130,130,130) is closer to gray ramp value 128 (index 244) than to the nearest cube step 135.
            Assert.Equal(244, Ansi256.FromRgb(130, 130, 130));
        }

        [Fact]
        public void GrayFromRgb_MidGray_MapsToRampStep()
        {
            Assert.Equal(244, Ansi256.GrayFromRgb(128, 128, 128));
        }

        [Fact]
        public void GrayFromRgb_Endpoints_UseCubeBlackAndWhite()
        {
            Assert.Equal(16, Ansi256.GrayFromRgb(0, 0, 0));
            Assert.Equal(231, Ansi256.GrayFromRgb(255, 255, 255));
        }
    }
}
