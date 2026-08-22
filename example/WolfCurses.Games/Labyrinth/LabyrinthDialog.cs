// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

using System;
using System.Globalization;
using System.Text;
using WolfCurses.Graphics;
using WolfCurses.Window;
using WolfCurses.Window.Control;
using WolfCurses.Window.Form;

namespace WolfCurses.Games.Labyrinth
{
    /// <summary>
    ///     Labyrinth: find the way out of a maze too big to see, carrying a torch that only reaches down the corridor
    ///     you are standing in.
    ///     <para>
    ///         <b>This is the first screen in the arcade whose world is larger than the terminal.</b> Snake, Tetris,
    ///         minesweeper and the chessboard all fit, by construction — their boards are constants chosen to fit 80
    ///         by 24. A maze that fits is a maze you solve by reading it, so this one does not, and the consequence is
    ///         a camera: <see cref="TextGrid.CenterOrigin" /> picks the window of the grid to show, following the
    ///         player and stopping at the edges rather than scrolling past the end of the world. That is the whole of
    ///         the feature, and it is the reason <see cref="TextGrid" /> ended up in the library instead of in this
    ///         folder.
    ///     </para>
    ///     <para>
    ///         <b>It is also the first game here with no clock at all.</b> Snake and Tetris are paced by an
    ///         <see cref="IntervalTimer" />, Missile Command integrates real elapsed time, minesweeper waits for a
    ///         typed line. This one is steered like the snake — <see cref="InputFillsBuffer" /> off, arrows straight
    ///         to <see cref="OnKeyPressed(ConsoleKey)" /> — and yet nothing whatever happens between key presses, so
    ///         there is no <c>OnTick</c> override on it at all and nothing to restart when a modal steals focus. Three
    ///         input styles and three pacing models across five games, and each one is a real choice rather than a
    ///         house style.
    ///     </para>
    ///     <para>
    ///         <b>The wheel looks around, and the question worth writing down is why that does not wreck the
    ///         game.</b> This is the one screen here built on not being able to see, so a camera the player can move
    ///         at will is exactly the sort of convenience that quietly deletes the difficulty. It does not, and the
    ///         reason is that the wheel moves the <i>window</i> while the torch decides what is <i>drawn</i>:
    ///         <see cref="MazeView" /> skips any cell <see cref="Maze.HasSeen" /> is false for, so scrolling over
    ///         unwalked ground shows blank. The camera cannot reveal a corridor because there is nothing painted
    ///         there to reveal. What it can reach is the ground the torch has <i>already lit</i> and the window is
    ///         too short to hold - the torch runs six cells down a corridor and an eighty-by-twenty-four terminal
    ///         shows about seven maze rows, so standing in a long north-south passage the player is being denied
    ///         sight of their own torchlight. That is a window problem, and the wheel is the fix for it.
    ///     </para>
    ///     <para>
    ///         <b>The peek is self-limiting: it ends at the next step, whether or not the step was possible.</b>
    ///         Nothing has to be pressed to get back, and there is no mode to be stuck in - which matters more here
    ///         than anywhere, since a player who has scrolled the view off themselves cannot see which way they are
    ///         about to walk. The wheel's one axis is the vertical, because that is the axis the terminal clips
    ///         hardest (seven maze rows of thirteen against nineteen columns of twenty-five) and because the library
    ///         refuses the sideways wheel outright rather than let a horizontal flick scroll something vertically.
    ///     </para>
    ///     <para>
    ///         <b>The frame does not breathe.</b> Every redraw asks the grid for exactly the same number of cells, and
    ///         <see cref="TextGrid" /> promises a full rectangle back however far off the edge that window hangs — so
    ///         the <see cref="Box" /> around it is the same size on every frame, even standing in a corner with three
    ///         quarters of the view outside the maze. A renderer that stopped at the edge instead would resize the box
    ///         on every step near a wall.
    ///     </para>
    /// </summary>
    [ParentWindow(typeof (GamesWindow))]
    public sealed class LabyrinthDialog : Form<GamesWindowInfo>
    {
        /// <summary>
        ///     Maze size in cells. Deliberately larger than a terminal in both directions once walls are counted —
        ///     51 by 27 characters, and 102 columns wide at two columns per cell — because a maze that fits on screen
        ///     is a picture of a maze rather than one to get lost in.
        /// </summary>
        private const int MazeWidth = 25;

