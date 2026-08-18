using System;
using System.Collections.Generic;
using System.Linq;
using WolfCurses.Core;
using WolfCurses.Games.Battlezone;
using Xunit;

namespace WolfCurses.Games.Tests.Battlezone
{
    /// <summary>
    ///     The plain and everything standing on it. No console anywhere near any of this — the rules are two
    ///     dimensional and the third one is added by the renderer, which is what makes them testable at all.
    /// </summary>
    public class BattleFieldTests
    {
        private static readonly TimeSpan _frame = TimeSpan.FromMilliseconds(33);

        [Fact]
        public void AnAngleIsAlwaysTheShortWayRound()
        {
            // Everything that turns goes through here. Without it a tank facing just west of north and asked to face
            // just east of north turns the LONG way, all the way round the compass, which looks exactly like the
            // steering being broken rather than like arithmetic.
            Assert.InRange(BattleField.WrapAngle(3.2), -Math.PI, Math.PI);
            Assert.InRange(BattleField.WrapAngle(-3.2), -Math.PI, Math.PI);
            Assert.InRange(BattleField.WrapAngle(100.0), -Math.PI, Math.PI);

            Assert.Equal(0.2, BattleField.WrapAngle(0.2), 9);
            Assert.Equal(-0.2, BattleField.WrapAngle(2.0*Math.PI - 0.2), 9);
            Assert.Equal(0.1, BattleField.WrapAngle(0.1 + 20.0*Math.PI), 9);
        }

        [Fact]
        public void DistanceToASegmentIsMeasuredToTheSegmentAndNotToItsLine()
        {
            // The clamp is what makes it a segment. Without it a shell that stopped short still counts as having
            // passed through everything further along its bearing, so a tank fifty units behind the shell's resting
            // place dies to a shot that never reached it.
            Assert.Equal(3.0, BattleField.DistanceToSegment(0, 3, -10, 0, 10, 0), 9);
            Assert.Equal(5.0, BattleField.DistanceToSegment(0, 5, 0, 0, 0, 0), 9);

            // Beyond the end: measured to the end, not perpendicular to the line.
            Assert.Equal(10.0, BattleField.DistanceToSegment(20, 0, -10, 0, 10, 0), 9);
            Assert.Equal(Math.Sqrt(200.0), BattleField.DistanceToSegment(20, 10, -10, 0, 10, 0), 9);
        }

        [Fact]
        public void AShellThatCrossesRightThroughATankInOneFrameStillKillsIt()
        {
            // A shell covers about fifteen units in a frame and a tank is thirteen across, so at any plausible frame
            // rate the shell is simply on one side of the target one frame and the other side the next, having never
            // been ON it. Clamping the frame length bounds the step but does NOT remove the problem - which is the
            // finding worth keeping. Only testing the segment it swept does.
            var field = new BattleField(new Randomizer(5));
            var enemy = field.Enemies[0];
            enemy.X = 0;
            enemy.Z = 15;

            Assert.True(field.Fire());

            var startZ = 7.0;
            var endZ = startZ + BattleField.ShellSpeed*0.1;

            // Both ends of the swept segment are clear of the tank, so a test on either one alone would miss.
            Assert.True(Math.Abs(15.0 - startZ) > enemy.Radius, "the shell started inside the tank");
            Assert.True(Math.Abs(15.0 - endZ) > enemy.Radius, "the shell finished inside the tank");

            field.Advance(TimeSpan.FromSeconds(1), 0, 0);

            Assert.Equal(1, field.Kills);
            Assert.Equal(1000, field.Score);
            Assert.False(enemy.Alive);
        }

        [Fact]
        public void SceneryStopsAShell()
        {
            var field = new BattleField(new Randomizer(5));
            var enemy = field.Enemies[0];
            enemy.X = 0;
            enemy.Z = 120;

            // A block squarely between the two, which is the whole reason the scenery is tactical rather than
            // decorative: there is somewhere to hide, and therefore somewhere to be hidden from.
            var block = field.Obstacles[0];
            block.X = 0;
            block.Z = 60;

            Assert.False(field.HasLineOfSight(0, 0, enemy.X, enemy.Z));
            Assert.True(field.Fire());

            for (var i = 0; i < 40; i++)
                field.Advance(_frame, 0, 0);

            Assert.Equal(0, field.Kills);
            Assert.True(enemy.Alive);
        }

