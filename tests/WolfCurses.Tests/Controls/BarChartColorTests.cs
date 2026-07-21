using System;
using System.Text.RegularExpressions;
using WolfCurses.Graphics;
using WolfCurses.Tests.Support;
using WolfCurses.Window.Control;
using Xunit;

namespace WolfCurses.Tests.Controls
{
    /// <summary>
    ///     Covers <see cref="BarChart" />'s color support. Three things are being defended here: that an uncolored
    ///     chart is byte-for-byte the chart this class drew before color existed, that a resolved
    ///     <see cref="AnsiColorModeEnum.None" /> silences even a fully styled one, and that the pieces which turn a
    ///     chart into a flag — a settable <see cref="BarChart.Separator" /> plus a stepped <see cref="BarChart.Ramp" />
    ///     read across the rows — put the stops down one per row in order.
    ///     <para>
    ///         Every colored case pins an explicit <see cref="BarChart.ColorMode" /> instead of leaning on
    ///         <c>NO_COLOR</c> and <see cref="AnsiConsole.ResetColorModeCache" />, which is precisely what that
    ///         property is for: these tests run in the assembly's default parallel collection, so a test that mutated
    ///         the process-wide detection cache would be racing every other widget test in the suite.
    ///     </para>
    ///     <para>
    ///         The layout assertions are the ones worth reading twice. A bar chart draws zero-length runs routinely
    ///         (the bar of a negative value, the track of a full one) and its rows carry trailing spaces that the
    ///         column arithmetic depends on, so an open/close pair emitted around nothing would be invisible in a
    ///         screenshot and fatal to the alignment.
    ///     </para>
    /// </summary>
    public class BarChartColorTests
    {
        /// <summary>The sequence every styled run in this widget closes with.</summary>
        private const string RESET = "\x1b[0m";

        /// <summary>The separator this class has always drawn: space, U+2502 BOX DRAWINGS LIGHT VERTICAL, space.</summary>
        private const string SEPARATOR = " │ ";

        [Fact]
        public void Render_EveryStyleLeftAtItsDefault_EmitsNotOneEscape()
        {
            // Deliberately the awkward rows: a full bar, a negative (zero-length bar, two adjacent spaces) and a
            // zero. All three are places an eager open/close pair would show up.
            var chart = new BarChart {Width = 4};

            var expected =
                "A │ ████ 4" + Text.NL +
                "B │  -3" + Text.NL +
                "C │  0";

            var result = chart.Render(new[]
            {
                new BarChartValue("A", 4),
                new BarChartValue("B", -3),
                new BarChartValue("C", 0)
            });

            Assert.Equal(expected, result);
            Assert.DoesNotContain('\x1b', result);
        }

        [Fact]
        public void Separator_DefaultsToTheLightVerticalRuleItAlwaysDrew()
        {
            // The property exists so the gutter can be turned off, not to change what an existing chart looks like.
            Assert.Equal(SEPARATOR, new BarChart().Separator);
        }

        [Fact]
        public void Render_SeparatorEmptied_PutsTheBarStraightAgainstTheLabel()
        {
            var chart = new BarChart {Width = 3, Separator = string.Empty, ShowValues = false};

            var expected =
                "A███" + Text.NL +
                "B███";

            Assert.Equal(expected, chart.Render(new[]
            {
                new BarChartValue("A", 3),
                new BarChartValue("B", 3)
            }));
        }

        [Fact]
        public void Render_SeparatorNull_IsTreatedAsEmptyRatherThanThrowing()
        {
            var chart = new BarChart {Width = 3, Separator = null, ShowValues = false};

            Assert.Equal("A███", chart.Render(new[] {new BarChartValue("A", 3)}));
        }

