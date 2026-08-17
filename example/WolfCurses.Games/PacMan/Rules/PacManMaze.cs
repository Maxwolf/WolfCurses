// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

using System;
using System.Collections.Generic;

namespace WolfCurses.Games.PacMan
{
    /// <summary>
    ///     The board: where the walls are, where the food is, and where everything starts. No console, no window, no
    ///     form — <see cref="PacManView" /> is what draws it and <see cref="PacManGame" /> is what plays on it.
    ///     <para>
    ///         <b>The map is authored as its left half and mirrored</b>, which is not a space saving — it is what
    ///         makes the maze symmetrical <i>by construction</i>. A hand-drawn arcade maze is symmetrical, and forty
    ///         columns of hand-drawn wall are exactly the kind of thing where one row ends up a character adrift and
    ///         nobody sees it until the ghosts start behaving oddly on one side. Mirroring makes that unrepresentable.
    ///         The width is <b>odd</b> so the centre column is shared rather than doubled, which is what lets a
    ///         corridor run straight down the middle.
    ///     </para>
    ///     <para>
    ///         <b>Every wall is one cell thick, and that is a hard rule rather than a style.</b> The walls are drawn
    ///         as connected lines by <see cref="Window.Control.BoxDrawing" />, and a line renderer given a wall two
    ///         cells thick draws a lattice of crossings through the middle of it instead of a solid block — so a
    ///         2x2 square of wall anywhere is a drawing bug, and <c>PacManMazeTests</c> refuses one outright rather
    ///         than leaving it to be noticed.
    ///     </para>
    ///     <para>
    ///         Contrast with <see cref="Labyrinth.Maze" />, which generates itself: this one is <i>designed</i>, and
    ///         it has to be, because a maze that is fun to be chased around is a different thing from a maze that is
    ///         hard to solve. The pellets, the four power pellets in the corners, the tunnel and the ghost house are
    ///         all placed by hand for that reason.
    ///     </para>
    /// </summary>
    public sealed class PacManMaze
    {
        /// <summary>Solid wall.</summary>
        public const char Wall = '#';

        /// <summary>The ghost house door: ghosts pass through it, the player does not.</summary>
        public const char Door = '-';

        /// <summary>
        ///     The left half of the board, mirrored about its last column to make the whole thing. Twenty columns
        ///     become thirty-nine.
        ///     <para>
        ///         <c>#</c> wall, <c>.</c> pellet, <c>o</c> power pellet, <c>-</c> the ghost house door, a space is
        ///         open floor with nothing on it, and <c>P</c> is where the player starts (also open floor). Row 8 is
        ///         the tunnel: it runs off both ends of the board and wraps around.
        ///     </para>
        /// </summary>
        private static readonly string[] _leftHalf =
        {
            "####################",
            "#o..................",
            "#..####..####..####.",
            "#....#....#......#..",
            "#..####..##..##..##.",
            "#...#.........#.....",
            "#..####..##.........",
            "#..............####-",
            "  .............#    ",
            "#..............#####",
            "#..####..##.........",
            "#...#.........#....P",
            "#..####..##..##..##.",
            "#....#....#......#..",
            "#..####..####..####.",
            "#o..................",
            "####################"
        };

        private readonly char[] _tiles;
        private readonly bool[] _pellets;
        private readonly bool[] _power;
        private readonly bool[] _inHouse;

