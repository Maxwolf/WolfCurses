using System;
using System.Threading;
using WolfCurses.Games.Tests.Support;
using Xunit;

namespace WolfCurses.Games.Tests.MissileCommand
{
    /// <summary>
    ///     Clicking to fire, driven through the same public door a real console reader uses.
    ///     <para>
    ///         A test host has no mouse and never will, so nothing here pretends to test whether the terminal
    ///         reports one. What it tests is everything <i>after</i> that: that a press reaches the form at all, that
    ///         the cell it names turns into the world position it was drawn at, and that a shell actually leaves a
    ///         battery. <c>InputManager.SendMousePress</c> is public precisely so a host — or a test — can feed
    ///         presses that did not come from a console.
    ///     </para>
    /// </summary>
    [Collection("GamesApp")]
    public class MissileMouseTests
    {
        /// <summary>
        ///     Where the board is drawn in the frame. Derived here independently of the game's own counting, so the
        ///     two have to agree: the library contributes exactly one un-terminated line above the form body, and the
        ///     form's own Compose opens with a blank line, a status line and another blank.
        /// </summary>
        private const int BoardOriginRow = 3;

        [Fact]
        public void TheBoardIsDrawnWhereTheClickMathThinksItIs()
        {
            // The single most valuable assertion in this file. Everything else could be right and every shot would
            // still land in the wrong place if this number were off by one - a row is about 5.5% of the field, most
            // of a blast radius, which reads as being bad at the game rather than as a bug.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.MissileCommand);

            var lines = game.Screen.Replace("\r\n", "\n").Split('\n');

            Assert.Contains("Wave 1", lines[1], StringComparison.Ordinal);
            Assert.Equal(string.Empty, lines[2].Trim());
            Assert.True(lines[BoardOriginRow].Length > 0, "the board's first row is empty");
        }

        [Fact]
        public void AClickOnTheBoardSpendsAShell()
        {
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.MissileCommand);
            Assert.Contains("Ammo 10/10/10", game.Screen, StringComparison.Ordinal);

            // Well inside the board, and above the aim floor.
            game.App.InputManager.SendMousePress(new MouseEvent(30, BoardOriginRow + 3, MouseButtonEnum.Left));
            game.App.PumpInput();
            Thread.Sleep(45);
            game.Tick();

