// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

using System;
using WolfCurses.Graphics;
using WolfCurses.Window.Control;

namespace WolfCurses.Games.Labyrinth
{
    /// <summary>
    ///     Paints a <see cref="Maze" /> into a <see cref="TextGrid" />, which is then shown a window at a time by
    ///     <see cref="LabyrinthDialog" />.
    ///     <para>
    ///         <b>A maze of W by H cells needs a grid of 2W+1 by 2H+1 characters</b>, because the walls are as real as
    ///         the corridors and have to live somewhere: odd positions are cells, even positions are the walls between
    ///         them, and the corners are always wall. Trying to draw one cell per character is the mistake that makes
    ///         a maze come out as a field of dots with no way to tell which gaps you can walk through.
    ///     </para>
    ///     <para>
    ///         <b>Only cells the player has seen are drawn at all</b>, and a wall is painted from whichever of the two
    ///         cells it separates has been seen — so a corridor that runs off into the dark shows as a gap in the wall
    ///         rather than as a wall, which is exactly what someone standing there would know about it.
    ///     </para>
    ///     <para>
    ///         <b>Every kind of thing has its own glyph as well as its own colour</b>, the lesson
    ///         <see cref="Chess.ChessTextBoard" /> and <see cref="MissileCommand.MissileFieldText" /> both record: at
    ///         <c>NO_COLOR</c> or under a forced grayscale, a maze that told the player where they were with colour
    ///         alone becomes an unreadable field of identical blocks, and looks perfectly fine to whoever wrote it.
    ///     </para>
    /// </summary>
    public static class MazeView
    {
        /// <summary>Solid stone. Drawn two columns wide, like everything else here.</summary>
        private const char WallGlyph = '█';

        /// <summary>A cell the player has walked through, which is the trail behind them.</summary>
        private const char TrailGlyph = '·';

        /// <summary>The player.</summary>
        private const char PlayerGlyph = '@';

        /// <summary>The way out.</summary>
        private const char ExitGlyph = '>';

        /// <summary>Where they started, so the trail has a beginning that reads as one.</summary>
        private const char StartGlyph = '<';

        private static readonly TextStyle _wallStyle = new(ConsoleColor.DarkGray);
        private static readonly TextStyle _trailStyle = new(ConsoleColor.DarkCyan);
        private static readonly TextStyle _playerStyle = new(ConsoleColor.Yellow, bold: true);
        private static readonly TextStyle _exitStyle = new(ConsoleColor.Green, bold: true);
        private static readonly TextStyle _startStyle = new(ConsoleColor.DarkGreen);

        /// <summary>How wide the character grid for a maze is, walls included.</summary>
        /// <param name="maze">The maze to measure.</param>
        /// <returns>The grid width in cells.</returns>
        public static int GridWidth(Maze maze)
        {
            return 2*(maze ?? throw new ArgumentNullException(nameof(maze))).Width + 1;
        }

        /// <summary>How tall the character grid for a maze is, walls included.</summary>
        /// <param name="maze">The maze to measure.</param>
        /// <returns>The grid height in cells.</returns>
        public static int GridHeight(Maze maze)
        {
            return 2*(maze ?? throw new ArgumentNullException(nameof(maze))).Height + 1;
        }

        /// <summary>
        ///     Builds a grid the right size for a maze, two screen columns per cell so the corridors come out roughly
        ///     square instead of tall and thin.
        /// </summary>
        /// <param name="maze">The maze the grid has to hold.</param>
        /// <returns>An empty grid of the right shape.</returns>
        public static TextGrid CreateGrid(Maze maze)
        {
            return new TextGrid(GridWidth(maze), GridHeight(maze)) {CellWidth = 2};
        }

