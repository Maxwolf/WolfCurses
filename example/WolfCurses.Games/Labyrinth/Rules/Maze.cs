// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

using System;
using System.Collections.Generic;
using WolfCurses.Core;

namespace WolfCurses.Games.Labyrinth
{
    /// <summary>
    ///     A randomly generated maze, the player standing in it, and how much of it they have seen. No console, no
    ///     window, no form — <see cref="LabyrinthDialog" /> is what draws it.
    ///     <para>
    ///         <b>The maze is "perfect": exactly one route joins any two cells, and there are no loops.</b> That is
    ///         what a recursive backtracker produces, and it is worth knowing rather than discovering, because it has
    ///         two consequences the game is built on. Keeping one hand on one wall is guaranteed to get you out, which
    ///         is the classic solution and is not a bug to be fixed. And the shortest way out is unique, so
    ///         <see cref="ShortestSteps" /> is a real number to be scored against rather than one route among many.
    ///     </para>
    ///     <para>
    ///         <b>The exit is the border cell furthest from the start, measured in steps rather than in distance</b>,
    ///         with a gap knocked through the outer wall beside it. Picking a corner would be shorter to describe and
    ///         would sometimes sit around the first bend; picking the furthest cell anywhere would put the way out in
    ///         the middle of the maze, which is not what getting out means. Furthest border cell is the only one of
    ///         the three that is always both on the edge and worth walking to.
    ///     </para>
    ///     <para>
    ///         <b>Escaping means leaving, so the player's position goes off the grid.</b> That gap is the only
    ///         opening in the outer wall, which is what makes it safe: a legal move can leave the maze by exactly one
    ///         route, so <see cref="Contains" /> turning false after a move <i>means</i> the player got out and needs
    ///         no other test. It costs two things worth stating plainly rather than discovering. Everything that
    ///         indexes by the player's position — the torch, the trail — has to stop once they are outside, and
    ///         <see cref="MazeView" /> has no cell to draw them on, so it draws them standing in the doorway, which
    ///         is a wall square rather than a maze square.
    ///     </para>
    /// </summary>
    public sealed class Maze
    {
        /// <summary>How far a torch reaches down a straight corridor, in cells.</summary>
        private const int SightRange = 6;

        /// <summary>The four directions, in a fixed order so a maze dug from a seeded source is reproducible.</summary>
        private static readonly DirectionEnum[] _allDirections =
        {
            DirectionEnum.Up, DirectionEnum.Down, DirectionEnum.Left, DirectionEnum.Right
        };

        private readonly Randomizer _random;

        /// <summary>Which sides of each cell are open, one bit per <see cref="DirectionEnum" />.</summary>
        private readonly byte[] _passages;

        /// <summary>Which cells the player has ever been able to see. Never un-set: this is memory, not line of sight.</summary>
        private readonly bool[] _seen;

        /// <summary>Which cells the player has actually stood in, which is the trail drawn behind them.</summary>
        private readonly bool[] _walked;

        /// <summary>Initializes a new instance of the <see cref="Maze" /> class and digs it out.</summary>
        /// <param name="width">How many cells across; at least three.</param>
        /// <param name="height">How many cells down; at least three.</param>
        /// <param name="random">The simulation's shared random source.</param>
        public Maze(int width, int height, Randomizer random)
        {
            if (width < 3 || height < 3)
                throw new ArgumentOutOfRangeException(nameof(width), "A maze smaller than 3x3 has no inside.");

            Width = width;
            Height = height;
            _random = random ?? throw new ArgumentNullException(nameof(random));

            _passages = new byte[width*height];
            _seen = new bool[width*height];
            _walked = new bool[width*height];

            // Dug from the middle so the opening view is walls in every direction rather than a corner, and so the
            // furthest border cell below is a genuine hike whichever way it lands.
            StartX = width / 2;
            StartY = height / 2;

            Carve();
            ChooseExit();

            PlayerX = StartX;
            PlayerY = StartY;
            Illuminate();
        }

        /// <summary>How many cells across.</summary>
        public int Width { get; }

        /// <summary>How many cells down.</summary>
        public int Height { get; }

        /// <summary>Where the player started, and still the only cell they began knowing.</summary>
        public int StartX { get; }

        /// <summary>Where the player started, and still the only cell they began knowing.</summary>
        public int StartY { get; }

        /// <summary>The way out: the border cell that takes the most steps to reach from <see cref="StartX" />.</summary>
        public int ExitX { get; private set; }

