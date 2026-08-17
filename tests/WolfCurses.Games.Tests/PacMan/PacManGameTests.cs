using System;
using System.Collections.Generic;
using WolfCurses.Core;
using WolfCurses.Games.PacMan;
using Xunit;

namespace WolfCurses.Games.Tests.PacMan
{
    /// <summary>
    ///     The rules, driven with no console and no clock. Everything in the game is counted in steps, so a hundred
    ///     steps of play happen instantly and none of these tests sleep.
    /// </summary>
    public class PacManGameTests
    {
        [Fact]
        public void TheBoardHoldsStillForAMomentBeforeALifeStarts()
        {
            // The pause exists so the player can see where everyone is. It also has to actually stop things, or the
            // ghosts get a free run while the caption is up.
            var game = Fresh();
            var (x, y) = (game.PacManX, game.PacManY);

            Assert.True(game.IsReady);
            game.Step();
            Assert.Equal((x, y), (game.PacManX, game.PacManY));

            Ready(game);
            Assert.False(game.IsReady);
        }

        [Fact]
        public void ThePlayerEatsWhatTheyWalkOverAndScoresForIt()
        {
            var game = Ready(Fresh());
            var before = game.Maze.PelletsLeft;

            game.Steer(DirectionEnum.Up);
            game.Step();

            Assert.Equal(before - 1, game.Maze.PelletsLeft);
            Assert.Equal(10, game.Score);
            Assert.Equal(1, game.PelletsEaten);
        }

        [Fact]
        public void ThePlayerCannotWalkThroughAWall()
        {
            // Placed against a wall rather than assuming there is one where the player starts - there is not, the
            // starting square is a crossroads, which is the point of it.
            var game = Ready(Fresh());
            var (x, y) = FaceAWall(game);

            game.Step();

            Assert.Equal((x, y), (game.PacManX, game.PacManY));
            Assert.Equal(0, game.Score);
        }

        [Fact]
        public void ATurnAskedForTooEarlyIsRememberedUntilItBecomesLegal()
        {
            // The whole feel of the controls. Asking to turn into a wall must not be thrown away, or the game reads
            // as dropping inputs - which is exactly what it would be doing.
            var game = Ready(Fresh());

            game.Steer(DirectionEnum.Up);
            game.Step();
            Assert.Equal(DirectionEnum.Up, game.Facing);

            // Ask for a turn that is illegal right now, then keep walking until it becomes legal.
            game.Steer(DirectionEnum.Left);
            var turned = false;
            for (var step = 0; step < 6 && !turned; step++)
            {
                game.Step();
                turned = game.Facing == DirectionEnum.Left;
            }

            Assert.True(turned, "the turn was forgotten instead of being taken at the first corner");
        }

        [Fact]
        public void ThePlayerMayNotGoIntoTheGhostHouse()
        {
            var game = Ready(Fresh());
            var maze = game.Maze;

            // The red ghost starts on the square above the door, so it has to be sent away first or this test
            // measures a death rather than a locked door.
            Banish(game);
            game.PlacePlayer(maze.DoorX, maze.DoorY - 1, DirectionEnum.Down);
            game.Steer(DirectionEnum.Down);
            game.Step();

            Assert.Equal((maze.DoorX, maze.DoorY - 1), (game.PacManX, game.PacManY));
        }

        [Fact]
        public void TheTunnelComesOutTheOtherSide()
        {
            var game = Ready(Fresh());
            var maze = game.Maze;

            game.PlacePlayer(0, maze.TunnelRow, DirectionEnum.Left);
            game.Step();

            Assert.Equal(maze.Width - 1, game.PacManX);
            Assert.Equal(maze.TunnelRow, game.PacManY);
        }

        // ------------------------------------------------------------ the ghosts