        [Fact]
        public void Render_ColorModeNone_MatchesTheUncoloredChartExactlyDespiteEveryStyleBeingSet()
        {
            // The NO_COLOR stance: someone who asked for no escape sequences gets none of them, not a cheaper subset.
            var items = new[]
            {
                new BarChartValue("A", 3),
                new BarChartValue("B", 6)
            };

            var plain = new BarChart {Width = 6, ShowTrack = true};
            var styled = new BarChart
            {
                Width = 6,
                ShowTrack = true,
                ColorMode = AnsiColorModeEnum.None,
                BarStyle = ConsoleColor.Red,
                TrackStyle = ConsoleColor.Blue,
                LabelStyle = ConsoleColor.Green,
                ValueStyle = ConsoleColor.Yellow,
                SeparatorStyle = ConsoleColor.Magenta,
                Ramp = ColorRamp.PrideRainbow
            };

            var result = styled.Render(items);

            Assert.Equal(plain.Render(items), result);
            Assert.DoesNotContain('\x1b', result);
        }

        [Fact]
        public void Render_BarStyle_WrapsTheBarGlyphsAndNothingElse()
        {
            var chart = new BarChart
            {
                Width = 5,
                ColorMode = AnsiColorModeEnum.TrueColor,
                BarStyle = ConsoleColor.Green
            };

            // 92, not 32: ConsoleColor's numeric order is not ANSI's, and the bright half is the aixterm range.
            var expected = "A" + SEPARATOR + "\x1b[92m" + "█████" + RESET + " 5";

            Assert.Equal(expected, chart.Render(new[] {new BarChartValue("A", 5)}));
        }

        [Fact]
        public void Render_ZeroLengthBar_GetsNoOpenAndNoResetEvenWithABarStyleSet()
        {
            // "B │  -3" is separator, empty bar, then the value's own leading space — two adjacent spaces that a
            // wrapped empty run would push apart with four bytes of escape.
            var chart = new BarChart
            {
                Width = 10,
                ColorMode = AnsiColorModeEnum.TrueColor,
                BarStyle = ConsoleColor.Red
            };

            var expected =
                "A" + SEPARATOR + "\x1b[91m" + "██████████" + RESET + " 10" + Text.NL +
                "B" + SEPARATOR + " -3";

            Assert.Equal(expected, chart.Render(new[]
            {
                new BarChartValue("A", 10),
                new BarChartValue("B", -3)
            }));
        }

        [Fact]
        public void Render_ZeroLengthTrack_GetsNoOpenAndNoResetEither()
        {
            var chart = new BarChart
            {
                Width = 4,
                ShowTrack = true,
                ShowValues = false,
                ColorMode = AnsiColorModeEnum.TrueColor,
                TrackStyle = ConsoleColor.DarkGray
            };

            var expected =
                "A" + SEPARATOR + "██" + "\x1b[90m" + "░░" + RESET + Text.NL +
                "B" + SEPARATOR + "████";

            Assert.Equal(expected, chart.Render(new[]
            {
                new BarChartValue("A", 2),
                new BarChartValue("B", 4)
            }));
        }

        [Fact]
        public void Render_LabelStyle_CoversTheLabelButNotTheColumnPadding()
        {
            // The padding stays plain so a background does not bleed across the gutter, and — more importantly — the
            // width is still measured on the raw label, so no amount of styling can move a bar out of its column.
            var chart = new BarChart
            {
                Width = 2,
                ShowValues = false,
                ColorMode = AnsiColorModeEnum.TrueColor,
                LabelStyle = ConsoleColor.Cyan
            };

            var expected =
                "\x1b[96m" + "AB" + RESET + SEPARATOR + "██" + Text.NL +
                "\x1b[96m" + "C" + RESET + " " + SEPARATOR + "██";

            Assert.Equal(expected, chart.Render(new[]
            {
                new BarChartValue("AB", 2),
                new BarChartValue("C", 2)
            }));
        }

        [Fact]
        public void Render_ValueStyle_CoversTheDigitsButNotTheSpaceBeforeThem()
        {
            var chart = new BarChart
            {
                Width = 5,
                ColorMode = AnsiColorModeEnum.TrueColor,
                ValueStyle = ConsoleColor.Yellow
            };

            var expected = "A" + SEPARATOR + "█████ " + "\x1b[93m" + "5" + RESET;

            Assert.Equal(expected, chart.Render(new[] {new BarChartValue("A", 5)}));
        }

