using System;
using System.Collections.Generic;
using WolfCurses;
using WolfCurses.Games.Minesweeper;
using WolfCurses.Games.Tests.Support;
using Xunit;

namespace WolfCurses.Games.Tests.Minesweeper
{
    /// <summary>
    ///     Clicking squares, driven through the same public door a real console reader uses.
    /// </summary>
    [Collection("GamesApp")]
    public class MinesweeperMouseTests
    {
        [Fact]
        public void TheMapTurnsACellBackIntoASquare()
        {
            // Origin is the field's own top-left CORNER, so the first square's interior starts one row down and one
            // column right of it - the line between two squares belongs to both, and the map hands it to the one
            // above and left.
            var map = new MinesweeperBoardMap(5, 2, 9, 9, 4, 2);

            Assert.True(map.TryToSquare(6, 3, out var x, out var y));
            Assert.Equal((0, 0), (x, y));

            Assert.True(map.TryToSquare(5 + 8*2 + 1, 2 + 8*4 + 1, out x, out y));
            Assert.Equal((8, 8), (x, y));
        }

        [Fact]
        public void EveryColumnOfASquareIsTheSameSquare()
        {
            // A square is three columns of interior plus the line down its right, and every one of those four has to
            // answer the same square or clicking a box on its edge opens its neighbour.
            var map = new MinesweeperBoardMap(5, 2, 9, 9, 4, 2);

            for (var offset = 0; offset < 4; offset++)
            {
                Assert.True(map.TryToSquare(6, 2 + 4*4 + 1 + offset, out var x, out var y));
                Assert.Equal((4, 0), (x, y));
            }
        }

        [Fact]
        public void AClickOffTheFieldIsRefusedRatherThanRoundedOntoIt()
        {
            // Refusing matters more than it sounds: the chrome is where the counters and the face live, and a click
            // there that got rounded onto the nearest square would open a corner every time somebody reached for
            // the frame.
            var map = new MinesweeperBoardMap(5, 2, 9, 9, 4, 2);

            Assert.False(map.TryToSquare(4, 10, out _, out _));
            Assert.False(map.TryToSquare(5 + 9*2 + 1, 10, out _, out _));
            Assert.False(map.TryToSquare(7, 1, out _, out _));
            Assert.False(map.TryToSquare(7, 2 + 9*4 + 1, out _, out _));
        }

        [Fact]
        public void AMapWithNoBoardAnswersNothing()
        {
            var map = default(MinesweeperBoardMap);

            Assert.False(map.IsUsable);
            Assert.False(map.TryToSquare(0, 0, out _, out _));
        }

        [Fact]
        public void TheBoardIsDrawnWhereTheClickMathThinksItIs()
        {
            // The one that would catch the whole thing being a row out. Rather than trusting a constant, this finds
            // the top-left square ON SCREEN and clicks exactly there, then asks whether that square opened.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Minesweeper);

            var row = MinesweeperScreen.OriginRow(game.Screen);
            Assert.True(row >= 0, "no board on screen:\n" + game.Describe());

            var before = MinesweeperScreen.Hidden(game.Screen);
            game.App.InputManager.SendMousePress(
                new MouseEvent(MinesweeperScreen.OriginColumn(game.Screen), row, MouseButtonEnum.Left));
            game.App.PumpInput();

            Assert.True(MinesweeperScreen.Hidden(game.Screen) < before,
                "clicking the top-left square opened nothing:\n" + game.Describe());
        }

        [Fact]
        public void ARightClickPlantsAFlagRatherThanOpeningTheSquare()
        {
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Minesweeper);

            var row = MinesweeperScreen.OriginRow(game.Screen);
            var before = MinesweeperScreen.Hidden(game.Screen);

            game.App.InputManager.SendMousePress(
                new MouseEvent(MinesweeperScreen.OriginColumn(game.Screen), row, MouseButtonEnum.Right));
            game.App.PumpInput();

            Assert.Contains('¶', game.Screen);
            Assert.Equal(before, MinesweeperScreen.Hidden(game.Screen));
        }

        [Fact]
        public void AClickOnTheChromeDoesNothingAtAll()
        {
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Minesweeper);

            var before = game.Screen;

            // The panel's own raised edge, which is not a square and must not be treated as the nearest one.
            game.App.InputManager.SendMousePress(new MouseEvent(0, MinesweeperScreen.OriginRow(before), MouseButtonEnum.Left));
            game.App.PumpInput();

            Assert.Equal(MinesweeperScreen.Hidden(before), MinesweeperScreen.Hidden(game.Screen));
        }

        [Fact]
        public void ClickingTheFaceDealsANewBoard()
        {
            // It always did, and it is where a player's hand already is when a board ends.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Minesweeper);

            var full = MinesweeperScreen.Hidden(game.Screen);

            game.Type("e5");
            Assert.True(MinesweeperScreen.Hidden(game.Screen) < full,
                "nothing was opened, so a fresh board proves nothing");

            // The face, found on screen rather than computed: the board size is chosen from the terminal, so a test
            // that worked out where the smiley "should" be would be right on one machine and wrong on the next.
            var (column, row) = MinesweeperScreen.Smiley(game.Screen);
            Assert.True(row >= 0, "no face on screen:\n" + game.Describe());

            game.App.InputManager.SendMousePress(new MouseEvent(column, row, MouseButtonEnum.Left));
            game.App.PumpInput();

            Assert.Equal(full, MinesweeperScreen.Hidden(game.Screen));
        }

        [Fact]
        public void TypingStillWorksWithTheMouseSittingThere()
        {
            // The mouse is an addition and not a replacement: this is still the one game in the arcade driven by
            // typing, and adding a pointer must not have quietly taken that away.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Minesweeper);

            var before = MinesweeperScreen.Hidden(game.Screen);
            game.Type("e5");

            Assert.True(MinesweeperScreen.Hidden(game.Screen) < before, "typing a square opened nothing:\n" + game.Describe());
        }

    }
}