        [Fact]
        public void TheGhostsComeOutOfTheHouseOneAtATime()
        {
            var game = Ready(Fresh());

            Assert.False(game.GhostOf(GhostKindEnum.Blinky).Penned, "the red one should never be penned");
            Assert.True(game.GhostOf(GhostKindEnum.Clyde).Penned, "the orange one should not be out yet");

            // Stopped at the first death rather than run for a flat two hundred steps: losing a life puts everyone
            // back in the house and restarts the release clock, so a longer run measures how long the player lived
            // rather than how the ghosts come out.
            var seen = new List<int>();
            var lives = game.Lives;

            for (var step = 0; step < 200 && game.Lives == lives; step++)
            {
                game.Step();

                var loose = 0;
                foreach (var ghost in game.Ghosts)
                {
                    if (!ghost.Penned)
                        loose++;
                }

                seen.Add(loose);
            }

            // Staggered, not all at once: one, then two, then three, then all four.
            Assert.Contains(1, seen);
            Assert.Contains(2, seen);
            Assert.Contains(3, seen);
            Assert.Contains(4, seen);
        }

        [Fact]
        public void NoGhostEverGetsStuck()
        {
            // A greedy mover with a ban on reversing is one bad maze cell away from standing still forever, and a
            // ghost that stopped would be invisible as a bug and fatal as a game.
            //
            // Measured as a running property rather than by watching one ghost for eight steps after some fixed
            // number: a ghost that has not left the house yet is correctly stationary, dying resets every release
            // counter, and a first version of this test failed on exactly that rather than on anything being wrong.
            // While a ghost is out, only two things may hold it still - being blue (it moves every other step) and
            // the step it is eaten on - so more than two in a row means it is stuck.
            var game = Ready(Fresh());
            var still = new int[game.Ghosts.Count];
            var last = new (int X, int Y)[game.Ghosts.Count];
            var lives = game.Lives;

            for (var i = 0; i < game.Ghosts.Count; i++)
                last[i] = (game.Ghosts[i].X, game.Ghosts[i].Y);

            for (var step = 0; step < 600 && !game.IsOver; step++)
            {
                game.Step();

                // A death puts every ghost back on its own home square and then holds the whole board still for the
                // opening pause, which looks exactly like being stuck and is not. Both halves matter: skipping only
                // the step the life was lost on leaves the nine frozen steps after it, which is what this test
                // actually failed on. Losing a life is common now that the board is open enough for the ghosts to
                // converge, so neither branch is rare.
                if (game.Lives != lives || game.IsReady)
                {
                    lives = game.Lives;
                    Array.Clear(still);

                    for (var i = 0; i < game.Ghosts.Count; i++)
                        last[i] = (game.Ghosts[i].X, game.Ghosts[i].Y);

                    continue;
                }

                for (var i = 0; i < game.Ghosts.Count; i++)
                {
                    var ghost = game.Ghosts[i];
                    var now = (ghost.X, ghost.Y);

                    if (ghost.Penned || now != last[i])
                        still[i] = 0;
                    else
                        still[i]++;

                    last[i] = now;
                    Assert.True(still[i] <= 2, $"{ghost.Kind} has been standing on {now} for {still[i]} steps");
                }
            }
        }

        [Fact]
        public void EveryGhostStaysOnTheBoard()
        {
            var game = Ready(Fresh());

            for (var step = 0; step < 400; step++)
            {
                game.Step();

                foreach (var ghost in game.Ghosts)
                {
                    Assert.True(game.Maze.Contains(ghost.X, ghost.Y),
                        $"{ghost.Kind} left the board at {ghost.X},{ghost.Y}");
                    Assert.False(game.Maze.IsWall(ghost.X, ghost.Y),
                        $"{ghost.Kind} is standing inside a wall at {ghost.X},{ghost.Y}");
                }
            }
        }

        [Fact]
        public void AGhostNeverTurnsBackOnItself()
        {
            // The one restriction the whole movement rule rests on. Without it a greedy ghost with its target behind
            // it simply turns round, and two ghosts in a corridor oscillate forever instead of patrolling - which is
            // also why the game reverses them deliberately on a mode change and why that reads as an event.
            var game = Chasing(Fresh());
            Banish(game);

            var blinky = game.GhostOf(GhostKindEnum.Blinky);
            var maze = game.Maze;

            // Somewhere with corridor either side, travelling right, with the player behind it - so the greedy rule
            // wants to go left and the ban is the only thing stopping it.
            var (x, y) = OpenRun(maze);
            blinky.PlaceAt(x, y, DirectionEnum.Right);
            game.PlacePlayer(x - 2, y, DirectionEnum.Left);

            blinky.Step(game, new Randomizer(1));

            Assert.NotEqual(x - 1, blinky.X);
        }