        [Fact]
        public void Render_SeparatorStyle_CoversTheWholeSeparatorIncludingItsSpaces()
        {
            // Unlike the label's padding and the value's leading space, the separator's spaces belong to the
            // separator rather than to the layout, so they are styled with it.
            var chart = new BarChart
            {
                Width = 2,
                ShowValues = false,
                ColorMode = AnsiColorModeEnum.TrueColor,
                SeparatorStyle = ConsoleColor.DarkGray
            };

            var expected = "A" + "\x1b[90m" + SEPARATOR + RESET + "██";

            Assert.Equal(expected, chart.Render(new[] {new BarChartValue("A", 2)}));
        }

        [Fact]
        public void Render_StyledLabels_LeaveEveryBarInTheSameColumn()
        {
            var items = new[]
            {
                new BarChartValue("Wood", 12),
                new BarChartValue("Iron", 6),
                new BarChartValue("Gold", 20)
            };

            var plain = new BarChart {Width = 6};
            var styled = new BarChart
            {
                Width = 6,
                ColorMode = AnsiColorModeEnum.TrueColor,
                LabelStyle = ConsoleColor.Cyan
            };

            var result = styled.Render(items);

            // Stripping the SGR runs has to give back the uncolored chart character for character; if the padding
            // were computed on styled text instead, every row would be pushed out by however many bytes its own
            // label's color cost and the columns would come apart.
            Assert.Equal(plain.Render(items), StripSgr(result));

            var lines = result.Split(Text.NL);
            var column = StripSgr(lines[0]).IndexOf(SEPARATOR, StringComparison.Ordinal);
            Assert.All(lines,
                line => Assert.Equal(column, StripSgr(line).IndexOf(SEPARATOR, StringComparison.Ordinal)));
        }

        [Fact]
        public void Render_SpreadRamp_GivesRowIOfNTheIthStopOfAnNStopSteppedRamp()
        {
            // This is the flag mechanism, stated as plainly as it can be stated: n stops over n rows come out one
            // per row, in order, with no stop skipped and none repeated.
            var flag = new BarChart
            {
                Width = 3,
                Separator = string.Empty,
                ShowValues = false,
                ColorMode = AnsiColorModeEnum.TrueColor,
                Ramp = ColorRamp.Stepped(
                    new Rgb24(10, 20, 30),
                    new Rgb24(40, 50, 60),
                    new Rgb24(70, 80, 90),
                    new Rgb24(100, 110, 120))
            };

            var expected =
                Fg(10, 20, 30) + "███" + RESET + Text.NL +
                Fg(40, 50, 60) + "███" + RESET + Text.NL +
                Fg(70, 80, 90) + "███" + RESET + Text.NL +
                Fg(100, 110, 120) + "███" + RESET;

            Assert.Equal(expected, flag.Render(new[]
            {
                new BarChartValue(string.Empty, 1),
                new BarChartValue(string.Empty, 1),
                new BarChartValue(string.Empty, 1),
                new BarChartValue(string.Empty, 1)
            }));
        }

        [Fact]
        public void Render_SpreadRamp_DrawsThePrideRainbowOneStripePerRow()
        {
            var flag = new BarChart
            {
                Width = 3,
                Separator = string.Empty,
                ShowValues = false,
                ColorMode = AnsiColorModeEnum.TrueColor,
                Ramp = ColorRamp.PrideRainbow
            };

            var expected =
                Fg(0xE4, 0x03, 0x03) + "███" + RESET + Text.NL + // red
                Fg(0xFF, 0x8C, 0x00) + "███" + RESET + Text.NL + // orange
                Fg(0xFF, 0xED, 0x00) + "███" + RESET + Text.NL + // yellow
                Fg(0x00, 0x80, 0x26) + "███" + RESET + Text.NL + // green
                Fg(0x00, 0x4D, 0xFF) + "███" + RESET + Text.NL + // blue (004DFF, not the Philadelphia navy 24408E)
                Fg(0x75, 0x07, 0x87) + "███" + RESET; // violet (750787, the partner of that blue)

            Assert.Equal(expected, flag.Render(new[]
            {
                new BarChartValue(string.Empty, 1),
                new BarChartValue(string.Empty, 1),
                new BarChartValue(string.Empty, 1),
                new BarChartValue(string.Empty, 1),
                new BarChartValue(string.Empty, 1),
                new BarChartValue(string.Empty, 1)
            }));
        }

