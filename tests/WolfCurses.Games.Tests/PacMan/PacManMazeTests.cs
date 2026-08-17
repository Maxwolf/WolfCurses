using System.Collections.Generic;
using System.Text;
using WolfCurses.Games.PacMan;
using Xunit;

namespace WolfCurses.Games.Tests.PacMan
{
    /// <summary>
    ///     The board, which is hand-drawn and therefore the one part of this game that cannot be reasoned about — it
    ///     has to be checked.
    ///     <para>
    ///         Everything here is a property of the <i>design</i> rather than of the code that reads it: that the maze
    ///         is symmetrical, that every pellet can be reached, that no wall is two cells thick, that the tunnel goes
    ///         somewhere and that the ghosts can get out of their house. Each of these is a mistake a person makes
    ///         while drawing forty columns of wall by eye, and each of them is invisible until you play.
    ///     </para>
    /// </summary>
    public class PacManMazeTests
    {
        [Fact]
        public void TheBoardIsOddWidthSoItsHalvesShareACentreColumn()
        {
            var maze = new PacManMaze();

            Assert.Equal(1, maze.Width % 2);
            Assert.True(maze.Width >= 30, $"the board is only {maze.Width} columns wide");
            Assert.True(maze.Height >= 15, $"the board is only {maze.Height} rows tall");
        }

        [Fact]
        public void TheBoardIsPerfectlySymmetrical()
        {
            // Free, because the map is authored as one half - so this is really a test that the mirroring is right,
            // and it would catch a centre column that got doubled instead of shared.
            var maze = new PacManMaze();

            for (var y = 0; y < maze.Height; y++)
            for (var x = 0; x < maze.Width; x++)
                Assert.Equal(maze.TileAt(x, y), maze.TileAt(maze.Width - 1 - x, y));
        }

        [Fact]
        public void NoWallIsTwoCellsThick()
        {
            // THE drawing rule. Walls are rendered as connected lines, and a line renderer handed a 2x2 block of wall
            // draws crossings through the middle of it - so a solid-looking lump in the map comes out as a lattice.
            // This is the one maze mistake that looks like a rendering bug rather than a design one.
            var maze = new PacManMaze();

            for (var y = 0; y < maze.Height - 1; y++)
            for (var x = 0; x < maze.Width - 1; x++)
            {
                var block = maze.IsWall(x, y) && maze.IsWall(x + 1, y) &&
                            maze.IsWall(x, y + 1) && maze.IsWall(x + 1, y + 1);

                Assert.False(block, $"there is a 2x2 block of wall at {x},{y}:\n{Describe(maze)}");
            }
        }

        [Fact]
        public void EveryPelletCanBeReachedFromWhereThePlayerStarts()
        {
            // A pellet behind a wall is a board that can never be finished, and nobody discovers that until they have
            // eaten all the others.
            var maze = new PacManMaze();
            var reached = Reachable(maze);

            for (var y = 0; y < maze.Height; y++)
            for (var x = 0; x < maze.Width; x++)
            {
                if (maze.HasPellet(x, y))
                    Assert.True(reached.Contains((x, y)), $"the pellet at {x},{y} is walled off:\n{Describe(maze)}");
            }
        }

        [Fact]
        public void EveryOpenSquareOutsideTheHouseCanBeReached()
        {
            // Stronger than the pellet check and worth having separately: an unreachable empty corridor is not a bug
            // the player can see, but it is a place a ghost sent home could get stuck in forever.
            var maze = new PacManMaze();
            var reached = Reachable(maze);

            for (var y = 0; y < maze.Height; y++)
            for (var x = 0; x < maze.Width; x++)
            {
                if (!maze.IsOpen(x, y) || maze.IsDoor(x, y) || maze.IsInsideHouse(x, y))
                    continue;

                Assert.True(reached.Contains((x, y)), $"the floor at {x},{y} is cut off:\n{Describe(maze)}");
            }
        }

        [Fact]
        public void ThereAreFourPowerPelletsAndTheyAreInTheCorners()
        {
            var maze = new PacManMaze();
            var found = new List<(int X, int Y)>();

            for (var y = 0; y < maze.Height; y++)
            for (var x = 0; x < maze.Width; x++)
            {
                if (maze.HasPowerPellet(x, y))
                    found.Add((x, y));
            }

            Assert.Equal(4, found.Count);

            foreach (var (x, y) in found)
            {
                var nearASide = x <= 2 || x >= maze.Width - 3;
                var nearTopOrBottom = y <= 2 || y >= maze.Height - 3;
                Assert.True(nearASide && nearTopOrBottom, $"the power pellet at {x},{y} is not in a corner");
            }
        }

        [Fact]
        public void TheTunnelRunsOffBothEndsOfTheBoardAndComesBack()
        {
            var maze = new PacManMaze();

            Assert.True(maze.IsOpen(0, maze.TunnelRow), "the left end of the tunnel is walled up");
            Assert.True(maze.IsOpen(maze.Width - 1, maze.TunnelRow), "the right end of the tunnel is walled up");

            Assert.Equal(maze.Width - 1, maze.WrapX(-1));
            Assert.Equal(0, maze.WrapX(maze.Width));
            Assert.Equal(5, maze.WrapX(5));
        }

