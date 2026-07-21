using System;
using WolfCurses.Graphics;
using Xunit;

namespace WolfCurses.Tests.Graphics
{
    /// <summary>
    ///     Pins <see cref="TextColor" />'s two jobs: carrying the difference between a theme-respecting named
    ///     <see cref="ConsoleColor" /> and an exact <see cref="Rgb24" /> triple, and turning either into the correct
    ///     SGR parameter body for a given <see cref="AnsiColorModeEnum" />.
    ///     <para>
    ///         Most of this file is the sixteen-row named-color table, and it is here because that mapping is the
    ///         classic silent bug in this area. <see cref="ConsoleColor" /> numbers its members in DOS/CGA bit order
    ///         (blue-green-red) while ANSI numbers them red-green-blue, so blue and red are swapped and so are cyan
    ///         and yellow. Anything derived arithmetically from the enum's value paints a bar red while it claims to
    ///         be blue — and looks entirely plausible in a screenshot, which is why it needs a table rather than a
    ///         spot check.
    ///     </para>
    /// </summary>
    public class TextColorTests
    {
        [Theory]
        [InlineData(ConsoleColor.Black, "30")]
        [InlineData(ConsoleColor.DarkBlue, "34")]
        [InlineData(ConsoleColor.DarkGreen, "32")]
        [InlineData(ConsoleColor.DarkCyan, "36")]
        [InlineData(ConsoleColor.DarkRed, "31")]
        [InlineData(ConsoleColor.DarkMagenta, "35")]
        [InlineData(ConsoleColor.DarkYellow, "33")]
        [InlineData(ConsoleColor.Gray, "37")]
        [InlineData(ConsoleColor.DarkGray, "90")]
        [InlineData(ConsoleColor.Blue, "94")]
        [InlineData(ConsoleColor.Green, "92")]
        [InlineData(ConsoleColor.Cyan, "96")]
        [InlineData(ConsoleColor.Red, "91")]
        [InlineData(ConsoleColor.Magenta, "95")]
        [InlineData(ConsoleColor.Yellow, "93")]
        [InlineData(ConsoleColor.White, "97")]
        public void ForegroundSequence_EveryNamedColor_UsesTheAnsiCodeNotTheEnumOrder(ConsoleColor name, string expected)
        {
            var color = new TextColor(name);

            Assert.Equal(expected, color.ForegroundSequence(AnsiColorModeEnum.TrueColor));
        }

        [Theory]
        [InlineData(ConsoleColor.Black, "40")]
        [InlineData(ConsoleColor.DarkBlue, "44")]
        [InlineData(ConsoleColor.DarkGreen, "42")]
        [InlineData(ConsoleColor.DarkCyan, "46")]
        [InlineData(ConsoleColor.DarkRed, "41")]
        [InlineData(ConsoleColor.DarkMagenta, "45")]
        [InlineData(ConsoleColor.DarkYellow, "43")]
        [InlineData(ConsoleColor.Gray, "47")]
        [InlineData(ConsoleColor.DarkGray, "100")]
        [InlineData(ConsoleColor.Blue, "104")]
        [InlineData(ConsoleColor.Green, "102")]
        [InlineData(ConsoleColor.Cyan, "106")]
        [InlineData(ConsoleColor.Red, "101")]
        [InlineData(ConsoleColor.Magenta, "105")]
        [InlineData(ConsoleColor.Yellow, "103")]
        [InlineData(ConsoleColor.White, "107")]
        public void BackgroundSequence_EveryNamedColor_IsTheForegroundCodePlusTen(ConsoleColor name, string expected)
        {
            var color = new TextColor(name);

            Assert.Equal(expected, color.BackgroundSequence(AnsiColorModeEnum.TrueColor));
        }

