using System;
using System.Collections.Generic;
using WolfCurses.Core;
using WolfCurses.Games.Labyrinth;
using Xunit;

namespace WolfCurses.Games.Tests.Labyrinth
{
    /// <summary>
    ///     The rules of the maze, driven with no console anywhere near them.
    ///     <para>
    ///         <b>Every maze here is dug from a seeded <see cref="Randomizer" />, and where the answer depends on the
    ///         seed the assertion is an invariant over several of them</b> rather than one expected number. That is
    ///         the discipline the missile field tests landed on after this repository shipped two flaky tests that
    ///         fished for a situation in unseeded random state — and a maze is exactly the shape of thing that
    ///         tempts you to write down the answer one particular seed happened to give.
    ///     </para>
    /// </summary>
    public class MazeTests
    {
        private const int Width = 15;
        private const int Height = 9;

        public static TheoryData<int> Seeds
        {
            get
            {
                var data = new TheoryData<int>();
                for (var seed = 1; seed <= 12; seed++)
                    data.Add(seed);

                return data;
            }
        }

        [Theory]
        [MemberData(nameof(Seeds))]
        public void EveryCellCanBeReachedFromTheStart(int seed)
        {
            // The one thing a maze absolutely must be. A backtracker that forgot to mark a cell visited, or that
            // carved the wall from only one side, produces an island that looks perfectly like a maze on screen.
            var maze = Dig(seed);
            var distance = Distances(maze, maze.StartX, maze.StartY);

            for (var y = 0; y < maze.Height; y++)
            for (var x = 0; x < maze.Width; x++)
                Assert.True(distance[y*maze.Width + x] >= 0, $"cell {x},{y} is walled off from the start");
        }

        [Theory]
        [MemberData(nameof(Seeds))]
        public void EveryPassageIsOpenFromBothSides(int seed)
        {
            // Carving one side only gives corridors you can walk down and not back up, which reads on screen as the
            // player getting stuck for no visible reason.
            var maze = Dig(seed);

            for (var y = 0; y < maze.Height; y++)
            for (var x = 0; x < maze.Width; x++)
            {
                AssertMutual(maze, x, y, DirectionEnum.Up, 0, -1);
                AssertMutual(maze, x, y, DirectionEnum.Down, 0, 1);
                AssertMutual(maze, x, y, DirectionEnum.Left, -1, 0);
                AssertMutual(maze, x, y, DirectionEnum.Right, 1, 0);
            }
        }

        [Theory]
        [MemberData(nameof(Seeds))]
        public void TheMazeHasNoLoopsAtAll(int seed)
        {
            // A connected graph over N cells with no cycles has exactly N-1 edges - so counting the passages proves
            // "perfect" outright, and it is the property the unique shortest route is scored against. One extra
            // carve and ShortestSteps stops being a fact about the maze.
            var maze = Dig(seed);
            var passages = 0;

            for (var y = 0; y < maze.Height; y++)
            for (var x = 0; x < maze.Width; x++)
            {
                // Interior sides only. The doorway is an opening too, and counting it would make this assertion
                // depend on which wall the exit happened to land against.
                if (x < maze.Width - 1 && maze.IsOpen(x, y, DirectionEnum.Right))
                    passages++;

                if (y < maze.Height - 1 && maze.IsOpen(x, y, DirectionEnum.Down))
                    passages++;
            }

            Assert.Equal(maze.Width*maze.Height - 1, passages);
        }

        [Theory]
        [MemberData(nameof(Seeds))]
        public void TheOutsideWallHasExactlyOneWayThroughIt(int seed)
        {
            // THE property the escape rule rests on. A move that leaves the grid is treated as getting out with no
            // further test, which is only sound while there is exactly one hole to leave through - a second one
            // anywhere would turn walking into the edge of the maze into winning it.
            var maze = Dig(seed);
            var holes = 0;

            for (var x = 0; x < maze.Width; x++)
            {
                if (maze.IsOpen(x, 0, DirectionEnum.Up))
                    holes += CountAsDoorway(maze, x, 0, DirectionEnum.Up);

                if (maze.IsOpen(x, maze.Height - 1, DirectionEnum.Down))
                    holes += CountAsDoorway(maze, x, maze.Height - 1, DirectionEnum.Down);
            }

            for (var y = 0; y < maze.Height; y++)
            {
                if (maze.IsOpen(0, y, DirectionEnum.Left))
                    holes += CountAsDoorway(maze, 0, y, DirectionEnum.Left);

                if (maze.IsOpen(maze.Width - 1, y, DirectionEnum.Right))
                    holes += CountAsDoorway(maze, maze.Width - 1, y, DirectionEnum.Right);
            }

            Assert.Equal(1, holes);
        }

