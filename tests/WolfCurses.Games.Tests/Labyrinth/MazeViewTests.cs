using System;
using System.Collections.Generic;
using WolfCurses.Core;
using WolfCurses.Games.Labyrinth;
using WolfCurses.Graphics;
using WolfCurses.Window.Control;
using Xunit;

namespace WolfCurses.Games.Tests.Labyrinth
{
    /// <summary>
    ///     Turning a maze into characters. Read off the grid rather than off a string wherever possible, since the
    ///     grid is where the mistakes are — a wrong colour is visible and a wrong wall is not.
    /// </summary>
    public class MazeViewTests
    {
        private const char Wall = '█';
        private const char Player = '@';
        private const char Exit = '>';
        private const char Start = '<';
        private const char Trail = '·';

        [Fact]
        public void TheGridIsTwiceTheMazePlusTheWallsAroundIt()
        {
            // A maze of W by H cells needs 2W+1 by 2H+1 characters, because the walls are as real as the corridors
            // and have to live somewhere. Drawing one character per cell is the mistake that produces a field of dots
            // with no way to tell which gaps can be walked through.
            var maze = new Maze(9, 5, new Randomizer(1));
            var grid = MazeView.CreateGrid(maze);

            Assert.Equal(19, grid.Width);
            Assert.Equal(11, grid.Height);
            Assert.Equal(2, grid.CellWidth);
        }

        [Fact]
        public void GroundTheTorchHasNotReachedIsNotDrawnAtAll()
        {
            var maze = new Maze(11, 7, new Randomizer(4));
            var grid = MazeView.CreateGrid(maze);
            MazeView.Paint(grid, maze);

            var painted = 0;
            for (var y = 0; y < grid.Height; y++)
            for (var x = 0; x < grid.Width; x++)
            {
                if (grid.GlyphAt(x, y) != ' ')
                    painted++;
            }

            // A handful of cells at most: the whole maze drawn would be hundreds. The exact number depends on where
            // the corridors happen to run, which is why this is a bound rather than an equality.
            Assert.InRange(painted, 1, grid.Width*grid.Height / 4);
        }

        [Fact]
        public void TheOutsideIsSolidStoneApartFromTheOneDoorway()
        {
            // The gap needs no special case in the painter: the exit cell's outward side is genuinely open, so the
            // same line that draws a corridor mouth draws the way out. What this pins is that there is exactly ONE
            // of them and it is where the rules say it is - a second gap would be a second way to win.
            var maze = new Maze(11, 7, new Randomizer(4));
            var grid = MazeView.CreateGrid(maze);
            maze.RevealAll();
            MazeView.Paint(grid, maze);

            var (dx, dy) = Offset(maze.ExitSide);
            var doorX = 2*maze.ExitX + 1 + dx;
            var doorY = 2*maze.ExitY + 1 + dy;

            var gaps = 0;
            foreach (var (x, y) in Border(grid))
            {
                if (grid.GlyphAt(x, y) == Wall)
                    continue;

                gaps++;
                Assert.Equal((doorX, doorY), (x, y));
            }

            Assert.Equal(1, gaps);
        }

        [Fact]
        public void EveryJointBetweenFourCellsStaysSolid()
        {
            // The corners are the bit that is easy to leave out, and leaving them out puts a hole at every crossroads
            // - which reads as a maze full of diagonal shortcuts that the rules do not allow.
            var maze = new Maze(11, 7, new Randomizer(6));
            var grid = MazeView.CreateGrid(maze);
            maze.RevealAll();
            MazeView.Paint(grid, maze);

            for (var y = 0; y < grid.Height; y += 2)
            for (var x = 0; x < grid.Width; x += 2)
                Assert.Equal(Wall, grid.GlyphAt(x, y));
        }

