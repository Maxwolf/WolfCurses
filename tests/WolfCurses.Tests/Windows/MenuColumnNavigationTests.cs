using System;
using System.Text.RegularExpressions;
using WolfCurses.Tests.Support;
using WolfCurses.Tests.TestDoubles;
using Xunit;

namespace WolfCurses.Tests.Windows
{
    /// <summary>
    ///     Covers Left and Right over a menu <see cref="WolfCurses.Window.Menu.MenuLayout" /> has reflowed into
    ///     columns. Up and Down keep walking the items in numbered order and wrapping round the ends, which is what
    ///     the numbering promises and what <see cref="MenuHighlightTests" /> already pins; these two keys are the only
    ///     thing that crosses the grid sideways.
    ///     <para>
    ///         The arithmetic itself is pinned in <see cref="MenuColumnLayoutTests" />, where the grid's shape is an
    ///         argument rather than a fact about whatever console the suite is running on. What is left for here is
    ///         the wiring: that the keys reach the stepper at all, and that they step through the same grid the
    ///         renderer drew. So the assertions read the highlight cursor's position off the rendered frame rather
    ///         than naming an index, which is also the only form they could take: the column count depends on the
    ///         console, and a test that named a cell would be a test about the machine it ran on.
    ///     </para>
    /// </summary>
    public class MenuColumnNavigationTests
    {
        /// <summary>
        ///     Long enough that the menu reflows on any console anyone runs a test suite on: columns appear once the
        ///     list outgrows the rows available, so this needs a terminal over a hundred rows tall to stay in one.
        /// </summary>
        private const int LongMenu = 100;

        /// <summary>Inverse video depends on the environment's color mode; the "&gt; " marker is the contract.</summary>
        private static string StripSgr(string text)
        {
            return Regex.Replace(text, @"\x1b\[[0-9;]*m", string.Empty);
        }

        private static (TestSimulationApp app, TestWindow window) NewAppWithMenu(int commands)
        {
            var app = new TestSimulationApp();
            app.WindowManager.Add(typeof(TestWindow));
            var window = (TestWindow) app.WindowManager.FocusedWindow;
            window.AddTestCommands(commands);
            return (app, window);
        }

        /// <summary>
        ///     Where the highlight cursor sits in a rendered frame: which physical line, and how many visible columns
        ///     in. Both are (-1, -1) when nothing is highlighted.
        /// </summary>
        private static (int Line, int Offset) Cursor(string rendered)
        {
            var lines = StripSgr(rendered).Split('\n');
            for (var line = 0; line < lines.Length; line++)
            {
                var offset = lines[line].IndexOf("> ", StringComparison.Ordinal);
                if (offset >= 0)
                    return (line, offset);
            }

            return (-1, -1);
        }

        /// <summary>
        ///     Whether the menu on screen actually has columns to cross between. A reflowed menu puts several items on
        ///     one physical line, so it occupies fewer lines than it has items. Whether it reflowed at all is a fact
        ///     about the console the suite happens to be running on, not about the code: a headless host reports 80x24
        ///     and splits a hundred items into five columns, but a terminal under about thirty columns wide has no
        ///     room for a second one and correctly stays single-column.
        /// </summary>
        private static bool IsReflowed(string rendered)
        {
            return StripSgr(rendered).Split('\n', StringSplitOptions.RemoveEmptyEntries).Length < LongMenu;
        }

        [Fact]
        public void WhileTheMenuFitsInOneColumnLeftAndRightDoNothingAtAll()
        {
            // The compatibility half, and the one that always runs: under MinItemsToReflow a menu is single-column
            // whatever the console reports, and there Left and Right stay as inert as any other non-arrow key. Not
            // even a highlight is summoned, so a window whose menu fits still renders byte-identically to a library
            // that had never heard of columns.
            var (app, window) = NewAppWithMenu(3);
            var untouched = window.OnRenderWindow();

            app.InputManager.SendKeyPress(ConsoleKey.RightArrow);
            app.InputManager.SendKeyPress(ConsoleKey.LeftArrow);
            app.OnTick(false);

            Assert.Equal(untouched, window.OnRenderWindow());
            Assert.DoesNotContain('\x1b', untouched);
            Assert.StartsWith("  1. First" + Text.NL, untouched, StringComparison.Ordinal);
        }

        [Fact]
        public void RightCrossesToTheColumnBesideItOnTheSameRow()
        {
            var (app, window) = NewAppWithMenu(LongMenu);
            app.InputManager.SendKeyPress(ConsoleKey.DownArrow); // Summons the highlight onto the first item.
            app.OnTick(false);

            var before = window.OnRenderWindow();
            Assert.SkipUnless(IsReflowed(before), "this console is too narrow for the menu to have a second column");
            var from = Cursor(before);

            app.InputManager.SendKeyPress(ConsoleKey.RightArrow);
            app.OnTick(false);
            var to = Cursor(window.OnRenderWindow());

            Assert.Equal(from.Line, to.Line);
            Assert.True(to.Offset > from.Offset,
                $"Right left the highlight in the same column, at offset {to.Offset} against {from.Offset}");
        }

