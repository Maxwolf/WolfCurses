// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

using System;
using System.Collections.Generic;
using WolfCurses.Core;

namespace WolfCurses.Games.PacMan
{
    /// <summary>
    ///     One ghost: where it is, what it is doing, and — the only part that differs between the four of them —
    ///     which square it is heading for.
    ///     <para>
    ///         <b>This class is the demonstration.</b> All four ghosts run the same seven lines of movement code in
    ///         <see cref="Step" />: look at the four neighbours, throw away walls and the way you came, and take
    ///         whichever of the rest is nearest to your target as the crow flies. Nothing coordinates them, nothing
    ///         plans, nothing searches, and there is no path-finding anywhere in this file. The behaviour that comes
    ///         out — being flanked, being herded away from a corner, the orange one losing its nerve — is
    ///         <i>entirely</i> a consequence of four different one-line answers to <see cref="Target" />. It is worth
    ///         reading beside <see cref="Chess.Bot.WolfChessBot" />, which is the opposite bargain: a real search,
    ///         sliced across ticks, to play one opponent well.
    ///     </para>
    ///     <para>
    ///         <b>Ghosts may not reverse.</b> That single restriction is what stops them oscillating in a corridor
    ///         forever, and it is also why the game reverses them deliberately when the mode changes — the reversal
    ///         is the player's cue that something changed, and without the ban it would not be visible at all.
    ///     </para>
    /// </summary>
    public sealed class Ghost
    {
        /// <summary>
        ///     Tried in this order, which decides ties. Up first and right last is the original arcade's order, and it
        ///     matters more than it looks: at a four-way junction equidistant from the target, the tie-break is the
        ///     entire difference between two ghosts taking the same turn and taking different ones.
        /// </summary>
        private static readonly DirectionEnum[] _preference =
        {
            DirectionEnum.Up, DirectionEnum.Left, DirectionEnum.Down, DirectionEnum.Right
        };

        private readonly PacManMaze _maze;

        /// <summary>Initializes a new instance of the <see cref="Ghost" /> class in its house.</summary>
        /// <param name="kind">Which ghost this is, which is how it hunts.</param>
        /// <param name="maze">The board it moves on.</param>
        /// <param name="x">Where it starts.</param>
        /// <param name="y">Where it starts.</param>
        /// <param name="scatterX">The corner it retreats to.</param>
        /// <param name="scatterY">The corner it retreats to.</param>
        /// <param name="releaseAfter">How many steps it waits in the house before first coming out.</param>
        public Ghost(GhostKindEnum kind, PacManMaze maze, int x, int y, int scatterX, int scatterY, int releaseAfter)
        {
            Kind = kind;
            _maze = maze ?? throw new ArgumentNullException(nameof(maze));
            HomeX = x;
            HomeY = y;
            ScatterX = scatterX;
            ScatterY = scatterY;
            ReleaseAfter = releaseAfter;
            Reset();
        }

        /// <summary>Which ghost this is.</summary>
        public GhostKindEnum Kind { get; }

        /// <summary>Where it is.</summary>
        public int X { get; private set; }

        /// <summary>Where it is.</summary>
        public int Y { get; private set; }

        /// <summary>Which way it last moved, and so which way it may not turn.</summary>
        public DirectionEnum Facing { get; private set; }

        /// <summary>What it is doing, as distinct from what the board-wide mode says.</summary>
        public GhostStateEnum State { get; private set; }

        /// <summary>Whether it is still shut in the house waiting to be let out.</summary>
        public bool Penned { get; private set; }

        /// <summary>Where it waits in the house.</summary>
        public int HomeX { get; }

        /// <summary>Where it waits in the house.</summary>
        public int HomeY { get; }

        /// <summary>The corner it heads for while the board is scattering.</summary>
        public int ScatterX { get; }

        /// <summary>The corner it heads for while the board is scattering.</summary>
        public int ScatterY { get; }

        /// <summary>How many steps it waits in the house at the start of a life.</summary>
        public int ReleaseAfter { get; }

        /// <summary>Puts it back in its house, hunting, facing up.</summary>
        public void Reset()
        {
            PlaceAt(HomeX, HomeY, DirectionEnum.Up);
            State = GhostStateEnum.Hunting;
            Penned = true;
        }

