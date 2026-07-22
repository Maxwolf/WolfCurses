using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using WolfCurses.Graphics;
using WolfCurses.Tests.Support;
using WolfCurses.Window.Control;
using Xunit;

namespace WolfCurses.Tests.Controls
{
    /// <summary>
    ///     Covers coloring <see cref="MarqueeBar" />, whose animation makes it the most fragile widget to color.
    ///     <para>
    ///         The class animates by mutating one plain string in place — it blanks the old pointer with
    ///         <c>Replace("***", "   ")</c> and stamps the new one with <c>Remove</c>/<c>Insert</c> at absolute
    ///         indices. A single escape sequence stored inside that string would shift every index past it and
    ///         <c>Replace</c> would start matching payload bytes, so the pointer would smear or stop. Nothing here
    ///         can inspect the private field, so the tests prove it the only way available from outside: they step a
    ///         styled bar and a plain bar in lockstep for a hundred frames and assert the styled frames are the plain
    ///         frames with escapes woven in and nothing else. If color ever reaches the stored bar, the two walks
    ///         diverge within a few frames.
    ///     </para>
    ///     <para>
    ///         Also pinned: the trailing <see cref="Environment.NewLine" /> survives, the style closes <em>before</em>
    ///         it so nothing bleeds onto the next line, and both the untouched default and an explicit
    ///         <see cref="AnsiColorModeEnum.None" /> emit not one escape byte.
    ///     </para>
    /// </summary>
    public class MarqueeBarColorTests
    {
        /// <summary>The bar is a pipe, twenty-five columns of track, and a pipe.</summary>
        private const int BAR_LENGTH = 27;

        /// <summary>How many track columns sit to the right of a pointer parked at the left edge.</summary>
        private const int TRAILING_TRACK = 22;

        private const string RED = "\x1b[91m";
        private const string BLUE = "\x1b[94m";
        private const string RESET = "\x1b[0m";
        // A char, not a string, and deliberately so: the string overloads of Assert.Contains/DoesNotContain default to
        // CurrentCulture, where ESC is a zero-weight ignorable character - so DoesNotContain("\x1b", s) reports a hit
        // at position 0 of every string on earth. The char overloads compare ordinally.
        private const char ESCAPE = '\x1b';

        [Fact]
        public void PointerStyleWrapsThePointerAndLeavesTheTrackAlone()
        {
            var bar = new MarqueeBar
            {
                ColorMode = AnsiColorModeEnum.TrueColor,
                PointerStyle = ConsoleColor.Red
            };

            var expected = "|" + RED + "***" + RESET + new string(' ', TRAILING_TRACK) + "|" + Text.NL;

            Assert.Equal(expected, bar.Step());
        }

        [Fact]
        public void TrackStyleWrapsBothSidesOfThePointerAndLeavesItPlain()
        {
            // Two runs, not one background wash: the pointer sits between them, so the track cannot be a single
            // sequence without the pointer having to re-open it afterwards.
            var bar = new MarqueeBar
            {
                ColorMode = AnsiColorModeEnum.TrueColor,
                TrackStyle = ConsoleColor.Blue
            };

            var expected = BLUE + "|" + RESET +
                           "***" +
                           BLUE + new string(' ', TRAILING_TRACK) + "|" + RESET +
                           Text.NL;

            Assert.Equal(expected, bar.Step());
        }

        [Fact]
        public void PointerAndTrackAreStyledIndependentlyOfEachOther()
        {
            var bar = new MarqueeBar
            {
                ColorMode = AnsiColorModeEnum.TrueColor,
                PointerStyle = ConsoleColor.Red,
                TrackStyle = ConsoleColor.Blue
            };

            var expected = BLUE + "|" + RESET +
                           RED + "***" + RESET +
                           BLUE + new string(' ', TRAILING_TRACK) + "|" + RESET +
                           Text.NL;

            Assert.Equal(expected, bar.Step());
        }

        [Fact]
        public void StyledFramesAreExactlyThePlainFramesWithEscapesWovenIn()
        {
            // The load-bearing test of the whole file. An escape that leaked into the stored bar would move the
            // absolute indices the animation stamps at, so the two walks would stop agreeing — not on frame one,
            // but within a handful of frames, and permanently.
            var plain = new MarqueeBar();
            var styled = new MarqueeBar
            {
                ColorMode = AnsiColorModeEnum.TrueColor,
                PointerStyle = ConsoleColor.Red,
                TrackStyle = ConsoleColor.Blue
            };

            for (var frame = 0; frame < 100; frame++)
                Assert.Equal(plain.Step(), StripEscapes(styled.Step()));
        }

        [Fact]
        public void TheStyledPointerStillPingPongsToTheRightEdgeAndBackToTheFirstFrame()
        {
            var bar = new MarqueeBar
            {
                ColorMode = AnsiColorModeEnum.TrueColor,
                PointerStyle = ConsoleColor.Red,
                TrackStyle = ConsoleColor.Blue
            };

            var frames = new List<string>();
            for (var frame = 0; frame < 100; frame++)
                frames.Add(StripEscapes(bar.Step()));

            var atTheRightEdge = "|" + new string(' ', TRAILING_TRACK) + "***|";
            Assert.Contains(frames, frame => frame.StartsWith(atTheRightEdge, StringComparison.Ordinal));
            Assert.Contains(frames.Skip(1), frame => frame == frames[0]);
        }