        [Fact]
        public void AHuntingGhostNeverWandersIntoTheHouse()
        {
            // The door is a way home and a way out, never a shortcut. Letting any ghost use it puts hunting ghosts
            // into a dead end they then have to climb out of, which reads as the game glitching.
            // Asserted about ENTERING rather than about being there, which is the distinction the first version of
            // this test missed: a ghost that has just been released is legitimately inside the house on its way out,
            // and so is a pair of eyes that has just arrived and turned back into a ghost. What must never happen is
            // a hunting ghost that was outside being inside a step later.
            var game = Ready(Fresh());
            var wasOutside = new bool[game.Ghosts.Count];
            var lives = game.Lives;

            for (var step = 0; step < 500 && !game.IsOver; step++)
            {
                game.Step();

                // Dying puts every ghost back inside the house, which is arriving rather than walking in.
                if (game.Lives != lives)
                {
                    lives = game.Lives;
                    Array.Clear(wasOutside);
                    continue;
                }

                for (var i = 0; i < game.Ghosts.Count; i++)
                {
                    var ghost = game.Ghosts[i];
                    var inside = game.Maze.IsInsideHouse(ghost.X, ghost.Y);

                    if (ghost.State == GhostStateEnum.Hunting && wasOutside[i])
                    {
                        Assert.False(inside,
                            $"{ghost.Kind} walked into the ghost house at step {step} while hunting");
                    }

                    wasOutside[i] = !inside && !ghost.Penned;
                }
            }
        }

        [Fact]
        public void ABlueGhostMovesAtHalfSpeed()
        {
            // Being able to outrun them is the entire point of a power pellet. Speed here is "how many steps you get"
            // rather than a fraction of a square, which is what keeps the whole game on the lattice.
            var game = Ready(Fresh());
            Banish(game);

            foreach (var ghost in game.Ghosts)
                ghost.Frighten();

            var blinky = game.GhostOf(GhostKindEnum.Blinky);
            var last = (blinky.X, blinky.Y);
            var moved = 0;

            for (var step = 0; step < 20; step++)
            {
                game.Step();

                if ((blinky.X, blinky.Y) != last)
                    moved++;

                last = (blinky.X, blinky.Y);
            }

            Assert.InRange(moved, 8, 12);
        }

        [Fact]
        public void APairOfEyesGetsHomeFasterThanItCouldWalk()
        {
            // Eyes move twice per step. Asserted against the actual distance home rather than against a step count,
            // so the test says "faster than walking" rather than pinning a number that the maze decides.
            var game = Ready(Fresh());
            Banish(game);

            var blinky = game.GhostOf(GhostKindEnum.Blinky);
            var walk = StepsHome(game.Maze, blinky.X, blinky.Y);
            blinky.Eat();

            var taken = 0;
            while (blinky.State == GhostStateEnum.Eaten && taken < walk + 10)
            {
                game.Step();
                taken++;
            }

            Assert.Equal(GhostStateEnum.Hunting, blinky.State);
            Assert.True(taken < walk, $"the eyes took {taken} steps to walk {walk} squares");
        }

        [Fact]
        public void BlinkyGoesStraightForThePlayer()
        {
            var game = Chasing(Fresh());
            game.PlacePlayer(5, 5, DirectionEnum.Right);

            Assert.Equal((5, 5), game.GhostOf(GhostKindEnum.Blinky).Target(game));
        }

        [Fact]
        public void PinkyAimsInFrontOfThePlayerRatherThanAtThem()
        {
            // The difference between following and heading off, and it is four squares of one line.
            var game = Chasing(Fresh());
            game.PlacePlayer(10, 5, DirectionEnum.Right);

            Assert.Equal((14, 5), game.GhostOf(GhostKindEnum.Pinky).Target(game));

            game.PlacePlayer(10, 5, DirectionEnum.Up);
            Assert.Equal((10, 1), game.GhostOf(GhostKindEnum.Pinky).Target(game));
        }