        private const int MazeHeight = 13;

        /// <summary>
        ///     Rows the rest of the screen needs: the scene graph's own status line, this form's heading and hint, the
        ///     blank lines between them, the box border, and the input prompt the presenter reserves at the bottom.
        ///     Guess this low and the prompt falls off the bottom of the terminal, which is the bug that put
        ///     <c>MenuLayout</c> in the library.
        /// </summary>
        private const int ReservedRows = 10;

        /// <summary>Columns the box border and a little slack need.</summary>
        private const int ReservedColumns = 4;

        /// <summary>Smallest view worth drawing, for a terminal that reports something absurd.</summary>
        private const int MinimumViewColumns = 12;

        private const int MinimumViewRows = 5;

        /// <summary>
        ///     Columns held for the step counter. Four: the maze is 325 cells and has no loops, so even a player who
        ///     walked into every dead end and back would finish in three digits, and the fourth is room for somebody
        ///     pacing a corridor.
        /// </summary>
        private const int StepColumns = 4;

        /// <summary>Columns held for the explored reading, which runs "0%" to "100%" and so needs four.</summary>
        private const int ExploredColumns = 4;

        /// <summary>
        ///     Columns held for the compass reading. Twelve is <c>"N (step out)"</c>, the widest thing
        ///     <see cref="Compass" /> can say, against the two of the "NE" it says the rest of the time. It swings
        ///     further than either number on this line, so leaving it ragged would shuffle the session counter beside
        ///     it every time the player turned a corner.
        /// </summary>
        private const int CompassColumns = 12;

        /// <summary>The frame around the view, the same widget the snake and the minefield play inside.</summary>
        private readonly Box _frame = new() {Title = "Labyrinth", Padding = 0};

        private Maze _maze;
        private TextGrid _grid;
        private string _rendered;
        private string _message;
        private bool _solved;

        /// <summary>
        ///     Where the camera is looking, in grid cells. Follows the player while the maze is unsolved and becomes
        ///     free to move once it is — the arrows have nothing left to steer at that point, so they become a way to
        ///     look around the maze you were just lost in.
        /// </summary>
        private int _focusX;

        private int _focusY;

        /// <summary>
        ///     Whether the wheel has taken the camera off the player and it has to be put back on the next step.
        ///     <para>
        ///         Stored rather than derived from comparing the focus against the player's position, because those
        ///         two are legitimately unequal without anybody having peeked: the last move of a winning game steps
        ///         out of the grid, and the focus is clamped back on to the doorway. A flag says what happened; the
        ///         comparison would only say what the numbers are.
        ///     </para>
        /// </summary>
        private bool _peeking;

        /// <summary>Initializes a new instance of the <see cref="LabyrinthDialog" /> class.</summary>
        /// <param name="window">The parent window.</param>
        // ReSharper disable once UnusedMember.Global
        public LabyrinthDialog(IWindow window) : base(window)
        {
        }

        /// <summary>
        ///     Keeps typed characters out of the input buffer, because this game is steered rather than typed at.
        ///     ENTER still arrives at <see cref="OnInputBufferReturned" /> — it is buffer control rather than buffer
        ///     content — which is what lets ENTER quit even though nothing can be typed.
        /// </summary>
        public override bool InputFillsBuffer => false;

        /// <inheritdoc />
        public override void OnFormPostCreate()
        {
            base.OnFormPostCreate();

            StartNewMaze();
        }

        /// <inheritdoc />
        public override string OnRenderForm()
        {
            // Called on every system tick, roughly a thousand times a second. Nothing here moves on its own, so the
            // string is built when a key is pressed and simply handed back.
            return _rendered;
        }