        /// <summary>The way out: the border cell that takes the most steps to reach from <see cref="StartY" />.</summary>
        public int ExitY { get; private set; }

        /// <summary>
        ///     Which way the exit cell opens onto the outside — the one gap in the otherwise sealed outer wall.
        ///     <para>
        ///         A corner cell faces two ways, and the tie is broken toward whichever axis the exit is further from
        ///         the start along, so the door opens away from the maze rather than back across it.
        ///     </para>
        /// </summary>
        public DirectionEnum ExitSide { get; private set; }

        /// <summary>Where the player is standing.</summary>
        public int PlayerX { get; private set; }

        /// <summary>Where the player is standing.</summary>
        public int PlayerY { get; private set; }

        /// <summary>How many moves the player has made, including the ones that walked back over old ground.</summary>
        public int Steps { get; private set; }

        /// <summary>
        ///     The fewest moves that could possibly get <i>out</i> — the walk to the exit cell plus the one step
        ///     through the doorway. Unique, because the maze has no loops, so the ratio of <see cref="Steps" /> to
        ///     this is an honest score and not a comparison against one arbitrary route out of many.
        /// </summary>
        public int ShortestSteps { get; private set; }

        /// <summary>
        ///     How many cells the player has ever been able to see, which is the exploration figure on screen. Kept
        ///     as a running count rather than recounted, because the thing that would recount it is a redraw and a
        ///     redraw is the one place a game should not be walking the whole world.
        /// </summary>
        public int SeenCount { get; private set; }

        /// <summary>
        ///     True once the player has stepped out through the gap and is no longer inside the maze at all.
        ///     <para>
        ///         Stored rather than derived from the player's position, because once they are out that position is
        ///         <b>off the grid</b> — the one place <see cref="Contains" /> is false for somewhere the player can
        ///         legitimately be. Reaching the exit <i>cell</i> is not escaping; walking through the doorway is.
        ///     </para>
        /// </summary>
        public bool IsSolved { get; private set; }

        /// <summary>Whether a cell has ever been lit, and so whether it should be drawn at all.</summary>
        /// <param name="x">The column, counting from zero.</param>
        /// <param name="y">The row, counting from zero.</param>
        /// <returns>True when the player has seen that cell.</returns>
        public bool HasSeen(int x, int y)
        {
            return Contains(x, y) && _seen[y*Width + x];
        }

        /// <summary>Whether the player has stood in a cell, which is what draws the trail behind them.</summary>
        /// <param name="x">The column, counting from zero.</param>
        /// <param name="y">The row, counting from zero.</param>
        /// <returns>True when that cell has been walked through.</returns>
        public bool HasWalked(int x, int y)
        {
            return Contains(x, y) && _walked[y*Width + x];
        }

        /// <summary>Whether a cell is on the maze at all.</summary>
        /// <param name="x">The column, counting from zero.</param>
        /// <param name="y">The row, counting from zero.</param>
        /// <returns>True when both coordinates are in range.</returns>
        public bool Contains(int x, int y)
        {
            return x >= 0 && x < Width && y >= 0 && y < Height;
        }

        /// <summary>Whether one side of a cell is open. A cell on the border is closed toward the outside.</summary>
        /// <param name="x">The column, counting from zero.</param>
        /// <param name="y">The row, counting from zero.</param>
        /// <param name="direction">Which side to ask about.</param>
        /// <returns>True when the player could walk that way.</returns>
        public bool IsOpen(int x, int y, DirectionEnum direction)
        {
            if (!Contains(x, y) || direction == DirectionEnum.None)
                return false;

            return (_passages[y*Width + x] & Bit(direction)) != 0;
        }

        /// <summary>
        ///     Walks one cell if there is no wall in the way. A move into a wall is refused rather than fatal, and
        ///     costs nothing — so holding an arrow against a wall is not a way to inflate the step count.
        /// </summary>
        /// <param name="direction">Which way the player pushed.</param>
        /// <returns>True when the player actually moved.</returns>
        public bool TryMove(DirectionEnum direction)
        {
            if (IsSolved || !IsOpen(PlayerX, PlayerY, direction))
                return false;

            var (dx, dy) = Step(direction);
            PlayerX += dx;
            PlayerY += dy;
            Steps++;

            // Off the grid is only reachable through the single gap in the outer wall, so arriving there IS getting
            // out - there is no other way for a legal move to leave the maze. Nothing is lit from outside, and the
            // player's position deliberately stays where it landed rather than being clamped back onto the exit
            // cell, because "standing in the doorway" is a different fact from "standing on the last square".
            if (!Contains(PlayerX, PlayerY))
            {
                IsSolved = true;
                return true;
            }

            Illuminate();
            return true;
        }

