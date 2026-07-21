using System;
using System.Linq;
using System.Text.RegularExpressions;
using WolfCurses.Graphics;
using WolfCurses.Tests.Support;
using WolfCurses.Window.Control;
using Xunit;

namespace WolfCurses.Tests.Controls
{
    /// <summary>
    ///     Covers coloring <see cref="Box" />, where the thing that can go wrong is not the color but the geometry.
    ///     <para>
    ///         A box's frame is sized from measurements — the widest content row, the visible width of the title, the
    ///         horizontal runs either side of it — and every one of those measurements is taken in characters. Style
    ///         the runs before measuring them, or let something downstream measure a styled run as its raw
    ///         <see cref="string.Length" />, and the frame skews by however many bytes the color cost. So almost every
    ///         test here strips the escapes and asserts the result is <em>exactly</em> the uncolored box, or asserts
    ///         every line of the frame is the same visible width. Color has to be pure decoration laid over an
    ///         unchanged layout.
    ///     </para>
    ///     <para>
    ///         The nested cases matter for the same reason from the other side: <see cref="Box" /> measures its
    ///         content escape-blind, so a colored box inside another one — and a caller's own pre-colored title or
    ///         content — all have to come out square too.
    ///     </para>
    /// </summary>
    public class BoxColorTests
    {
        private const string YELLOW = "\x1b[93m";
        private const string BLUE = "\x1b[94m";
        private const string RESET = "\x1b[0m";
        // A char, not a string, and deliberately so: the string overloads of Assert.Contains/DoesNotContain default to
        // CurrentCulture, where ESC is a zero-weight ignorable character - so DoesNotContain("\x1b", s) reports a hit
        // at position 0 of every string on earth. The char overloads compare ordinally.
        private const char ESCAPE = '\x1b';

        [Fact]
        public void BorderAndTitleAreStyledIndependentlyOfEachOther()
        {
            // The top edge goes out as three runs — left glyphs, title, right glyphs — which is what lets the two
            // styles differ at all. Note the title's style covers the spaces that frame it, so a background reads as
            // a chip sitting in the border rather than as two bare glyphs.
            var box = new Box
            {
                Title = "T",
                ColorMode = AnsiColorModeEnum.TrueColor,
                BorderStyle = ConsoleColor.Blue,
                TitleStyle = ConsoleColor.Yellow
            };

            var lines = box.Render("Hello").Split(Text.NL);

            Assert.Equal(BLUE + "┌─" + RESET + YELLOW + " T " + RESET + BLUE + "─┐" + RESET, lines[0]);
            Assert.Equal(BLUE + "│" + RESET + "Hello" + BLUE + "│" + RESET, lines[1]);
            Assert.Equal(BLUE + "└─────┘" + RESET, lines[2]);
        }

        [Fact]
        public void StrippingTheColorGivesBackTheUncoloredBoxExactly()
        {
            var content = "All systems nominal." + Text.NL + "Ready.";
            var plain = new Box {Title = "Status", Padding = 1, MinimumWidth = 24};
            var colored = new Box
            {
                Title = "Status",
                Padding = 1,
                MinimumWidth = 24,
                ColorMode = AnsiColorModeEnum.TrueColor,
                BorderStyle = ConsoleColor.Blue,
                TitleStyle = ConsoleColor.Yellow
            };

            var rendered = colored.Render(content);

            Assert.NotEqual(plain.Render(content), rendered);
            Assert.Equal(plain.Render(content), StripEscapes(rendered));
        }

        [Theory]
        [InlineData(BoxBorderEnum.Single)]
        [InlineData(BoxBorderEnum.Double)]
        [InlineData(BoxBorderEnum.Rounded)]
        [InlineData(BoxBorderEnum.Ascii)]
        public void EveryBorderStyleKeepsItsGlyphsWhenPainted(BoxBorderEnum border)
        {
            var plain = new Box {Border = border, Title = "T", Padding = 1};
            var colored = new Box
            {
                Border = border,
                Title = "T",
                Padding = 1,
                ColorMode = AnsiColorModeEnum.TrueColor,
                BorderStyle = ConsoleColor.Blue,
                TitleStyle = ConsoleColor.Yellow
            };

            Assert.Equal(plain.Render("Hello"), StripEscapes(colored.Render("Hello")));
        }

