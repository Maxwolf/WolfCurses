using System;
using WolfCurses.Example.Tests.Support;
using Xunit;

namespace WolfCurses.Example.Tests
{
    /// <summary>
    ///     The demo application as a person meets it. Keys in, frames out, and nothing reaches inside a form.
    /// </summary>
    [Collection("ExampleApp")]
    public class ExampleScreenTests
    {
        [Fact]
        public void ItOpensOnTheAsciiWordmark()
        {
            using var app = new DrivenExampleApp();

            // The splash draws no image at all - letters, so that a library whose subject is text does not open on
            // a photograph, and so it survives a terminal that can do nothing else.
            Assert.Contains("a text user interface library", app.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void TheWordmarkAnimatesWithoutTheArtChanging()
        {
            using var app = new DrivenExampleApp();

            var first = app.RawScreen;
            System.Threading.Thread.Sleep(120);
            app.Tick(4);
            var later = app.RawScreen;

            // The gradient slides, so the bytes differ; strip the colour and the letters are identical, because
            // colour is the only thing moving.
            Assert.NotEqual(first, later);
            Assert.Equal(ArtOnly(first), ArtOnly(later));
        }

        /// <summary>
        ///     The frame with the colour and the status line taken off. The scene graph's first row carries a
        ///     spinner that advances every tick, so it is text that legitimately changes and has nothing to do with
        ///     whether the wordmark did.
        /// </summary>
        private static string ArtOnly(string frame)
        {
            var stripped = WolfCurses.Graphics.AnsiText.StripEscapes(frame);
            var rows = stripped.Split('\n');
            return rows.Length < 2 ? stripped : string.Join('\n', rows, 1, rows.Length - 1);
        }

        [Fact]
        public void EnterDismissesTheSplashAndRevealsTheMenu()
        {
            using var app = new DrivenExampleApp();

            app.DismissSplash();

            Assert.Contains("Example Console Application", app.Screen, StringComparison.Ordinal);
            Assert.Contains("Render type:", app.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void TheMenuNamesTheRendererRatherThanItsClass()
        {
            // The renderer describes itself now; before, this line came from a switch over the built-in types that
            // named a third-party renderer by its class name.
            using var app = new DrivenExampleApp();
            app.DismissSplash();

            Assert.Contains("half blocks", app.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void EscapeBacksOutOfADemoToTheMenu()
        {
            using var app = new DrivenExampleApp();
            app.DismissSplash();

            app.ChooseMenuItem((int) ExampleCommandsEnum.PrideFlags);
            var demo = app.Screen;

            app.Press(ConsoleKey.Escape);

            Assert.NotEqual(demo, app.Screen);
            Assert.Contains("What is your choice?", app.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void ThePrideFlagDemoDrawsAFlagInColour()
        {
            using var app = new DrivenExampleApp();
            app.DismissSplash();

            app.ChooseMenuItem((int) ExampleCommandsEnum.PrideFlags);

            // Colour is the entire content of this screen - the bars carry no text - so a frame with no escapes in
            // it would be a blank rectangle.
            Assert.Contains("", app.RawScreen, StringComparison.Ordinal);
        }

        [Fact]
        public void TheProgressAndGraphDemoAnimatesOffTheTick()
        {
            using var app = new DrivenExampleApp();
            app.DismissSplash();

            app.ChooseMenuItem((int) ExampleCommandsEnum.ProgressAndGraphs);
            var first = app.Screen;

            System.Threading.Thread.Sleep(120);
            app.Tick(6);

            Assert.NotEqual(first, app.Screen);
        }

        [Fact]
        public void AnImageDemoDrawsSomethingRatherThanTheErrorCheckerboard()
        {
            using var app = new DrivenExampleApp();
            app.DismissSplash();

            app.ChooseMenuItem((int) ExampleCommandsEnum.Slideshow);
            app.Tick(3);

            Assert.DoesNotContain("could not", app.Screen, StringComparison.OrdinalIgnoreCase);
            Assert.True(app.RawScreen.Length > 200, "the slideshow drew almost nothing:\n" + app.Describe());
        }

        [Fact]
        public void TheImageErrorDemoShowsWhyEachLoadFailed()
        {
            // The demo exists to prove a bad image never throws; what it must show is the reason, not just the
            // checkerboard.
            using var app = new DrivenExampleApp();
            app.DismissSplash();

            app.ChooseMenuItem((int) ExampleCommandsEnum.ImageErrorDemo);
            app.Tick(2);

            Assert.Contains("404", app.Screen, StringComparison.Ordinal);
            Assert.Contains("Exception", app.Screen, StringComparison.Ordinal);
        }
    }
}