        [Fact]
        public void InkyAimsThroughWhereverTheRedOneIs()
        {
            // The only rule that reads another ghost. Two squares in front of the player, doubled from Blinky - so
            // moving the red one moves the cyan one's target, which is the whole pincer and none of it is planned.
            var game = Chasing(Fresh());
            game.PlacePlayer(10, 5, DirectionEnum.Right);

            var blinky = game.GhostOf(GhostKindEnum.Blinky);
            var inky = game.GhostOf(GhostKindEnum.Inky);

            blinky.PlaceAt(4, 5, DirectionEnum.Right);
            Assert.Equal((20, 5), inky.Target(game));

            blinky.PlaceAt(8, 5, DirectionEnum.Right);
            Assert.Equal((16, 5), inky.Target(game));
        }

        [Fact]
        public void ClydeChasesFromAfarAndLosesItsNerveUpClose()
        {
            var game = Chasing(Fresh());
            var clyde = game.GhostOf(GhostKindEnum.Clyde);

            game.PlacePlayer(30, 5, DirectionEnum.Right);
            clyde.PlaceAt(2, 5, DirectionEnum.Right);
            Assert.Equal((30, 5), clyde.Target(game));

            clyde.PlaceAt(28, 5, DirectionEnum.Right);
            Assert.Equal((clyde.ScatterX, clyde.ScatterY), clyde.Target(game));
        }

        [Fact]
        public void ScatteringSendsEveryoneToTheirOwnCornerAndTheCornersAreOffTheBoard()
        {
            // Off the board on purpose: a target that can never be reached is what makes a ghost circle its corner
            // instead of parking on it and waiting.
            var game = Ready(Fresh());
            Assert.Equal(GhostModeEnum.Scatter, game.Mode);

            var corners = new HashSet<(int, int)>();
            foreach (var ghost in game.Ghosts)
            {
                var target = ghost.Target(game);
                Assert.Equal((ghost.ScatterX, ghost.ScatterY), target);
                Assert.False(game.Maze.Contains(target.X, target.Y),
                    $"{ghost.Kind} scatters to {target}, which is on the board");

                corners.Add(target);
            }

            Assert.Equal(4, corners.Count);
        }

        [Fact]
        public void TheBoardAlternatesBetweenScatteringAndChasing()
        {
            var game = Ready(Fresh());
            var modes = new List<GhostModeEnum> {game.Mode};

            for (var step = 0; step < 400; step++)
            {
                game.Step();
                if (game.Mode != modes[modes.Count - 1])
                    modes.Add(game.Mode);
            }

            Assert.True(modes.Count >= 3, "the board never changed its mind");
            for (var i = 1; i < modes.Count; i++)
                Assert.NotEqual(modes[i - 1], modes[i]);
        }

        [Fact]
        public void AChangeOfModeSendsTheHuntingGhostsBackTheWayTheyCame()
        {
            // The only announcement the player gets that the rhythm shifted, and it is only legible because ghosts
            // may not otherwise reverse at all.
            //
            // Asserted on where the ghost ENDS UP, not on which way it is facing. A first version compared the
            // facing before and after the step and passed with the reversal deleted, because a ghost picks a new
            // heading on almost every step anyway - the direction it happened to be facing was never evidence of
            // anything. Standing it in a one-way corridor is what turns this into a fact: once turned round, the way
            // it came is the only move left, and without the turn the only move is straight on.
            var game = Ready(Fresh());
            var blinky = game.GhostOf(GhostKindEnum.Blinky);
            var (x, y) = StraightCorridor(game.Maze);

            var sawChange = false;
            for (var step = 0; step < 400 && !sawChange; step++)
            {
                // Put back in the corridor before every step, so wherever the flip lands it lands on a ghost with
                // exactly one way to go.
                blinky.PlaceAt(x, y, DirectionEnum.Right);
                var mode = game.Mode;

                game.Step();

                if (game.Mode == mode)
                    continue;

                sawChange = true;
                Assert.Equal((x - 1, y), (blinky.X, blinky.Y));
            }

            Assert.True(sawChange, "the board never changed its mode at all");
        }

        [Fact]
        public void APlayerWhoStandsStillIsEventuallyCaught()
        {
            // The cheapest possible check that four greedy movers with four different targets add up to a threat.
            // Everything else here tests a rule; this tests that the rules together make a game.
            var game = Ready(Fresh());

            var caught = false;
            for (var step = 0; step < 900 && !caught; step++)
            {
                game.Step();
                caught = game.Lives < 3;
            }

            Assert.True(caught, "nobody caught a player who never moved, in nine hundred steps");
        }