        [Fact]
        public void SceneryStopsATank()
        {
            var field = new BattleField(new Randomizer(9));
            var block = field.Obstacles[0];
            block.X = 0;
            block.Z = 12;

            for (var i = 0; i < 60; i++)
                field.Advance(_frame, 0, 1);

            Assert.True(field.PlayerZ < 12.0 - block.Radius, $"drove to {field.PlayerZ}, which is inside the block");
        }

        [Fact]
        public void OnlyOneShellOfTheOwnMayBeInTheAirAtATime()
        {
            // The arcade's rule, and it is what turns every shot into a decision: miss, and the tank bearing down on
            // you is unopposed for as long as the shell takes to reach the horizon and expire.
            var field = new BattleField(new Randomizer(4));

            Assert.True(field.Fire());
            Assert.False(field.Fire());
            Assert.False(field.Fire());

            Assert.Single(field.Shells, shell => shell.FromPlayer);
        }

        [Fact]
        public void AShellFallsShortRatherThanFlyingForever()
        {
            var field = new BattleField(new Randomizer(4));
            Assert.True(field.Fire());

            for (var i = 0; i < 400 && field.PlayerShellInFlight; i++)
                field.Advance(_frame, 0, 0);

            Assert.False(field.PlayerShellInFlight);
            Assert.True(field.Fire());
        }

        [Theory]
        [InlineData(EnemyKindEnum.Tank)]
        [InlineData(EnemyKindEnum.SuperTank)]
        public void EveryEnemyTurnsSlowerThanThePlayer(EnemyKindEnum kind)
        {
            // THE number the game is built on. Give an enemy the player's turn rate and every decision in the game
            // disappears: it still compiles, still shoots, still keeps score, and is about nothing.
            var enemy = new Enemy(kind, 0, 0, 0);

            Assert.True(enemy.TurnRate < BattleField.PlayerTurnRate,
                $"a {kind} turns at {enemy.TurnRate}, the player at {BattleField.PlayerTurnRate}");
        }

        [Fact]
        public void KeepingMovingKeepsAPlayerAliveLongerThanStandingStill()
        {
            // The claim the turn rates exist to make, asserted rather than assumed. A player who circles is turning
            // faster, as seen from the enemy, than the enemy can follow - so the enemy spends its life swinging
            // towards where the player was. Level the two turn rates and this fails, which is exactly what it is
            // for.
            //
            // Totals over several seeds rather than one game: whether any single game is survived depends on where
            // the first tank happens to spawn.
            var still = 0.0;
            var moving = 0.0;

            for (var seed = 1; seed <= 6; seed++)
            {
                still += Survive(seed, circle: false);
                moving += Survive(seed, circle: true);
            }

            Assert.True(moving > still*1.5,
                $"standing still lasted {still:F1}s in total and circling {moving:F1}s, which is not a difference");
        }