        [Fact]
        public void NamedCodes_AreNotDerivableFromTheEnumsNumericOrder()
        {
            // The guard for the whole table above: DarkBlue is enum value 1 but ANSI 34, DarkRed is enum value 4 but
            // ANSI 31. Any implementation that computed "30 + (int) name" would produce 31 and 34 respectively — the
            // two swapped — and would still pass a test that only ever checked black and white.
            Assert.Equal(1, (int) ConsoleColor.DarkBlue);
            Assert.Equal(4, (int) ConsoleColor.DarkRed);

            Assert.Equal("34", new TextColor(ConsoleColor.DarkBlue).ForegroundSequence(AnsiColorModeEnum.TrueColor));
            Assert.Equal("31", new TextColor(ConsoleColor.DarkRed).ForegroundSequence(AnsiColorModeEnum.TrueColor));

            // The other half of the swap.
            Assert.Equal("36", new TextColor(ConsoleColor.DarkCyan).ForegroundSequence(AnsiColorModeEnum.TrueColor));
            Assert.Equal("33", new TextColor(ConsoleColor.DarkYellow).ForegroundSequence(AnsiColorModeEnum.TrueColor));
        }

        [Theory]
        [InlineData(ConsoleColor.Black, "30")]
        [InlineData(ConsoleColor.Blue, "94")]
        [InlineData(ConsoleColor.White, "97")]
        public void ForegroundSequence_NamedColorInPalette256_KeepsTheNamedCode(ConsoleColor name, string expected)
        {
            // A named color is theme-respecting in every mode that has color at all; downgrading it into an indexed
            // approximation would throw away the whole reason for keeping the two kinds apart.
            var color = new TextColor(name);

            Assert.Equal(expected, color.ForegroundSequence(AnsiColorModeEnum.Palette256));
        }

        [Fact]
        public void ForegroundSequence_ExactColorInTrueColor_IsATwentyFourBitTriple()
        {
            var color = new TextColor(new Rgb24(255, 0, 0));

            Assert.Equal("38;2;255;0;0", color.ForegroundSequence(AnsiColorModeEnum.TrueColor));
        }

        [Fact]
        public void BackgroundSequence_ExactColorInTrueColor_SwapsThirtyEightForFortyEight()
        {
            var color = new TextColor(new Rgb24(255, 255, 255));

            Assert.Equal("48;2;255;255;255", color.BackgroundSequence(AnsiColorModeEnum.TrueColor));
        }

        [Fact]
        public void ForegroundSequence_ExactColorInTrueColor_KeepsTheChannelsInRedGreenBlueOrder()
        {
            // The pride rainbow's blue, 004DFF: distinct in all three channels, so a transposed pair shows up.
            var color = new TextColor(new Rgb24(0x00, 0x4D, 0xFF));

            Assert.Equal("38;2;0;77;255", color.ForegroundSequence(AnsiColorModeEnum.TrueColor));
        }

        [Fact]
        public void ForegroundSequence_ExactColorInPalette256_GoesThroughTheCubeIndex()
        {
            var color = new TextColor(new Rgb24(255, 0, 0));

            Assert.Equal("38;5;" + Ansi256.FromRgb(255, 0, 0), color.ForegroundSequence(AnsiColorModeEnum.Palette256));
            Assert.Equal("38;5;196", color.ForegroundSequence(AnsiColorModeEnum.Palette256));
        }

        [Fact]
        public void BackgroundSequence_ExactColorInPalette256_GoesThroughTheCubeIndex()
        {
            var color = new TextColor(new Rgb24(0, 0, 0));

            Assert.Equal("48;5;16", color.BackgroundSequence(AnsiColorModeEnum.Palette256));
        }

        [Theory]
        [InlineData(0, 0, 0, "38;5;16")]
        [InlineData(255, 255, 255, "38;5;231")]
        [InlineData(255, 0, 0, "38;5;239")]
        [InlineData(192, 192, 192, "38;5;250")]
        public void ForegroundSequence_ExactColorInGrayscale_UsesTheGrayRamp(byte r, byte g, byte b, string expected)
        {
            var color = new TextColor(new Rgb24(r, g, b));

            Assert.Equal(expected, color.ForegroundSequence(AnsiColorModeEnum.Grayscale));
        }

        [Fact]
        public void ForegroundSequence_NamedColorInGrayscale_IsGreyedRatherThanPassedThrough()
        {
            // The one place a named color does NOT keep its code. Grayscale means "the palette restricted to gray
            // shades", so letting ConsoleColor.Red out as "91" would sneak real color past a mode that exists to
            // forbid it. It is downgraded through its canonical shade instead.
            var red = new TextColor(ConsoleColor.Red);

            Assert.Equal("38;5;239", red.ForegroundSequence(AnsiColorModeEnum.Grayscale));
            Assert.NotEqual("91", red.ForegroundSequence(AnsiColorModeEnum.Grayscale));
        }

