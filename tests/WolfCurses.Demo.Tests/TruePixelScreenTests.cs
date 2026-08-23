using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using WolfCurses.Demo.Screens;
using WolfCurses.Demo.Tests.Support;
using WolfCurses.Graphics;
using Xunit;

namespace WolfCurses.Demo.Tests
{
    /// <summary>
    ///     The demo drawn with a terminal's real pixels, which nothing else here has ever done.
    ///     <para>
    ///         <b>Every renderer test in this repository until now handed a renderer to a helper and measured what
    ///         came back.</b> That covers the arithmetic and covers none of the composition, and composition is
    ///         where the marker contract lives: a sixel or kitty picture is one escape blob of no visible width
    ///         that paints across a dozen rows, so it is shipped as a payload row plus a placeholder for every
    ///         further row it covers, and every one of those rules is about the frame <i>around</i> the picture
    ///         rather than the picture. A screen that puts anything beside a payload, or loses count of the rows
    ///         one claims, is broken in a way no unit test of a renderer can see.
    ///     </para>
    ///     <para>
    ///         <b>These screens are the ones testable anywhere, and which ones those are is worth knowing.</b> The
    ///         demo's picture screens draw unconditionally; the arcade's and the office suite's ask
    ///         <see cref="AnsiConsole.SupportsPictures" /> first, which is false on any host whose output is a pipe
    ///         and whose virtual terminal could not be enabled - every Windows test host, and not a Linux one. So
    ///         those screens take their text fallback here and their picture path on a CI runner, which is worth
    ///         knowing before believing a green suite says anything about them.
    ///     </para>
    ///     <para>
    ///         The render type is forced through the demo's own menu rather than by assigning
    ///         <c>ImageRenderers.Default</c>, so what is under test is what a person can reach; the assignment is
    ///         what the demo does at the end of it. <see cref="RenderTypeScope" /> puts the globals back, because a
    ///         forced renderer outlives the application that set it.
    ///     </para>
    /// </summary>
    [Collection("DemoApp")]
    public class TruePixelScreenTests
    {
        /// <summary>The character a frame drawn for a terminal with no colour must not contain one of.</summary>
        private const char Escape = '\u001b';

        /// <summary>The demos that draw a picture without first asking whether the console can carry one.</summary>
        private static readonly DemoCommandsEnum[] _pictureScreens =
        {
            DemoCommandsEnum.Slideshow,
            DemoCommandsEnum.CompositeSlideshow,
            DemoCommandsEnum.SpriteTestBasic
        };

        [Fact]
        public void TheRealPixelRenderersDrawOnePayloadThatAccountsForEveryRowItCovers()
        {
            Assert.SkipUnless(Directory.Exists(DemoImages.Folder), "media was not copied beside the tests");

            using var restore = new RenderTypeScope();

            foreach (var named in new[] {"Sixel", "Kitty"})
            {
                using var app = new DrivenDemoApp();
                app.DismissSplash();
                app.ForceRenderType(named);

                Assert.True(ImageRenderers.Default.DrawsTruePixels,
                    named + " did not install a real-pixel renderer; it left " + ImageRenderers.Default.Name);

                foreach (var screen in _pictureScreens)
                {
                    Open(app, screen);
                    AssertOnePictureBlock(app.RawScreen, named + " on " + screen + ":" + app.Describe());
                    Close(app);
                }
            }
        }

        [Fact]
        public void NothingIsEverDrawnBesideAPicture()
        {
            Assert.SkipUnless(Directory.Exists(DemoImages.Folder), "media was not copied beside the tests");

            using var restore = new RenderTypeScope();
            using var app = new DrivenDemoApp();

            app.DismissSplash();
            app.ForceRenderType("Sixel");

            foreach (var screen in _pictureScreens)
            {
                Open(app, screen);

                foreach (var row in Rows(app.RawScreen))
                {
                    // The presenter recognises a payload by the marker sitting at index zero, so a row with
                    // anything in front of the picture is not recognised as one: it is erased to end of line and
                    // written as ordinary text, which reaches the terminal as several hundred thousand characters
                    // of escape garbage. A right-hand column, a border, a picture inside a TextColumns column -
                    // every one of them arrives as that.
                    Assert.False(row.IndexOf(AnsiGraphics.Marker) > 0,
                        "something was drawn beside the picture on " + screen + ":" + app.Describe());
                }

                Close(app);
            }
        }