        [Fact]
        public void Render_SpreadRampOverASingleRow_TakesTheRampsFirstStop()
        {
            var chart = new BarChart
            {
                Width = 2,
                Separator = string.Empty,
                ShowValues = false,
                ColorMode = AnsiColorModeEnum.TrueColor,
                Ramp = ColorRamp.Stepped(new Rgb24(1, 2, 3), new Rgb24(4, 5, 6))
            };

            // One row has nowhere to be spread across, so the question "where along the ramp" has no answer but zero.
            Assert.Equal(Fg(1, 2, 3) + "██" + RESET, chart.Render(new[] {new BarChartValue(string.Empty, 1)}));
        }

        [Fact]
        public void Render_LevelRamp_ReadsEachRowsOwnValueRatherThanItsRowNumber()
        {
            // The same data, the same ramp, the two modes: Spread walks the ramp down the rows, Level asks each row
            // how tall it is. Values chosen so the two answers are reversed and neither can be mistaken for the other.
            var items = new[]
            {
                new BarChartValue(string.Empty, 4),
                new BarChartValue(string.Empty, 2),
                new BarChartValue(string.Empty, 1)
            };

            var ramp = ColorRamp.Stepped(
                new Rgb24(255, 0, 0),
                new Rgb24(255, 255, 0),
                new Rgb24(0, 128, 0));

            var level = new BarChart
            {
                Width = 4,
                Separator = string.Empty,
                ShowValues = false,
                ColorMode = AnsiColorModeEnum.TrueColor,
                RampMode = ColorRampModeEnum.Level,
                Ramp = ramp
            };

            var expectedLevel =
                Fg(0, 128, 0) + "████" + RESET + Text.NL +
                Fg(255, 255, 0) + "██" + RESET + Text.NL +
                Fg(255, 0, 0) + "█" + RESET;

            Assert.Equal(expectedLevel, level.Render(items));

            var spread = new BarChart
            {
                Width = 4,
                Separator = string.Empty,
                ShowValues = false,
                ColorMode = AnsiColorModeEnum.TrueColor,
                RampMode = ColorRampModeEnum.Spread,
                Ramp = ramp
            };

            var expectedSpread =
                Fg(255, 0, 0) + "████" + RESET + Text.NL +
                Fg(255, 255, 0) + "██" + RESET + Text.NL +
                Fg(0, 128, 0) + "█" + RESET;

            Assert.Equal(expectedSpread, spread.Render(items));
        }

        [Fact]
        public void Render_LevelRampWithNothingToScaleAgainst_DrawsNoBarsAndSoNoEscapes()
        {
            // Every value zero means no scale, and no scale means no fraction — but it also means no bar, so the
            // question never reaches the terminal at all.
            var chart = new BarChart
            {
                Width = 4,
                ShowValues = false,
                ColorMode = AnsiColorModeEnum.TrueColor,
                RampMode = ColorRampModeEnum.Level,
                Ramp = ColorRamp.Traffic
            };

            var result = chart.Render(new[]
            {
                new BarChartValue("A", 0),
                new BarChartValue("B", 0)
            });

            Assert.Equal("A" + SEPARATOR + Text.NL + "B" + SEPARATOR, result);
            Assert.DoesNotContain('\x1b', result);
        }

        [Fact]
        public void Render_ItemStyle_BeatsTheRamp()
        {
            // The escape hatch a ramp cannot express: a row whose color means something categorical rather than
            // positional. The more specific instruction wins.
            var chart = new BarChart
            {
                Width = 2,
                Separator = string.Empty,
                ShowValues = false,
                ColorMode = AnsiColorModeEnum.TrueColor,
                Ramp = ColorRamp.Stepped(new Rgb24(1, 2, 3), new Rgb24(4, 5, 6))
            };

            var expected =
                Fg(1, 2, 3) + "██" + RESET + Text.NL +
                "\x1b[91m" + "██" + RESET;

            Assert.Equal(expected, chart.Render(new[]
            {
                new BarChartValue(string.Empty, 1),
                new BarChartValue(string.Empty, 1, ConsoleColor.Red)
            }));
        }