        /// <summary>
        ///     Repaints the whole grid from the maze. Called on every move, into the same grid every time — the same
        ///     bargain <see cref="MissileCommand.MissileFieldArt" /> strikes with its pixel buffer, and for the same
        ///     reason: the picture is small, the allocation is not, and a maze redraws only when a key is pressed.
        ///     <para>
        ///         <b>The clear is what makes this a full repaint rather than an incremental one</b>, and it is worth
        ///         being clear about why, because within a single maze it does nothing: no cell is ever un-seen and no
        ///         wall ever opens, so every cell painted last time is painted again this time. Deleting it survives
        ///         every test about a game in progress. What it is actually for is that the grid belongs to the
        ///         caller, so the same buffer can be handed back holding a <i>different</i> maze — which is precisely
        ///         what starting a new one does — and half of the old maze showing through the new one is the kind of
        ///         bug that looks like a corrupted save.
        ///     </para>
        /// </summary>
        /// <param name="grid">The grid to draw into, sized by <see cref="CreateGrid" />.</param>
        /// <param name="maze">What to draw.</param>
        public static void Paint(TextGrid grid, Maze maze)
        {
            if (grid == null)
                throw new ArgumentNullException(nameof(grid));

            if (maze == null)
                throw new ArgumentNullException(nameof(maze));

            grid.Clear();

            for (var y = 0; y < maze.Height; y++)
            for (var x = 0; x < maze.Width; x++)
            {
                if (!maze.HasSeen(x, y))
                    continue;

                var gx = 2*x + 1;
                var gy = 2*y + 1;

                // The four corners of this cell are wall no matter what, which is what keeps the diagonal joints
                // solid where two open corridors cross - miss them and the maze grows holes at every junction.
                grid.Set(gx - 1, gy - 1, WallGlyph, _wallStyle);
                grid.Set(gx + 1, gy - 1, WallGlyph, _wallStyle);
                grid.Set(gx - 1, gy + 1, WallGlyph, _wallStyle);
                grid.Set(gx + 1, gy + 1, WallGlyph, _wallStyle);

                PaintSide(grid, maze, x, y, DirectionEnum.Up, gx, gy - 1);
                PaintSide(grid, maze, x, y, DirectionEnum.Down, gx, gy + 1);
                PaintSide(grid, maze, x, y, DirectionEnum.Left, gx - 1, gy);
                PaintSide(grid, maze, x, y, DirectionEnum.Right, gx + 1, gy);

                if (maze.HasWalked(x, y))
                    grid.Set(gx, gy, TrailGlyph, _trailStyle);
            }

            // Painted last and in this order, so that standing on the exit shows the player rather than the door.
            if (maze.HasSeen(maze.StartX, maze.StartY))
                grid.Set(2*maze.StartX + 1, 2*maze.StartY + 1, StartGlyph, _startStyle);

            if (maze.HasSeen(maze.ExitX, maze.ExitY))
                grid.Set(2*maze.ExitX + 1, 2*maze.ExitY + 1, ExitGlyph, _exitStyle);

            grid.Set(PlayerColumn(maze), PlayerRow(maze), PlayerGlyph, _playerStyle);
        }

        /// <summary>
        ///     Where to draw the player, in grid cells.
        ///     <para>
        ///         <b>Once they are out there is no maze cell to draw them on</b>, because their position has left
        ///         the grid entirely — so they are drawn standing in the doorway, which is a <i>wall</i> square one
        ///         step outward from the exit cell's centre rather than a cell square. The arithmetic does not line
        ///         up on its own: a cell at −1 maps to grid column −1, while the gap it walked through is grid column
        ///         0, and the difference is the one square of wall between them.
        ///     </para>
        /// </summary>
        /// <param name="maze">The maze being drawn.</param>
        /// <returns>The grid column to put the player glyph in.</returns>
        private static int PlayerColumn(Maze maze)
        {
            return maze.IsSolved
                ? 2*maze.ExitX + 1 + Outward(maze.ExitSide).X
                : 2*maze.PlayerX + 1;
        }

        /// <summary>Where to draw the player, in grid rows. See <see cref="PlayerColumn" />.</summary>
        /// <param name="maze">The maze being drawn.</param>
        /// <returns>The grid row to put the player glyph in.</returns>
        private static int PlayerRow(Maze maze)
        {
            return maze.IsSolved
                ? 2*maze.ExitY + 1 + Outward(maze.ExitSide).Y
                : 2*maze.PlayerY + 1;
        }

        /// <summary>One step in a direction, in grid cells.</summary>
        /// <param name="side">Which way.</param>
        /// <returns>The offset.</returns>
        private static (int X, int Y) Outward(DirectionEnum side)
        {
            return side switch
            {
                DirectionEnum.Up => (0, -1),
                DirectionEnum.Down => (0, 1),
                DirectionEnum.Left => (-1, 0),
                DirectionEnum.Right => (1, 0),
                _ => (0, 0)
            };
        }

        /// <summary>
        ///     Draws one side of a cell: stone where there is a wall, and nothing at all where there is a way
        ///     through.
        ///     <para>
        ///         This is also what cuts the doorway on screen, with no special case: the exit cell's outward side
        ///         is genuinely open, so the same line that draws a corridor mouth draws the gap in the outer wall.
        ///     </para>
        /// </summary>
        private static void PaintSide(TextGrid grid, Maze maze, int x, int y, DirectionEnum side, int gx, int gy)
        {
            if (maze.IsOpen(x, y, side))
                grid.Set(gx, gy, ' ');
            else
                grid.Set(gx, gy, WallGlyph, _wallStyle);
        }
    }
}
