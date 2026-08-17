// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

using System;
using System.Collections.Generic;
using WolfCurses.Core;

namespace WolfCurses.Games.PacMan
{
    /// <summary>
    ///     The whole game: the player, the four ghosts, the score and the rhythm they all move to. No console
    ///     anywhere — <see cref="PacManDialog" /> draws it and this decides what happens.
    ///     <para>
    ///         <b>Everything here is counted in steps rather than measured in seconds</b>, and that is deliberate.
    ///         The form calls <see cref="Step" /> on a timer, so the rules never touch a clock, never drift, and can
    ///         be driven a thousand steps deep in a unit test with no <c>Sleep</c> anywhere — which is the only reason
    ///         the ghost behaviour below is testable at all. The step is about a ninth of a second; the durations are
    ///         written as steps with the arcade's seconds beside them.
    ///     </para>
    ///     <para>
    ///         <b>Speed is expressed as how many steps a mover gets, not as a fraction of a square.</b> A frightened
    ///         ghost moves every other step and a pair of eyes moves twice per step, which is how the original feels
    ///         without anything here needing a position between two squares. The whole game is on the lattice, which
    ///         is what keeps collision detection down to comparing two pairs of integers.
    ///     </para>
    /// </summary>
    public sealed class PacManGame
    {
        /// <summary>
        ///     How long the board spends scattering and chasing, alternately, in steps. Roughly 7, 20, 7, 20, 5, 20, 5
        ///     seconds and then the hunt never stops — the arcade's own table, and the reason the early part of a
        ///     level has a rhythm and the late part does not.
        /// </summary>
        private static readonly int[] _modeSchedule = {60, 180, 60, 180, 45, 180, 45};

        /// <summary>How long a power pellet lasts, in steps — about seven seconds, shortening as the levels go up.</summary>
        private const int FrightenedSteps = 62;

        /// <summary>How long the board sits still at the start of a life, so the player can see where everyone is.</summary>
        private const int ReadySteps = 9;

        /// <summary>What the pellets are worth.</summary>
        private const int PelletScore = 10;

        private const int PowerPelletScore = 50;

        /// <summary>The first ghost of a chain is worth this, and it doubles for each one after it.</summary>
        private const int FirstGhostScore = 200;

        /// <summary>A free life, once.</summary>
        private const int ExtraLifeAt = 10_000;

        private readonly Randomizer _random;
        private readonly List<Ghost> _ghosts = new();

        private DirectionEnum _pending;
        private int _modeIndex;
        private int _modeSteps;
        private int _frightenedLeft;
        private int _ghostChain;
        private int _stepsThisLife;
        private int _readyLeft;
        private bool _extraLifeAwarded;

        /// <summary>Initializes a new instance of the <see cref="PacManGame" /> class with a full board.</summary>
        /// <param name="random">The simulation's shared random source, used only by frightened ghosts.</param>
        public PacManGame(Randomizer random)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
            Maze = new PacManMaze();

            // The corners are outside the board on purpose - a target that can never be reached is what makes a
            // scattering ghost circle its corner rather than park on it.
            var starts = Maze.GhostStarts;
            _ghosts.Add(new Ghost(GhostKindEnum.Blinky, Maze, Maze.DoorX, Maze.DoorY - 1, Maze.Width - 2, -3, 0));
            _ghosts.Add(new Ghost(GhostKindEnum.Pinky, Maze, starts[starts.Count / 2].X, starts[0].Y, 2, -3, 12));
            _ghosts.Add(new Ghost(GhostKindEnum.Inky, Maze, starts[0].X, starts[0].Y, Maze.Width - 2, Maze.Height + 2, 40));
            _ghosts.Add(new Ghost(GhostKindEnum.Clyde, Maze, starts[starts.Count - 1].X, starts[0].Y, 2, Maze.Height + 2, 80));

            Lives = 3;
            Level = 1;
            StartLife();

            // Blinky is never penned - it is out from the first step, which is what stops the opening of a level
            // being a free run around an empty board.
            GhostOf(GhostKindEnum.Blinky).Release();
        }

        /// <summary>The board.</summary>
        public PacManMaze Maze { get; }

        /// <summary>The ghosts, in the order they were introduced.</summary>
        public IReadOnlyList<Ghost> Ghosts => _ghosts;

        /// <summary>Where the player is.</summary>
        public int PacManX { get; private set; }

        /// <summary>Where the player is.</summary>
        public int PacManY { get; private set; }