        // ------------------------------------------------------------ power pellets

        [Fact]
        public void APowerPelletTurnsEveryHuntingGhostBlueAndSendsItTheOtherWay()
        {
            var game = Ready(Fresh());
            var (px, py) = FindPowerPellet(game.Maze);

            var facings = new List<DirectionEnum>();
            foreach (var ghost in game.Ghosts)
                facings.Add(ghost.Facing);

            game.PlacePlayer(px + 1, py, DirectionEnum.Left);
            game.Steer(DirectionEnum.Left);
            game.Step();

            Assert.Equal(50, game.Score);
            Assert.True(game.FrightenedLeft > 0);

            for (var i = 0; i < game.Ghosts.Count; i++)
            {
                Assert.Equal(GhostStateEnum.Frightened, game.Ghosts[i].State);
                Assert.NotEqual(facings[i], game.Ghosts[i].Facing);
            }
        }

        [Fact]
        public void TheBlueRunsOutAndEveryoneGoesBackToHunting()
        {
            var game = Ready(Fresh());
            var (px, py) = FindPowerPellet(game.Maze);

            game.PlacePlayer(px + 1, py, DirectionEnum.Left);
            game.Steer(DirectionEnum.Left);
            game.Step();

            var length = game.FrightenedLength;
            for (var step = 0; step < length + 4; step++)
                game.Step();

            Assert.Equal(0, game.FrightenedLeft);
            foreach (var ghost in game.Ghosts)
                Assert.NotEqual(GhostStateEnum.Frightened, ghost.State);
        }

        [Fact]
        public void EatingABlueGhostScoresAndSendsItHomeAsEyes()
        {
            // The player moves before anything is checked, so a ghost dropped on the square they are standing on
            // would simply be left behind. Facing them into a wall is what makes the collision happen where it was
            // set up - which is a fact about the order inside Step, and worth knowing before writing another of
            // these.
            var game = Ready(Fresh());
            Banish(game);
            FaceAWall(game);

            var blinky = game.GhostOf(GhostKindEnum.Blinky);
            blinky.Frighten();
            blinky.PlaceAt(game.PacManX, game.PacManY, DirectionEnum.Up);

            var before = game.Score;
            game.Step();

            Assert.Equal(before + 200, game.Score);
            Assert.Equal(GhostStateEnum.Eaten, blinky.State);
        }

        [Fact]
        public void EachGhostInAChainIsWorthTwiceTheLastUpToSixteenHundred()
        {
            var game = Ready(Fresh());
            Banish(game);
            FaceAWall(game);

            var expected = new[] {200, 400, 800, 1600};
            var scored = new List<int>();

            foreach (var ghost in game.Ghosts)
            {
                ghost.Frighten();
                ghost.PlaceAt(game.PacManX, game.PacManY, DirectionEnum.Up);

                var before = game.Score;
                game.Step();
                scored.Add(game.Score - before);
            }

            Assert.Equal(expected, scored);
        }

        [Fact]
        public void EyesGoHomeAndComeBackHunting()
        {
            var game = Ready(Fresh());
            var blinky = game.GhostOf(GhostKindEnum.Blinky);

            blinky.Eat();
            Assert.Equal(GhostStateEnum.Eaten, blinky.State);

            for (var step = 0; step < 120 && blinky.State == GhostStateEnum.Eaten; step++)
                game.Step();

            Assert.Equal(GhostStateEnum.Hunting, blinky.State);
        }

        [Fact]
        public void APairOfEyesIsHarmless()
        {
            var game = Ready(Fresh());
            Banish(game);
            FaceAWall(game);

            var blinky = game.GhostOf(GhostKindEnum.Blinky);
            blinky.Eat();
            blinky.PlaceAt(game.PacManX, game.PacManY, DirectionEnum.Up);

            var lives = game.Lives;
            game.Step();

            Assert.Equal(lives, game.Lives);
        }

        // ------------------------------------------------------------ dying

