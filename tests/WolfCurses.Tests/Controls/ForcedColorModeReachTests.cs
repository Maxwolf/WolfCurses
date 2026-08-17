using System;
using WolfCurses.Graphics;
using WolfCurses.Window.Control;
using Xunit;

namespace WolfCurses.Tests.Controls
{
    /// <summary>
    ///     The <c>Auto → DetectColorMode()</c> hop, which nothing else exercises.
    ///     <para>
    ///         Every test in <see cref="WidgetColorIntegrationTests" /> pins <c>ColorMode</c> on the instance,
    ///         because that is how a widget test avoids racing the process-wide cache — so the path a <i>running
    ///         application</i> actually takes, where every widget is left at <see cref="AnsiColorModeEnum.Auto" />
    ///         and the answer comes from <see cref="AnsiConsole.ForcedColorMode" />, is tested by none of them. That
    ///         hop is the entire mechanism by which the example's "Force render type" menu greys the graphs and the
    ///         flags, and a widget that stopped consulting it would look correct in the whole suite.
    ///     </para>
    ///     <para>
    ///         In the non-parallel collection and restoring in a <c>finally</c>, because the override is read by
    ///         everything that draws.
    ///     </para>
    /// </summary>
    [Collection("ColorModeMutation")]
    public class ForcedColorModeReachTests
    {
        /// <summary>Widgets carrying real styles and ramps, every one left at <see cref="AnsiColorModeEnum.Auto" />.</summary>
        private static string[] RenderEverything()
        {
            var bar = new ProgressBar {Width = 20, FillRamp = ColorRamp.Heat};
            var spark = new Sparkline {SparklineColorRamp = ColorRamp.Rainbow};
            var box = new Box
            {
                Title = "Status",
                BorderStyle = new TextStyle(ConsoleColor.Red),
                TitleStyle = new TextStyle(ConsoleColor.Cyan)
            };

            return new[]
            {
                bar.Render(0.5),
                spark.Render(new[] {1.0, 4.0, 2.0, 8.0, 3.0}),
                box.Render("all systems nominal")
            };
        }

        [Fact]
        public void ForcingNoColourEmptiesEveryWidgetLeftAtAuto()
        {
            var previous = AnsiConsole.ForcedColorMode;
            try
            {
                AnsiConsole.ForcedColorMode = AnsiColorModeEnum.None;

                foreach (var rendered in RenderEverything())
                {
                    // The char overload, not the string one: a string search for an escape is a search for a
                    // one-character string and reads as a typo.
                    Assert.DoesNotContain('', rendered);
                }
            }
            finally
            {
                AnsiConsole.ForcedColorMode = previous;
            }
        }

        [Fact]
        public void ForcingAPaletteColoursTheSameWidgetsThroughTheSameHop()
        {
            // The other half, and it is not redundant: without it, a widget that quietly stopped colouring at all
            // would pass the test above and look like a success.
            var previous = AnsiConsole.ForcedColorMode;
            try
            {
                AnsiConsole.ForcedColorMode = AnsiColorModeEnum.Palette256;

                var rendered = RenderEverything();

                // Every widget coloured something...
                foreach (var output in rendered)
                    Assert.Contains('', output);

                // ...and the two carrying an Rgb24 ramp quantized THROUGH the hop to a palette index. The box is
                // deliberately not held to that: its style is a named ConsoleColor, which emits ESC[31m in every
                // mode that has colour at all, so demanding "38;5;" of it would be testing the wrong thing.
                Assert.Contains("[38;5;", rendered[0], StringComparison.Ordinal);
                Assert.Contains("[38;5;", rendered[1], StringComparison.Ordinal);
            }
            finally
            {
                AnsiConsole.ForcedColorMode = previous;
            }
        }

        [Fact]
        public void TheMenuHighlightObeysAGlobalNoColourToo()
        {
            // ListNavigator has no per-widget ColorMode at all, so the global override is the ONLY thing that can
            // reach it - and its own tests strip escapes before asserting, which hides both branches.
            var previous = AnsiConsole.ForcedColorMode;
            try
            {
                AnsiConsole.ForcedColorMode = AnsiColorModeEnum.None;

                // The "> " marker is the always-present contract; the inverse video around it is not.
                Assert.Equal("> 1. First", ListNavigator.DecorateRow("1. First", true));

                AnsiConsole.ForcedColorMode = AnsiColorModeEnum.Palette256;
                var coloured = ListNavigator.DecorateRow("1. First", true);

                Assert.NotEqual("> 1. First", coloured);
                Assert.Equal("> 1. First", AnsiText.StripEscapes(coloured));
            }
            finally
            {
                AnsiConsole.ForcedColorMode = previous;
            }
        }
    }
}