        [Fact]
        public void ATankSteeringAroundABlockStillShootsAtThePlayer()
        {
            // Where the player is, not where the tank is steering. The two are the same number until the tank leans
            // around scenery, and firing on the steering error means a tank that is dodging a block empties its
            // magazine into the block - measured, one seed in twenty-four spent three minutes doing exactly that.
            //
            // The block is placed to be close enough to steer around but not close enough to block the shot, which
            // is the only arrangement where the two angles can be told apart at all.
            // Swept over every facing rather than set up at one, which is what makes this able to fail. Aimed at a
            // single arrangement the tank simply drives out from behind the block while it turns, the lean goes
            // away, and it ends up firing correctly for the wrong reason - the first version of this test did
            // exactly that and the mutation survived it. Somewhere in a full turn is the heading that matches the
            // steering target, and that is the one that gives the game away.
            var fired = 0;

            for (var step = 0; step < 240; step++)
            {
                var field = new BattleField(new Randomizer(5));
                var enemy = field.Enemies[0];
                enemy.X = 0;
                enemy.Z = 200;

                foreach (var obstacle in field.Obstacles)
                    obstacle.Z = 900;

                var block = field.Obstacles[0];
                block.Kind = ObstacleKindEnum.Cube;
                block.X = 12;
                block.Z = 170;

                Assert.True(field.HasLineOfSight(enemy.X, enemy.Z, 0, 0), "the block was in the way of the shot");
                Assert.True(field.TryFindBlocker(enemy.X, enemy.Z, Math.PI, 40, enemy.Radius, out _),
                    "the block was not close enough to be steered around");

                enemy.Heading = step*2.0*Math.PI/240.0;
                enemy.Reload = 0;

                // One very short step, so it fires or does not from where it is standing without driving anywhere.
                enemy.Think(field, 0.0005);

                foreach (var shot in field.Shells)
                {
                    fired++;

                    var wanted = Math.Atan2(0 - enemy.X, 0 - enemy.Z);
                    var wrong = Math.Abs(BattleField.WrapAngle(shot.Heading - wanted));

                    Assert.True(wrong <= enemy.FireCone + enemy.FireSpread + 1e-9,
                        $"a shell went {wrong:F2} radians off the player, which is more than aim and gunnery together");
                }
            }

            Assert.True(fired > 0, "it never fired at all, so this proved nothing");
        }

        [Fact]
        public void ABlockFurtherAwayDeflectsATankLessThanOneRightInFrontOfIt()
        {
            // The avoidance angle is the one that just clears the block's edge, so it shrinks with distance. A
            // CONSTANT angle is the bug, and the reason is not obvious: the lean is applied to the bearing to the
            // player, which rotates as the tank drives, so holding a fixed offset does not trace a detour - it
            // traces a circle, and the tank orbits at a fixed range for ever without ever closing or firing.
            //
            // Asserted as a property of the steering rather than through a game that stalls, which is the second
            // version of this test. The first leaned on "a player who does nothing is eventually destroyed", and
            // once the three other stalls were fixed a constant lean no longer hung any of sixteen plains - so the
            // mutation survived a test that was, by then, about something else.
            Assert.True(Deflection(20.0) > Deflection(38.0) + 0.1,
                $"a block at 20 deflected {Deflection(20.0):F2} and one at 38 deflected {Deflection(38.0):F2}");
        }

        [Fact]
        public void AnEnemyIsNeverPutDownInsideABlock()
        {
            // A tank dropped inside scenery has every direction it might drive refused, so it turns on the spot for
            // the rest of the game and the player is never attacked at all. One seed in twenty-four, before the
            // spawn started looking for clear ground.
            for (var seed = 1; seed <= 30; seed++)
            {
                var field = new BattleField(new Randomizer(seed));

                for (var spawn = 0; spawn < 12; spawn++)
                {
                    var enemy = field.Enemies[field.Enemies.Count - 1];
                    Assert.False(field.IsBlocked(enemy.X, enemy.Z, enemy.Radius),
                        $"seed {seed} put a tank inside a block");

                    field.SpawnHostile();
                }
            }
        }

        [Fact]
        public void APlayerWhoDoesNothingAtAllIsEventuallyDestroyed()
        {
            // The counterpart: the enemy has to actually be dangerous, or the test above would pass with an enemy
            // that never fires at all.
            //
            // SIXTEEN SEEDS, and the count is the test. Every stall this game has had - a tank put down inside a
            // block, a tank orbiting one because its avoidance angle was a constant, a stopped tank still steering
            // around scenery it was never going to reach - showed up on one plain in eight or one in twenty-four and
            // on none of the first four. At four seeds all three mutations survive; the property is about the AI, so
            // it has to be asked of enough worlds to find the one where the AI gets stuck.
            for (var seed = 1; seed <= 16; seed++)
            {
                var field = new BattleField(new Randomizer(seed));

                for (var i = 0; i < 6000 && !field.IsOver; i++)
                    field.Advance(_frame, 0, 0);

                Assert.True(field.IsOver, $"seed {seed} survived doing nothing for {field.Elapsed:F0} seconds");
            }
        }