        /// <inheritdoc />
        public override void OnKeyPressed(ConsoleKey key)
        {
            base.OnKeyPressed(key);

            if (key == ConsoleKey.R)
            {
                StartNewMaze();
                return;
            }

            var pushed = key switch
            {
                ConsoleKey.UpArrow or ConsoleKey.W => DirectionEnum.Up,
                ConsoleKey.DownArrow or ConsoleKey.S => DirectionEnum.Down,
                ConsoleKey.LeftArrow or ConsoleKey.A => DirectionEnum.Left,
                ConsoleKey.RightArrow or ConsoleKey.D => DirectionEnum.Right,
                _ => DirectionEnum.None
            };

            if (pushed == DirectionEnum.None)
                return;

            // Once the maze is solved the arrows stop being movement and become the camera, so the whole thing can be
            // looked over rather than only the corner the player happened to escape from.
            if (_solved)
            {
                Pan(pushed);
                _rendered = Compose();
                return;
            }

            var walked = _maze.TryMove(pushed);

            // A refused move costs nothing, exactly as it always did - unless the wheel has taken the view off the
            // player, in which case even a bump into a wall puts the camera back. Trying to walk is what ends a
            // peek, not succeeding at it: a player steering a screen they cannot see themselves on is the one state
            // this must not be able to sit in, and the way out of it should be whichever key they were going to
            // press anyway.
            if (!walked && !_peeking)
                return;

            Recenter();

            if (_maze.IsSolved)
                Escaped();

            _rendered = Compose();
        }

        /// <summary>
        ///     The wheel looks up and down the maze without walking it.
        ///     <para>
        ///         <b>It moves the window, never the torch.</b> Only cells the player has seen are painted at all,
        ///         so scrolling over ground they have not walked shows blank - the camera has nothing to give away
        ///         because nothing is drawn there. What it reaches is the lit ground the window is too small to
        ///         hold, which on an eighty-by-twenty-four terminal is most of a torch's own reach down a long
        ///         corridor.
        ///     </para>
        ///     <para>
        ///         <b>The clamp is the whole of the bound</b>, so a flick reporting several notches at once simply
        ///         parks at the end of the maze rather than counting off into space - which is where "as far as it
        ///         goes" should land, and means nothing here has to guess how many notches a platform batches into
        ///         one event.
        ///     </para>
        /// </summary>
        /// <param name="mouse">What the mouse did, and where.</param>
        public override void OnMouseEvent(MouseEvent mouse)
        {
            base.OnMouseEvent(mouse);

            if (mouse.Kind != MouseEventKindEnum.Wheel || mouse.WheelDelta == 0)
                return;

            // Positive is away from the user, which every platform treats as scrolling up, and up here is north. Two
            // grid rows to the notch because a maze cell is two rows of characters, the same step the arrows pan by.
            _focusY -= 2*mouse.WheelDelta;
            ClampFocus();

            // Once the maze is solved the arrows are already the camera and there is no player left to follow, so
            // there is nothing for the next key press to put back. Steered by this form's own flag rather than by
            // the maze's, the same one the arrow branch above reads, so the two ways in cannot come to disagree
            // about which mode the screen is in.
            _peeking = !_solved;
            _rendered = Compose();
        }

        /// <inheritdoc />
        public override void OnInputBufferReturned(string input)
        {
            ClearForm();
        }

        /// <summary>Digs a fresh maze and puts the player back at the middle of it.</summary>
        private void StartNewMaze()
        {
            _maze = new Maze(MazeWidth, MazeHeight, SimUnit.Random);
            _grid = MazeView.CreateGrid(_maze);
            _solved = false;

            // The controls live on the window's prompt rather than in this message, and the message is kept short
            // enough to sit on one line of an eighty-column terminal. Both matter: the prompt is a row the scene
            // graph draws anyway, so putting the controls there costs nothing, while a message that wrapped would
            // cost a row this screen has already spent - it fits 80x24 with none to spare.
            ParentWindow.PromptText = "Arrows or WASD to move, wheel to look, R for a new maze, ENTER or ESC quits";
            _message = "Find the way out. The compass gives the direction, not the route.";
            Recenter();
            _rendered = Compose();
        }

