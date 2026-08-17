using WolfCurses.Graphics;
using Xunit;

namespace WolfCurses.Tests.Graphics
{
    /// <summary>
    ///     The safe console metrics are public because every application that sizes anything to the terminal needs
    ///     them and needs the two guards they carry. These pin the publicness and the fallbacks; they deliberately
    ///     read nothing process-global, so they belong in the default parallel collection rather than in
    ///     ColorModeMutation or RendererDefaultMutation.
    /// </summary>
    public class AnsiConsoleMetricsTests
    {
        [Fact]
        public void TheAccessorsArePublic_NotMerelyVisibleToTheTestAssembly()
        {
            // InternalsVisibleTo would let a plain call compile whether these were public or not, so the change is
            // asserted by reflection. This is the test that fails if someone reverts the keywords.
            var type = typeof(AnsiConsole);

            Assert.True(type.GetMethod(nameof(AnsiConsole.SafeIsOutputRedirected)).IsPublic);
            Assert.True(type.GetMethod(nameof(AnsiConsole.SafeWindowWidth), new System.Type[0]).IsPublic);
            Assert.True(type.GetMethod(nameof(AnsiConsole.SafeWindowHeight), new System.Type[0]).IsPublic);
            Assert.True(type.GetMethod(nameof(AnsiConsole.SafeWindowWidth), new[] {typeof(int)}).IsPublic);
            Assert.True(type.GetMethod(nameof(AnsiConsole.SafeWindowHeight), new[] {typeof(int)}).IsPublic);
        }

        [Fact]
        public void TheFallbackIsUsedWhenThereIsNoConsoleToAsk()
        {
            // Asserted through an explicit fallback rather than by comparing to Console.WindowWidth, which throws on
            // exactly the host this method exists for. On a test host with no console the answer is the fallback; on
            // one with a console it is the real size — either is correct, and neither is 0 or negative, which is the
            // property callers divide by.
            Assert.InRange(AnsiConsole.SafeWindowWidth(7), 1, int.MaxValue);
            Assert.InRange(AnsiConsole.SafeWindowHeight(3), 1, int.MaxValue);
        }

        [Fact]
        public void TheNoArgumentOverloadsReportEightyByTwentyFourWhenTheConsoleCannotSay()
        {
            // 80x24 is contract rather than an implementation detail: MenuLayout's single-column path, whose exact
            // byte-for-byte output MenuHighlightTests pins, is reached because a headless host reports 24 rows.
            var width = AnsiConsole.SafeWindowWidth();
            var height = AnsiConsole.SafeWindowHeight();

            Assert.True(width == 80 || width == AnsiConsole.SafeWindowWidth(-1));
            Assert.True(height == 24 || height == AnsiConsole.SafeWindowHeight(-1));
        }
    }
}
