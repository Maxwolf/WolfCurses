// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

using System;
using WolfCurses.Graphics;
using WolfCurses.Window.Control;

namespace WolfCurses.Games.PacMan
{
    /// <summary>
    ///     Paints a <see cref="PacManGame" /> into a <see cref="TextGrid" />.
    ///     <para>
    ///         <b>The walls are drawn as a network of connected lines, not as blocks</b>, which is what makes the
    ///         board look like the arcade cabinet rather than like a block-pushing puzzle. Each wall cell asks
    ///         <see cref="BoxDrawing.Junction" /> which glyph joins the neighbours it actually has, so a corner comes
    ///         out as a corner and a crossroads as a crossroads with nothing in this file knowing what shape the maze
    ///         is. It is also why <see cref="PacManMaze" /> forbids a wall two cells thick: a line renderer draws the
    ///         inside of a thick wall as a lattice of crossings.
    ///     </para>
    ///     <para>
    ///         <b>Every kind of thing has its own glyph as well as its own colour</b> — the lesson
    ///         <see cref="Chess.ChessTextBoard" />, <see cref="MissileCommand.MissileFieldText" /> and
    ///         <see cref="Labyrinth.MazeView" /> all record. Four ghosts in four colours become four identical
    ///         letters under <c>NO_COLOR</c>, which is survivable; a frightened ghost that looked exactly like a
    ///         hunting one would not be, so being edible is a change of <i>letter</i> and the colour is the garnish.
    ///     </para>
    /// </summary>
    public static class PacManView
    {
        private const char PelletGlyph = '·';
        private const char PowerGlyph = '●';
        private const char GhostGlyph = 'M';
        private const char FrightenedGlyph = 'm';
        private const char EyesGlyph = '"';
        private const char DoorGlyph = '─';

        private static readonly TextStyle _wallStyle = new(ConsoleColor.Blue);
        private static readonly TextStyle _doorStyle = new(ConsoleColor.Magenta);
        private static readonly TextStyle _pelletStyle = new(ConsoleColor.DarkYellow);
        private static readonly TextStyle _powerStyle = new(ConsoleColor.Yellow, bold: true);
        private static readonly TextStyle _pacManStyle = new(ConsoleColor.Yellow, bold: true);
        private static readonly TextStyle _eyesStyle = new(ConsoleColor.DarkGray);
        private static readonly TextStyle _frightenedStyle = new(ConsoleColor.White, bold: true);
        private static readonly TextStyle _flashingStyle = new(ConsoleColor.DarkBlue);
        private static readonly TextStyle _readyStyle = new(ConsoleColor.Yellow, bold: true);

        /// <summary>Builds a grid the right size for a board. One column per cell: the walls are lines, not slabs.</summary>
        /// <param name="game">The game the grid has to hold.</param>
        /// <returns>An empty grid of the right shape.</returns>
        public static TextGrid CreateGrid(PacManGame game)
        {
            if (game == null)
                throw new ArgumentNullException(nameof(game));

            return new TextGrid(game.Maze.Width, game.Maze.Height);
        }