        /// <summary>Which way the player is travelling, which is what Pinky and Inky aim in front of.</summary>
        public DirectionEnum Facing { get; private set; }

        /// <summary>What the board as a whole is doing.</summary>
        public GhostModeEnum Mode { get; private set; }

        /// <summary>How many steps of blue are left, or zero.</summary>
        public int FrightenedLeft => _frightenedLeft;

        /// <summary>How long a power pellet lasts at this level, so the bar underneath it has a scale.</summary>
        public int FrightenedLength => Math.Max(12, FrightenedSteps - 6*(Level - 1));

        /// <summary>The score.</summary>
        public int Score { get; private set; }

        /// <summary>How many lives are left, the current one included.</summary>
        public int Lives { get; private set; }

        /// <summary>Which board this is; they get faster and the blue gets shorter.</summary>
        public int Level { get; private set; }

        /// <summary>True once the last life is gone.</summary>
        public bool IsOver { get; private set; }

        /// <summary>True while the board is showing everyone their starting places before a life begins.</summary>
        public bool IsReady => _readyLeft > 0;

        /// <summary>How many pellets have been eaten on this board, for the progress readout.</summary>
        public int PelletsEaten => Maze.TotalPellets - Maze.PelletsLeft;

        /// <summary>The ghost of a given kind.</summary>
        /// <param name="kind">Which one.</param>
        /// <returns>That ghost.</returns>
        public Ghost GhostOf(GhostKindEnum kind)
        {
            return _ghosts[(int) kind];
        }

        /// <summary>
        ///     Points the player somewhere.
        ///     <para>
        ///         Held as a <i>wish</i> rather than applied, and retried on every step until it becomes legal. That
        ///         is the whole feel of the controls: pressing up a moment before the corner turns you at the corner,
        ///         rather than being ignored for having been early. Without it the game feels like it is dropping
        ///         inputs, which is exactly what it would be doing.
        ///     </para>
        /// </summary>
        /// <param name="direction">Where the player wants to go.</param>
        public void Steer(DirectionEnum direction)
        {
            if (direction != DirectionEnum.None)
                _pending = direction;
        }

        /// <summary>Moves everything one step, in the order the arcade does: player, then board, then ghosts.</summary>
        public void Step()
        {
            if (IsOver)
                return;

            if (_readyLeft > 0)
            {
                _readyLeft--;
                return;
            }

            _stepsThisLife++;

            MovePacMan();

            if (CheckCollisions())
                return;

            AdvanceMode();
            ReleaseGhosts();
            MoveGhosts();

            CheckCollisions();

            if (Maze.PelletsLeft == 0)
                NextLevel();
        }

        /// <summary>Takes the player one square, turning first if the way they asked for has opened up.</summary>
        private void MovePacMan()
        {
            if (CanWalk(_pending))
                Facing = _pending;

            if (!CanWalk(Facing))
                return;

            var (dx, dy) = Offset(Facing);
            PacManX = Maze.WrapX(PacManX + dx);
            PacManY += dy;

            switch (Maze.Eat(PacManX, PacManY))
            {
                case PelletEnum.Pellet:
                    Award(PelletScore);
                    break;
                case PelletEnum.Power:
                    Award(PowerPelletScore);
                    _frightenedLeft = FrightenedLength;
                    _ghostChain = 0;
                    foreach (var ghost in _ghosts)
                        ghost.Frighten();

                    break;
            }
        }

        /// <summary>Whether the player could move that way from where they are standing.</summary>
        private bool CanWalk(DirectionEnum direction)
        {
            if (direction == DirectionEnum.None)
                return false;

            var (dx, dy) = Offset(direction);
            return Maze.CanEnter(Maze.WrapX(PacManX + dx), PacManY + dy, false);
        }

        /// <summary>Runs the scatter/chase clock and turns everyone round when it ticks over.</summary>
        private void AdvanceMode()
        {
            if (_frightenedLeft > 0)
            {
                _frightenedLeft--;
                if (_frightenedLeft == 0)
                {
                    foreach (var ghost in _ghosts)
                        ghost.Calm();
                }
            }

            // The last entry never expires: past the end of the table the hunt simply does not stop, which is what
            // makes a long level get harder rather than staying on a loop.
            if (_modeIndex >= _modeSchedule.Length)
                return;

            _modeSteps++;
            if (_modeSteps < _modeSchedule[_modeIndex])
                return;

            _modeSteps = 0;
            _modeIndex++;
            Mode = Mode == GhostModeEnum.Scatter ? GhostModeEnum.Chase : GhostModeEnum.Scatter;

            // Reversing on a mode change is the only announcement the player gets that the rhythm has shifted, and
            // the ban on reversing everywhere else is what makes it legible.
            foreach (var ghost in _ghosts)
                ghost.Reverse();
        }

