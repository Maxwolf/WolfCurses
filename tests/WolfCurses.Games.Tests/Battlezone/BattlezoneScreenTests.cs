using System;
using System.Collections.Generic;
using System.Threading;
using WolfCurses.Games.Tests.Support;
using Xunit;

namespace WolfCurses.Games.Tests.Battlezone
{
    /// <summary>
    ///     The plain, driven through the real arcade the way a player would drive it.
    ///     <para>
    ///         These sleep, and cannot not: everything on this screen is paced by an <c>IntervalTimer</c> against
    ///         real elapsed time, so spinning the tick loop as fast as it will go produces exactly one frame.
    ///     </para>
    /// </summary>
    [Collection("GamesApp")]
    public class BattlezoneScreenTests
    {
        /// <summary>The frame as rows, however the platform spells a line break.</summary>
        /// <param name="game">The running arcade.</param>
        /// <returns>The rows.</returns>
        private static string[] Rows(DrivenGamesApp game)
        {
            return game.Screen.Replace("\r\n", "\n").Split('\n');
        }

        /// <summary>How many full-width rows the frame has, which is the view and nothing else.</summary>
        /// <param name="game">The running arcade.</param>
        /// <returns>The count.</returns>
        private static int ViewRows(DrivenGamesApp game)
        {
            var rows = Rows(game);
            var widest = 0;

            foreach (var row in rows)
                widest = Math.Max(widest, row.Length);

            var count = 0;
            foreach (var row in rows)
            {
                if (row.Length == widest)
                    count++;
            }

            return count;
        }

        [Fact]
        public void TheArcadeMenuOffersIt()
        {
            using var game = new DrivenGamesApp();

            Assert.Contains("Battlezone", game.Screen, StringComparison.Ordinal);
            Assert.Contains("Battlezone best:", game.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void ItOpensLookingOutOverThePlain()
        {
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Battlezone);

            var screen = game.Screen;

            Assert.Contains("Score 0", screen, StringComparison.Ordinal);
            Assert.Contains("Tanks ###", screen, StringComparison.Ordinal);
            Assert.Contains("Kills 0", screen, StringComparison.Ordinal);

            // The gunsight, which is the one thing on the screen that is drawn in the same place every frame. The
            // horizon would be the obvious thing to look for and is the wrong thing: the plain is randomised, so
            // whether any particular run of it survives being crossed by blocks is a coin toss - the exact shape of
            // flaky screen assertion this arcade has shipped once already.
            Assert.Contains("==", screen, StringComparison.Ordinal);
            Assert.True(ViewRows(game) >= 8, "the view is not there");
        }

        [Fact]
        public void TheViewIsCharactersRatherThanEscapeGarbageOnATerminalWithoutRealPixels()
        {
            // A wireframe is the one subject a character grid is genuinely good at, so on a small terminal the
            // character view is the better picture rather than a fallback - and the picture is refused outright
            // rather than degraded, since the presenter blanks a payload row it cannot interpret.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Battlezone);

            Assert.DoesNotContain('', game.RawScreen);
            Assert.Contains('|', game.Screen);
        }

        [Fact]
        public void TheWholeScreenFitsAnEightyByTwentyFourTerminal()
        {
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Battlezone);

            var rows = new List<string>(Rows(game));

            // Trailing blank rows are dropped first: ConsolePresenter clips the frame to one row short of the
            // window, and a blank row carries nothing. What has to fit is the content.
            while (rows.Count > 0 && rows[rows.Count - 1].Length == 0)
                rows.RemoveAt(rows.Count - 1);

            Assert.InRange(rows.Count, 1, 23);
            foreach (var row in rows)
                Assert.InRange(row.Length, 0, 80);
        }

        [Fact]
        public void TurningChangesWhatIsOnTheScreen()
        {
            // A view rather than a map: turning the tank is the only way to see anything that is not in front of it,
            // so if the frame does not move when the tank turns there is no game at all.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Battlezone);

            Thread.Sleep(50);
            game.Tick();
            var before = game.Screen;

            for (var i = 0; i < 12; i++)
            {
                game.Press(ConsoleKey.LeftArrow);
                Thread.Sleep(45);
                game.Tick();
            }

            Assert.NotEqual(before, game.Screen);
        }