            Assert.DoesNotContain("Ammo 10/10/10", game.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void AClickAboveTheBoardIsIgnoredRatherThanFiredAtTheTopOfTheSky()
        {
            // Dropped, not clamped. Clamping would turn a click on the status line into a shot, which is a worse
            // answer than doing nothing at all.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.MissileCommand);

            game.App.InputManager.SendMousePress(new MouseEvent(30, 1, MouseButtonEnum.Left));
            game.App.PumpInput();
            Thread.Sleep(45);
            game.Tick();

            Assert.Contains("Ammo 10/10/10", game.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void ARightClickDoesNotFire()
        {
            // Right-click is paste in a console host's own quick-edit mode and in several terminals' passthrough, so
            // binding it to anything is asking for accidental shots.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.MissileCommand);

            game.App.InputManager.SendMousePress(new MouseEvent(30, BoardOriginRow + 3, MouseButtonEnum.Right));
            game.App.PumpInput();
            Thread.Sleep(45);
            game.Tick();

            Assert.Contains("Ammo 10/10/10", game.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void AClickMovesTheCrosshairToWhereItWasClicked()
        {
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.MissileCommand);

            // Far from the opening position, which is the middle of the field.
            const int column = 8;
            game.App.InputManager.SendMousePress(new MouseEvent(column, BoardOriginRow + 4, MouseButtonEnum.Left));
            game.App.PumpInput();
            Thread.Sleep(45);
            game.Tick();

            var crosshair = CrosshairColumn(game);
            Assert.True(Math.Abs(crosshair - column) <= 1,
                $"clicked column {column} but the crosshair is at {crosshair}");
        }

        [Fact]
        public void AClickTakesTheCrosshairOffWhateverTheKeyboardWasDoing()
        {
            // The arbitration rule, and it is the whole of it: a click zeroes the drift, nothing revives drift
            // without a fresh key press, so the pointer wins until an arrow is touched again.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.MissileCommand);

            game.Press(ConsoleKey.RightArrow);
            Thread.Sleep(45);
            game.Tick();

            game.App.InputManager.SendMousePress(new MouseEvent(10, BoardOriginRow + 4, MouseButtonEnum.Left));
            game.App.PumpInput();
            Thread.Sleep(45);
            game.Tick();

            var settled = CrosshairColumn(game);

            // Several frames with no input at all. If the click had left the keyboard drift running, the crosshair
            // would keep sliding right for up to the 180 ms the game waits before inferring a key-up.
            Thread.Sleep(250);
            game.Tick();
            game.Tick();

            Assert.Equal(settled, CrosshairColumn(game));
        }

        [Fact]
        public void TheStatusLineReportsWhetherTheMouseIsAvailableAtAll()
        {
            // The diagnostic that exists because no test can establish whether a real terminal reports a click.
            // Headless, EnableMouse was never called and cannot succeed, so it must say so rather than promising
            // something the player will then find does not work.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.MissileCommand);

            Assert.Contains("Mouse off", game.Screen, StringComparison.Ordinal);
            Assert.DoesNotContain("Move the mouse", game.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void MovingThePointerAimsAndSpendsNothing()
        {
            // The whole reason motion was worth adopting here, and the thing a press can never do: a click puts the
            // sight where a shell is already going, so aiming by clicking spends the very ammunition the game is
            // about. Moving the pointer aims for free, which is what the cabinet's trackball did.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.MissileCommand);

            // Far from the opening position, which is the middle of the field.
            const int column = 8;
            game.App.InputManager.SendMouseEvent(new MouseEvent(column, BoardOriginRow + 4, MouseButtonEnum.None,
                kind: MouseEventKindEnum.Move));
            game.App.PumpInput();
            Thread.Sleep(45);
            game.Tick();

            var crosshair = CrosshairColumn(game);
            Assert.True(Math.Abs(crosshair - column) <= 1,
                $"pointer moved to column {column} but the crosshair is at {crosshair}");

            // Absolute, not "fewer than before": a hover that quietly fired would still move the crosshair.
            Assert.Contains("Ammo 10/10/10", game.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void SweepingWithTheButtonHeldKeepsFiring()
        {
            // A move carrying a button is a drag, and a drag is the other thing presses cannot express however many
            // of them arrive. The shot pace already rate-limits it, so a sweep lays down a barrage at the cadence a
            // held SPACE does rather than one shell for every cell the pointer crosses.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.MissileCommand);
            Assert.Contains("Ammo 10/10/10", game.Screen, StringComparison.Ordinal);

            game.App.InputManager.SendMouseEvent(new MouseEvent(30, BoardOriginRow + 3, MouseButtonEnum.Left,
                kind: MouseEventKindEnum.Move));
            game.App.PumpInput();
            Thread.Sleep(45);
            game.Tick();

            Assert.DoesNotContain("Ammo 10/10/10", game.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void ThePointerTakesTheCrosshairOffWhateverTheKeyboardWasDoing()
        {
            // The same arbitration a click has, asked of a bare hover: the pointer zeroes the drift, and nothing
            // revives drift without a fresh key press. Worth its own test because a hover reaches the form by a
            // different door from a press.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.MissileCommand);

            game.Press(ConsoleKey.RightArrow);
            Thread.Sleep(45);
            game.Tick();

            game.App.InputManager.SendMouseEvent(new MouseEvent(10, BoardOriginRow + 4, MouseButtonEnum.None,
                kind: MouseEventKindEnum.Move));
            game.App.PumpInput();
            Thread.Sleep(45);
            game.Tick();

            var settled = CrosshairColumn(game);

            // Several frames with no input at all. If the hover had left the keyboard drift running, the crosshair
            // would keep sliding right for up to the 180 ms the game waits before inferring a key-up.
            Thread.Sleep(250);
            game.Tick();
            game.Tick();

            Assert.Equal(settled, CrosshairColumn(game));
        }

        [Fact]
        public void TheScreenAsksForPointerReportingAndHandsItBackWhenItCloses()
        {
            // Motion is one event for every cell the pointer crosses, so it is asked for by the screen that wants
            // it rather than switched on for the whole arcade. The handing back is the half that is easy to leave
            // out and impossible to see: nothing would look wrong, the menu and every later game would simply be
            // paying for a flood none of them read.
            using var game = new DrivenGamesApp();
            Assert.False(game.App.InputManager.ReportsMouseMotion);

            game.ChooseMenuItem((int) GamesCommandsEnum.MissileCommand);
            Assert.True(game.App.InputManager.ReportsMouseMotion);

            game.Escape();
            Assert.False(game.App.InputManager.ReportsMouseMotion);
        }

        /// <summary>Where the crosshair is on screen, read off the frame rather than out of the form.</summary>
        private static int CrosshairColumn(DrivenGamesApp game)
        {
            foreach (var line in game.Screen.Split('\n'))
            {
                var column = line.IndexOf('+', StringComparison.Ordinal);
                if (column >= 0)
                    return column;
            }

            return -1;
        }
    }
}