        /// <summary>
        ///     Drops it on a square, facing a way. Used by <see cref="Reset" /> to put it home, and by tests to build
        ///     an exact situation — a ghost two squares from the player, say — rather than running the board until
        ///     something like it turns up, which is how this repository has already shipped two flaky tests.
        /// </summary>
        /// <param name="x">Where to put it.</param>
        /// <param name="y">Where to put it.</param>
        /// <param name="facing">Which way it is travelling, which is also the way it may not turn.</param>
        internal void PlaceAt(int x, int y, DirectionEnum facing)
        {
            X = x;
            Y = y;
            Facing = facing;
        }

        /// <summary>
        ///     Lets it out of the house, facing the door.
        ///     <para>
        ///         The heading matters: a ghost released while bobbing downward may not then turn round, so it would
        ///         shuffle to the end of the house and back before finding the way out. Pointing it at the door is
        ///         one line and saves half a second of a ghost looking lost.
        ///     </para>
        /// </summary>
        public void Release()
        {
            Penned = false;
            Facing = DirectionEnum.Up;
        }

        /// <summary>
        ///     Turns it blue. Reverses it as well, because being sent the other way is the visible half of eating a
        ///     power pellet — the colour tells the player they are safe, the reversal tells them the board changed.
        /// </summary>
        public void Frighten()
        {
            if (State == GhostStateEnum.Eaten)
                return;

            State = GhostStateEnum.Frightened;
            Facing = Opposite(Facing);
        }

        /// <summary>Sends it home as a pair of eyes.</summary>
        public void Eat()
        {
            State = GhostStateEnum.Eaten;
        }

        /// <summary>Takes the blue off, whether the timer ran out or a life was lost.</summary>
        public void Calm()
        {
            if (State == GhostStateEnum.Frightened)
                State = GhostStateEnum.Hunting;
        }

        /// <summary>Turns it around, which is how the board announces a change of mode.</summary>
        public void Reverse()
        {
            if (State == GhostStateEnum.Hunting)
                Facing = Opposite(Facing);
        }

        /// <summary>
        ///     Moves one square toward wherever it is currently going.
        ///     <para>
        ///         Greedy and memoryless: of the directions that are not walls and not backwards, take the one whose
        ///         resulting square is nearest the target in a straight line. It cannot see round corners, which is
        ///         exactly why a ghost can be led into a dead end — and why the maze has none.
        ///     </para>
        /// </summary>
        /// <param name="game">The game, for where the player is and what mode the board is in.</param>
        /// <param name="random">The shared random source, used only while frightened.</param>
        public void Step(PacManGame game, Randomizer random)
        {
            if (game == null)
                throw new ArgumentNullException(nameof(game));

            if (Penned)
            {
                // Bobbing on the spot. Not decorative - it is what makes "the ghosts come out one at a time" read as
                // waiting rather than as the house sitting frozen until something teleports out of it.
                Facing = Facing == DirectionEnum.Up ? DirectionEnum.Down : DirectionEnum.Up;
                return;
            }

            var (targetX, targetY) = Target(game);
            var choices = new List<DirectionEnum>(4);
            var back = Opposite(Facing);

            foreach (var direction in _preference)
            {
                if (direction == back)
                    continue;

                var (nx, ny) = Ahead(direction, 1);
                if (!_maze.CanEnter(nx, ny, CanCrossDoor()))
                    continue;

                choices.Add(direction);
            }

            // Only in a dead end, which this maze does not have - but a maze somebody edits later might, and a ghost
            // with nowhere to go must turn round rather than stand still and let the player farm it.
            if (choices.Count == 0)
            {
                Facing = back;
                Advance();
                return;
            }

            Facing = State == GhostStateEnum.Frightened
                ? choices[random.Next(choices.Count)]
                : Nearest(choices, targetX, targetY);

            Advance();

            if (State == GhostStateEnum.Eaten && X == _maze.HouseX && Y == _maze.HouseY)
            {
                State = GhostStateEnum.Hunting;
                Facing = DirectionEnum.Up;
            }
        }

