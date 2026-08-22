using System;
using System.Threading;
using WolfCurses.Games.Tests.Support;
using Xunit;

namespace WolfCurses.Games.Tests.Battlezone
{
    /// <summary>
    ///     The gear lever and the trigger, fed through the same public door a real console reader uses.
    ///     <para>
    ///         A test host has no mouse and never will, so nothing here says whether a terminal reports a notch.
    ///         What it says is everything after that: that a notch works the gear rather than pulling the trigger,
    ///         that the lever stops at each end of its travel, and that a button fires. Every assertion is about
    ///         what is on the screen, since the gear is on the status line and a shell in the air is drawn as a star
    ///         that nothing else on this screen draws.
    ///     </para>
    ///     <para>
    ///         <b>These sleep, and cannot not.</b> This screen is paced by an <c>IntervalTimer</c> against real
    ///         elapsed time, so an input that is not followed by a real frame reaches no frame at all.
    ///     </para>
    /// </summary>
    [Collection("GamesApp")]
    public class BattlezoneWheelTests
    {
        /// <summary>Turns the wheel by a number of notches; positive is away from the user, which is ahead.</summary>
        /// <param name="game">The running arcade.</param>
        /// <param name="notches">How far the wheel turned.</param>
        private static void Wheel(DrivenGamesApp game, int notches)
        {
            // The cell is deliberately arbitrary. Nothing on this screen reads a wheel event's column or row, which
            // is exactly what lets it take the mouse without asking for motion reporting.
            game.App.InputManager.SendMouseEvent(new MouseEvent(10, 10, MouseButtonEnum.None,
                kind: MouseEventKindEnum.Wheel, wheelDelta: notches));

            game.App.PumpInput();
        }

        /// <summary>Lets one paced frame land, which is the only way anything here reaches the screen.</summary>
        /// <param name="game">The running arcade.</param>
        private static void Frame(DrivenGamesApp game)
        {
            Thread.Sleep(45);
            game.Tick();
        }

        [Fact]
        public void AWheelNotchWorksTheGearLever()
        {
            // A notch is a lever position rather than a rate, which is the whole reason the wheel fits a throttle
            // that stays where it is put: one notch, one gear, and no held key anywhere in it.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Battlezone);

            Assert.Contains("STOP", game.Screen, StringComparison.Ordinal);

            Wheel(game, 1);
            Frame(game);
            Assert.Contains("AHEAD", game.Screen, StringComparison.Ordinal);

            Wheel(game, -1);
            Frame(game);
            Assert.Contains("STOP", game.Screen, StringComparison.Ordinal);

            Wheel(game, -1);
            Frame(game);
            Assert.Contains("ASTERN", game.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void AFlickIsWorthTheWholeTravelOfTheLeverAndNoMore()
        {
            // The assertion that catches the lazy version. A handler taking Math.Sign of the delta and stepping once
            // lands this on STOP - a tank that came to a halt when the player asked for full ahead - and one looping
            // over the raw delta is looping over however many notches somebody else's mouse decided to batch.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Battlezone);

            game.PressChar('s', ConsoleKey.S);
            Frame(game);
            Assert.Contains("ASTERN", game.Screen, StringComparison.Ordinal);

            Wheel(game, 5);
            Frame(game);
            Assert.Contains("AHEAD", game.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void AWheelNotchIsNotATriggerPull()
        {
            // The hazard MouseEventKindEnum exists to remove, asserted where it would actually show: every mouse
            // protocol encodes a notch as a button press, so a fire-on-click screen reading one as a button empties
            // its magazine on a scroll. A shell is the only star drawn here - enemies are hashes, blips are at-signs
            // and the spinner cycles slashes and bars - and no enemy can have fired yet, since every one of them
            // starts with at least 1.9 seconds of reload against the fifth of a second of world time this spends.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Battlezone);

            Frame(game);
            Assert.DoesNotContain('*', game.Screen);

            Wheel(game, 1);
            Wheel(game, -1);
            Wheel(game, 1);

            // Long enough for a shot fired on the first of them to have flown into view, which is what makes the
            // absence of a star mean something.
            Frame(game);
            Frame(game);
            Frame(game);

            Assert.DoesNotContain('*', game.Screen);

            // And the notches did land somewhere, or this is a statement about a screen that ignored the mouse.
            Assert.Contains("AHEAD", game.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void TheLeftButtonFiresWhereverThePointerIs()
        {
            // The gun is bolted to the hull, so unlike Missile Command there is no cell to turn into a place and a
            // press anywhere is simply the trigger. Two presses, one on either side of the view, and the star has to
            // be there for the same reason it was absent above.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Battlezone);

            Frame(game);
            Assert.DoesNotContain('*', game.Screen);

            game.App.InputManager.SendMouseEvent(new MouseEvent(4, 12, MouseButtonEnum.Left));
            game.App.PumpInput();

            // Three frames, because a shell leaves the barrel seven units ahead and four and a half units below the
            // eye - which projects well below the bottom of the view until it has flown a little way. This is the
            // one number in the file that is about the camera rather than about the mouse.
            Frame(game);
            Frame(game);
            Frame(game);

            Assert.Contains('*', game.Screen);
        }

        [Fact]
        public void ARightClickDoesNotFire()
        {
            // Right-click is paste in a console host's own quick-edit mode and in several terminals' passthrough, so
            // binding a shot to it is asking for accidental ones. Missile Command's rule, for Missile Command's
            // reason.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Battlezone);

            Frame(game);
            Assert.DoesNotContain('*', game.Screen);

            game.App.InputManager.SendMouseEvent(new MouseEvent(30, 10, MouseButtonEnum.Right));
            game.App.PumpInput();

            // The same three frames the left button gets, so the two are asking the screen the same question.
            Frame(game);
            Frame(game);
            Frame(game);

            Assert.DoesNotContain('*', game.Screen);
        }

        [Fact]
        public void TheMouseNeverReachesTheEchoedPrompt()
        {
            // The same thing SingleKeyPlayNeverReachesThePrompt says about the keyboard. A mouse event is not text
            // and the library does not gate it on AcceptingInput, so this is really a check that nothing on this
            // screen answers a click by typing something.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Battlezone);

            Wheel(game, 2);
            game.App.InputManager.SendMouseEvent(new MouseEvent(20, 10, MouseButtonEnum.Left));
            game.App.PumpInput();
            Frame(game);

            Assert.Equal(string.Empty, game.App.InputManager.InputBuffer);
        }
    }
}