        [Fact]
        public void TheTunnelIsTheOnlyHoleInTheOutsideWall()
        {
            // Otherwise walking off the board somewhere unexpected teleports the player across it, which reads as the
            // game having glitched rather than as a feature.
            var maze = new PacManMaze();

            for (var y = 0; y < maze.Height; y++)
            {
                if (y == maze.TunnelRow)
                    continue;

                Assert.True(maze.IsWall(0, y), $"row {y} is open at the left edge");
                Assert.True(maze.IsWall(maze.Width - 1, y), $"row {y} is open at the right edge");
            }

            for (var x = 0; x < maze.Width; x++)
            {
                Assert.True(maze.IsWall(x, 0), $"column {x} is open at the top");
                Assert.True(maze.IsWall(x, maze.Height - 1), $"column {x} is open at the bottom");
            }
        }

        [Fact]
        public void TheGhostHouseIsSealedApartFromItsDoor()
        {
            // The house is found by flooding out from under the door rather than declared as a rectangle, so this is
            // really a test that the map draws a closed pocket - a gap anywhere in it and the flood escapes, marking
            // most of the board as "house" and letting every ghost treat every corridor as its own doorway.
            var maze = new PacManMaze();

            Assert.True(maze.IsInsideHouse(maze.HouseX, maze.HouseY));
            Assert.False(maze.IsInsideHouse(maze.DoorX, maze.DoorY), "the door counts itself as inside the house");

            var inside = 0;
            for (var y = 0; y < maze.Height; y++)
            for (var x = 0; x < maze.Width; x++)
            {
                if (maze.IsInsideHouse(x, y))
                    inside++;
            }

            Assert.InRange(inside, 3, 12);
        }

        [Fact]
        public void TheGhostsStartInTheHouseAndThePlayerDoesNot()
        {
            var maze = new PacManMaze();

            Assert.True(maze.GhostStarts.Count >= 3, "there is not room in the house for the ghosts");
            foreach (var (x, y) in maze.GhostStarts)
                Assert.True(maze.IsInsideHouse(x, y), $"a ghost starts at {x},{y}, which is not in the house");

            var (px, py) = maze.PacManStart;
            Assert.True(maze.IsOpen(px, py));
            Assert.False(maze.IsInsideHouse(px, py), "the player starts inside the ghost house");
        }

        [Fact]
        public void ThePlayerMayNotUseTheDoorAndTheGhostsMay()
        {
            var maze = new PacManMaze();

            Assert.False(maze.CanEnter(maze.DoorX, maze.DoorY, false));
            Assert.True(maze.CanEnter(maze.DoorX, maze.DoorY, true));
        }

        [Fact]
        public void EatingTakesThePelletAndSaysWhatItWas()
        {
            var maze = new PacManMaze();
            var before = maze.PelletsLeft;

            var (px, py) = FindPellet(maze, false);
            Assert.Equal(PelletEnum.Pellet, maze.Eat(px, py));
            Assert.Equal(PelletEnum.None, maze.Eat(px, py));

            var (qx, qy) = FindPellet(maze, true);
            Assert.Equal(PelletEnum.Power, maze.Eat(qx, qy));

            Assert.Equal(before - 2, maze.PelletsLeft);
        }

        [Fact]
        public void RefillingPutsEveryPelletBack()
        {
            var maze = new PacManMaze();
            var total = maze.TotalPellets;

            for (var y = 0; y < maze.Height; y++)
            for (var x = 0; x < maze.Width; x++)
                maze.Eat(x, y);

            Assert.Equal(0, maze.PelletsLeft);

            maze.Refill();

            Assert.Equal(total, maze.PelletsLeft);
        }

        [Fact]
        public void ThereIsEnoughFoodOnTheBoardToBeWorthEating()
        {
            var maze = new PacManMaze();

            Assert.InRange(maze.TotalPellets, 120, 500);
        }

        // ------------------------------------------------------------ helpers

        /// <summary>Every square the player can walk to from where they start.</summary>
        private static HashSet<(int X, int Y)> Reachable(PacManMaze maze)
        {
            var seen = new HashSet<(int X, int Y)> {maze.PacManStart};
            var queue = new Queue<(int X, int Y)>();
            queue.Enqueue(maze.PacManStart);

            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();

                foreach (var (dx, dy) in new[] {(0, -1), (0, 1), (-1, 0), (1, 0)})
                {
                    var nx = maze.WrapX(x + dx);
                    var ny = y + dy;

                    if (!maze.CanEnter(nx, ny, false) || !seen.Add((nx, ny)))
                        continue;

                    queue.Enqueue((nx, ny));
                }
            }

            return seen;
        }

        private static (int X, int Y) FindPellet(PacManMaze maze, bool power)
        {
            for (var y = 0; y < maze.Height; y++)
            for (var x = 0; x < maze.Width; x++)
            {
                if (maze.HasPellet(x, y) && maze.HasPowerPellet(x, y) == power)
                    return (x, y);
            }

            throw new KeyNotFoundException("the board has no such pellet");
        }

        /// <summary>The board as text, so a failure shows the maze rather than a coordinate.</summary>
        private static string Describe(PacManMaze maze)
        {
            var sb = new StringBuilder();
            for (var y = 0; y < maze.Height; y++)
            {
                for (var x = 0; x < maze.Width; x++)
                    sb.Append(maze.TileAt(x, y));

                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