        [Theory]
        [MemberData(nameof(Seeds))]
        public void TheDoorwayIsBesideTheExitCellAndFacesOutOfTheMaze(int seed)
        {
            var maze = Dig(seed);

            Assert.NotEqual(DirectionEnum.None, maze.ExitSide);
            Assert.True(maze.IsOpen(maze.ExitX, maze.ExitY, maze.ExitSide), "the doorway is not open");

            var (dx, dy) = Offset(maze.ExitSide);
            Assert.False(maze.Contains(maze.ExitX + dx, maze.ExitY + dy),
                "the doorway faces another cell of the maze rather than the outside");
        }

        [Theory]
        [MemberData(nameof(Seeds))]
        public void SteppingThroughTheDoorwayLeavesTheMazeAltogether(int seed)
        {
            // Escaping is leaving, so the player ends up off the grid - the one place Contains is false for
            // somewhere they can legitimately be, and the thing MazeView has to draw around.
            var maze = Dig(seed);
            var route = ShortestRoute(maze);

            for (var i = 0; i < route.Count - 1; i++)
                Assert.True(maze.TryMove(route[i]));

            Assert.Equal((maze.ExitX, maze.ExitY), (maze.PlayerX, maze.PlayerY));
            Assert.False(maze.IsSolved, "standing on the exit cell is not yet being out of the maze");

            Assert.True(maze.TryMove(maze.ExitSide));

            Assert.True(maze.IsSolved);
            Assert.False(maze.Contains(maze.PlayerX, maze.PlayerY));
        }

        [Theory]
        [MemberData(nameof(Seeds))]
        public void TheExitIsOnTheBorderAndIsTheFurthestBorderCellThereIs(int seed)
        {
            var maze = Dig(seed);
            var distance = Distances(maze, maze.StartX, maze.StartY);

            var onBorder = maze.ExitX == 0 || maze.ExitY == 0 ||
                           maze.ExitX == maze.Width - 1 || maze.ExitY == maze.Height - 1;
            Assert.True(onBorder, $"the exit at {maze.ExitX},{maze.ExitY} is not on the border");

            for (var y = 0; y < maze.Height; y++)
            for (var x = 0; x < maze.Width; x++)
            {
                var border = x == 0 || y == 0 || x == maze.Width - 1 || y == maze.Height - 1;
                if (border)
                    Assert.True(distance[y*maze.Width + x] <= maze.ShortestSteps - 1);
            }

            // One more than the walk to the exit cell: the last step goes through the doorway, and it is the one
            // that actually wins.
            Assert.Equal(maze.ShortestSteps - 1, distance[maze.ExitY*maze.Width + maze.ExitX]);
            Assert.True(maze.ShortestSteps > 1, "the exit is where the player is standing");
        }

        [Theory]
        [MemberData(nameof(Seeds))]
        public void WalkingTheShortestRouteGetsOutInExactlyThatManySteps(int seed)
        {
            // End to end, and the assertion that ties the score to reality: whatever ShortestSteps claims, a player
            // who walks it must actually arrive, and must not need one more step than advertised.
            var maze = Dig(seed);
            var route = ShortestRoute(maze);

            Assert.Equal(maze.ShortestSteps, route.Count);

            foreach (var direction in route)
                Assert.True(maze.TryMove(direction), "the shortest route walks through a wall");

            Assert.True(maze.IsSolved);
            Assert.Equal(maze.ShortestSteps, maze.Steps);
        }

        [Theory]
        [MemberData(nameof(Seeds))]
        public void WalkingIntoAWallCostsNothingAtAll(int seed)
        {
            // Otherwise holding an arrow against a wall inflates the step count, and the efficiency score at the end
            // measures how long the player leant on a key rather than how well they found their way.
            var maze = Dig(seed);

            // Walked to a wall rather than hoping the starting cell has one - a cell in the middle of a maze can
            // perfectly well be open on all four sides, and a test that assumed otherwise would be a coin toss
            // dressed as an assertion. The direction is chosen AWAY from the exit's row, so this walk cannot end by
            // escaping instead of by being stopped, which would prove nothing about walls; and the outer wall is
            // sealed, so it cannot run forever either.
            var into = maze.ExitY <= maze.StartY ? DirectionEnum.Down : DirectionEnum.Up;
            while (maze.TryMove(into))
            {
            }

            Assert.False(maze.IsSolved, "the walk got out instead of hitting a wall");
            Assert.False(maze.IsOpen(maze.PlayerX, maze.PlayerY, into));

            var (x, y, steps) = (maze.PlayerX, maze.PlayerY, maze.Steps);
            Assert.False(maze.TryMove(into));
            Assert.Equal((x, y, steps), (maze.PlayerX, maze.PlayerY, maze.Steps));
        }