        [Fact]
        public void Render_ItemStyle_BeatsBarStyleToo()
        {
            var chart = new BarChart
            {
                Width = 2,
                Separator = string.Empty,
                ShowValues = false,
                ColorMode = AnsiColorModeEnum.TrueColor,
                BarStyle = ConsoleColor.Green
            };

            var expected =
                "\x1b[92m" + "██" + RESET + Text.NL +
                "\x1b[91m" + "██" + RESET;

            Assert.Equal(expected, chart.Render(new[]
            {
                new BarChartValue(string.Empty, 1),
                new BarChartValue(string.Empty, 1, ConsoleColor.Red)
            }));
        }

        [Fact]
        public void BarChartValue_TwoArgumentConstructor_LeavesTheStyleUnset()
        {
            // The whole of why every existing call site keeps drawing exactly as it did.
            Assert.False(new BarChartValue("A", 5).Style.HasValue);

            // And the three-argument form reads without ceremony, because TextStyle converts from ConsoleColor.
            var styled = new BarChartValue("A", 5, ConsoleColor.Red);

            Assert.True(styled.Style.HasValue);
            Assert.Equal(new TextStyle(new TextColor(ConsoleColor.Red)), styled.Style.Value);
        }

        [Fact]
        public void Render_Ramp_LaysItsColorOverBarStyleRatherThanReplacingIt()
        {
            // A ramp answers "what color", not "what look" — a chart that asked for bold bars on a dark background
            // still gets them. Parameter order inside the one escape is bold, foreground, background.
            var chart = new BarChart
            {
                Width = 2,
                Separator = string.Empty,
                ShowValues = false,
                ColorMode = AnsiColorModeEnum.TrueColor,
                BarStyle = new TextStyle(background: new TextColor(ConsoleColor.Black), bold: true),
                Ramp = ColorRamp.Stepped(new Rgb24(255, 0, 0))
            };

            var expected = "\x1b[1;38;2;255;0;0;40m" + "██" + RESET;

            Assert.Equal(expected, chart.Render(new[] {new BarChartValue(string.Empty, 1)}));
        }

        [Fact]
        public void Render_Ramp_LeavesTheTrackAlone()
        {
            // The track is the absence of the bar; coloring it from the same ramp would erase the distinction it
            // exists to draw.
            var chart = new BarChart
            {
                Width = 4,
                Separator = string.Empty,
                ShowTrack = true,
                ShowValues = false,
                ColorMode = AnsiColorModeEnum.TrueColor,
                Ramp = ColorRamp.Stepped(new Rgb24(255, 0, 0))
            };

            var expected =
                Fg(255, 0, 0) + "██" + RESET + "░░" + Text.NL +
                Fg(255, 0, 0) + "████" + RESET;

            Assert.Equal(expected, chart.Render(new[]
            {
                new BarChartValue(string.Empty, 2),
                new BarChartValue(string.Empty, 4)
            }));
        }

        [Fact]
        public void Render_NullOrEmptyItems_StillReturnAnEmptyStringWithARampSet()
        {
            // The guards have to stay ahead of the color work: a ramp sampled over zero rows has no row number to
            // ask about.
            var chart = new BarChart
            {
                ColorMode = AnsiColorModeEnum.TrueColor,
                Ramp = ColorRamp.PrideProgress,
                BarStyle = ConsoleColor.Red
            };

            Assert.Equal(string.Empty, chart.Render(null));
            Assert.Equal(string.Empty, chart.Render(new BarChartValue[0]));
        }