        [Fact]
        public void WalkingIntoAHuntingGhostCostsALife()
        {
            var game = Ready(Fresh());
            Banish(game);
            FaceAWall(game);

            var blinky = game.GhostOf(GhostKindEnum.Blinky);
            blinky.PlaceAt(game.PacManX, game.PacManY, DirectionEnum.Up);

            game.Step();

            Assert.Equal(2, game.Lives);
            Assert.False(game.IsOver);
            Assert.Equal(game.Maze.PacManStart, (game.PacManX, game.PacManY));
        }

        [Fact]
        public void TheGameEndsWhenTheLastLifeGoes()
        {
            var game = Ready(Fresh());
            var blinky = game.GhostOf(GhostKindEnum.Blinky);

            for (var life = 0; life < 3; life++)
            {
                Ready(game);
                Banish(game);
                FaceAWall(game);
                blinky.PlaceAt(game.PacManX, game.PacManY, DirectionEnum.Up);
                game.Step();
            }

            Assert.Equal(0, game.Lives);
            Assert.True(game.IsOver);

            // And it stays over: another step must not quietly restart anything.
            var score = game.Score;
            game.Step();
            Assert.True(game.IsOver);
            Assert.Equal(score, game.Score);
        }

        [Fact]
        public void ClearingTheBoardStartsTheNextOneWithTheScoreIntact()
        {
            var game = Ready(Fresh());

            // Eaten off the board directly rather than walked, because walking four hundred pellets would be a test
            // of the path this test chose rather than of what happens when the last one goes.
            for (var y = 0; y < game.Maze.Height; y++)
            for (var x = 0; x < game.Maze.Width; x++)
            {
                if (game.Maze.HasPellet(x, y))
                    game.Maze.Eat(x, y);
            }

            Assert.Equal(0, game.Maze.PelletsLeft);

            game.Step();

            Assert.Equal(2, game.Level);
            Assert.Equal(game.Maze.TotalPellets, game.Maze.PelletsLeft);
            Assert.Equal(3, game.Lives);
            Assert.True(game.IsReady);
        }

        [Fact]
        public void TheBlueGetsShorterAsTheBoardsGoUp()
        {
            var game = Ready(Fresh());
            var first = game.FrightenedLength;

            for (var y = 0; y < game.Maze.Height; y++)
            for (var x = 0; x < game.Maze.Width; x++)
            {
                if (game.Maze.HasPellet(x, y))
                    game.Maze.Eat(x, y);
            }

            game.Step();

            Assert.True(game.FrightenedLength < first,
                $"board 2 gives {game.FrightenedLength} steps of blue, the same as board 1");
            Assert.True(game.FrightenedLength >= 12, "the blue has shrunk to nothing");
        }

        [Fact]
        public void ANullRandomSourceIsRefused()
        {
            Assert.Throws<ArgumentNullException>(() => new PacManGame(null));
        }

        // ------------------------------------------------------------ helpers

        private static PacManGame Fresh()
        {
            return new PacManGame(new Randomizer(11));
        }

        /// <summary>Steps past the opening pause so a test can get on with what it is about.</summary>
        private static PacManGame Ready(PacManGame game)
        {
            while (game.IsReady)
                game.Step();

            return game;
        }

        /// <summary>Runs the board on until the ghosts are chasing rather than scattering.</summary>
        private static PacManGame Chasing(PacManGame game)
        {
            Ready(game);

            for (var step = 0; step < 500 && game.Mode != GhostModeEnum.Chase; step++)
                game.Step();

            Assert.Equal(GhostModeEnum.Chase, game.Mode);
            return game;
        }

        /// <summary>
        ///     Stands the player against a wall, facing it, so they cannot move on the next step.
        ///     <para>
        ///         Every collision test needs this. <c>Step</c> moves the player <i>first</i> and only then looks for
        ///         who ran into whom, so a ghost placed on the square the player is standing on gets left behind
        ///         before anything is checked. Pinning them in place is what makes the situation the test set up be
        ///         the situation the test measures.
        ///     </para>
        /// </summary>
        /// <returns>Where the player now is.</returns>
        private static (int X, int Y) FaceAWall(PacManGame game)
        {
            var maze = game.Maze;

            // Searched from the middle outward so it lands somewhere central, and away from the corners the ghosts
            // are sent to below.
            for (var y = maze.Height / 2; y < maze.Height - 1; y++)
            for (var x = maze.Width / 2; x < maze.Width - 1; x++)
            {
                if (!maze.IsOpen(x, y) || maze.IsDoor(x, y) || maze.IsInsideHouse(x, y) || !maze.IsWall(x, y + 1))
                    continue;

                game.PlacePlayer(x, y, DirectionEnum.Down);
                return (x, y);
            }

            throw new KeyNotFoundException("the board has no square with a wall below it");
        }

