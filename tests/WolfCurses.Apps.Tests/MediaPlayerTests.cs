using System;
using System.Text.RegularExpressions;
using WolfCurses.Apps.MediaPlayer;
using WolfCurses.Apps.Tests.Support;
using WolfCurses.Graphics;
using Xunit;

namespace WolfCurses.Apps.Tests
{
    /// <summary>
    ///     The media player as a person meets it: keys in, frames out.
    ///     <para>
    ///         <b>A test host has no console, so it reports that it cannot draw pictures</b> - and the player takes
    ///         that seriously, falling back to the bars for a file it would otherwise show. That is the behaviour
    ///         rather than a limitation of the tests, and it is asserted here: the one thing worse than a terminal
    ///         that cannot show a film is one that shows a blank rectangle and says nothing.
    ///     </para>
    ///     <para>
    ///         Where the clock is comes off the scrub bar, which writes it out, rather than from counting anything.
    ///     </para>
    /// </summary>
    [Collection("Suite")]
    public class MediaPlayerTests
    {
        private static DrivenSuite OpenPlayer()
        {
            var suite = new DrivenSuite();
            suite.ChooseMenuItem((int) OfficeCommandsEnum.MediaPlayer);

            return suite;
        }

        /// <summary>Where the clock is and how long the media runs, read off the scrub bar itself.</summary>
        private static (TimeSpan At, TimeSpan Of) Clock(DrivenSuite suite)
        {
            var row = suite.Screen.Split('\n')[PlayerChrome.TimelineRow];
            var match = Regex.Match(row, @"^\s*(\d+:\d+(?::\d+)?)\D+(\d+:\d+(?::\d+)?)\s*$");

            Assert.True(match.Success, "the scrub bar did not say where it was:\n" + suite.Describe());

            return (Parse(match.Groups[1].Value), Parse(match.Groups[2].Value));
        }