        [Fact]
        public void EveryStyledFrameKeepsItsTwentySevenColumnsAndItsTrailingNewline()
        {
            var bar = new MarqueeBar
            {
                ColorMode = AnsiColorModeEnum.TrueColor,
                PointerStyle = ConsoleColor.Red,
                TrackStyle = ConsoleColor.Blue
            };

            for (var frame = 0; frame < 100; frame++)
            {
                var rendered = bar.Step();

                Assert.EndsWith(Text.NL, rendered, StringComparison.Ordinal);
                Assert.Equal(BAR_LENGTH + Text.NL.Length, StripEscapes(rendered).Length);
                Assert.Contains("***", StripEscapes(rendered), StringComparison.Ordinal);
            }
        }

        [Fact]
        public void TheStyleClosesBeforeTheNewlineSoItCannotBleedOntoTheNextLine()
        {
            // The newline is appended after decoration, so the last thing on the line has to be the reset. A frame
            // ending "reset, newline" is fine; one ending "newline, reset" would color whatever the owner draws next.
            var bar = new MarqueeBar
            {
                ColorMode = AnsiColorModeEnum.TrueColor,
                TrackStyle = ConsoleColor.Blue
            };

            for (var frame = 0; frame < 60; frame++)
                Assert.EndsWith(RESET + Text.NL, bar.Step(), StringComparison.Ordinal);
        }

        [Fact]
        public void TurningColorOnPartWayThroughTheAnimationDoesNotDisturbTheBounce()
        {
            // Proof from the other direction: twenty plain frames, then a style, then forty more. If the earlier
            // frames had stored anything, the styled continuation would not line up with the plain one.
            var plain = new MarqueeBar();
            var styled = new MarqueeBar {ColorMode = AnsiColorModeEnum.TrueColor};

            for (var frame = 0; frame < 20; frame++)
            {
                plain.Step();
                styled.Step();
            }

            styled.PointerStyle = ConsoleColor.Red;
            styled.TrackStyle = ConsoleColor.Blue;

            for (var frame = 0; frame < 40; frame++)
                Assert.Equal(plain.Step(), StripEscapes(styled.Step()));
        }

        [Fact]
        public void StyledMarqueesAreStillDeterministicAcrossInstances()
        {
            var first = new MarqueeBar
            {
                ColorMode = AnsiColorModeEnum.TrueColor,
                PointerStyle = ConsoleColor.Red,
                TrackStyle = ConsoleColor.Blue
            };
            var second = new MarqueeBar
            {
                ColorMode = AnsiColorModeEnum.TrueColor,
                PointerStyle = ConsoleColor.Red,
                TrackStyle = ConsoleColor.Blue
            };

            for (var frame = 0; frame < 60; frame++)
                Assert.Equal(first.Step(), second.Step());
        }

        [Fact]
        public void NoneColorModeMakesAFullyStyledMarqueeIdenticalToAnUnstyledOne()
        {
            // Invariant: a resolved mode of None emits nothing at all, however loudly the styles were set.
            var plain = new MarqueeBar();
            var suppressed = new MarqueeBar
            {
                ColorMode = AnsiColorModeEnum.None,
                PointerStyle = ConsoleColor.Red,
                TrackStyle = new TextStyle(ConsoleColor.White, ConsoleColor.DarkBlue, true)
            };

            for (var frame = 0; frame < 60; frame++)
            {
                var rendered = suppressed.Step();

                Assert.Equal(plain.Step(), rendered);
                Assert.DoesNotContain(ESCAPE, rendered);
            }
        }

        [Fact]
        public void AnUntouchedMarqueeEmitsNoEscapeWhateverTheEnvironmentSays()
        {
            // ColorMode is left at Auto on purpose: with both styles empty the widget must never even ask the
            // environment, so this stays deterministic on a developer's colored terminal and on a CI pipe alike.
            var bar = new MarqueeBar();

            for (var frame = 0; frame < 60; frame++)
                Assert.DoesNotContain(ESCAPE, bar.Step());
        }

        [Fact]
        public void StyledRenderIsAStyledStepWithoutTheTrailingNewline()
        {
            // The newline-free Render() must carry exactly the same woven-in escapes as Step(), just without the
            // trailing newline — proved by stepping a Render() bar and a Step() bar in lockstep.
            var rendered = new MarqueeBar
            {
                ColorMode = AnsiColorModeEnum.TrueColor,
                PointerStyle = ConsoleColor.Red,
                TrackStyle = ConsoleColor.Blue
            };
            var stepped = new MarqueeBar
            {
                ColorMode = AnsiColorModeEnum.TrueColor,
                PointerStyle = ConsoleColor.Red,
                TrackStyle = ConsoleColor.Blue
            };

            for (var frame = 0; frame < 60; frame++)
                Assert.Equal(rendered.Render() + Text.NL, stepped.Step());
        }

        [Fact]
        public void NoneColorModeMakesAStyledRenderEmitNoEscapes()
        {
            // The None "emit no escapes" guarantee holds for the newline-free producer too, not just Step().
            var plain = new MarqueeBar();
            var suppressed = new MarqueeBar
            {
                ColorMode = AnsiColorModeEnum.None,
                PointerStyle = ConsoleColor.Red,
                TrackStyle = new TextStyle(ConsoleColor.White, ConsoleColor.DarkBlue, true)
            };

            for (var frame = 0; frame < 60; frame++)
            {
                var rendered = suppressed.Render();

                Assert.Equal(plain.Render(), rendered);
                Assert.DoesNotContain(ESCAPE, rendered);
            }
        }

        private static string StripEscapes(string text)
        {
            return Regex.Replace(text, @"\x1b\[[0-9;]*m", string.Empty);
        }
    }
}