        [Fact]
        public void AWallIsStoneAndAWayThroughIsNot()
        {
            var maze = new Maze(11, 7, new Randomizer(2));
            var grid = MazeView.CreateGrid(maze);
            maze.RevealAll();
            MazeView.Paint(grid, maze);

            var openings = 0;
            for (var y = 0; y < maze.Height; y++)
            for (var x = 0; x < maze.Width; x++)
            {
                var gx = 2*x + 1;
                var gy = 2*y + 1;

                // Interior sides only, so the count does not depend on which wall the doorway landed against.
                if (x < maze.Width - 1)
                    AssertSide(grid, maze, x, y, DirectionEnum.Right, gx + 1, gy, ref openings);

                if (y < maze.Height - 1)
                    AssertSide(grid, maze, x, y, DirectionEnum.Down, gx, gy + 1, ref openings);
            }

            Assert.Equal(maze.Width*maze.Height - 1, openings);
        }

        [Fact]
        public void ACorridorRunningOffIntoTheDarkShowsAsAGapRatherThanAWall()
        {
            // Painted from whichever of the two cells a wall separates has been seen. Insisting on both would draw a
            // wall across every corridor the torch has not walked down yet, which tells the player the maze is a set
            // of sealed rooms.
            var maze = new Maze(15, 9, new Randomizer(11));
            var grid = MazeView.CreateGrid(maze);
            MazeView.Paint(grid, maze);

            // Every direction, not just one: the torch walks straight lines, so the corridors it leaves unlit are
            // overwhelmingly the side turnings off the ones it walked, and which way those face is up to the maze.
            var found = 0;
            for (var y = 0; y < maze.Height; y++)
            for (var x = 0; x < maze.Width; x++)
            {
                if (!maze.HasSeen(x, y))
                    continue;

                foreach (var (direction, dx, dy) in new[]
                         {
                             (DirectionEnum.Up, 0, -1), (DirectionEnum.Down, 0, 1),
                             (DirectionEnum.Left, -1, 0), (DirectionEnum.Right, 1, 0)
                         })
                {
                    if (!maze.IsOpen(x, y, direction) || maze.HasSeen(x + dx, y + dy))
                        continue;

                    found++;
                    Assert.Equal(' ', grid.GlyphAt(2*x + 1 + dx, 2*y + 1 + dy));
                }
            }

            Assert.True(found > 0, "no corridor ran off into the dark, so this proved nothing");
        }

        [Fact]
        public void EveryThingOnTheBoardHasItsOwnGlyphAndNotJustItsOwnColour()
        {
            // The lesson the chess text board and the missile field both record: under NO_COLOR, or a forced
            // grayscale, colour stops telling anything apart - and a board that leaned on it becomes unreadable while
            // looking perfectly fine to whoever wrote it.
            //
            // Walked four steps rather than one, because a single step fills only the start cell and the current one,
            // which are drawn as the start marker and the player - so the trail glyph would never appear and the
            // assertion about it would be checking that this test can walk.
            var maze = new Maze(11, 7, new Randomizer(8));
            WalkTowardTheExit(maze, 4);
            maze.RevealAll();

            var grid = MazeView.CreateGrid(maze);
            grid.ColorMode = AnsiColorModeEnum.None;
            MazeView.Paint(grid, maze);

            var plain = grid.Render();

            Assert.DoesNotContain('', plain);
            Assert.Contains(Wall, plain);
            Assert.Contains(Player, plain);
            Assert.Contains(Exit, plain);
            Assert.Contains(Start, plain);
            Assert.Contains(Trail, plain);
        }

        [Fact]
        public void APlayerWhoIsOutIsDrawnStandingInTheDoorway()
        {
            // The one place the cell-to-grid arithmetic does not work, and the reason the renderer has a special
            // case at all: once out, the player's position is off the maze entirely, so doubling it lands one past
            // the end of the grid and the glyph would simply be dropped. The doorway is a WALL square, one step
            // outward from the exit cell's centre - not the exit cell, and not the cell beyond it either.
            //
            // Swept over seeds rather than pinned to one, and BOTH orientations of doorway have to turn up, because
            // each of them can only catch half the mistake: a door in the top or bottom wall leaves the player's
            // column unchanged, so a column that ignored the doorway would still be right, and a door in a side wall
            // does the same for the row. The first version of this test pinned a single seed whose door happened to
            // be in a side wall, and a mutation that dropped the doorway from the column arithmetic sailed through.
            var sides = new HashSet<DirectionEnum>();

            for (var seed = 1; seed <= 12; seed++)
            {
                var maze = new Maze(9, 5, new Randomizer(seed));
                var grid = MazeView.CreateGrid(maze);

                WalkTowardTheExit(maze, maze.ShortestSteps);
                Assert.True(maze.IsSolved);
                Assert.False(maze.Contains(maze.PlayerX, maze.PlayerY));

                MazeView.Paint(grid, maze);
                sides.Add(maze.ExitSide);

                var (dx, dy) = Offset(maze.ExitSide);
                Assert.Equal(Player, grid.GlyphAt(2*maze.ExitX + 1 + dx, 2*maze.ExitY + 1 + dy));

                // And the naive position really would have been dropped, which is what makes the special case
                // earned rather than defensive.
                Assert.False(grid.Contains(2*maze.PlayerX + 1, 2*maze.PlayerY + 1));
            }

            Assert.Contains(sides, side => side == DirectionEnum.Left || side == DirectionEnum.Right);
            Assert.Contains(sides, side => side == DirectionEnum.Up || side == DirectionEnum.Down);
        }