        [Fact]
        public void BackgroundSequence_NamedColorInGrayscale_IsGreyedRatherThanPassedThrough()
        {
            var red = new TextColor(ConsoleColor.Red);

            Assert.Equal("48;5;239", red.BackgroundSequence(AnsiColorModeEnum.Grayscale));
        }

        [Fact]
        public void Grayscale_GreysAllSixteenNamedColorsThroughTheirCanonicalShade()
        {
            // Belt and braces on the rule above: not one of the sixteen may come out as a named SGR code, and each
            // must agree with what the gray ramp says about its canonical shade.
            foreach (ConsoleColor name in Enum.GetValues<ConsoleColor>())
            {
                var color = new TextColor(name);
                var expected = "38;5;" + Ansi256.GrayFromRgb(color.Rgb.R, color.Rgb.G, color.Rgb.B);

                Assert.Equal(expected, color.ForegroundSequence(AnsiColorModeEnum.Grayscale));
                Assert.StartsWith("38;5;", color.ForegroundSequence(AnsiColorModeEnum.Grayscale));
            }
        }

        [Fact]
        public void Sequences_ModeNone_AreEmptyForBothKinds()
        {
            const AnsiColorModeEnum mode = AnsiColorModeEnum.None;

            Assert.Equal(string.Empty, new TextColor(ConsoleColor.Red).ForegroundSequence(mode));
            Assert.Equal(string.Empty, new TextColor(new Rgb24(1, 2, 3)).ForegroundSequence(mode));
            Assert.Equal(string.Empty, new TextColor(ConsoleColor.Red).BackgroundSequence(mode));
            Assert.Equal(string.Empty, new TextColor(new Rgb24(1, 2, 3)).BackgroundSequence(mode));
        }

        [Fact]
        public void Sequences_NeverContainAnEscapeOrTheTrailingM()
        {
            // The contract that lets TextStyle join several bodies into ONE escape: these are parameter bodies, not
            // sequences. A body that carried its own ESC[ ... m would produce a run of adjacent escapes instead.
            var exact = new TextColor(new Rgb24(10, 20, 30));
            var named = new TextColor(ConsoleColor.Cyan);

            foreach (var body in new[]
                     {
                         exact.ForegroundSequence(AnsiColorModeEnum.TrueColor),
                         exact.BackgroundSequence(AnsiColorModeEnum.Palette256),
                         named.ForegroundSequence(AnsiColorModeEnum.Grayscale),
                         named.BackgroundSequence(AnsiColorModeEnum.TrueColor)
                     })
            {
                // The char overloads compare ordinally. The string overloads of Contains/DoesNotContain default to
                // CurrentCulture, under which ESC is a zero-weight ignorable character - so DoesNotContain("\x1b", s)
                // "finds" it at position 0 of every string on earth. Never search for an escape as a string.
                Assert.DoesNotContain('\x1b', body);
                Assert.DoesNotContain('m', body);
                Assert.DoesNotContain('[', body);
            }
        }

        [Fact]
        public void NamedConstructor_RecordsTheNameAndItsCanonicalShade()
        {
            var color = new TextColor(ConsoleColor.Red);

            Assert.True(color.IsNamed);
            Assert.Equal(ConsoleColor.Red, color.Name);
            Assert.Equal("#FF0000", Hex(color.Rgb));
        }

        [Fact]
        public void ExactConstructor_IsNotNamedAndKeepsTheTripleVerbatim()
        {
            var color = new TextColor(new Rgb24(0x12, 0x34, 0x56));

            Assert.False(color.IsNamed);
            Assert.Equal("#123456", Hex(color.Rgb));
        }

