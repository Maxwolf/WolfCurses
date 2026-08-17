using System;
using System.Reflection;
using WolfCurses.Example.Demos;
using WolfCurses.Graphics;
using Xunit;

namespace WolfCurses.Example.Tests
{
    /// <summary>
    ///     The start-up wordmark, and the claim its documentation makes: with colour resolved to
    ///     <see cref="AnsiColorModeEnum.None" /> the output is <b>byte-identical</b> to the plain art — not merely
    ///     "mostly plain", not "one reset at the end", identical.
    ///     <para>
    ///         Composed through the private method the running form calls, so what is tested is the shipped code
    ///         rather than a copy of it. The alternative — driving the app and reading frames — cannot pin this,
    ///         because the frame also carries a spinner that changes every tick.
    ///     </para>
    ///     <para>
    ///         In the <see cref="ExampleAppCollectionMembership">non-parallel collection</see> because it moves
    ///         <see cref="AnsiConsole.ForcedColorMode" />, which is process-wide and read by everything that draws.
    ///     </para>
    /// </summary>
    [Collection("ExampleApp")]
    public class LogoSplashTests
    {
        /// <summary>Calls the real <c>LogoSplashDialog.Compose</c>, which is private and static.</summary>
        private static string Compose(double seconds)
        {
            var method = typeof(LogoSplashDialog).GetMethod("Compose", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            return (string) method.Invoke(null, new object[] {seconds});
        }

        [Fact]
        public void WithNoColourTheOutputIsExactlyThePlainArt()
        {
            var previous = AnsiConsole.ForcedColorMode;
            try
            {
                AnsiConsole.ForcedColorMode = AnsiColorModeEnum.None;
                var plain = Compose(0.0);

                AnsiConsole.ForcedColorMode = AnsiColorModeEnum.TrueColor;
                var coloured = Compose(0.0);

                // Not "few escapes" - none at all. NO_COLOR asks for no escapes, not for a subset, and the same
                // rule governs every styled thing in this library.
                Assert.DoesNotContain('', plain);
                Assert.Equal(plain, AnsiText.StripEscapes(coloured));
            }
            finally
            {
                AnsiConsole.ForcedColorMode = previous;
            }
        }

        [Fact]
        public void TheGradientSlidesWithoutTheLettersMoving()
        {
            var previous = AnsiConsole.ForcedColorMode;
            try
            {
                AnsiConsole.ForcedColorMode = AnsiColorModeEnum.TrueColor;

                var first = Compose(0.0);
                var later = Compose(0.4);

                Assert.NotEqual(first, later);
                Assert.Equal(AnsiText.StripEscapes(first), AnsiText.StripEscapes(later));
            }
            finally
            {
                AnsiConsole.ForcedColorMode = previous;
            }
        }

        [Fact]
        public void TheSweepIsContinuousSoTheGradientHasNoSeam()
        {
            // The fold is the whole trick, and this tests the real function rather than a restatement of it.
            // ColorRamp.Rainbow runs red to violet and STOPS, so merely wrapping the position round would snap
            // violet back to red once per cycle and send a hard edge travelling across the wordmark. Folding the
            // wrapped position back on itself (0-1 then 1-0) makes the sequence red-violet-red, which is
            // continuous - so the function's value never jumps between neighbouring inputs, anywhere, including
            // across a cycle boundary where a bare wrap would jump from 1 to 0.
            var biggestJump = 0.0;
            var previous = Sweep(0.0);

            for (var step = 1; step <= 4000; step++)
            {
                // Fine steps, so a discontinuity cannot hide between two samples.
                var value = Sweep(step / 100.0);
                biggestJump = Math.Max(biggestJump, Math.Abs(value - previous));
                previous = value;
            }

            // Each 0.01-column step moves the position by 0.01/30 and the fold doubles it, so the honest bound is
            // a whisker over 0.0007. A bare wrap would show a jump of 1.0 at every cycle boundary.
            Assert.True(biggestJump < 0.01,
                $"the sweep jumps by {biggestJump:F4} between neighbouring columns, so the gradient has a seam");
        }

        /// <summary>Calls the real private sweep, so the fold under test is the one that ships.</summary>
        private static double Sweep(double column)
        {
            var method = typeof(LogoSplashDialog).GetMethod("Sweep", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            return (double) method.Invoke(null, new object[] {column, 0.0, 0.0});
        }

        [Fact]
        public void TheWordmarkIsFlushLeftAndSaysWhatTheLibraryIs()
        {
            var previous = AnsiConsole.ForcedColorMode;
            try
            {
                AnsiConsole.ForcedColorMode = AnsiColorModeEnum.None;
                var art = Compose(0.0);

                Assert.Contains("a text user interface library for .NET console applications", art,
                    StringComparison.Ordinal);

                // Flush left as a BLOCK, which is what lines it up with the module header above and the prompt
                // below. Individual rows are indented by the shape of the letters themselves - the bottom of the
                // W really does start three columns in - so the invariant is that at least one row begins at
                // column zero, not that none of them are indented. Centring it again would push every row right
                // and fail this.
                var anyRowAtColumnZero = false;
                foreach (var row in art.Split('\n'))
                {
                    var line = row.TrimEnd('\r');
                    if (line.Length > 0 && line[0] != ' ')
                        anyRowAtColumnZero = true;
                }

                Assert.True(anyRowAtColumnZero, "every row is indented, so the block is not flush left:\n" + art);
            }
            finally
            {
                AnsiConsole.ForcedColorMode = previous;
            }
        }

        /// <summary>Documentation anchor for the collection this class joins; see <see cref="Support.ExampleAppCollection" />.</summary>
        private sealed class ExampleAppCollectionMembership
        {
        }
    }
}