        [Fact]
        public void BeingHitBreaksTheScreenAndCostsATank()
        {
            var field = new BattleField(new Randomizer(2));

            for (var i = 0; i < 6000 && !field.IsCracked; i++)
                field.Advance(_frame, 0, 0);

            Assert.True(field.IsCracked);
            Assert.Equal(BattleField.StartingLives - 1, field.Lives);
            Assert.True(field.IsRespawning);
        }

        [Fact]
        public void TheBrokenScreenClearsAndTheEnemyIsPushedBackOutOfArmsReach()
        {
            // Reappearing under the guns of the tank that has just killed you is not a game.
            var field = new BattleField(new Randomizer(2));

            for (var i = 0; i < 6000 && !field.IsCracked; i++)
                field.Advance(_frame, 0, 0);

            Assert.True(field.IsRespawning);

            for (var i = 0; i < 200 && field.IsRespawning; i++)
                field.Advance(_frame, 0, 0);

            Assert.False(field.IsCracked);
            Assert.Empty(field.Shells);

            foreach (var enemy in field.Enemies)
            {
                var dx = enemy.X - field.PlayerX;
                var dz = enemy.Z - field.PlayerZ;
                Assert.True(Math.Sqrt(dx*dx + dz*dz) > 150.0, "an enemy was left standing over the new tank");
            }
        }

        [Fact]
        public void NothingMovesWhileTheScreenIsBroken()
        {
            // Deliberately unlike Missile Command, where the field keeps advancing after the last city falls so the
            // warheads already in the air finish their arcs. There the last frame is the best one; here the player
            // is looking at a broken screen and being shot behind it would be indefensible.
            var field = new BattleField(new Randomizer(2));

            for (var i = 0; i < 6000 && !field.IsCracked; i++)
                field.Advance(_frame, 0, 0);

            Assert.True(field.IsRespawning);
            var enemy = field.Enemies[0];
            var before = (enemy.X, enemy.Z, enemy.Heading);

            field.Advance(_frame, 1, 1);

            Assert.Equal(before, (enemy.X, enemy.Z, enemy.Heading));
        }

        [Fact]
        public void TheWorldStopsWhenTheLastTankIsLost()
        {
            var field = new BattleField(new Randomizer(2));

            for (var i = 0; i < 30000 && !field.IsOver; i++)
                field.Advance(_frame, 0, 0);

            Assert.True(field.IsOver);
            Assert.True(field.IsCracked);
            Assert.Equal(0, field.Lives);

            var before = field.Enemies.Select(enemy => (enemy.X, enemy.Z)).ToList();
            var wasAt = (field.PlayerX, field.PlayerZ, field.PlayerHeading);

            for (var i = 0; i < 100; i++)
                field.Advance(_frame, 1, 1);

            Assert.Equal(wasAt, (field.PlayerX, field.PlayerZ, field.PlayerHeading));
            Assert.Equal(before, field.Enemies.Select(enemy => (enemy.X, enemy.Z)).ToList());
        }

        [Fact]
        public void FiringIsRefusedOnceTheGameIsOver()
        {
            var field = new BattleField(new Randomizer(2));

            for (var i = 0; i < 30000 && !field.IsOver; i++)
                field.Advance(_frame, 0, 0);

            Assert.False(field.Fire());
        }

        [Fact]
        public void OneEnormousFrameCannotTeleportAnything()
        {
            // A breakpoint, a garbage collection or a window being dragged all hand back one very long frame. Better
            // to run slow for a moment than to run wrong - a shell that moved a whole second's worth would pass
            // through every tank between here and the horizon.
            var field = new BattleField(new Randomizer(6));

            field.Advance(TimeSpan.FromSeconds(10), 0, 1);

            Assert.InRange(field.PlayerZ, 0.0, BattleField.PlayerSpeed*0.1 + 0.001);
        }