        [Fact]
        public void StandingOnTheExitCellStillDrawsThePlayerRatherThanTheDoor()
        {
            // Painted last on purpose. Losing the player under the thing they were walking toward, one step before
            // they get out, is a small bug that reads as the game having crashed.
            var maze = new Maze(9, 5, new Randomizer(3));
            var grid = MazeView.CreateGrid(maze);

            WalkTowardTheExit(maze, maze.ShortestSteps - 1);
            Assert.False(maze.IsSolved);
            Assert.Equal((maze.ExitX, maze.ExitY), (maze.PlayerX, maze.PlayerY));

            MazeView.Paint(grid, maze);
            Assert.Equal(Player, grid.GlyphAt(2*maze.ExitX + 1, 2*maze.ExitY + 1));
        }

        [Fact]
        public void PaintingTwiceLeavesNoTrailOfWhereThePlayerUsedToBe()
        {
            // The same trap the sprite scene has with its background: the grid is reused between frames, so a paint
            // that did not clear first would leave a line of players behind.
            var maze = new Maze(11, 7, new Randomizer(5));
            var grid = MazeView.CreateGrid(maze);

            // Stepped off the start first: the cell the player began in carries its own marker, so checking THAT one
            // for a trail would be asking the wrong question about the right bug.
            WalkTowardTheExit(maze, 1);
            MazeView.Paint(grid, maze);
            var wasX = 2*maze.PlayerX + 1;
            var wasY = 2*maze.PlayerY + 1;

            WalkTowardTheExit(maze, 1);
            MazeView.Paint(grid, maze);

            Assert.Equal(Trail, grid.GlyphAt(wasX, wasY));
            Assert.Equal(Player, grid.GlyphAt(2*maze.PlayerX + 1, 2*maze.PlayerY + 1));
        }

        [Fact]
        public void PaintingADifferentMazeIntoTheSameGridLeavesNothingOfTheOldOne()
        {
            // The test the repaint's clear actually needs, and it exists because the obvious one does not reach it:
            // deleting the clear survived a first round of mutation testing, since within one maze nothing is ever
            // un-seen and every painted cell is simply painted again. The grid belongs to the caller, so the real
            // hazard is the same buffer coming back holding a DIFFERENT maze - which is what starting a new one does.
            //
            // Asserted by comparing against a fresh grid rather than by counting lit cells, so the assertion is exact
            // and does not depend on how far the torch happened to reach in whichever maze the seed produced.
            var first = new Maze(11, 7, new Randomizer(1));
            first.RevealAll();

            var second = new Maze(11, 7, new Randomizer(2));

            var reused = MazeView.CreateGrid(first);
            MazeView.Paint(reused, first);

            var firstAlone = reused.Render();
            MazeView.Paint(reused, second);

            var fresh = MazeView.CreateGrid(second);
            MazeView.Paint(fresh, second);

            Assert.NotEqual(firstAlone, fresh.Render());
            Assert.Equal(fresh.Render(), reused.Render());
        }

        [Fact]
        public void NullsAreRefusedRatherThanDrawnAsAnEmptyMaze()
        {
            var maze = new Maze(5, 5, new Randomizer(1));

            Assert.Throws<ArgumentNullException>(() => MazeView.CreateGrid(null));
            Assert.Throws<ArgumentNullException>(() => MazeView.Paint(null, maze));
            Assert.Throws<ArgumentNullException>(() => MazeView.Paint(new TextGrid(3, 3), null));
        }