        /// <summary>Initializes a new instance of the <see cref="PacManMaze" /> class with every pellet on the board.</summary>
        public PacManMaze()
        {
            Height = _leftHalf.Length;
            Width = 2*_leftHalf[0].Length - 1;

            _tiles = new char[Width*Height];
            _pellets = new bool[Width*Height];
            _power = new bool[Width*Height];

            var ghostStarts = new List<(int X, int Y)>();

            for (var y = 0; y < Height; y++)
            {
                var row = _leftHalf[y];
                if (row.Length != _leftHalf[0].Length)
                    throw new InvalidOperationException($"Row {y} of the maze is {row.Length} columns, not {_leftHalf[0].Length}.");

                for (var x = 0; x < Width; x++)
                {
                    // Past the centre column, read backwards from it. The centre itself is shared, which is why the
                    // board comes out odd-width rather than double the half.
                    var tile = x < row.Length ? row[x] : row[Width - 1 - x];
                    _tiles[y*Width + x] = tile;

                    switch (tile)
                    {
                        case '.':
                            _pellets[y*Width + x] = true;
                            PelletsLeft++;
                            break;
                        case 'o':
                            _pellets[y*Width + x] = true;
                            _power[y*Width + x] = true;
                            PelletsLeft++;
                            break;
                        case 'P':
                            PacManStart = (x, y);
                            break;
                        case Door:
                            DoorX = x;
                            DoorY = y;
                            break;
                    }
                }
            }

            // The house is the open pocket under the door. Found rather than declared, so moving the door in the map
            // moves the ghosts with it and the two cannot drift apart.
            HouseX = DoorX;
            HouseY = DoorY + 1;

            for (var offset = -3; offset <= 3; offset++)
            {
                if (IsOpen(HouseX + offset, HouseY))
                    ghostStarts.Add((HouseX + offset, HouseY));
            }

            GhostStarts = ghostStarts;
            TotalPellets = PelletsLeft;

            _inHouse = new bool[Width*Height];
            FloodHouse();
        }

        /// <summary>
        ///     Marks the pocket of floor the ghosts start in, by flooding out from under the door and stopping at
        ///     walls and at the door itself.
        ///     <para>
        ///         Found rather than declared, so the house is wherever the map draws it. Declaring it as a rectangle
        ///         of coordinates would be two facts that have to agree, and the day they stop agreeing the ghosts
        ///         either cannot get out or can walk through a wall — neither of which announces itself.
        ///     </para>
        /// </summary>
        private void FloodHouse()
        {
            var queue = new Queue<(int X, int Y)>();
            queue.Enqueue((HouseX, HouseY));
            _inHouse[HouseY*Width + HouseX] = true;

            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();

                foreach (var (dx, dy) in new[] {(0, -1), (0, 1), (-1, 0), (1, 0)})
                {
                    var nx = x + dx;
                    var ny = y + dy;

                    // The door is the boundary, not a way through - flooding past it would mark the whole board as
                    // house and the ghosts would treat every doorway in the maze as their own.
                    if (!Contains(nx, ny) || IsWall(nx, ny) || IsDoor(nx, ny) || _inHouse[ny*Width + nx])
                        continue;

                    _inHouse[ny*Width + nx] = true;
                    queue.Enqueue((nx, ny));
                }
            }
        }

        /// <summary>How many columns the board has. Odd, because the halves share their centre.</summary>
        public int Width { get; }

        /// <summary>How many rows the board has.</summary>
        public int Height { get; }

        /// <summary>Where the player starts each life.</summary>
        public (int X, int Y) PacManStart { get; }

        /// <summary>The ghost house door, which only ghosts may cross.</summary>
        public int DoorX { get; }

        /// <summary>The ghost house door, which only ghosts may cross.</summary>
        public int DoorY { get; }

        /// <summary>The middle of the ghost house, one row below the door.</summary>
        public int HouseX { get; }

        /// <summary>The middle of the ghost house, one row below the door.</summary>
        public int HouseY { get; }

        /// <summary>Where each ghost waits inside the house, left to right.</summary>
        public IReadOnlyList<(int X, int Y)> GhostStarts { get; }

        /// <summary>How much food is still on the board.</summary>
        public int PelletsLeft { get; private set; }

        /// <summary>How much food a full board holds, which is what a level is scored against.</summary>
        public int TotalPellets { get; }

        /// <summary>The row the tunnel runs along — the one row whose ends are open.</summary>
        public int TunnelRow => HouseY;

        /// <summary>The character the map declares for a cell, or <see cref="Wall" /> for anywhere off the board.</summary>
        /// <param name="x">The column.</param>
        /// <param name="y">The row.</param>
        /// <returns>The map character.</returns>
        public char TileAt(int x, int y)
        {
            return Contains(x, y) ? _tiles[y*Width + x] : Wall;
        }