        /// <summary>Lets ghosts out of the house as their turn comes round.</summary>
        private void ReleaseGhosts()
        {
            foreach (var ghost in _ghosts)
            {
                if (ghost.Penned && _stepsThisLife >= ghost.ReleaseAfter)
                    ghost.Release();
            }
        }

        /// <summary>
        ///     Moves the ghosts, each as many squares as its state allows. Frightened ghosts move every other step
        ///     and eyes move twice, which is the entire speed system.
        /// </summary>
        private void MoveGhosts()
        {
            foreach (var ghost in _ghosts)
            {
                var moves = ghost.State switch
                {
                    GhostStateEnum.Frightened => _stepsThisLife % 2 == 0 ? 1 : 0,
                    GhostStateEnum.Eaten => 2,
                    _ => 1
                };

                for (var move = 0; move < moves; move++)
                    ghost.Step(this, _random);
            }
        }

        /// <summary>
        ///     Works out who ran into whom.
        ///     <para>
        ///         Called <b>twice</b> per step, once after the player moves and once after the ghosts do, and that
        ///         is not belt and braces: on a lattice two things travelling toward each other swap squares without
        ///         ever sharing one, so a single check at the end of the step lets the player walk clean through a
        ///         ghost. Checking before the ghosts move catches the player walking into them; checking after
        ///         catches them walking into the player.
        ///     </para>
        /// </summary>
        /// <returns>True when a life was lost, which ends the step.</returns>
        private bool CheckCollisions()
        {
            foreach (var ghost in _ghosts)
            {
                if (ghost.X != PacManX || ghost.Y != PacManY)
                    continue;

                if (ghost.State == GhostStateEnum.Eaten)
                    continue;

                if (ghost.State == GhostStateEnum.Frightened)
                {
                    _ghostChain++;
                    Award(FirstGhostScore*(1 << Math.Min(3, _ghostChain - 1)));
                    ghost.Eat();
                    continue;
                }

                LoseLife();
                return true;
            }

            return false;
        }

        /// <summary>Adds to the score, and hands out the one free life on the way past.</summary>
        private void Award(int points)
        {
            Score += points;

            if (_extraLifeAwarded || Score < ExtraLifeAt)
                return;

            _extraLifeAwarded = true;
            Lives++;
        }

        /// <summary>Takes a life and puts everyone back, or ends the game.</summary>
        private void LoseLife()
        {
            Lives--;

            if (Lives <= 0)
            {
                Lives = 0;
                IsOver = true;
                return;
            }

            StartLife();
        }

        /// <summary>Clears the board and starts the next one, keeping the score and the lives.</summary>
        private void NextLevel()
        {
            Level++;
            Maze.Refill();
            StartLife();
        }

        /// <summary>Puts the player and the ghosts back where they start, and holds everything for a moment.</summary>
        private void StartLife()
        {
            PlacePlayer(Maze.PacManStart.X, Maze.PacManStart.Y, DirectionEnum.Left);

            Mode = GhostModeEnum.Scatter;
            _modeIndex = 0;
            _modeSteps = 0;
            _frightenedLeft = 0;
            _ghostChain = 0;
            _stepsThisLife = 0;
            _readyLeft = ReadySteps;

            foreach (var ghost in _ghosts)
                ghost.Reset();

            GhostOf(GhostKindEnum.Blinky).Release();
        }

        /// <summary>
        ///     Puts the player on a square, facing a way, with that way also queued. Used by <see cref="StartLife" />
        ///     and by tests that need an exact situation rather than one they had to play their way into.
        /// </summary>
        /// <param name="x">Where to put them.</param>
        /// <param name="y">Where to put them.</param>
        /// <param name="facing">Which way they are travelling.</param>
        internal void PlacePlayer(int x, int y, DirectionEnum facing)
        {
            PacManX = x;
            PacManY = y;
            Facing = facing;
            _pending = facing;
        }

        /// <summary>How far one step in a direction moves.</summary>
        private static (int X, int Y) Offset(DirectionEnum direction)
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
    }
}