        // ------------------------------------------------------------ helpers

        /// <summary>Every cell around the outside of the grid, each visited once.</summary>
        private static IEnumerable<(int X, int Y)> Border(TextGrid grid)
        {
            for (var x = 0; x < grid.Width; x++)
            {
                yield return (x, 0);
                yield return (x, grid.Height - 1);
            }

            for (var y = 1; y < grid.Height - 1; y++)
            {
                yield return (0, y);
                yield return (grid.Width - 1, y);
            }
        }

        /// <summary>One step in a direction.</summary>
        private static (int X, int Y) Offset(DirectionEnum direction)
        {
            return direction switch
            {
                DirectionEnum.Up => (0, -1),
                DirectionEnum.Down => (0, 1),
                DirectionEnum.Left => (-1, 0),
                _ => (1, 0)
            };
        }

        private static void AssertSide(TextGrid grid, Maze maze, int x, int y, DirectionEnum side,
            int gx, int gy, ref int openings)
        {
            if (maze.IsOpen(x, y, side))
            {
                Assert.Equal(' ', grid.GlyphAt(gx, gy));
                openings++;
            }
            else
            {
                Assert.Equal(Wall, grid.GlyphAt(gx, gy));
            }
        }

        /// <summary>Walks a few steps of the shortest way out, so the trail is a line of distinct cells.</summary>
        private static void WalkTowardTheExit(Maze maze, int steps)
        {
            for (var step = 0; step < steps && !maze.IsSolved; step++)
            {
                var moved = false;
                foreach (var direction in new[]
                         {DirectionEnum.Up, DirectionEnum.Down, DirectionEnum.Left, DirectionEnum.Right})
                {
                    if (!Toward(maze, direction) || !maze.TryMove(direction))
                        continue;

                    moved = true;
                    break;
                }

                Assert.True(moved, "could not walk toward the exit");
            }
        }

        /// <summary>Whether a move takes the player nearer the exit, measured through the maze rather than across it.</summary>
        private static bool Toward(Maze maze, DirectionEnum direction)
        {
            var (dx, dy) = direction switch
            {
                DirectionEnum.Up => (0, -1),
                DirectionEnum.Down => (0, 1),
                DirectionEnum.Left => (-1, 0),
                _ => (1, 0)
            };

            // The step out is the one move that does not reduce the distance to the exit cell - it leaves the maze
            // instead - so it has to be recognised on its own terms or a walker following the gradient stops on the
            // threshold forever.
            if (!maze.Contains(maze.PlayerX + dx, maze.PlayerY + dy))
                return maze.IsOpen(maze.PlayerX, maze.PlayerY, direction);

            return Distance(maze, maze.PlayerX + dx, maze.PlayerY + dy) <
                   Distance(maze, maze.PlayerX, maze.PlayerY);
        }

        private static int Distance(Maze maze, int fromX, int fromY)
        {
            if (!maze.Contains(fromX, fromY))
                return int.MaxValue;

            var distance = new int[maze.Width*maze.Height];
            Array.Fill(distance, -1);

            var queue = new System.Collections.Generic.Queue<(int X, int Y)>();
            distance[fromY*maze.Width + fromX] = 0;
            queue.Enqueue((fromX, fromY));

            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                if (x == maze.ExitX && y == maze.ExitY)
                    return distance[y*maze.Width + x];

                foreach (var (direction, dx, dy) in new[]
                         {
                             (DirectionEnum.Up, 0, -1), (DirectionEnum.Down, 0, 1),
                             (DirectionEnum.Left, -1, 0), (DirectionEnum.Right, 1, 0)
                         })
                {
                    if (!maze.IsOpen(x, y, direction) || !maze.Contains(x + dx, y + dy))
                        continue;

                    var index = (y + dy)*maze.Width + x + dx;
                    if (distance[index] >= 0)
                        continue;

                    distance[index] = distance[y*maze.Width + x] + 1;
                    queue.Enqueue((x + dx, y + dy));
                }
            }

            return int.MaxValue;
        }
    }
}