        [Fact]
        public void AScreenIsTheSameHeightWhateverItIsDrawnWith()
        {
            Assert.SkipUnless(Directory.Exists(DemoImages.Folder), "media was not copied beside the tests");

            // The whole reason a picture ships as a payload row plus a placeholder per row it covers: the frame
            // must have as many lines as the screen has rows, or the scene graph's diff and the presenter's row
            // model are both describing a screen that is not there. A picture claiming no rows leaves the frame
            // short by a dozen and everything under it is written over the picture.
            using var restore = new RenderTypeScope();

            var heights = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var named in new[] {"Half blocks - two pixels per cell, 24-bit", "Sixel", "Kitty", "Text only"})
            {
                using var app = new DrivenDemoApp();
                app.DismissSplash();
                app.ForceRenderType(named);

                Open(app, DemoCommandsEnum.Slideshow);
                heights[named] = Rows(app.RawScreen).Length;
            }

            foreach (var pair in heights)
            {
                Assert.True(pair.Value == heights["Sixel"],
                    "the slideshow is " + pair.Value + " rows on " + pair.Key + " and " + heights["Sixel"] +
                    " on Sixel");
            }
        }

        [Fact]
        public void ACharacterCellRendererNeverEmitsAPictureRow()
        {
            Assert.SkipUnless(Directory.Exists(DemoImages.Folder), "media was not copied beside the tests");

            // The other half of the contract, and the half a consumer leans on without knowing it: half blocks are
            // ordinary text, so nothing in a frame drawn with them may need special handling on the way out.
            using var restore = new RenderTypeScope();
            using var app = new DrivenDemoApp();

            app.DismissSplash();
            app.ForceRenderType("Half blocks - two pixels per cell, 24-bit");

            foreach (var screen in _pictureScreens)
            {
                Open(app, screen);

                foreach (var row in Rows(app.RawScreen))
                    Assert.False(AnsiGraphics.IsPictureRow(row), screen + " drew a picture row with half blocks");

                Close(app);
            }
        }

        [Fact]
        public void TextOnlyDrawsThePictureWithNoEscapeAtAll()
        {
            Assert.SkipUnless(Directory.Exists(DemoImages.Folder), "media was not copied beside the tests");

            // What a terminal with no colour is owed, asserted where it can actually be checked: a whole
            // application screen, drawn from a photograph, carrying not one escape sequence. NO_COLOR asked for no
            // escapes rather than for fewer of them, and the shaded ASCII ramp is what honours it.
            using var restore = new RenderTypeScope();
            using var app = new DrivenDemoApp();

            app.DismissSplash();
            app.ForceRenderType("Text only");

            Open(app, DemoCommandsEnum.Slideshow);

            var frame = app.RawScreen;

            Assert.True(frame.IndexOf(Escape) < 0, "the text-only slideshow emitted an escape sequence");
            Assert.True(frame.IndexOf(AnsiGraphics.Marker) < 0, "the text-only slideshow emitted a picture marker");

            // And it is still a picture rather than the blank rectangle where one was: the shaded ramp is made of
            // characters like this one.
            Assert.Contains("#", frame, StringComparison.Ordinal);
        }

        [Fact]
        public void TabOnTheSpriteScreenSwapsTheRendererAndSwapsItBack()
        {
            Assert.SkipUnless(Directory.Exists(DemoImages.Folder), "media was not copied beside the tests");

            // RendererSwitch is the demo's own claim that the difference between two renderers can be watched
            // rather than taken on trust, and nothing checked that pressing TAB changed anything at all.
            using var restore = new RenderTypeScope();
            using var app = new DrivenDemoApp();

            app.DismissSplash();
            app.ForceRenderType("Sixel");

            Open(app, DemoCommandsEnum.SpriteTestBasic);
            Animate(app);

            Assert.True(HasPayload(Rows(app.RawScreen)), "the sprite scene drew no picture under sixel:" + app.Describe());

            app.Press(ConsoleKey.Tab);
            Animate(app);

            Assert.False(HasPayload(Rows(app.RawScreen)),
                "TAB was pressed and the scene was still drawn with real pixels:" + app.Describe());

            app.Press(ConsoleKey.Tab);
            Animate(app);

            Assert.True(HasPayload(Rows(app.RawScreen)), "TAB did not bring the real pixels back:" + app.Describe());
        }