        /// <summary>
        ///     Lights every cell, for the moment the player gets out and the whole maze is worth looking at. Leaves
        ///     the trail alone, which is what makes the route they actually took readable against the maze they were
        ///     in.
        /// </summary>
        public void RevealAll()
        {
            Array.Fill(_seen, true);
            SeenCount = Width*Height;
        }

        /// <summary>
        ///     Lights the cell the player is standing in and everything visible from it — straight down each of the
        ///     four corridors, stopping at the first wall.
        ///     <para>
        ///         A torch rather than a radius, and the difference is the whole feel of the game. A radius lights
        ///         cells through walls, which tells the player where the corridors are without their having to walk
        ///         them; a corridor walk tells them exactly what someone standing there could actually see. It also
        ///         means a dead end reveals nothing, which is the correct amount of information about a dead end.
        ///     </para>
        /// </summary>
        private void Illuminate()
        {
            Light(PlayerX, PlayerY);
            _walked[PlayerY*Width + PlayerX] = true;

            foreach (var direction in _allDirections)
            {
                var (dx, dy) = Step(direction);
                var x = PlayerX;
                var y = PlayerY;

                for (var reach = 0; reach < SightRange; reach++)
                {
                    // The Contains half is not redundant with the IsOpen half: the exit cell is open onto the
                    // OUTSIDE, so a torch shone down that corridor would walk straight through the doorway and index
                    // past the end of the arrays. It is the only side in the maze where an open wall leads nowhere.
                    if (!IsOpen(x, y, direction) || !Contains(x + dx, y + dy))
                        break;

                    x += dx;
                    y += dy;
                    Light(x, y);
                }
            }
        }

        /// <summary>Marks one cell as seen, keeping <see cref="SeenCount" /> in step with it.</summary>
        /// <param name="x">The column, counting from zero.</param>
        /// <param name="y">The row, counting from zero.</param>
        private void Light(int x, int y)
        {
            var index = y*Width + x;
            if (_seen[index])
                return;

            _seen[index] = true;
            SeenCount++;
        }

        /// <summary>
        ///     Digs the maze with a recursive backtracker, held on an explicit stack rather than on the call stack.
        ///     <para>
        ///         Iterative on purpose: the recursion depth of this algorithm is the length of its longest corridor,
        ///         which for a maze of any size worth playing is most of the cells in it. A 25x13 maze can reach a
        ///         depth in the hundreds, and a maze sized from the terminal — which is the obvious next change
        ///         somebody makes to this file — would put a stack overflow one wide monitor away.
        ///     </para>
        /// </summary>
        private void Carve()
        {
            var visited = new bool[Width*Height];
            var stack = new Stack<(int X, int Y)>();
            var neighbours = new List<DirectionEnum>(4);

            visited[StartY*Width + StartX] = true;
            stack.Push((StartX, StartY));

            while (stack.Count > 0)
            {
                var (x, y) = stack.Peek();

                neighbours.Clear();
                foreach (var direction in _allDirections)
                {
                    var (dx, dy) = Step(direction);
                    var nx = x + dx;
                    var ny = y + dy;

                    if (Contains(nx, ny) && !visited[ny*Width + nx])
                        neighbours.Add(direction);
                }

                if (neighbours.Count == 0)
                {
                    stack.Pop();
                    continue;
                }

                var chosen = neighbours[_random.Next(neighbours.Count)];
                var (sx, sy) = Step(chosen);
                var tx = x + sx;
                var ty = y + sy;

                // Both sides of the wall, or the maze is only passable in the direction it was dug.
                _passages[y*Width + x] |= Bit(chosen);
                _passages[ty*Width + tx] |= Bit(Opposite(chosen));

                visited[ty*Width + tx] = true;
                stack.Push((tx, ty));
            }
        }