        /// <summary>
        ///     The square this ghost is heading for. <b>The one line that differs between the four of them.</b>
        /// </summary>
        /// <param name="game">The game, for the player's position and heading and the board-wide mode.</param>
        /// <returns>The square being aimed at, which may well be off the board — that is allowed and is the point.</returns>
        public (int X, int Y) Target(PacManGame game)
        {
            // Eyes always go home, whatever else is happening - and home is the middle of the house rather than
            // the door itself, or they would arrive on the doorstep and have nothing left to aim at.
            if (State == GhostStateEnum.Eaten)
                return (_maze.HouseX, _maze.HouseY);

            // Scatter sends everyone to their own corner. The corners are OUTSIDE the board on purpose: a target that
            // can never be reached is what makes a ghost circle its corner forever instead of parking on it.
            if (game.Mode == GhostModeEnum.Scatter)
                return (ScatterX, ScatterY);

            switch (Kind)
            {
                case GhostKindEnum.Blinky:
                    // Straight at the player. The simplest rule there is, and the one that makes the game a chase.
                    return (game.PacManX, game.PacManY);

                case GhostKindEnum.Pinky:
                    // Four squares in front of the player rather than at them, which is what turns a chase into an
                    // ambush: it arrives where you are going instead of following where you have been.
                    return AheadOf(game, 4);

                case GhostKindEnum.Inky:
                    // Two in front of the player, then that vector doubled from Blinky. So this one's target depends
                    // on where the RED ghost is - the only rule here that reads another ghost, and the reason the two
                    // of them pincer without either of them knowing the other exists.
                    var (pivotX, pivotY) = AheadOf(game, 2);
                    var blinky = game.GhostOf(GhostKindEnum.Blinky);
                    return (2*pivotX - blinky.X, 2*pivotY - blinky.Y);

                default:
                    // Chases from a distance and loses its nerve within eight squares, wandering back to its corner.
                    // Which is why the bottom-left of the board is the safest place to stand, and why standing there
                    // is a trap when the others arrive.
                    var dx = game.PacManX - X;
                    var dy = game.PacManY - Y;
                    return dx*dx + dy*dy > 64 ? (game.PacManX, game.PacManY) : (ScatterX, ScatterY);
            }
        }

        /// <summary>The square a number of steps ahead of the player, in the direction they are facing.</summary>
        private static (int X, int Y) AheadOf(PacManGame game, int distance)
        {
            var (dx, dy) = Offset(game.Facing);
            return (game.PacManX + dx*distance, game.PacManY + dy*distance);
        }

        /// <summary>Whichever of the offered directions lands nearest the target, straight-line.</summary>
        private DirectionEnum Nearest(List<DirectionEnum> choices, int targetX, int targetY)
        {
            var best = choices[0];
            var bestDistance = long.MaxValue;

            foreach (var direction in choices)
            {
                var (nx, ny) = Ahead(direction, 1);
                long dx = nx - targetX;
                long dy = ny - targetY;
                var distance = dx*dx + dy*dy;

                // Strictly less than, so an earlier direction in _preference wins a tie - which is what makes the
                // tie-break order above a real decision rather than an accident of iteration.
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                best = direction;
            }

            return best;
        }

        /// <summary>Where a number of steps in a direction lands, wrapped through the tunnel.</summary>
        private (int X, int Y) Ahead(DirectionEnum direction, int distance)
        {
            var (dx, dy) = Offset(direction);
            return (_maze.WrapX(X + dx*distance), Y + dy*distance);
        }

        /// <summary>Takes one step in whatever direction it is now facing.</summary>
        private void Advance()
        {
            var (x, y) = Ahead(Facing, 1);
            X = x;
            Y = y;
        }

        /// <summary>
        ///     Whether this ghost may cross the house door right now: on its way home as eyes, or on its way out
        ///     from inside.
        ///     <para>
        ///         Not simply "ghosts may use the door", which is the obvious reading and is wrong in a way that
        ///         looks like a bug: a hunting ghost passing over the door on its way somewhere would drop into the
        ///         house, which is a dead end, and have to climb back out. The door is one-way in spirit — a way home
        ///         and a way out, never a shortcut.
        ///     </para>
        /// </summary>
        /// <returns>True when the door is passable to this ghost this step.</returns>
        private bool CanCrossDoor()
        {
            return State == GhostStateEnum.Eaten || _maze.IsInsideHouse(X, Y);
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

        /// <summary>Which way is backwards.</summary>
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