        /// <summary>
        ///     Puts the camera back on the player and ends any peek.
        ///     <para>
        ///         Clamped rather than assumed to be on the grid: the last move of a winning game steps OUT, so the
        ///         player's position is off the maze by one cell and the doubled coordinate lands one past either
        ///         end. Clamping puts the camera on the doorway, which is exactly where the player now is - the
        ///         arithmetic works out because the wall ring is the one square between the last cell and the
        ///         outside.
        ///     </para>
        /// </summary>
        private void Recenter()
        {
            _focusX = 2*_maze.PlayerX + 1;
            _focusY = 2*_maze.PlayerY + 1;
            _peeking = false;
            ClampFocus();
        }

        /// <summary>
        ///     Holds the camera inside the grid, whichever moved it.
        ///     <para>
        ///         <c>CenterOrigin</c> clamps the drawn window either way, so this is not about what appears on
        ///         screen: an unclamped focus would keep counting off into the distance and the view would then sit
        ///         still for however many presses or notches it took to walk back.
        ///     </para>
        /// </summary>
        private void ClampFocus()
        {
            _focusX = Math.Clamp(_focusX, 0, _grid.Width - 1);
            _focusY = Math.Clamp(_focusY, 0, _grid.Height - 1);
        }

        /// <summary>Records the escape, lights the whole maze, and says how well it went.</summary>
        private void Escaped()
        {
            _solved = true;
            UserData.LabyrinthMazesEscaped++;
            _maze.RevealAll();

            // Against the shortest route rather than against a par: the maze has no loops, so there is exactly one
            // shortest way out and the ratio means something. Walking it perfectly is 100%, and wandering the whole
            // maze first is a number the player can argue with.
            var efficiency = _maze.Steps > 0 ? 100.0*_maze.ShortestSteps/_maze.Steps : 100.0;

            _message = $"Out in {_maze.Steps} steps - the shortest way was {_maze.ShortestSteps} " +
                       $"({efficiency.ToString("F0", CultureInfo.InvariantCulture)}% efficient).";
            ParentWindow.PromptText = "Arrows or wheel look around the maze, R for another, ENTER or ESC quits";
        }

        /// <summary>Moves the camera by one maze cell, which is two grid cells.</summary>
        /// <param name="direction">Which way the player pushed.</param>
        private void Pan(DirectionEnum direction)
        {
            switch (direction)
            {
                case DirectionEnum.Up:
                    _focusY -= 2;
                    break;
                case DirectionEnum.Down:
                    _focusY += 2;
                    break;
                case DirectionEnum.Left:
                    _focusX -= 2;
                    break;
                case DirectionEnum.Right:
                    _focusX += 2;
                    break;
            }

            // Held inside the grid so that panning to an edge and back lands where it started - see ClampFocus,
            // which the wheel goes through as well so the two ways of moving the camera cannot come to disagree
            // about where the ends of the maze are.
            ClampFocus();
        }

        /// <summary>Draws the heading, the view inside its frame, and whatever the maze has to say underneath.</summary>
        private string Compose()
        {
            MazeView.Paint(_grid, _maze);

            // One syscall each, read into a local. Sampled per redraw rather than once at start-up so that resizing
            // the terminal and pressing a key puts the view right, which is as much as a game with no clock can do
            // about a resize.
            var consoleWidth = AnsiConsole.SafeWindowWidth();
            var consoleHeight = AnsiConsole.SafeWindowHeight();

            var columns = Math.Clamp((consoleWidth - ReservedColumns)/_grid.CellWidth, MinimumViewColumns, _grid.Width);
            var rows = Math.Clamp(consoleHeight - ReservedRows, MinimumViewRows, _grid.Height);

            var view = _grid.Render(
                TextGrid.CenterOrigin(_focusX, columns, _grid.Width),
                TextGrid.CenterOrigin(_focusY, rows, _grid.Height),
                columns,
                rows);

            var body = new StringBuilder();
            body.AppendLine();
            body.AppendLine(Heading());
            body.AppendLine();
            body.AppendLine(_frame.Render(view));
            body.AppendLine();
            body.Append(_message);
            return body.ToString();
        }