        /// <summary>Reads a time back off the bar.</summary>
        private static TimeSpan Parse(string text)
        {
            var parts = text.Split(':');

            return parts.Length == 3
                ? new TimeSpan(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]))
                : new TimeSpan(0, int.Parse(parts[0]), int.Parse(parts[1]));
        }

        /// <summary>Runs the player for about this long, letting real time pass so the clock moves.</summary>
        private static void Watch(DrivenSuite suite, double seconds)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();

            while (clock.Elapsed < TimeSpan.FromSeconds(seconds))
            {
                suite.Tick(3);
                System.Threading.Thread.Sleep(20);
            }
        }

        /// <summary>
        ///     Starts the generated tone, muted, which needs no file and no licence.
        ///     <para>
        ///         <b>Muted first, and every one of these tests does it.</b> Running the suite should not play a
        ///         440Hz tone at whoever is running it, fourteen times, through their speakers. Nothing here is
        ///         about whether sound comes out - that is ffplay's job and it has its own tests somewhere - so the
        ///         player's own mute is used, which leaves all three processes running exactly as they would be
        ///         and turns only the volume down. The same stance the BASIC screen takes with <c>audible</c>.
        ///     </para>
        /// </summary>
        private static void PlayTone(DrivenSuite suite)
        {
            Assert.SkipUnless(FfmpegTools.HasFfmpeg, "ffmpeg is not on this machine.");

            suite.PressChar('m', ConsoleKey.M);
            Assert.Contains("Muted", suite.Screen, StringComparison.Ordinal);

            suite.Press(ConsoleKey.F8);
            Watch(suite, 1d);
        }

        [Fact]
        public void ItOpensExplainingItselfAndSayingWhatWasFound()
        {
            using var suite = OpenPlayer();

            // The idle page is where the three separate answers live, because this screen can fail in three
            // separate ways and a blank rectangle explains none of them.
            Assert.Contains("WolfCurses Media Player", suite.Screen, StringComparison.Ordinal);
            Assert.Contains("ffmpeg", suite.Screen, StringComparison.Ordinal);
            Assert.Contains("ffprobe", suite.Screen, StringComparison.Ordinal);
            Assert.Contains("ffplay", suite.Screen, StringComparison.Ordinal);
            Assert.Contains("Pictures", suite.Screen, StringComparison.Ordinal);
            Assert.Contains("Nothing open", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void ATerminalThatCannotDrawPicturesSaysSoRatherThanShowingNothing()
        {
            using var suite = OpenPlayer();

            // A test host has no console to enable VT on, so this is the honest answer and the screen gives it.
            Assert.False(AnsiConsole.SupportsPictures());
            Assert.Contains("Pictures  not here", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void TheWholeScreenFitsAnEightyColumnTerminal()
        {
            using var suite = OpenPlayer();

            foreach (var row in suite.Screen.Split('\n'))
                Assert.True(row.TrimEnd('\r').Length <= 80, "a row was " + row.Length + " columns wide");
        }

        [Fact]
        public void TheScrubBarIsThereBeforeAnythingIsOpen()
        {
            using var suite = OpenPlayer();

            var clock = Clock(suite);

            Assert.Equal(TimeSpan.Zero, clock.At);
            Assert.Equal(TimeSpan.Zero, clock.Of);
        }

        [Fact]
        public void PlayingSomethingStartsTheClockAndSaysHowLongItIs()
        {
            using var suite = OpenPlayer();
            PlayTone(suite);

            // Watched past a whole second, because the bar counts them and nine tenths of one reads as none.
            Watch(suite, 1.5d);

            var clock = Clock(suite);

            Assert.Equal(TimeSpan.FromSeconds(30d), clock.Of);
            Assert.True(clock.At > TimeSpan.Zero, "the clock did not move:\n" + suite.Describe());
            Assert.Contains("Playing", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void SomethingWithNoPictureInItGetsTheBarsInstead()
        {
            using var suite = OpenPlayer();
            PlayTone(suite);

            Watch(suite, 1d);

            // A tone is a tone, so this asserts that something is being drawn on the stage rather than which
            // bands: what the bars show is a fact about the sound, and the spectrum is pinned where it is pure.
            var stage = string.Join('\n', suite.Screen.Split('\n'),
                PlayerChrome.StageRow, PlayerChrome.StageRows);

            Assert.Contains('█', stage);
            Assert.Contains("Test tone", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void TheStageIsAlwaysTheSameHeight()
        {
            using var suite = OpenPlayer();

            var idle = suite.Screen.Split('\n').Length;

            PlayTone(suite);
            Assert.Equal(idle, suite.Screen.Split('\n').Length);

            suite.Press(ConsoleKey.F6);
            Assert.Equal(idle, suite.Screen.Split('\n').Length);
        }

        [Fact]
        public void SpaceStopsTheClockAndStartsItAgain()
        {
            using var suite = OpenPlayer();
            PlayTone(suite);

            suite.Press(ConsoleKey.Spacebar);
            Assert.Contains("Paused", suite.Screen, StringComparison.Ordinal);

            var paused = Clock(suite).At;
            Watch(suite, 1.5d);

            // The whole of what a pause is: real time passes and the media time does not.
            Assert.Equal(paused, Clock(suite).At);

            suite.Press(ConsoleKey.Spacebar);
            Watch(suite, 1.5d);

            Assert.Contains("Playing", suite.Screen, StringComparison.Ordinal);
            Assert.True(Clock(suite).At > paused, "the clock did not start again");
        }

        [Fact]
        public void TheArrowsSeek()
        {
            using var suite = OpenPlayer();
            PlayTone(suite);

            suite.Press(ConsoleKey.Spacebar);
            var from = Clock(suite).At;

            suite.Press(ConsoleKey.RightArrow);
            var forward = Clock(suite).At;

            Assert.True(forward >= from + TimeSpan.FromSeconds(4d),
                "five seconds forward went from " + from + " to " + forward);

            suite.Press(ConsoleKey.LeftArrow);

            Assert.True(Clock(suite).At < forward, "five seconds back did not go back");
        }

        [Fact]
        public void SeekingDoesNotStartAPausedPlayerPlayingAgain()
        {
            using var suite = OpenPlayer();
            PlayTone(suite);

            suite.Press(ConsoleKey.Spacebar);
            Assert.Contains("Paused", suite.Screen, StringComparison.Ordinal);

            suite.Press(ConsoleKey.RightArrow);
            var at = Clock(suite).At;

            Watch(suite, 1d);

            // Dragging a scrub bar about must not quietly resume, which is the half of seeking that is easy to
            // get wrong because forwards-while-playing works either way.
            Assert.Contains("Paused", suite.Screen, StringComparison.Ordinal);
            Assert.Equal(at, Clock(suite).At);
        }

        [Fact]
        public void HomeGoesBackToTheStart()
        {
            using var suite = OpenPlayer();
            PlayTone(suite);

            suite.Press(ConsoleKey.UpArrow);
            Assert.True(Clock(suite).At > TimeSpan.FromSeconds(20d));

            suite.Press(ConsoleKey.Home);
            Assert.Equal(TimeSpan.Zero, Clock(suite).At);
        }

        [Fact]
        public void ClickingTheScrubBarSeeksToWhereItWasClicked()
        {
            using var suite = OpenPlayer();
            PlayTone(suite);

            suite.Press(ConsoleKey.Spacebar);

            // Read off the bar's own layout: the column halfway along it must be about halfway through.
            var row = suite.Screen.Split('\n')[PlayerChrome.TimelineRow].TrimEnd('\r');
            var bar = row.IndexOf('─');

            Assert.True(bar > 0, "no scrub bar was drawn:\n" + suite.Describe());

            suite.Click(PlayerChrome.TimelineRow, bar + (row.Length - bar - 5) / 2);

            var at = Clock(suite).At;

            Assert.True(at > TimeSpan.FromSeconds(8d) && at < TimeSpan.FromSeconds(22d),
                "a click halfway along a thirty second bar landed at " + at);
        }

        [Fact]
        public void ClickingSomewhereThatIsNotTheBarSeeksNowhere()
        {
            using var suite = OpenPlayer();
            PlayTone(suite);

            suite.Press(ConsoleKey.Spacebar);
            var at = Clock(suite).At;

            suite.Click(PlayerChrome.StageRow + 4, 20);

            Assert.Equal(at, Clock(suite).At);
        }

        [Fact]
        public void ClosingPutsTheIdlePageBack()
        {
            using var suite = OpenPlayer();
            PlayTone(suite);

            suite.Press(ConsoleKey.F6);

            Assert.Contains("Nothing open", suite.Screen, StringComparison.Ordinal);
            Assert.Contains("Stopped", suite.Screen, StringComparison.Ordinal);
            Assert.Equal(TimeSpan.Zero, Clock(suite).Of);
        }

        [Fact]
        public void RestartingPlaysItFromTheBeginningAgain()
        {
            using var suite = OpenPlayer();
            PlayTone(suite);

            suite.Press(ConsoleKey.UpArrow);
            Assert.True(Clock(suite).At > TimeSpan.FromSeconds(20d));

            suite.Press(ConsoleKey.F5);

            Assert.True(Clock(suite).At < TimeSpan.FromSeconds(2d), "F5 did not go back to the start");
        }

        [Fact]
        public void ThePlayMenuIsGreyedUntilSomethingIsOpen()
        {
            using var suite = OpenPlayer();

            suite.Press(ConsoleKey.F10);
            suite.Press(ConsoleKey.RightArrow);

            Assert.True(IsGreyed(RawRowWith(suite, "Play / Pause")),
                "Play should be greyed with nothing open:\n" + suite.Describe());

            suite.Press(ConsoleKey.Escape);
            PlayTone(suite);

            suite.Press(ConsoleKey.F10);
            suite.Press(ConsoleKey.RightArrow);

            Assert.False(IsGreyed(RawRowWith(suite, "Play / Pause")), "Play should be live once something is open");
        }

        [Fact]
        public void AMenuDrawsOverTheStageAndTheStageComesBack()
        {
            using var suite = OpenPlayer();

            suite.Press(ConsoleKey.F10);
            Assert.Contains("Test Pattern", suite.Screen, StringComparison.Ordinal);

            suite.Press(ConsoleKey.Escape);

            Assert.DoesNotContain("Test Pattern", suite.Screen, StringComparison.Ordinal);
            Assert.Contains("WolfCurses Media Player", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void EscapeShutsAnOpenMenuBeforeItLeavesTheApplication()
        {
            using var suite = OpenPlayer();

            suite.Press(ConsoleKey.F10);
            suite.Press(ConsoleKey.Escape);
            Assert.Contains("WolfCurses Media Player", suite.Screen, StringComparison.Ordinal);

            suite.Press(ConsoleKey.Escape);
            Assert.Contains("Which application?", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void LeavingTheApplicationWhilePlayingStopsEverything()
        {
            using var suite = OpenPlayer();
            PlayTone(suite);

            // ESC clears the form, which is the path OnFormClosing exists for: without it, ffplay would still be
            // making a noise back at the suite menu with nothing on screen to stop it.
            suite.Escape();

            Assert.Contains("Which application?", suite.Screen, StringComparison.Ordinal);

            // And going back in finds it stopped rather than still running.
            suite.ChooseMenuItem((int) OfficeCommandsEnum.MediaPlayer);

            Assert.Contains("Nothing open", suite.Screen, StringComparison.Ordinal);
            Assert.Contains("Stopped", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void QuittingTheProgramWhilePlayingStopsTheSound()
        {
            Assert.SkipUnless(FfmpegTools.HasFfmpeg, "ffmpeg is not on this machine.");
            Assert.SkipUnless(AudioPlayer.IsAvailable, "ffplay is not on this machine.");

            // The path reported from a real run: something playing, then the program quits. Tearing the
            // simulation down is what the Quit menu and CTRL+C both do, and it has to reach the form.
            var before = Playing();
            var suite = new DrivenSuite();

            try
            {
                suite.ChooseMenuItem((int) OfficeCommandsEnum.MediaPlayer);
                suite.PressChar('m', ConsoleKey.M);
                suite.Press(ConsoleKey.F8);

                Watch(suite, 1d);

                Assert.True(Settles(() => Playing() > before), "the sound never started");
            }
            finally
            {
                suite.Dispose();
            }

            // Counted rather than read off the screen: there is no screen any more, and the only thing that
            // matters is whether a program is still making a noise somewhere.
            Assert.True(Settles(() => Playing() <= before), "the sound outlived the program that started it");
        }

        /// <summary>How many copies of ffplay are running just now.</summary>
        private static int Playing()
        {
            return System.Diagnostics.Process.GetProcessesByName("ffplay").Length;
        }

        /// <summary>Waits a few seconds for something to become true.</summary>
        private static bool Settles(Func<bool> settled)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();

            while (clock.Elapsed < TimeSpan.FromSeconds(10d))
            {
                if (settled())
                    return true;

                System.Threading.Thread.Sleep(50);
            }

            return settled();
        }

        /// <summary>The raw row holding some text, escapes and all.</summary>
        private static string RawRowWith(DrivenSuite suite, string text)
        {
            foreach (var row in suite.RawScreen.Split('\n'))
            {
                if (AnsiText.StripEscapes(row).Contains(text, StringComparison.Ordinal))
                    return row;
            }

            Assert.Fail("no row held that text:\n" + suite.Describe());
            return string.Empty;
        }

        /// <summary>Whether a menu row is painted in the greyed style.</summary>
        private static bool IsGreyed(string row)
        {
            return row.Contains(DosTheme.MenuDisabled.OpenSequence(), StringComparison.Ordinal);
        }
    }
}