        [Fact]
        public void ItKeepsMovingWhileNobodyTouchesAnything()
        {
            // Unlike the maze, which has no clock at all, and like Missile Command: the world advances on elapsed
            // time whether or not a key is pressed, so an enemy closes on a player who has gone to make tea.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Battlezone);

            Thread.Sleep(50);
            game.Tick();
            var before = game.Screen;

            for (var i = 0; i < 20; i++)
            {
                Thread.Sleep(45);
                game.Tick();
            }

            Assert.NotEqual(before, game.Screen);
        }

        [Fact]
        public void TheGearIsOnTheScreenBecauseItStaysWhereItIsPut()
        {
            // A throttle that holds its setting is exactly the kind of state a player fights when they cannot see
            // it — "why is my tank still moving" is not a question the screen should leave unanswered.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Battlezone);

            Assert.Contains("STOP", game.Screen, StringComparison.Ordinal);

            game.PressChar('w', ConsoleKey.W);
            Thread.Sleep(50);
            game.Tick();
            Assert.Contains("AHEAD", game.Screen, StringComparison.Ordinal);

            game.PressChar('s', ConsoleKey.S);
            game.PressChar('s', ConsoleKey.S);
            Thread.Sleep(50);
            game.Tick();
            Assert.Contains("ASTERN", game.Screen, StringComparison.Ordinal);

            game.PressChar('x', ConsoleKey.X);
            Thread.Sleep(50);
            game.Tick();
            Assert.Contains("STOP", game.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void SingleKeyPlayNeverReachesThePrompt()
        {
            // Steering is WASD and firing is SPACE, all printable - left at the default every one of them would
            // widen the echoed prompt at the bottom of the screen.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Battlezone);

            game.PressChar('w', ConsoleKey.W);
            game.PressChar('a', ConsoleKey.A);
            game.PressChar(' ', ConsoleKey.Spacebar);
            game.PressChar('d', ConsoleKey.D);

            Assert.Equal(string.Empty, game.App.InputManager.InputBuffer);
        }

        [Fact]
        public void EnterDoesNotAbandonAGameThatIsStillBeingPlayed()
        {
            // The binding the card tables settled on: a game with rounds must not close the cabinet on the key a
            // player hits by reflex. Mid-game it does nothing at all rather than restarting, since throwing away a
            // game in progress for a stray keystroke is the worst of the three things it could do.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Battlezone);

            game.Type(string.Empty);
            game.Type(string.Empty);
            game.Type(string.Empty);

            Assert.Contains("Score", game.Screen, StringComparison.Ordinal);
            Assert.DoesNotContain("Which game?", game.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void EscapeLeavesThePlain()
        {
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Battlezone);
            Assert.Contains("Score", game.Screen, StringComparison.Ordinal);

            game.Escape();

            Assert.Contains("Which game?", game.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void SwitchingTheViewDoesNotBreakAnything()
        {
            // On a host with no real pixels both sides of the switch are the character view, so this can only say
            // that the key is harmless - which is still worth saying, since TAB is bound in every screen here.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Battlezone);

            game.Press(ConsoleKey.Tab);
            Thread.Sleep(50);
            game.Tick();

            Assert.Contains("Score", game.Screen, StringComparison.Ordinal);
            Assert.Equal(string.Empty, game.App.InputManager.InputBuffer);
        }

        [Fact]
        public void TheStatusLineIsTheOnlyThingAboveTheView()
        {
            // A true-pixel payload row is recognised by its marker being character zero, so nothing may ever sit
            // beside the view - the status goes above it and the message below, exactly as in chess and Missile
            // Command. Asserted on the character view because that is what a test host draws, but the layout is the
            // same either way and it is the layout that has to be right.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Battlezone);

            var rows = Rows(game);
            var status = Array.FindIndex(rows, row => row.Contains("Score", StringComparison.Ordinal));
            var widest = 0;

            foreach (var row in rows)
                widest = Math.Max(widest, row.Length);

            // The view is the block of full-width rows — it always renders a complete rectangle, blanks included,
            // which is exactly the TextGrid guarantee it is built on.
            var firstViewRow = Array.FindIndex(rows, row => row.Length == widest);

            Assert.True(status >= 0, "no status line");
            Assert.True(firstViewRow > status, "the view is not below the status line");
        }
    }
}
