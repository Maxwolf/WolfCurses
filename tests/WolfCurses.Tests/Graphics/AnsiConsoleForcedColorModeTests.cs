using WolfCurses.Graphics;
using Xunit;

namespace WolfCurses.Tests.Graphics
{
    /// <summary>
    ///     Covers <see cref="AnsiConsole.ForcedColorMode" />, the programmatic override that pins the color mode every
    ///     <see cref="AnsiColorModeEnum.Auto" /> consumer resolves to. In the non-parallel <c>ColorModeMutation</c>
    ///     collection because it mutates a process-global static that <see cref="AnsiConsole.DetectColorMode" /> reads;
    ///     every test restores the previous value in a <c>finally</c>.
    /// </summary>
    [Collection("ColorModeMutation")]
    public class AnsiConsoleForcedColorModeTests
    {
        [Fact]
        public void WhenSetToAConcreteMode_DetectColorModeReturnsIt()
        {
            var saved = AnsiConsole.ForcedColorMode;
            try
            {
                AnsiConsole.ForcedColorMode = AnsiColorModeEnum.Grayscale;
                Assert.Equal(AnsiColorModeEnum.Grayscale, AnsiConsole.DetectColorMode());

                AnsiConsole.ForcedColorMode = AnsiColorModeEnum.None;
                Assert.Equal(AnsiColorModeEnum.None, AnsiConsole.DetectColorMode());

                AnsiConsole.ForcedColorMode = AnsiColorModeEnum.Palette256;
                Assert.Equal(AnsiColorModeEnum.Palette256, AnsiConsole.DetectColorMode());
            }
            finally
            {
                AnsiConsole.ForcedColorMode = saved;
            }
        }

        [Fact]
        public void WhenNull_FallsBackToDetectionAndStaysConcrete()
        {
            var saved = AnsiConsole.ForcedColorMode;
            try
            {
                AnsiConsole.ForcedColorMode = null;

                var detected = AnsiConsole.DetectColorMode();
                Assert.NotEqual(AnsiColorModeEnum.Auto, detected);
                // Detection is remembered, so a second read agrees with the first.
                Assert.Equal(detected, AnsiConsole.DetectColorMode());
            }
            finally
            {
                AnsiConsole.ForcedColorMode = saved;
            }
        }

        [Fact]
        public void ForcingAuto_IsTreatedAsNoOverride()
        {
            // Auto is not a concrete mode; forcing it must not make DetectColorMode break its "never returns Auto"
            // contract, so it falls back to detection just like null.
            var saved = AnsiConsole.ForcedColorMode;
            try
            {
                AnsiConsole.ForcedColorMode = AnsiColorModeEnum.Auto;
                Assert.NotEqual(AnsiColorModeEnum.Auto, AnsiConsole.DetectColorMode());
            }
            finally
            {
                AnsiConsole.ForcedColorMode = saved;
            }
        }
    }
}
