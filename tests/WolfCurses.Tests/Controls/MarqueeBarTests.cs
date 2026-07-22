using System.Linq;
using WolfCurses.Tests.Support;
using WolfCurses.Window.Control;
using Xunit;

namespace WolfCurses.Tests.Controls
{
    public class MarqueeBarTests
    {
        private const int BAR_LENGTH = 27;

        [Fact]
        public void Step_FirstCall_PlacesPointerAtLeftEdge()
        {
            var bar = new MarqueeBar();

            Assert.Equal("|***" + new string(' ', 22) + "|" + Text.NL, bar.Step());
        }

        [Fact]
        public void Step_IsDeterministicAcrossInstances()
        {
            var first = new MarqueeBar();
            var second = new MarqueeBar();

            for (var i = 0; i < 60; i++)
                Assert.Equal(first.Step(), second.Step());
        }

        [Fact]
        public void Step_EveryFrameHasConstantLengthAndTrailingNewline()
        {
            var bar = new MarqueeBar();

            for (var i = 0; i < 100; i++)
            {
                var frame = bar.Step();
                Assert.Equal(BAR_LENGTH + Text.NL.Length, frame.Length);
                Assert.EndsWith(Text.NL, frame);
                Assert.Contains("***", frame);
            }
        }

        [Fact]
        public void Step_PingPongs_ReachesRightEdgeAndReturnsToFirstFrame()
        {
            var bar = new MarqueeBar();
            var frames = Enumerable.Range(0, 100).Select(_ => bar.Step()).ToList();

            // Pointer touches the right edge on the way out...
            Assert.Contains(frames, f => f.StartsWith("|" + new string(' ', 22) + "***|"));

            // ...and the initial frame comes around again after the bounce.
            Assert.Contains(frames.Skip(1), f => f == frames[0]);
        }

        [Fact]
        public void Render_ReturnsTheFrameWithoutATrailingNewline()
        {
            var bar = new MarqueeBar();

            var frame = bar.Render();

            Assert.Equal("|***" + new string(' ', 22) + "|", frame);
            Assert.Equal(BAR_LENGTH, frame.Length);
            Assert.DoesNotContain('\n', frame);
            Assert.DoesNotContain('\r', frame);
        }

        [Fact]
        public void Step_IsRenderPlusATrailingNewline()
        {
            // Two bars advanced in lockstep: whatever Render() gives one, Step() gives the other with exactly one
            // newline appended. That equivalence is what makes Render() the inline drop-in and Step() the compat form,
            // and it means the pinned Step() output above is unchanged by Render()'s arrival.
            var rendered = new MarqueeBar();
            var stepped = new MarqueeBar();

            for (var i = 0; i < 100; i++)
                Assert.Equal(rendered.Render() + Text.NL, stepped.Step());
        }
    }
}