        [Theory]
        [InlineData(ConsoleColor.Black, "#000000")]
        [InlineData(ConsoleColor.DarkBlue, "#000080")]
        [InlineData(ConsoleColor.DarkGreen, "#008000")]
        [InlineData(ConsoleColor.DarkCyan, "#008080")]
        [InlineData(ConsoleColor.DarkRed, "#800000")]
        [InlineData(ConsoleColor.DarkMagenta, "#800080")]
        [InlineData(ConsoleColor.DarkYellow, "#808000")]
        [InlineData(ConsoleColor.Gray, "#C0C0C0")]
        [InlineData(ConsoleColor.DarkGray, "#808080")]
        [InlineData(ConsoleColor.Blue, "#0000FF")]
        [InlineData(ConsoleColor.Green, "#00FF00")]
        [InlineData(ConsoleColor.Cyan, "#00FFFF")]
        [InlineData(ConsoleColor.Red, "#FF0000")]
        [InlineData(ConsoleColor.Magenta, "#FF00FF")]
        [InlineData(ConsoleColor.Yellow, "#FFFF00")]
        [InlineData(ConsoleColor.White, "#FFFFFF")]
        public void CanonicalRgb_IsTheLegacyConsolePaletteForEveryNamedColor(ConsoleColor name, string expected)
        {
            // Same set as xterm-256 indices 0-15. Only a stand-in for the user's real theme, but it is what the
            // grayscale downgrade and any ramp arithmetic starting from a name have to work with.
            Assert.Equal(expected, Hex(new TextColor(name).Rgb));
        }

        [Fact]
        public void ImplicitConversion_FromRgb24_ProducesAnExactColor()
        {
            TextColor color = new Rgb24(1, 2, 3);

            Assert.False(color.IsNamed);
            Assert.Equal("#010203", Hex(color.Rgb));
        }

        [Fact]
        public void ImplicitConversion_FromConsoleColor_ProducesANamedColor()
        {
            TextColor color = ConsoleColor.Magenta;

            Assert.True(color.IsNamed);
            Assert.Equal(ConsoleColor.Magenta, color.Name);
        }

        [Fact]
        public void Equality_NamedAndExactAreNeverEqualEvenWhenTheShadesCoincide()
        {
            // They do not render the same way — the named one follows the user's theme — so treating them as equal
            // would let a style-change check miss a real change.
            var named = new TextColor(ConsoleColor.Red);
            var exact = new TextColor(new Rgb24(255, 0, 0));

            Assert.NotEqual(named, exact);
            Assert.False(named == exact);
            Assert.True(named != exact);
        }

        [Fact]
        public void Equality_SameNameOrSameTripleCompareEqualAndHashAlike()
        {
            var a = new TextColor(ConsoleColor.Green);
            var b = new TextColor(ConsoleColor.Green);
            var c = new TextColor(new Rgb24(9, 8, 7));
            var d = new TextColor(new Rgb24(9, 8, 7));

            Assert.True(a == b);
            Assert.False(a != b);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());

            Assert.True(c == d);
            Assert.Equal(c.GetHashCode(), d.GetHashCode());
        }

        [Fact]
        public void Equality_AgainstAnUnrelatedObject_IsFalse()
        {
            var color = new TextColor(ConsoleColor.Green);

            Assert.False(color.Equals("green"));
            Assert.False(color.Equals(null));
        }

        [Fact]
        public void DefaultValue_IsAnExactBlackRatherThanANamedOne()
        {
            var color = default(TextColor);

            Assert.False(color.IsNamed);
            Assert.Equal("#000000", Hex(color.Rgb));
            Assert.NotEqual(new TextColor(ConsoleColor.Black), color);
        }

        [Fact]
        public void OutOfRangeConsoleColor_FallsBackToTheTerminalDefaultRatherThanAnUnreadableCell()
        {
            // Nothing stops a caller casting an int into ConsoleColor. 39/49 are "default foreground/background",
            // which is the honest answer to a color nobody named.
            var color = new TextColor((ConsoleColor) 99);

            Assert.Equal("39", color.ForegroundSequence(AnsiColorModeEnum.TrueColor));
            Assert.Equal("49", color.BackgroundSequence(AnsiColorModeEnum.TrueColor));
            Assert.Equal("#808080", Hex(color.Rgb));
        }

        [Fact]
        public void ToString_NamesTheColorOrSpellsItsHex()
        {
            Assert.Equal("Red", new TextColor(ConsoleColor.Red).ToString());
            Assert.Equal("#E40303", new TextColor(new Rgb24(0xE4, 0x03, 0x03)).ToString());
        }

        /// <summary>Renders a triple as an uppercase hex string, which gives readable assertion failures.</summary>
        private static string Hex(Rgb24 color)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }
    }
}
