using System;
using System.Collections.Generic;
using WolfCurses.Graphics;
using WolfCurses.Utility;
using Xunit;

namespace WolfCurses.Tests.Controls
{
    /// <summary>
    ///     Pins the composite the example app's demo screens rely on to keep their prose inside the console:
    ///     <b>word-wrap first, style second</b>, and the result never measures wider than the width asked for.
    ///     <para>
    ///         This exists because of a defect the widget tests structurally could not see. The colored demos grew
    ///         several fixed-length explanatory sentences — up to 96 characters — beside widgets that were all sized
    ///         from <c>Console.WindowWidth</c>. <see cref="ConsolePresenter" /> deliberately turns auto-wrap off for
    ///         the duration of a frame (DEC private mode 7, so a picture or a long row cannot shove the whole screen
    ///         down a line), which means an over-wide row is <em>truncated</em> rather than reflowed — the bisexual
    ///         flag's note lost the "2:1:2" it existed to explain. The fix routes that prose through
    ///         <see cref="StringExtensions.WordWrap" /> and applies the <see cref="TextStyle" /> per produced row.
    ///     </para>
    ///     <para>
    ///         The example app is a separate executable with no test project reference — <c>dotnet test</c> cannot
    ///         reach it — so what is pinned here is the guarantee the fix stands on rather than the demo screens
    ///         themselves: wrapping bounds every row, styling adds length but no <em>width</em>, and the two compose.
    ///         The strings below are the literal ones those screens carry, so shortening a fix by deleting the wrap
    ///         would leave this test still describing what the demos are supposed to do.
    ///     </para>
    /// </summary>
    public class StyledProseWidthTests
    {
        /// <summary>The narrowest console this library's demos claim to support, and the classic conhost default.</summary>
        private const int ClassicConsoleWidth = 80;

        /// <summary>
        ///     The exact prose the colored demo screens draw beside their widgets, longest first. Every one of these
        ///     overran an 80-column console as a flat literal.
        /// </summary>
        public static IEnumerable<object[]> DemoProse()
        {
            yield return new object[]
            {
                "This terminal reports no color support, so the widgets emit no escapes and the stripes are bare."
            };
            yield return new object[]
            {
                "Michael Page, 1998. Five stops, not three, so the narrow lavender keeps its 2:1:2 proportion."
            };
            yield return new object[]
            {
                "Daniel Quasar, 2018 (CC0). The chevron cannot be drawn in bands, so it is stacked on top."
            };
            yield return new object[]
            {
                "Colors come from TextStyle and ColorRamp; with NO_COLOR set not one escape is emitted."
            };
            yield return new object[]
            {
                "Monica Helms, 1999. Palindromic on purpose: correct whichever way up it is flown."
            };
            yield return new object[]
            {
                "Five-stripe form; seven-stripe original by Emily Gwen, 2018, reduced by taqwomen."
            };
        }

        [Theory]
        [MemberData(nameof(DemoProse))]
        public void WrappingBoundsEveryRow_AndStylingAddsNoVisibleWidth(string prose)
        {
            const int width = ClassicConsoleWidth - 2;
            var style = new TextStyle(new TextColor(ConsoleColor.DarkCyan));

            var wrapped = prose.WordWrap(width);

            foreach (var line in wrapped.Split(Environment.NewLine))
            {
                Assert.True(line.Length <= width, $"'{line}' is {line.Length} columns.");

                // Styling is applied per produced row, which is the half of the rule an implementation can get wrong
                // silently: the escapes cost bytes, the presenter measures cells, and the two must not be confused.
                var styled = style.Apply(line, AnsiColorModeEnum.TrueColor);
                Assert.Equal(line.Length, ConsolePresenter.VisibleLength(styled));
                Assert.True(ConsolePresenter.VisibleLength(styled) <= width);
            }
        }

        [Theory]
        [MemberData(nameof(DemoProse))]
        public void EveryDemoSentenceActuallyNeededTheWrap(string prose)
        {
            // Guards the guard. If these literals are ever shortened the test above would keep passing vacuously, so
            // say out loud that each of them is long enough to have been clipped — that is why the wrap is there.
            Assert.True(prose.Length > ClassicConsoleWidth - 2,
                $"'{prose}' is only {prose.Length} columns and no longer demonstrates the hazard.");
        }

        [Fact]
        public void StylingBeforeWrappingIsTheTrapAndIsMeasurablySo()
        {
            // The reason the demos wrap first: an escape sequence has Length but no width, so handing WordWrap
            // already-styled text makes it count the escape's bytes against the budget and break the line early —
            // and worse, the style opens on one row and is reset on another, bleeding across the newline.
            const int width = 40;
            var style = new TextStyle(new TextColor(ConsoleColor.Red), new TextColor(ConsoleColor.Black), true);
            var prose = "One short sentence that comfortably fits inside forty columns when nobody colors it first.";

            var wrapFirst = prose.WordWrap(width);
            var styleFirst = style.Apply(prose, AnsiColorModeEnum.TrueColor).WordWrap(width);

            var wrapFirstRows = Rows(wrapFirst);
            var styleFirstRows = Rows(styleFirst);

            // The escape is 21 bytes of zero width, and WordWrap counts bytes: the first row loses that much of its
            // budget, so it comes out visibly short of the column it should have filled.
            Assert.Equal(width, ConsolePresenter.VisibleLength(wrapFirstRows[0]));
            Assert.True(ConsolePresenter.VisibleLength(styleFirstRows[0]) < width - 10,
                $"styling first left row one only {ConsolePresenter.VisibleLength(styleFirstRows[0])} columns wide.");

            // And the style is opened on the first row while its reset lands on the last, so it hangs over every row
            // in between — the bleed across Environment.NewLine that the ordering rule prevents.
            Assert.Contains('\x1b', styleFirstRows[0]);
            Assert.DoesNotContain("\x1b[0m", styleFirstRows[0], StringComparison.Ordinal);
            Assert.Contains("\x1b[0m", styleFirstRows[styleFirstRows.Count - 1], StringComparison.Ordinal);
        }

        private static IReadOnlyList<string> Rows(string text)
        {
            var rows = new List<string>();
            foreach (var line in text.Split(Environment.NewLine))
            {
                if (line.Length > 0)
                    rows.Add(line);
            }

            return rows;
        }
    }
}