        [Theory]
        [InlineData(BoxAlignmentEnum.Left)]
        [InlineData(BoxAlignmentEnum.Center)]
        [InlineData(BoxAlignmentEnum.Right)]
        public void AStyledTitleLeavesTheFrameSquareAtEveryAlignment(BoxAlignmentEnum alignment)
        {
            // The title's own width drives how many horizontal glyphs sit either side of it. Measuring a styled
            // title would count its escapes as columns and eat the run on one side of it.
            var box = new Box
            {
                Title = "Status",
                TitleAlignment = alignment,
                MinimumWidth = 24,
                ColorMode = AnsiColorModeEnum.TrueColor,
                BorderStyle = ConsoleColor.Blue,
                TitleStyle = ConsoleColor.Yellow
            };

            AssertEveryLineIsTheSameVisibleWidth(box.Render("Hello"));
        }

        [Fact]
        public void AColoredFrameAroundAlreadyColoredContentStaysSquare()
        {
            var content = "\x1b[31mHi\x1b[0m" + Text.NL + "XYZ";
            var plain = new Box();
            var colored = new Box
            {
                ColorMode = AnsiColorModeEnum.TrueColor,
                BorderStyle = ConsoleColor.Blue
            };

            var rendered = colored.Render(content);

            Assert.Equal(StripEscapes(plain.Render(content)), StripEscapes(rendered));
            AssertEveryLineIsTheSameVisibleWidth(rendered);
            Assert.All(rendered.Split(Text.NL), line => Assert.Equal(5, StripEscapes(line).Length));
        }

        [Fact]
        public void ACallerColoredTitleIsEmbeddedVerbatimInsideAStyledFrame()
        {
            // The caller's escapes are passed through un-relocated and the frame's own style wraps around them. That
            // means the caller's reset closes this box's title style early — documented, and the only alternative
            // would be parsing whatever the caller handed over.
            var title = "\x1b[31mRED\x1b[0m";
            var box = new Box
            {
                Title = title,
                ColorMode = AnsiColorModeEnum.TrueColor,
                BorderStyle = ConsoleColor.Blue,
                TitleStyle = ConsoleColor.Yellow
            };

            var lines = box.Render("hello").Split(Text.NL);

            Assert.Equal(BLUE + "┌" + RESET + YELLOW + " " + title + " " + RESET + BLUE + "┐" + RESET, lines[0]);
            Assert.Contains(title, lines[0], StringComparison.Ordinal);
            AssertEveryLineIsTheSameVisibleWidth(box.Render("hello"));
        }

        [Fact]
        public void AColoredBoxNestedInsideAPlainOneIsMeasuredAsItsGlyphsAlone()
        {
            var inner = new Box
            {
                Title = "In",
                ColorMode = AnsiColorModeEnum.TrueColor,
                BorderStyle = ConsoleColor.Blue,
                TitleStyle = ConsoleColor.Yellow
            }.Render("Hi");

            var outer = new Box {Padding = 1}.Render(inner);

            // The outer frame sized itself off the inner box's visible glyphs, so stripping the whole thing gives
            // back precisely the frame it would have drawn around the uncolored inner box.
            Assert.Equal(new Box {Padding = 1}.Render(StripEscapes(inner)), StripEscapes(outer));
            AssertEveryLineIsTheSameVisibleWidth(outer);
        }