        [Fact]
        public void ScenerysNeverMovesWhereAnybodyCouldSeeItMove()
        {
            // The plain is endless because the scenery is recycled behind the player, and that is invisible ONLY
            // while the recycling distance stays comfortably beyond the drawing distance. Bring the two together and
            // blocks start appearing out of nothing in plain sight.
            var field = new BattleField(new Randomizer(8));
            var previous = field.Obstacles.Select(o => (o.X, o.Z)).ToList();

            for (var step = 0; step < 4000; step++)
            {
                var wasAt = (field.PlayerX, field.PlayerZ);
                field.Advance(_frame, step%400 < 30 ? 1 : 0, 1);

                for (var i = 0; i < field.Obstacles.Count; i++)
                {
                    var now = (field.Obstacles[i].X, field.Obstacles[i].Z);
                    if (now == previous[i])
                        continue;

                    var dx = previous[i].X - wasAt.PlayerX;
                    var dz = previous[i].Z - wasAt.PlayerZ;
                    Assert.True(Math.Sqrt(dx*dx + dz*dz) > BattleField.DrawRange,
                        "a block was picked up while it was still on screen");

                    previous[i] = now;
                }
            }
        }

        [Fact]
        public void TheSceneryIsAlwaysSpreadOutRatherThanStackedUp()
        {
            var field = new BattleField(new Randomizer(12));

            for (var step = 0; step < 3000; step++)
            {
                field.Advance(_frame, step%300 < 40 ? -1 : 0, 1);

                for (var a = 0; a < field.Obstacles.Count; a++)
                for (var b = a + 1; b < field.Obstacles.Count; b++)
                {
                    var dx = field.Obstacles[a].X - field.Obstacles[b].X;
                    var dz = field.Obstacles[a].Z - field.Obstacles[b].Z;
                    var reach = field.Obstacles[a].Radius + field.Obstacles[b].Radius;

                    Assert.True(dx*dx + dz*dz > reach*reach, "two blocks ended up inside one another");
                }
            }
        }

        [Fact]
        public void ASaucerNeverFiresAtAnything()
        {
            // It is worth a great deal and is completely harmless, which is what makes it a trap rather than a gift:
            // taking it means turning away from something that IS shooting.
            var field = new BattleField(new Randomizer(3));
            foreach (var hostile in field.Enemies)
                hostile.Alive = false;

            field.SpawnSaucer();
            var saucer = field.Enemies.Last();
            Assert.Equal(EnemyKindEnum.Saucer, saucer.Kind);
            Assert.False(saucer.IsHostile);

            var before = (saucer.X, saucer.Z);
            for (var i = 0; i < 200; i++)
            {
                saucer.Think(field, 0.033);
                Assert.DoesNotContain(field.Shells, shell => !shell.FromPlayer);
            }

            Assert.NotEqual(before, (saucer.X, saucer.Z));
        }

        [Fact]
        public void ASaucerFliesStraightAndDoesNotChaseTheEnemyItPassed()
        {
            var field = new BattleField(new Randomizer(3));
            field.SpawnSaucer();
            var saucer = field.Enemies.Last();
            var heading = saucer.Heading;

            for (var i = 0; i < 300; i++)
                saucer.Think(field, 0.033);

            Assert.Equal(heading, saucer.Heading, 9);
        }

        [Fact]
        public void ThereIsAlwaysSomethingToFight()
        {
            var field = new BattleField(new Randomizer(15));

            for (var i = 0; i < 2000; i++)
            {
                field.Advance(_frame, 0, 0);

                if (field.IsOver || field.IsRespawning)
                    continue;

                Assert.Contains(field.Enemies, enemy => enemy.IsHostile && enemy.Alive);
            }
        }

        [Fact]
        public void ASuperTankIsWorthMoreAndIsHarderToOutTurn()
        {
            var tank = new Enemy(EnemyKindEnum.Tank, 0, 0, 0);
            var super = new Enemy(EnemyKindEnum.SuperTank, 0, 0, 0);

            Assert.True(super.Value > tank.Value);
            Assert.True(super.TurnRate > tank.TurnRate);
            Assert.True(super.Speed > tank.Speed);
        }