        /// <summary>
        ///     Repaints the whole board, back to front: walls, then food, then the ghosts, then the player on top of
        ///     everything.
        /// </summary>
        /// <param name="grid">The grid to draw into, sized by <see cref="CreateGrid" />.</param>
        /// <param name="game">What to draw.</param>
        /// <param name="blink">
        ///     Whether things that blink are currently lit. Passed in rather than worked out here, because this class
        ///     has no clock and should not grow one — the form owns the timer that everything else is paced by.
        /// </param>
        public static void Paint(TextGrid grid, PacManGame game, bool blink)
        {
            if (grid == null)
                throw new ArgumentNullException(nameof(grid));

            if (game == null)
                throw new ArgumentNullException(nameof(game));

            var maze = game.Maze;
            grid.Clear();

            for (var y = 0; y < maze.Height; y++)
            for (var x = 0; x < maze.Width; x++)
            {
                if (maze.IsWall(x, y))
                {
                    // Only walls join to walls. The door is a gap in this network, which is what gives the ghost
                    // house a clean opening instead of a line drawn straight across it.
                    grid.Set(x, y, BoxDrawing.Junction(
                        Joins(maze, x, y - 1), Joins(maze, x, y + 1),
                        Joins(maze, x - 1, y), Joins(maze, x + 1, y),
                        BoxBorderEnum.Double), _wallStyle);
                }
                else if (maze.IsDoor(x, y))
                {
                    // Deliberately a SINGLE line among double ones: the door has to read as different from wall at a
                    // glance, and it is the one place on the board where the glyph alone carries a rule.
                    grid.Set(x, y, DoorGlyph, _doorStyle);
                }
                else if (maze.HasPowerPellet(x, y))
                {
                    if (blink)
                        grid.Set(x, y, PowerGlyph, _powerStyle);
                }
                else if (maze.HasPellet(x, y))
                {
                    grid.Set(x, y, PelletGlyph, _pelletStyle);
                }
            }

            foreach (var ghost in game.Ghosts)
                PaintGhost(grid, game, ghost, blink);

            grid.Set(game.PacManX, game.PacManY, PacManGlyph(game.Facing), _pacManStyle);

            if (game.IsReady)
            {
                // Two rows BELOW where the player stands, not above: above is the ghost house, and a caption written
                // across it covers the one thing the pause exists to let the player look at.
                const string ready = "READY!";
                grid.DrawText((maze.Width - ready.Length) / 2, maze.PacManStart.Y + 2, ready, _readyStyle);
            }
        }

        /// <summary>
        ///     Whether a cell is a wall that a neighbouring wall should draw a line into.
        ///     <para>
        ///         <b>Not the same question as <see cref="PacManMaze.IsWall" />, and the difference is a real bug
        ///         that shipped for one run.</b> The maze answers "wall" for everywhere off the board, which is
        ///         exactly right for movement — you cannot walk off the edge — and exactly wrong for drawing, because
        ///         it makes the whole outer border join to an imaginary wall beyond it and come out as a row of tees
        ///         pointing into space. For drawing, off the board is nothing at all.
        ///     </para>
        /// </summary>
        private static bool Joins(PacManMaze maze, int x, int y)
        {
            return maze.Contains(x, y) && maze.IsWall(x, y);
        }

        /// <summary>Draws one ghost, whose letter says what it is and whose colour says which it is.</summary>
        private static void PaintGhost(TextGrid grid, PacManGame game, Ghost ghost, bool blink)
        {
            switch (ghost.State)
            {
                case GhostStateEnum.Eaten:
                    grid.Set(ghost.X, ghost.Y, EyesGlyph, _eyesStyle);
                    break;

                case GhostStateEnum.Frightened:
                    // Flashing near the end is the warning the arcade gives, and it is worth having because the
                    // difference between four hundred points and a life is about a second and a half.
                    var expiring = game.FrightenedLeft <= 14 && !blink;
                    grid.Set(ghost.X, ghost.Y, FrightenedGlyph, expiring ? _flashingStyle : _frightenedStyle);
                    break;

                default:
                    grid.Set(ghost.X, ghost.Y, GhostGlyph, new TextStyle(ColorOf(ghost.Kind), bold: true));
                    break;
            }
        }

        /// <summary>Which way the player's mouth is pointing.</summary>
        /// <param name="facing">Which way they are travelling.</param>
        /// <returns>The glyph to draw.</returns>
        public static char PacManGlyph(DirectionEnum facing)
        {
            return facing switch
            {
                DirectionEnum.Up => '^',
                DirectionEnum.Down => 'v',
                DirectionEnum.Right => '>',
                _ => '<'
            };
        }

        /// <summary>The arcade's colour for each ghost, which is the only way to tell them apart at a glance.</summary>
        /// <param name="kind">Which ghost.</param>
        /// <returns>Its colour.</returns>
        public static ConsoleColor ColorOf(GhostKindEnum kind)
        {
            return kind switch
            {
                GhostKindEnum.Blinky => ConsoleColor.Red,
                GhostKindEnum.Pinky => ConsoleColor.Magenta,
                GhostKindEnum.Inky => ConsoleColor.Cyan,
                _ => ConsoleColor.DarkYellow
            };
        }
    }
}