        [Fact]
        public void BorderStyleIsCompletelyInertWithoutABorder()
        {
            // BoxBorderEnum.None has no glyphs to paint, and the borderless path returns before a style is ever
            // asked for a sequence — so the padding-into-a-rectangle behavior is untouched, trailing space and all.
            var box = new Box
            {
                Border = BoxBorderEnum.None,
                Title = "T",
                ColorMode = AnsiColorModeEnum.TrueColor,
                BorderStyle = ConsoleColor.Blue,
                TitleStyle = ConsoleColor.Yellow
            };

            Assert.Equal("ab " + Text.NL + "cde", box.Render("ab" + Text.NL + "cde"));
        }

        [Fact]
        public void AZeroWidthColoredBoxNeverWrapsAnEmptyRun()
        {
            // Render(null) shortens every horizontal run to nothing. An open/reset pair around a zero-length run is
            // two escapes saying nothing, and in widgets where a run sits between two layout spaces it is worse
            // than useless — so the rule is kept here even though this box has no such spaces.
            var box = new Box
            {
                ColorMode = AnsiColorModeEnum.TrueColor,
                BorderStyle = ConsoleColor.Blue
            };

            var rendered = box.Render(null);

            var expected = BLUE + "┌┐" + RESET + Text.NL +
                           BLUE + "│" + RESET + BLUE + "│" + RESET + Text.NL +
                           BLUE + "└┘" + RESET;

            Assert.Equal(expected, rendered);
            Assert.DoesNotContain(BLUE + RESET, rendered, StringComparison.Ordinal);
        }

        [Fact]
        public void NoStyleCrossesTheNewlineBetweenRows()
        {
            var box = new Box
            {
                Title = "Status",
                Padding = 1,
                MinimumWidth = 20,
                ColorMode = AnsiColorModeEnum.TrueColor,
                BorderStyle = ConsoleColor.Blue,
                TitleStyle = ConsoleColor.Yellow
            };

            var lines = box.Render("All systems nominal." + Text.NL + "Ready.").Split(Text.NL);

            Assert.All(lines, line => Assert.True(EndsInThePlainState(line),
                "A row left a style open across the line break: " + line));
        }

        [Fact]
        public void NoneColorModeSuppressesEveryEscapeInTheFrame()
        {
            var content = "All systems nominal." + Text.NL + "Ready.";
            var plain = new Box {Title = "Status", Padding = 1, MinimumWidth = 24};
            var suppressed = new Box
            {
                Title = "Status",
                Padding = 1,
                MinimumWidth = 24,
                ColorMode = AnsiColorModeEnum.None,
                BorderStyle = new TextStyle(ConsoleColor.White, ConsoleColor.DarkBlue, true),
                TitleStyle = ConsoleColor.Yellow
            };

            var rendered = suppressed.Render(content);

            Assert.Equal(plain.Render(content), rendered);
            Assert.DoesNotContain(ESCAPE, rendered);
        }

        [Fact]
        public void AnUntouchedBoxEmitsNoEscapeWhateverTheEnvironmentSays()
        {
            // ColorMode is deliberately left at Auto: with both styles empty the box must not consult the
            // environment at all, so this assertion holds on a colored terminal and on a redirected pipe alike.
            var box = new Box {Title = "Status", Padding = 1, MinimumWidth = 20};

            Assert.DoesNotContain(ESCAPE, box.Render("All systems nominal."));
        }

        private static void AssertEveryLineIsTheSameVisibleWidth(string rendered)
        {
            var widths = rendered.Split(Text.NL).Select(line => StripEscapes(line).Length).Distinct().ToArray();

            Assert.Single(widths);
        }

        private static bool EndsInThePlainState(string line)
        {
            var open = false;
            foreach (Match match in Regex.Matches(line, @"\x1b\[[0-9;]*m"))
                open = match.Value != TextStyle.ResetSequence;

            return !open;
        }

        private static string StripEscapes(string text)
        {
            return Regex.Replace(text, @"\x1b\[[0-9;]*m", string.Empty);
        }
    }
}