        [Fact]
        public void LeftComesBackToTheColumnItCameFrom()
        {
            var (app, window) = NewAppWithMenu(LongMenu);
            app.InputManager.SendKeyPress(ConsoleKey.DownArrow);
            app.OnTick(false);

            var start = window.OnRenderWindow();
            Assert.SkipUnless(IsReflowed(start), "this console is too narrow for the menu to have a second column");

            app.InputManager.SendKeyPress(ConsoleKey.RightArrow);
            app.OnTick(false);
            Assert.NotEqual(start, window.OnRenderWindow());

            app.InputManager.SendKeyPress(ConsoleKey.LeftArrow);
            app.OnTick(false);

            // The top row exists in every column, so this round trip is exact. It is not exact from a row only the
            // taller column has, and MenuColumnLayoutTests pins that asymmetry where the shape can be stated.
            Assert.Equal(start, window.OnRenderWindow());
        }

        [Fact]
        public void TheOuterColumnsAreWallsRatherThanWrappingRound()
        {
            var (app, window) = NewAppWithMenu(LongMenu);
            app.InputManager.SendKeyPress(ConsoleKey.DownArrow);
            app.OnTick(false);

            var leftmost = window.OnRenderWindow();
            Assert.SkipUnless(IsReflowed(leftmost), "this console is too narrow for the menu to have a second column");

            // Left on the leftmost column stays put rather than appearing at the far side of the screen.
            app.InputManager.SendKeyPress(ConsoleKey.LeftArrow);
            app.OnTick(false);
            Assert.Equal(leftmost, window.OnRenderWindow());

            // And walking off the right runs out of columns rather than coming back round the left. More presses than
            // any width can hold columns, so the last several are all against the wall.
            var furthest = Cursor(leftmost).Offset;
            for (var press = 0; press < 12; press++)
            {
                app.InputManager.SendKeyPress(ConsoleKey.RightArrow);
                app.OnTick(false);

                var offset = Cursor(window.OnRenderWindow()).Offset;
                Assert.True(offset >= furthest,
                    $"Right press {press + 1} moved the highlight back to offset {offset} from {furthest}");
                furthest = offset;
            }

            Assert.True(furthest > Cursor(leftmost).Offset, "twelve Right presses never left the first column");
        }

        [Fact]
        public void AStepAcrossFromTheLastItemStillLandsOnARealOne()
        {
            // The far corner of the grid is where an off-by-one shows up: End puts the highlight on the final item,
            // which is the bottom of the rightmost column, and a Left step from there has to land on a cell the
            // renderer will actually draw a cursor in rather than past the end of the list. Which cell that is
            // depends on the console's shape, so the assertion is that there is one. The clamp rule itself is pinned
            // in MenuColumnLayoutTests, where the shape is an argument.
            var (app, window) = NewAppWithMenu(LongMenu);
            app.InputManager.SendKeyPress(ConsoleKey.End);
            app.OnTick(false);

            var end = window.OnRenderWindow();
            Assert.SkipUnless(IsReflowed(end), "this console is too narrow for the menu to have a second column");
            Assert.Contains($"> {LongMenu}. ", StripSgr(end), StringComparison.Ordinal);

            app.InputManager.SendKeyPress(ConsoleKey.LeftArrow);
            app.OnTick(false);

            var stepped = Cursor(window.OnRenderWindow());
            Assert.True(stepped.Line >= 0, "the highlight vanished, so Left landed past the end of the list");
            Assert.True(stepped.Offset < Cursor(end).Offset, "Left did not move to an earlier column");
        }

        [Fact]
        public void TheFirstSidewaysArrowSummonsTheHighlightLikeEveryOtherArrow()
        {
            // Same rule the vertical keys follow: the first movement key summons the highlight rather than moving
            // one, onto whichever end of the list it points at.
            var (app, window) = NewAppWithMenu(LongMenu);
            app.InputManager.SendKeyPress(ConsoleKey.RightArrow);
            app.OnTick(false);

            var rendered = window.OnRenderWindow();
            Assert.SkipUnless(IsReflowed(rendered), "this console is too narrow for the menu to have a second column");
            Assert.Contains("> 1. First", StripSgr(rendered), StringComparison.Ordinal);

            var (other, secondWindow) = NewAppWithMenu(LongMenu);
            other.InputManager.SendKeyPress(ConsoleKey.LeftArrow);
            other.OnTick(false);

            Assert.Contains($"> {LongMenu}. ", StripSgr(secondWindow.OnRenderWindow()), StringComparison.Ordinal);
        }
    }
}