        [Fact]
        public void AHostThatWritesItsOwnFramesHasToStripTheMarkers()
        {
            Assert.SkipUnless(Directory.Exists(DemoImages.Folder), "media was not copied beside the tests");

            // The marker is a private-use character rather than an escape, so taking the escapes out of a frame -
            // the obvious thing for a driver, a screenshot tool or a bot to do - leaves it in, and writing that to
            // a terminal prints it. AnsiGraphics.StripMarkers is the one that removes it.
            using var restore = new RenderTypeScope();
            using var app = new DrivenDemoApp();

            app.DismissSplash();
            app.ForceRenderType("Sixel");

            Open(app, DemoCommandsEnum.Slideshow);

            Assert.True(app.Screen.IndexOf(AnsiGraphics.Marker) >= 0,
                "taking the escapes out of the frame took the markers with them, so this trap has moved");

            Assert.True(AnsiGraphics.StripMarkers(app.RawScreen).IndexOf(AnsiGraphics.Marker) < 0,
                "StripMarkers left a marker behind");
        }

        [Fact]
        public void EveryFrameOfTheAnimationClaimsTheSameRows()
        {
            Assert.SkipUnless(Directory.Exists(DemoImages.Folder), "media was not copied beside the tests");

            // The animated GIF reaches the same contract by a different road: it renders every frame to its own
            // string up front and plays back an array lookup, so the rows are decided ninety-one separate times
            // rather than once. Frames that disagreed about how many they cover would make the screen jump as it
            // played - the picture growing a row and shoving the prompt off the bottom, then giving it back - and
            // no still frame can show that.
            using var restore = new RenderTypeScope();
            using var app = new DrivenDemoApp();

            app.DismissSplash();
            app.ForceRenderType("Sixel");

            app.ChooseMenuItem((int) DemoCommandsEnum.ShowAnimatedGif);
            WaitForTheAnimationToLoad(app);

            var covered = -1;
            var height = -1;
            var frames = 0;

            for (var sample = 0; sample < 8; sample++)
            {
                var rows = Rows(app.RawScreen);

                if (HasPayload(rows))
                {
                    frames++;
                    AssertOnePictureBlock(app.RawScreen, "the animation:" + app.Describe());

                    var claimed = Placeholders(rows);

                    if (covered < 0)
                    {
                        covered = claimed;
                        height = rows.Length;
                    }

                    Assert.True(claimed == covered,
                        "one frame of the animation covered " + claimed + " rows and another " + covered);

                    Assert.True(rows.Length == height,
                        "the screen was " + rows.Length + " rows tall on one frame and " + height + " on another");
                }

                Animate(app);
            }

            Assert.True(frames >= 4, "only " + frames + " frames of the animation were drawn as pictures");
        }

        /// <summary>
        ///     Waits out the animation's up-front render, which is what the screen is named after: every frame is
        ///     rendered to its string before the first is shown, a slice per tick, so it needs ticks and real time
        ///     both. Into sixel that is a couple of seconds, which is the wait a person sees too.
        /// </summary>
        /// <param name="app">The running demo.</param>
        private static void WaitForTheAnimationToLoad(DrivenDemoApp app)
        {
            for (var attempt = 0; attempt < 400 && !HasPayload(Rows(app.RawScreen)); attempt++)
            {
                app.Tick(4);
                Thread.Sleep(10);
            }

            Assert.True(HasPayload(Rows(app.RawScreen)),
                "the animation never finished rendering:" + app.Describe());
        }

        /// <summary>Opens a demo from the menu and lets it draw.</summary>
        /// <param name="app">The running demo.</param>
        /// <param name="screen">Which demo.</param>
        private static void Open(DrivenDemoApp app, DemoCommandsEnum screen)
        {
            app.ChooseMenuItem((int) screen);
            app.Tick(3);
        }