        /// <summary>Finds the border cell that takes the most steps to reach, and remembers how many that is.</summary>
        private void ChooseExit()
        {
            var distance = Distances(StartX, StartY);

            ExitX = StartX;
            ExitY = StartY;
            ShortestSteps = -1;

            for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
            {
                var onBorder = x == 0 || y == 0 || x == Width - 1 || y == Height - 1;
                if (!onBorder)
                    continue;

                var steps = distance[y*Width + x];
                if (steps <= ShortestSteps)
                    continue;

                ShortestSteps = steps;
                ExitX = x;
                ExitY = y;
            }

            // And then knock the door through. This is the ONLY opening in the outer wall, and it is what makes
            // getting out an actual departure rather than arriving at a marked square - which is what anyone who has
            // ever been in a maze expects, and is worth the two facts it costs: the player's position leaves the
            // grid, and one wall of one cell opens onto nothing.
            ExitSide = OutwardSide(ExitX, ExitY);
            _passages[ExitY*Width + ExitX] |= Bit(ExitSide);

            // Plus the step through the doorway itself, or the score would be measured against reaching the
            // threshold while the game is only won by crossing it.
            ShortestSteps++;
        }

        /// <summary>
        ///     Which way a border cell faces the outside world.
        ///     <para>
        ///         A corner faces two ways, and the tie goes to whichever axis the cell is further from the start
        ///         along — so the door opens away from the maze rather than back across it. Any fixed order would
        ///         work and would always send a top-left exit through the same wall; this costs three lines and puts
        ///         the way out where the walk was heading.
        ///     </para>
        /// </summary>
        /// <param name="x">The exit column.</param>
        /// <param name="y">The exit row.</param>
        /// <returns>The outward direction, or <see cref="DirectionEnum.None" /> for a cell that is not on the border.</returns>
        private DirectionEnum OutwardSide(int x, int y)
        {
            var horizontal = x == 0 ? DirectionEnum.Left
                : x == Width - 1 ? DirectionEnum.Right
                : DirectionEnum.None;

            var vertical = y == 0 ? DirectionEnum.Up
                : y == Height - 1 ? DirectionEnum.Down
                : DirectionEnum.None;

            if (horizontal == DirectionEnum.None)
                return vertical;

            if (vertical == DirectionEnum.None)
                return horizontal;

            return Math.Abs(x - StartX) >= Math.Abs(y - StartY) ? horizontal : vertical;
        }

        /// <summary>Breadth-first step counts from one cell to every other, which in a perfect maze is the only route.</summary>
        /// <param name="fromX">Where to measure from.</param>
        /// <param name="fromY">Where to measure from.</param>
        /// <returns>Steps to each cell, indexed the same way the maze is.</returns>
        private int[] Distances(int fromX, int fromY)
        {
            var distance = new int[Width*Height];
            Array.Fill(distance, -1);

            var queue = new Queue<(int X, int Y)>();
            distance[fromY*Width + fromX] = 0;
            queue.Enqueue((fromX, fromY));

            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                var here = distance[y*Width + x];

                foreach (var direction in _allDirections)
                {
                    if (!IsOpen(x, y, direction))
                        continue;

                    var (dx, dy) = Step(direction);
                    var nx = x + dx;
                    var ny = y + dy;

                    // Runs before the doorway is cut, so today every open side leads to a real cell - the guard is
                    // here so that stays true if anything ever measures distances again afterwards.
                    if (!Contains(nx, ny) || distance[ny*Width + nx] >= 0)
                        continue;

                    distance[ny*Width + nx] = here + 1;
                    queue.Enqueue((nx, ny));
                }
            }

            return distance;
        }

        /// <summary>Which bit of a cell's passage byte belongs to one side.</summary>
        private static byte Bit(DirectionEnum direction)
        {
            return (byte) (1 << (int) direction);
        }

        /// <summary>How far one step in a direction moves, in cells.</summary>
        private static (int X, int Y) Step(DirectionEnum direction)
        {
            return direction switch
            {
                DirectionEnum.Up => (0, -1),
                DirectionEnum.Down => (0, 1),
                DirectionEnum.Left => (-1, 0),
                DirectionEnum.Right => (1, 0),
                _ => (0, 0)
            };
        }

        /// <summary>Which way is back the way you came.</summary>
        private static DirectionEnum Opposite(DirectionEnum direction)
        {
            return direction switch
            {
                DirectionEnum.Up => DirectionEnum.Down,
                DirectionEnum.Down => DirectionEnum.Up,
                DirectionEnum.Left => DirectionEnum.Right,
                DirectionEnum.Right => DirectionEnum.Left,
                _ => DirectionEnum.None
            };
        }
    }
}