        /// <summary>Whether a cell is on the board at all.</summary>
        /// <param name="x">The column.</param>
        /// <param name="y">The row.</param>
        /// <returns>True when both coordinates are in range.</returns>
        public bool Contains(int x, int y)
        {
            return x >= 0 && x < Width && y >= 0 && y < Height;
        }

        /// <summary>Whether a cell is solid wall.</summary>
        /// <param name="x">The column.</param>
        /// <param name="y">The row.</param>
        /// <returns>True when nothing may enter.</returns>
        public bool IsWall(int x, int y)
        {
            return TileAt(x, y) == Wall;
        }

        /// <summary>Whether a cell is the ghost house door.</summary>
        /// <param name="x">The column.</param>
        /// <param name="y">The row.</param>
        /// <returns>True when it is the door.</returns>
        public bool IsDoor(int x, int y)
        {
            return TileAt(x, y) == Door;
        }

        /// <summary>Whether a cell is inside the ghost house, which is the only place the door may be used from.</summary>
        /// <param name="x">The column.</param>
        /// <param name="y">The row.</param>
        /// <returns>True when the cell is in the pen.</returns>
        public bool IsInsideHouse(int x, int y)
        {
            return Contains(x, y) && _inHouse[y*Width + x];
        }

        /// <summary>Whether anything at all can stand in a cell — floor or door, but not wall.</summary>
        /// <param name="x">The column.</param>
        /// <param name="y">The row.</param>
        /// <returns>True when the cell is not wall.</returns>
        public bool IsOpen(int x, int y)
        {
            return Contains(x, y) && !IsWall(x, y);
        }

        /// <summary>
        ///     Whether a mover may enter a cell. The only difference between the two kinds of mover is the door: it
        ///     is how the ghosts get out and back in, and it is why the player cannot follow them home.
        /// </summary>
        /// <param name="x">The column.</param>
        /// <param name="y">The row.</param>
        /// <param name="isGhost">Whether the mover is a ghost.</param>
        /// <returns>True when the move is legal.</returns>
        public bool CanEnter(int x, int y, bool isGhost)
        {
            if (!IsOpen(x, y))
                return false;

            return isGhost || !IsDoor(x, y);
        }

        /// <summary>
        ///     Wraps a column around the tunnel. Walking off one end of the board arrives at the other, which is the
        ///     only place the board is not a rectangle.
        /// </summary>
        /// <param name="x">The column, possibly off the board.</param>
        /// <returns>The column on the board.</returns>
        public int WrapX(int x)
        {
            if (x < 0)
                return Width - 1;

            return x >= Width ? 0 : x;
        }

        /// <summary>Whether there is a pellet of any kind in a cell.</summary>
        /// <param name="x">The column.</param>
        /// <param name="y">The row.</param>
        /// <returns>True when there is food there.</returns>
        public bool HasPellet(int x, int y)
        {
            return Contains(x, y) && _pellets[y*Width + x];
        }

        /// <summary>Whether the pellet in a cell is a power pellet.</summary>
        /// <param name="x">The column.</param>
        /// <param name="y">The row.</param>
        /// <returns>True when it is one of the four big ones.</returns>
        public bool HasPowerPellet(int x, int y)
        {
            return Contains(x, y) && _power[y*Width + x];
        }

        /// <summary>Takes whatever food is in a cell, and says what it was.</summary>
        /// <param name="x">The column.</param>
        /// <param name="y">The row.</param>
        /// <returns>What was eaten, if anything.</returns>
        public PelletEnum Eat(int x, int y)
        {
            if (!HasPellet(x, y))
                return PelletEnum.None;

            var index = y*Width + x;
            var power = _power[index];

            _pellets[index] = false;
            _power[index] = false;
            PelletsLeft--;

            return power ? PelletEnum.Power : PelletEnum.Pellet;
        }

        /// <summary>Puts every pellet back, for the next level.</summary>
        public void Refill()
        {
            PelletsLeft = 0;

            for (var i = 0; i < _tiles.Length; i++)
            {
                _pellets[i] = _tiles[i] == '.' || _tiles[i] == 'o';
                _power[i] = _tiles[i] == 'o';

                if (_pellets[i])
                    PelletsLeft++;
            }
        }
    }
}