        [Fact]
        public void SuperTanksOnlyTurnUpOnceThePlayerHasEarnedThem()
        {
            // Difficulty is the only thing the kill count is used for, and a super tank on the first spawn would be
            // a game that opens by killing you.
            for (var seed = 1; seed <= 10; seed++)
            {
                var field = new BattleField(new Randomizer(seed));
                Assert.Equal(EnemyKindEnum.Tank, field.Enemies[0].Kind);
            }
        }

        [Fact]
        public void ThePlayerCannotDriveThroughAnEnemy()
        {
            var field = new BattleField(new Randomizer(7));
            var enemy = field.Enemies[0];
            enemy.X = 0;
            enemy.Z = 14;

            for (var i = 0; i < 40; i++)
            {
                enemy.X = 0;
                enemy.Z = 14;
                field.Advance(_frame, 0, 1);
            }

            Assert.True(field.PlayerZ < 14.0 - BattleField.PlayerRadius,
                $"drove to {field.PlayerZ}, which is inside the tank at 14");
        }

        [Fact]
        public void TheRadarSweepGoesRoundAndRound()
        {
            var field = new BattleField(new Randomizer(1));
            var seen = new HashSet<int>();

            for (var i = 0; i < 400; i++)
            {
                field.Advance(_frame, 0, 0);
                seen.Add((int) (field.RadarSweep/(Math.PI/2.0)));
                Assert.InRange(field.RadarSweep, 0.0, 2.0*Math.PI);
            }

            Assert.Equal(4, seen.Count);
        }

        /// <summary>
        ///     How far off the bearing to the player a tank steers when a block sits that far ahead of it.
        /// </summary>
        /// <param name="blockRange">How far in front of the tank the block stands.</param>
        /// <returns>The deflection in radians.</returns>
        private static double Deflection(double blockRange)
        {
            var field = new BattleField(new Randomizer(5));
            var enemy = field.Enemies[0];
            enemy.X = 0;
            enemy.Z = 200;
            enemy.Heading = Math.PI;

            foreach (var obstacle in field.Obstacles)
                obstacle.Z = 900;

            var block = field.Obstacles[0];
            block.Kind = ObstacleKindEnum.Cube;
            block.X = 0;
            block.Z = 200.0 - blockRange;

            // A long step, so the turn rate does not clamp the answer and what comes back is where it WANTED to
            // point rather than how far it got.
            enemy.Think(field, 5.0);

            return Math.Abs(BattleField.WrapAngle(enemy.Heading - Math.PI));
        }

        /// <summary>
        ///     Plays out one game under one policy and reports how long the player lasted.
        /// </summary>
        /// <param name="seed">Which plain.</param>
        /// <param name="circle">True to keep the nearest tank on the beam and drive; false to sit still.</param>
        /// <returns>How many seconds the player survived.</returns>
        private static double Survive(int seed, bool circle)
        {
            var field = new BattleField(new Randomizer(seed));

            for (var i = 0; i < 6000 && !field.IsOver; i++)
            {
                var turn = 0;
                var throttle = 0;

                if (circle)
                {
                    var target = field.Enemies.FirstOrDefault(enemy => enemy.IsHostile && enemy.Alive);
                    if (target != null)
                    {
                        // Keep it on the right beam and drive: the tank is then crossing the enemy's sights rather
                        // than sitting in them, and the enemy has to keep swinging to follow.
                        var bearing = Math.Atan2(target.X - field.PlayerX, target.Z - field.PlayerZ);
                        var wanted = BattleField.WrapAngle(bearing - Math.PI/2.0);
                        var error = BattleField.WrapAngle(wanted - field.PlayerHeading);

                        turn = Math.Abs(error) < 0.05 ? 0 : Math.Sign(error);
                        throttle = 1;
                    }
                }

                field.Advance(_frame, turn, throttle);
            }

            return field.Elapsed;
        }
    }
}