        [Fact]
        public void Render_WidthLessThanOne_StillThrowsBeforeAnyColorWork()
        {
            var chart = new BarChart
            {
                Width = 0,
                ColorMode = AnsiColorModeEnum.TrueColor,
                Ramp = ColorRamp.PrideTrans
            };

            Assert.Throws<ArgumentOutOfRangeException>(() => chart.Render(new[] {new BarChartValue("A", 1)}));
        }

        [Fact]
        public void Render_Palette256_IndexesRampColorsInsteadOfSpellingThemOut()
        {
            var chart = new BarChart
            {
                Width = 2,
                Separator = string.Empty,
                ShowValues = false,
                ColorMode = AnsiColorModeEnum.Palette256,
                Ramp = ColorRamp.Stepped(new Rgb24(255, 0, 0))
            };

            var result = chart.Render(new[] {new BarChartValue(string.Empty, 1)});

            // Ordinal throughout: the string overloads default to CurrentCulture, where ESC is a zero-weight
            // ignorable character, so these would really be searching for "[38;5;" and proving much less.
            Assert.Contains("\x1b[38;5;", result, StringComparison.Ordinal);
            Assert.DoesNotContain("38;2;", result, StringComparison.Ordinal);
            Assert.EndsWith(RESET, result, StringComparison.Ordinal);
        }

        [Fact]
        public void Render_Grayscale_DowngradesNamedColorsInsteadOfLettingThemSneakColorThrough()
        {
            // Grayscale means "the palette restricted to gray shades". A named color emitted as its own SGR code
            // would arrive in full color and quietly defeat the mode.
            var chart = new BarChart
            {
                Width = 2,
                Separator = string.Empty,
                ShowValues = false,
                ColorMode = AnsiColorModeEnum.Grayscale,
                BarStyle = ConsoleColor.Red
            };

            var result = chart.Render(new[] {new BarChartValue(string.Empty, 1)});

            Assert.DoesNotContain("\x1b[91m", result, StringComparison.Ordinal);
            Assert.Contains("\x1b[38;5;", result, StringComparison.Ordinal);
        }

        [Fact]
        public void Render_EveryRowClosesItsOwnStylesSoNothingCrossesTheLineBreak()
        {
            var chart = new BarChart
            {
                Width = 6,
                ShowTrack = true,
                ColorMode = AnsiColorModeEnum.TrueColor,
                LabelStyle = ConsoleColor.Cyan,
                SeparatorStyle = ConsoleColor.DarkGray,
                TrackStyle = ConsoleColor.DarkGray,
                ValueStyle = ConsoleColor.Yellow,
                Ramp = ColorRamp.PrideTrans
            };

            var lines = chart.Render(new[]
            {
                new BarChartValue("A", 3),
                new BarChartValue("B", 6),
                new BarChartValue("C", 1)
            }).Split(Text.NL);

            Assert.Equal(3, lines.Length);
            Assert.All(lines, line =>
            {
                var resets = Count(line, RESET);

                // Each styled run is exactly one open and one close, so every escape in the row is one half of a
                // matched pair — nothing is left open for the join to carry into the next row.
                Assert.True(resets > 0);
                Assert.Equal(resets * 2, Count(line, "\x1b["));
            });
        }

        /// <summary>The SGR sequence that sets an exact 24-bit foreground, the way a ramp color goes out in true color.</summary>
        private static string Fg(int r, int g, int b)
        {
            return "\x1b[38;2;" + r + ";" + g + ";" + b + "m";
        }

        /// <summary>Removes SGR runs, leaving what the terminal would actually show. Same shape ListNavigatorTests uses.</summary>
        private static string StripSgr(string text)
        {
            return Regex.Replace(text, @"\x1b\[[0-9;]*m", string.Empty);
        }

        /// <summary>Counts non-overlapping occurrences of a needle, which is all the escape-balance check needs.</summary>
        private static int Count(string haystack, string needle)
        {
            var total = 0;
            var at = haystack.IndexOf(needle, StringComparison.Ordinal);
            while (at >= 0)
            {
                total++;
                at = haystack.IndexOf(needle, at + needle.Length, StringComparison.Ordinal);
            }

            return total;
        }
    }
}
