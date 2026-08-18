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
        private static readonly MinesweeperFace _face = new(9, 9);

        [Fact]
        public void TheMapTurnsACellBackIntoASquare()
        {
            var map = new MinesweeperBoardMap(5, 2, 9, 9, 3);

            Assert.True(map.TryToSquare(5, 2, out var x, out var y));
            Assert.Equal((0, 0), (x, y));

            Assert.True(map.TryToSquare(13, 2 + 8*3, out x, out y));
            Assert.Equal((8, 8), (x, y));
        }

        [Fact]
        public void EveryColumnOfASquareIsTheSameSquare()
        {
            // A square is three columns wide, so all three have to answer the same or a player clicking its left
            // edge opens a different one from a player clicking its middle.
            var map = new MinesweeperBoardMap(5, 2, 9, 9, 3);

            for (var offset = 0; offset < 3; offset++)
            {
                Assert.True(map.TryToSquare(5, 2 + 3*4 + offset, out var x, out var y));
                Assert.Equal((4, 0), (x, y));
            }
        }

        [Fact]
        public void AClickOffTheFieldIsRefusedRatherThanRoundedOntoIt()
        {
            // Refusing matters more than it sounds: the chrome is where the counters and the face live, and a click
            // there that got rounded onto the nearest square would open a corner every time somebody reached for
            // the frame.
            var map = new MinesweeperBoardMap(5, 2, 9, 9, 3);

            Assert.False(map.TryToSquare(4, 10, out _, out _));
            Assert.False(map.TryToSquare(14, 10, out _, out _));
            Assert.False(map.TryToSquare(7, 1, out _, out _));
            Assert.False(map.TryToSquare(7, 2 + 9*3, out _, out _));
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

            var row = FirstBoardRow(game.Screen);
            Assert.True(row >= 0, "no board on screen:\n" + game.Describe());

            var before = HiddenCount(game.Screen);
            game.App.InputManager.SendMousePress(
                new MouseEvent(_face.BoardOriginColumn, row, MouseButtonEnum.Left));
            game.App.PumpInput();

            Assert.True(HiddenCount(game.Screen) < before,
                "clicking the top-left square opened nothing:\n" + game.Describe());
        }

        [Fact]
        public void ARightClickPlantsAFlagRatherThanOpeningTheSquare()
        {
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Minesweeper);

            var row = FirstBoardRow(game.Screen);
            var before = HiddenCount(game.Screen);

            game.App.InputManager.SendMousePress(
                new MouseEvent(_face.BoardOriginColumn, row, MouseButtonEnum.Right));
            game.App.PumpInput();

            Assert.Contains('¶', game.Screen);
            Assert.Equal(before, HiddenCount(game.Screen));
        }

        [Fact]
        public void AClickOnTheChromeDoesNothingAtAll()
        {
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Minesweeper);

            var before = game.Screen;

            // The panel's own raised edge, which is not a square and must not be treated as the nearest one.
            game.App.InputManager.SendMousePress(new MouseEvent(0, FirstBoardRow(before), MouseButtonEnum.Left));
            game.App.PumpInput();

            Assert.Equal(HiddenCount(before), HiddenCount(game.Screen));
        }

        [Fact]
        public void ClickingTheFaceDealsANewBoard()
        {
            // It always did, and it is where a player's hand already is when a board ends.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Minesweeper);

            game.Type("e5");
            var opened = HiddenCount(game.Screen);
            Assert.True(opened < 81, "nothing was opened, so a fresh board proves nothing");

            var row = FirstBoardRow(game.Screen) - _face.BoardOriginRow + _face.SmileyRow;
            game.App.InputManager.SendMousePress(
                new MouseEvent(_face.SmileyOriginColumn, row, MouseButtonEnum.Left));
            game.App.PumpInput();

            Assert.Equal(81, HiddenCount(game.Screen));
        }

        [Fact]
        public void TypingStillWorksWithTheMouseSittingThere()
        {
            // The mouse is an addition and not a replacement: this is still the one game in the arcade driven by
            // typing, and adding a pointer must not have quietly taken that away.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Minesweeper);

            var before = HiddenCount(game.Screen);
            game.Type("e5");

            Assert.True(HiddenCount(game.Screen) < before, "typing a square opened nothing:\n" + game.Describe());
        }

        /// <summary>Which screen row the top row of squares is drawn on, found by the row number down the side.</summary>
        private static int FirstBoardRow(string screen)
        {
            var rows = screen.Replace("\r\n", "\n").Split('\n');

            for (var i = 0; i < rows.Length; i++)
            {
                if (rows[i].Length > _face.BoardOriginColumn && rows[i][0] == '▌' &&
                    char.IsDigit(rows[i][_face.BoardOriginColumn - 1]))
                    return i;
            }

            return -1;
        }

        /// <summary>How many squares are still face down, counted off the raised bevel each one carries.</summary>
        private static int HiddenCount(string screen)
        {
            var hidden = 0;

            foreach (var row in BoardRows(screen))
            {
                for (var x = 0; x < 9; x++)
                {
                    var at = _face.BoardOriginColumn + x*MinesweeperFace.TileWidth;
                    if (at < row.Length && row[at] == '▌')
                        hidden++;
                }
            }

            return hidden;
        }

        private static IEnumerable<string> BoardRows(string screen)
        {
            foreach (var row in screen.Replace("\r\n", "\n").Split('\n'))
            {
                if (row.Length > _face.BoardOriginColumn && row[0] == '▌' &&
                    char.IsDigit(row[_face.BoardOriginColumn - 1]))
                    yield return row;
            }
        }
    }
}