        [Theory]
        [MemberData(nameof(Seeds))]
        public void TheTorchShinesDownCorridorsAndNeverThroughWalls(int seed)
        {
            // The assertion that separates a torch from a radius, and the reason it is written as "every lit cell is
            // reachable in a straight open line" rather than "this particular cell is dark": a radius lights the
            // diagonal neighbours, which tells the player where the corridors are without their walking them, and
            // that difference is the entire feel of the game.
            var maze = Dig(seed);

            for (var y = 0; y < maze.Height; y++)
            for (var x = 0; x < maze.Width; x++)
            {
                if (!maze.HasSeen(x, y))
                    continue;

                Assert.True(x == maze.PlayerX || y == maze.PlayerY,
                    $"cell {x},{y} is lit but is neither on the player's row nor their column");
                Assert.True(WalkableInAStraightLine(maze, x, y),
                    $"cell {x},{y} is lit through a wall");
            }
        }

        [Theory]
        [MemberData(nameof(Seeds))]
        public void SeenCountAgreesWithWhatHasSeenSays(int seed)
        {
            var maze = Dig(seed);

            for (var i = 0; i < 40; i++)
                maze.TryMove((DirectionEnum) (1 + i % 4));

            var counted = 0;
            for (var y = 0; y < maze.Height; y++)
            for (var x = 0; x < maze.Width; x++)
            {
                if (maze.HasSeen(x, y))
                    counted++;
            }

            Assert.Equal(counted, maze.SeenCount);

            maze.RevealAll();
            Assert.Equal(maze.Width*maze.Height, maze.SeenCount);
        }

        [Fact]
        public void RevealingEverythingLeavesTheTrailAloneSoTheRouteStaysReadable()
        {
            var maze = Dig(3);
            var route = ShortestRoute(maze);
            foreach (var direction in route)
                maze.TryMove(direction);

            maze.RevealAll();

            var walked = 0;
            for (var y = 0; y < maze.Height; y++)
            for (var x = 0; x < maze.Width; x++)
            {
                Assert.True(maze.HasSeen(x, y));
                if (maze.HasWalked(x, y))
                    walked++;
            }

            // The cell they started in plus every cell the route walked THROUGH - the last move of the route steps
            // out of the maze, so it marks nothing. Revealing must not mark the whole maze as walked either, or the
            // ending draws a solid trail over everything.
            Assert.Equal(route.Count, walked);
        }

        [Fact]
        public void OnceOutTheMazeStopsAcceptingMoves()
        {
            var maze = Dig(5);
            foreach (var direction in ShortestRoute(maze))
                maze.TryMove(direction);

            var steps = maze.Steps;

            foreach (var direction in new[]
                     {DirectionEnum.Up, DirectionEnum.Down, DirectionEnum.Left, DirectionEnum.Right})
                Assert.False(maze.TryMove(direction));

            Assert.Equal(steps, maze.Steps);
        }

