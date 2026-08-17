// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/16/2026

using WolfCurses.Utility;

namespace WolfCurses.Games
{
    /// <summary>
    ///     The arcade menu. Each value becomes a numbered choice on <see cref="GamesWindow" />, and the
    ///     <see cref="DescriptionAttribute" /> is the line the player reads.
    /// </summary>
    public enum GamesCommandsEnum
    {
        /// <summary>
        ///     Steered in real time with the arrow keys, paced by a stopwatch off the system tick. The game that shows
        ///     what a key press and a frame budget look like in this library.
        /// </summary>
        [Description("Snake - arrow keys, real time.")] Snake = 1,

        /// <summary>
        ///     Played by typing coordinates rather than steering, so it is the other half of the input story: the
        ///     command buffer, parsed by the form itself.
        /// </summary>
        [Description("Minesweeper - type a square, like B4.")] Minesweeper = 2,

        /// <summary>
        ///     Steered like the snake, but the first screen here that puts two things side by side — which is a
        ///     harder problem than it looks once the left column is full of color escapes.
        /// </summary>
        [Description("Tetris - arrows to move, SPACE to drop.")] Tetris = 3,

        /// <summary>
        ///     The one game here that draws real pixels: the board is composited from piece artwork and rendered
        ///     through the graphics stack, with a bot that thinks in slices so the screen keeps moving.
        /// </summary>
        [Description("WolfChess 5000 - chess, with graphics and a bot.")] Chess = 4,

        /// <summary>
        ///     The only game here whose board is not a grid: a continuous ballistics field, drawn as a picture the
        ///     library <i>generates</i> rather than decodes, moving by elapsed time rather than by step. It is also
        ///     where a terminal's missing key-up event stops being a curiosity and starts being the design problem.
        /// </summary>
        [Description("Missile Command - arrows aim, SPACE fires.")] MissileCommand = 5,

        /// <summary>
        ///     The only game here whose world is bigger than the terminal, which is what makes it the one that needs a
        ///     camera. Also the only one with no clock at all: it is steered, and yet nothing happens until a key is
        ///     pressed.
        /// </summary>
        [Description("Labyrinth - escape a maze bigger than the screen.")] Labyrinth = 6,

        /// <summary>Closes the arcade and returns to the operating system.</summary>
        [Description("Quit.")] Quit = 7
    }
}
