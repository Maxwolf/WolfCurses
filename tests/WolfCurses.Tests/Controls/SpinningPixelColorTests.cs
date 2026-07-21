using System;
using System.Text.RegularExpressions;
using WolfCurses.Graphics;
using WolfCurses.Window.Control;
using Xunit;

namespace WolfCurses.Tests.Controls
{
    /// <summary>
    ///     Covers coloring <see cref="SpinningPixel" />. It is the single-run cousin of <see cref="MarqueeBar" />:
    ///     one glyph per frame, no track and no newline, so the whole of its color story is "the returned glyph is
    ///     wrapped in <see cref="SpinningPixel.GlyphStyle" /> and nothing else changes."
    ///     <para>
    ///         Pinned here: the glyph cycle is untouched under the escapes, the default and an explicit
    ///         <see cref="AnsiColorModeEnum.None" /> emit not one escape byte, and the style closes with a reset so it
    ///         cannot bleed onto whatever the owner prints after the spinner.
    ///     </para>
    /// </summary>
    public class SpinningPixelColorTests
    {
        // ConsoleColor names go out as the terminal's own palette codes even in TrueColor mode (the theme keeps its
        // opinion about them), so these are the SGR codes for Red/Blue, the same constants MarqueeBarColorTests pins.
        private const string RED = "\x1b[91m";
        private const string BLUE = "\x1b[94m";
        private const string RESET = "\x1b[0m";

        // A char, not a string: the string overloads of Assert.Contains/DoesNotContain default to CurrentCulture,
        // where ESC is a zero-weight ignorable character, so DoesNotContain("\x1b", s) matches at position 0 of every
        // string. The char overloads compare ordinally.
        private const char ESCAPE = '\x1b';

        [Fact]
        public void GlyphStyleWrapsEachGlyphAndTheCycleIsUnchanged()
        {
            var spinner = new SpinningPixel
            {
                ColorMode = AnsiColorModeEnum.TrueColor,
                GlyphStyle = ConsoleColor.Red
            };

            Assert.Equal(RED + "/" + RESET, spinner.Step());
            Assert.Equal(RED + "-" + RESET, spinner.Step());
            Assert.Equal(RED + @"\" + RESET, spinner.Step());
            Assert.Equal(RED + "|" + RESET, spinner.Step());
            Assert.Equal(RED + "/" + RESET, spinner.Step());
        }

        [Fact]
        public void StyledFramesAreExactlyThePlainGlyphsWithEscapesWovenIn()
        {
            var plain = new SpinningPixel();
            var styled = new SpinningPixel
            {
                ColorMode = AnsiColorModeEnum.TrueColor,
                GlyphStyle = ConsoleColor.Blue
            };

            for (var frame = 0; frame < 100; frame++)
                Assert.Equal(plain.Step(), StripEscapes(styled.Step()));
        }

        [Fact]
        public void TheStyledGlyphClosesWithAResetSoItCannotBleed()
        {
            var spinner = new SpinningPixel
            {
                ColorMode = AnsiColorModeEnum.TrueColor,
                GlyphStyle = ConsoleColor.Blue
            };

            for (var frame = 0; frame < 20; frame++)
                Assert.EndsWith(RESET, spinner.Step(), StringComparison.Ordinal);
        }

        [Fact]
        public void NoneColorModeMakesAStyledSpinnerIdenticalToAnUnstyledOne()
        {
            // Invariant: a resolved mode of None emits nothing at all, however loudly the glyph style was set.
            var plain = new SpinningPixel();
            var suppressed = new SpinningPixel
            {
                ColorMode = AnsiColorModeEnum.None,
                GlyphStyle = new TextStyle(ConsoleColor.White, ConsoleColor.DarkBlue, true)
            };

            for (var frame = 0; frame < 20; frame++)
            {
                var rendered = suppressed.Step();

                Assert.Equal(plain.Step(), rendered);
                Assert.DoesNotContain(ESCAPE, rendered);
            }
        }

        [Fact]
        public void AnUntouchedSpinnerEmitsNoEscapeWhateverTheEnvironmentSays()
        {
            // GlyphStyle left empty and ColorMode left at Auto on purpose: with no style the widget must never even
            // ask the environment, so this stays deterministic on a colored terminal and on a CI pipe alike.
            var spinner = new SpinningPixel();

            for (var frame = 0; frame < 20; frame++)
                Assert.DoesNotContain(ESCAPE, spinner.Step());
        }

        [Fact]
        public void StyledSpinnersAreStillDeterministicAcrossInstances()
        {
            var first = new SpinningPixel
            {
                ColorMode = AnsiColorModeEnum.TrueColor,
                GlyphStyle = ConsoleColor.Red
            };
            var second = new SpinningPixel
            {
                ColorMode = AnsiColorModeEnum.TrueColor,
                GlyphStyle = ConsoleColor.Red
            };

            for (var frame = 0; frame < 20; frame++)
                Assert.Equal(first.Step(), second.Step());
        }

        private static string StripEscapes(string text)
        {
            return Regex.Replace(text, @"\x1b\[[0-9;]*m", string.Empty);
        }
    }
}