        [Theory]
        [InlineData(2, 5)]
        [InlineData(5, 2)]
        public void AMazeTooSmallToHaveAnInsideIsRefused(int width, int height)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Maze(width, height, new Randomizer(1)));
        }

        [Fact]
        public void TheSameSeedDigsTheSameMaze()
        {
            // Which is what makes every assertion above reproducible, and is worth pinning because the fixed order
            // of the four directions inside the carve is the only thing holding it up.
            var first = Dig(9);
            var second = Dig(9);

            Assert.Equal(first.ShortestSteps, second.ShortestSteps);
            Assert.Equal((first.ExitX, first.ExitY), (second.ExitX, second.ExitY));

            for (var y = 0; y < first.Height; y++)
            for (var x = 0; x < first.Width; x++)
                Assert.Equal(first.IsOpen(x, y, DirectionEnum.Right), second.IsOpen(x, y, DirectionEnum.Right));
        }

        // ------------------------------------------------------------ helpers

        private static Maze Dig(int seed)
        {
            return new Maze(Width, Height, new Randomizer(seed));
        }

        /// <summary>Asserts an opening in the outer wall is the doorway, and counts it.</summary>
        private static int CountAsDoorway(Maze maze, int x, int y, DirectionEnum side)
        {
            Assert.Equal((maze.ExitX, maze.ExitY, maze.ExitSide), (x, y, side));
            return 1;
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

        private static void AssertMutual(Maze maze, int x, int y, DirectionEnum side, int dx, int dy)
        {
            if (!maze.IsOpen(x, y, side))
                return;

            // The one opening in the maze with nothing on the other side of it.
            if (!maze.Contains(x + dx, y + dy))
            {
                Assert.Equal((maze.ExitX, maze.ExitY, maze.ExitSide), (x, y, side));
                return;
            }

            var back = side switch
            {
                DirectionEnum.Up => DirectionEnum.Down,
                DirectionEnum.Down => DirectionEnum.Up,
                DirectionEnum.Left => DirectionEnum.Right,
                _ => DirectionEnum.Left
            };

            Assert.True(maze.IsOpen(x + dx, y + dy, back),
                $"{x},{y} opens {side} but {x + dx},{y + dy} does not open back");
        }

        /// <summary>Whether a cell can be reached from the player in one straight run of open corridor.</summary>
        private static bool WalkableInAStraightLine(Maze maze, int targetX, int targetY)
        {
            if (targetX == maze.PlayerX && targetY == maze.PlayerY)
                return true;

            var stepX = Math.Sign(targetX - maze.PlayerX);
            var stepY = Math.Sign(targetY - maze.PlayerY);
            var direction = stepY < 0 ? DirectionEnum.Up
                : stepY > 0 ? DirectionEnum.Down
                : stepX < 0 ? DirectionEnum.Left
                : DirectionEnum.Right;

            var x = maze.PlayerX;
            var y = maze.PlayerY;
            while (x != targetX || y != targetY)
            {
                if (!maze.IsOpen(x, y, direction))
                    return false;

                x += stepX;
                y += stepY;
            }

            return true;
        }

        /// <summary>Step counts from a cell to every other, worked out here rather than asked of the maze.</summary>
        private static int[] Distances(Maze maze, int fromX, int fromY)
        {
            var distance = new int[maze.Width*maze.Height];
            Array.Fill(distance, -1);

            var queue = new Queue<(int X, int Y)>();
            distance[fromY*maze.Width + fromX] = 0;
            queue.Enqueue((fromX, fromY));

            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                foreach (var (direction, dx, dy) in _steps)
                {
                    // Contains as well as IsOpen: the exit cell opens onto the OUTSIDE, so a walk that trusted
                    // IsOpen alone would step through the doorway and index past the end of the array.
                    if (!maze.IsOpen(x, y, direction) || !maze.Contains(x + dx, y + dy))
                        continue;

                    var index = (y + dy)*maze.Width + x + dx;
                    if (distance[index] >= 0)
                        continue;

                    distance[index] = distance[y*maze.Width + x] + 1;
                    queue.Enqueue((x + dx, y + dy));
                }
            }

            return distance;
        }

        /// <summary>The moves that walk the shortest way out, found by working backwards from the exit.</summary>
        private static List<DirectionEnum> ShortestRoute(Maze maze)
        {
            var distance = Distances(maze, maze.ExitX, maze.ExitY);
            var route = new List<DirectionEnum>();
            var x = maze.PlayerX;
            var y = maze.PlayerY;

            // Bounded, and the bound is not paranoia. Mutating the carve to open only the near side of each wall
            // disconnects the maze, and the first version of this loop simply span - a test that HANGS the suite
            // instead of failing it tells you nothing and costs a full mutation run to notice.
            while (x != maze.ExitX || y != maze.ExitY)
            {
                var before = (x, y);

                foreach (var (direction, dx, dy) in _steps)
                {
                    if (!maze.IsOpen(x, y, direction))
                        continue;

                    if (distance[(y + dy)*maze.Width + x + dx] != distance[y*maze.Width + x] - 1)
                        continue;

                    route.Add(direction);
                    x += dx;
                    y += dy;
                    break;
                }

                Assert.True(before != (x, y),
                    $"no step from {x},{y} gets nearer the exit - the maze is not connected");
            }

            // And out. Reaching the exit cell is arriving at the threshold; the game is won by crossing it, so the
            // shortest way out is one step longer than the shortest way there.
            route.Add(maze.ExitSide);
            return route;
        }

        private static readonly (DirectionEnum Direction, int Dx, int Dy)[] _steps =
        {
            (DirectionEnum.Up, 0, -1),
            (DirectionEnum.Down, 0, 1),
            (DirectionEnum.Left, -1, 0),
            (DirectionEnum.Right, 1, 0)
        };
    }
}