        /// <summary>The status line: how far they have walked, how much they have seen, and which way the exit lies.</summary>
        private string Heading()
        {
            // The percent sign travels with the number rather than sitting after the padding, or a low reading draws
            // as "Explored 5   %" with the sign adrift from what it belongs to.
            var explored = string.Create(CultureInfo.InvariantCulture,
                $"{100*_maze.SeenCount/(_maze.Width*_maze.Height)}%");

            // Fixed-width fields, because this heading sits directly above a box whose whole claim is that it does
            // not move - the frame is the same size in a corner as it is in the middle, and a heading shuffling over
            // it on every step would undo that by eye. All three fields grow as the maze is walked: the steps through
            // 9, 99 and 999, the reading through 9% and 99%, and the compass from "N" to "N (step out)". The session
            // counter is left ragged deliberately, being last on the line with nothing to its right to displace.
            //
            // Plain composite-format widths rather than AnsiText.Fit: nothing here is styled, so there are no escape
            // bytes for a plain width to miscount, and a width only ever pads where Fit would trim - a value that
            // outgrew its field pushes the line along as it does today rather than losing a digit. At its widest this
            // comes to 77 columns, inside the eighty this screen is built for and checked by the fits-80x24 test.
            return string.Create(CultureInfo.InvariantCulture,
                $"Steps {_maze.Steps,-StepColumns}    Explored {explored,-ExploredColumns}    " +
                $"Exit {Compass(),-CompassColumns}    Escaped this session: {UserData.LabyrinthMazesEscaped}");
        }

        /// <summary>
        ///     Which way the exit lies, to the eight points of the compass.
        ///     <para>
        ///         This is the only help the game gives, and it is deliberately the kind that cannot be followed: it
        ///         names a direction through the walls, and the maze decides which turns actually get you there.
        ///         Without it a 325-cell maze seen six cells at a time is a search rather than a game; with a route
        ///         hint instead it would not be a maze at all.
        ///     </para>
        /// </summary>
        /// <returns>A compass point, or where they are standing.</returns>
        private string Compass()
        {
            if (_maze.IsSolved)
                return "out";

            var dx = _maze.ExitX - _maze.PlayerX;
            var dy = _maze.ExitY - _maze.PlayerY;

            // Standing on the exit cell itself, which stopped being the winning square when the doorway was cut and
            // is now the last one before it. Pointing at the door is both the only useful answer and the only one
            // the arithmetic below cannot give, since the offset to the exit is zero from here.
            if (dx == 0 && dy == 0)
                return Point(_maze.ExitSide) + " (step out)";

            var acrossness = Math.Abs(dx);
            var downness = Math.Abs(dy);

            // The minor axis is dropped once the major one is more than twice it, so "north east" means genuinely
            // diagonal rather than "north, and also one cell east".
            var vertical = downness*2 < acrossness ? string.Empty : dy < 0 ? "N" : dy > 0 ? "S" : string.Empty;
            var horizontal = acrossness*2 < downness ? string.Empty : dx > 0 ? "E" : dx < 0 ? "W" : string.Empty;

            // Both halves can only come out empty when the offsets are both zero, which the guard above already
            // took - so this is unreachable, and is here because an empty string would read as a missing status
            // line rather than as a bug, which is the worst way to find one.
            var compass = vertical + horizontal;
            return compass.Length == 0 ? "here" : compass;
        }

        /// <summary>The compass letter for one direction.</summary>
        /// <param name="direction">Which way.</param>
        /// <returns>N, S, E or W.</returns>
        private static string Point(DirectionEnum direction)
        {
            return direction switch
            {
                DirectionEnum.Up => "N",
                DirectionEnum.Down => "S",
                DirectionEnum.Left => "W",
                DirectionEnum.Right => "E",
                _ => "?"
            };
        }
    }
}