        /// <summary>
        ///     Lets a screen that paces itself compose another frame.
        ///     <para>
        ///         The sprite scene recomposes on an <c>IntervalTimer</c> and keeps the string it made last time
        ///         until the period is up, so a burst of ticks with no real time between them leaves the previous
        ///         frame exactly where it was. Without the wait, pressing TAB and looking at once reads as the
        ///         renderer not having changed rather than as the screen not having redrawn yet, and the test that
        ///         says so is measuring nothing.
        ///     </para>
        /// </summary>
        /// <param name="app">The running demo.</param>
        private static void Animate(DrivenDemoApp app)
        {
            Thread.Sleep(80);
            app.Tick(3);
        }

        /// <summary>Backs out of a demo to the menu.</summary>
        /// <param name="app">The running demo.</param>
        private static void Close(DrivenDemoApp app)
        {
            app.Press(ConsoleKey.Escape);
            app.Tick(1);
        }

        /// <summary>The frame's rows, with the carriage returns taken off.</summary>
        /// <param name="raw">The frame exactly as the terminal would receive it.</param>
        /// <returns>The rows.</returns>
        private static string[] Rows(string raw)
        {
            var rows = raw.Split('\n');

            for (var i = 0; i < rows.Length; i++)
                rows[i] = rows[i].TrimEnd('\r');

            return rows;
        }

        /// <summary>Whether a frame carries a real-pixel picture.</summary>
        /// <param name="rows">The frame's rows.</param>
        /// <returns>TRUE when one of them is a payload.</returns>
        private static bool HasPayload(string[] rows)
        {
            foreach (var row in rows)
            {
                if (AnsiGraphics.IsPayloadRow(row))
                    return true;
            }

            return false;
        }

        /// <summary>How many rows of a frame are covered by a picture drawn above them.</summary>
        /// <param name="rows">The frame's rows.</param>
        /// <returns>The count.</returns>
        private static int Placeholders(string[] rows)
        {
            var found = 0;

            foreach (var row in rows)
            {
                if (AnsiGraphics.IsRowPlaceholder(row))
                    found++;
            }

            return found;
        }

        /// <summary>
        ///     Asserts a frame holds exactly one picture, drawn as one payload row with a placeholder for every
        ///     further row it covers and none anywhere else.
        /// </summary>
        /// <param name="raw">The frame.</param>
        /// <param name="what">What was being drawn, for a failure message.</param>
        private static void AssertOnePictureBlock(string raw, string what)
        {
            var rows = Rows(raw);
            var payloads = new List<int>();
            var placeholders = 0;

            for (var i = 0; i < rows.Length; i++)
            {
                if (AnsiGraphics.IsPayloadRow(rows[i]))
                    payloads.Add(i);
                else if (AnsiGraphics.IsRowPlaceholder(rows[i]))
                    placeholders++;
            }

            // One picture per screen is the documented limit rather than a habit of these demos: two payloads in a
            // frame are two blobs painting over each other from wherever the terminal left the cursor.
            Assert.True(payloads.Count == 1, "there were " + payloads.Count + " payload rows on " + what);

            // A payload with nothing in it is what an empty rectangle on screen looks like from here, and it is
            // what a renderer handed a canvas of no pixels produces.
            Assert.True(rows[payloads[0]].Length > 500,
                "the payload was " + rows[payloads[0]].Length + " characters on " + what);

            var covered = 0;

            while (payloads[0] + 1 + covered < rows.Length &&
                   AnsiGraphics.IsRowPlaceholder(rows[payloads[0] + 1 + covered]))
                covered++;

            Assert.True(covered > 0, "the picture claimed no rows below its payload on " + what);

            // Every placeholder in the frame belongs to this picture and sits directly under it. One anywhere else
            // is a row the frame has lost count of, which on screen looks like everything below sliding up by
            // however many rows the picture was wrong by.
            Assert.True(placeholders == covered,
                "the frame has " + placeholders + " placeholder rows and the picture covers " + covered +
                " on " + what);
        }
    }
}