        /// <summary>
        ///     Sends every ghost to the far corner of the board, so a test can bring them back one at a time and
        ///     nothing wanders into the answer.
        /// </summary>
        private static void Banish(PacManGame game)
        {
            var maze = game.Maze;
            var (px, py) = (game.PacManX, game.PacManY);
            var best = (X: -1, Y: -1);
            var bestDistance = -1;

            for (var y = 0; y < maze.Height; y++)
            for (var x = 0; x < maze.Width; x++)
            {
                if (!maze.IsOpen(x, y) || maze.IsDoor(x, y) || maze.IsInsideHouse(x, y))
                    continue;

                var distance = Math.Abs(x - px) + Math.Abs(y - py);
                if (distance <= bestDistance)
                    continue;

                bestDistance = distance;
                best = (x, y);
            }

            foreach (var ghost in game.Ghosts)
            {
                ghost.Release();
                ghost.PlaceAt(best.X, best.Y, DirectionEnum.Up);
            }
        }

        /// <summary>
        ///     A square with walls above and below and floor either side — a stretch of corridor with no turnings,
        ///     where a ghost's next move is decided by which way it is facing and nothing else.
        /// </summary>
        private static (int X, int Y) StraightCorridor(PacManMaze maze)
        {
            for (var y = 1; y < maze.Height - 1; y++)
            for (var x = 2; x < maze.Width - 2; x++)
            {
                if (maze.IsWall(x, y) || maze.IsInsideHouse(x, y) || maze.IsDoor(x, y))
                    continue;

                if (maze.IsWall(x, y - 1) && maze.IsWall(x, y + 1) &&
                    maze.IsOpen(x - 1, y) && maze.IsOpen(x + 1, y))
                    return (x, y);
            }

            throw new KeyNotFoundException("the board has no corridor without a turning in it");
        }

        /// <summary>A square with open corridor two cells either side of it, for testing which way a ghost turns.</summary>
        private static (int X, int Y) OpenRun(PacManMaze maze)
        {
            for (var y = 1; y < maze.Height - 1; y++)
            for (var x = 3; x < maze.Width - 3; x++)
            {
                var run = true;
                for (var offset = -2; offset <= 2; offset++)
                    run &= maze.IsOpen(x + offset, y) && !maze.IsInsideHouse(x + offset, y) && !maze.IsDoor(x + offset, y);

                if (run)
                    return (x, y);
            }

            throw new KeyNotFoundException("the board has no straight run of five squares");
        }

        /// <summary>How many squares it is from a cell to the middle of the ghost house, walking.</summary>
        private static int StepsHome(PacManMaze maze, int fromX, int fromY)
        {
            var distance = new Dictionary<(int X, int Y), int> {[(fromX, fromY)] = 0};
            var queue = new Queue<(int X, int Y)>();
            queue.Enqueue((fromX, fromY));

            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                if (x == maze.HouseX && y == maze.HouseY)
                    return distance[(x, y)];

                foreach (var (dx, dy) in new[] {(0, -1), (0, 1), (-1, 0), (1, 0)})
                {
                    var next = (X: maze.WrapX(x + dx), Y: y + dy);
                    if (!maze.CanEnter(next.X, next.Y, true) || distance.ContainsKey(next))
                        continue;

                    distance[next] = distance[(x, y)] + 1;
                    queue.Enqueue(next);
                }
            }

            throw new InvalidOperationException("the ghost house cannot be reached");
        }

        private static (int X, int Y) FindPowerPellet(PacManMaze maze)
        {
            for (var y = 0; y < maze.Height; y++)
            for (var x = 0; x < maze.Width; x++)
            {
                if (maze.HasPowerPellet(x, y) && maze.IsOpen(x + 1, y))
                    return (x, y);
            }

            throw new KeyNotFoundException("the board has no power pellet with room beside it");
        }
    }
}
