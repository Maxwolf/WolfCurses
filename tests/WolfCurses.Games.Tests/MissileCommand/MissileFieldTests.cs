using System;
using System.Linq;
using WolfCurses.Core;
using WolfCurses.Games.MissileCommand;
using Xunit;

namespace WolfCurses.Games.Tests.MissileCommand
{
    /// <summary>
    ///     The rules of Missile Command, driven with no console anywhere near them.
    ///     <para>
    ///         <b>Every field here is built on a seeded <see cref="Randomizer" />.</b> The game's own is unseeded on
    ///         purpose, and a test that reaches into random state and names a particular city or a particular warhead
    ///         is the exact shape of the two flaky tests this repository has already shipped. Where a specific thing
    ///         is needed it is <i>put</i> there — <c>Spawn</c> and <c>Detonate</c> exist for that — and where an
    ///         outcome is randomised the assertion is an invariant checked across many seeds rather than one expected
    ///         answer.
    ///     </para>
    ///     <para>
    ///         <b>The eight-second window.</b> Several tests here place warheads at around half altitude and need no
    ///         interference from the wave the field is launching on its own. They get it for free: wave one drops at
    ///         0.055 world units a second from the very top, so nothing the field launches can reach the middle of
    ///         the sky for about eight seconds. Every scripted scenario below finishes long before that, which is
    ///         why none of them has to suppress the wave.
    ///     </para>
    /// </summary>
    public class MissileFieldTests
    {
        private static readonly TimeSpan _step = TimeSpan.FromMilliseconds(16);

        private static MissileField Field(int seed = 1) => new(new Randomizer(seed));

        /// <summary>Advances a number of whole steps.</summary>
        private static void Run(MissileField field, int steps, TimeSpan? size = null)
        {
            for (var i = 0; i < steps; i++)
                field.Advance(size ?? _step);
        }

        // ------------------------------------------------------------ firing

        [Fact]
        public void AFiredShellDetonatesWhereItWasAimedAndNotBefore()
        {
            var field = Field();
            const double aimX = 0.80;
            const double aimY = 0.70;

            Assert.True(field.Fire(1, aimX, aimY));
            Assert.Equal(MissileField.AmmoPerSilo - 1, field.SiloAmmo[1]);

            // Flight time is known exactly: the shell travels a straight line at the battery's own speed.
            var distance = Math.Sqrt(Math.Pow(aimX - MissileField.SiloPositions[1], 2) +
                                     Math.Pow(aimY - MissileField.GroundY, 2));
            var flight = distance/MissileField.CounterSpeed(1);

            // Half way there, nothing has gone off.
            Run(field, (int) (flight*0.5/_step.TotalSeconds));
            Assert.Empty(field.Blasts);

            Run(field, (int) (flight*0.6/_step.TotalSeconds) + 2);
            var blast = Assert.Single(field.Blasts);
            Assert.True(Math.Abs(blast.X - aimX) < 0.01 && Math.Abs(blast.Y - aimY) < 0.01,
                $"it went off at {blast.X:F3},{blast.Y:F3} rather than where it was aimed");
        }

        [Fact]
        public void FiringBelowTheAimFloorIsRefusedAndSpendsNothing()
        {
            var field = Field();

            Assert.False(field.Fire(1, 0.80, MissileField.MinAimY - 0.01));

            Assert.Equal(MissileField.AmmoPerSilo, field.SiloAmmo[1]);
            Assert.DoesNotContain(field.Missiles, m => m.Kind == MissileKindEnum.Counter);
        }

        [Fact]
        public void NoMoreThanEightShellsFlyAtOnce()
        {
            var field = Field();

            for (var i = 0; i < MissileField.MaxCountersInFlight; i++)
                Assert.True(field.Fire(i%3, 0.20 + i*0.15, 0.85), $"shell {i} was refused");

            Assert.False(field.Fire(1, 0.80, 0.85), "a ninth shell went up");
        }

        [Fact]
        public void APlayersOwnShellFliesStraightThroughItsOwnCloud()
        {
            // The arcade's rule, and the only thing stopping a wall of fireballs from being a wall to the player too.
            var field = Field();
            field.Detonate(0.80, 0.60, true);
            Run(field, 40); // let it grow

            Assert.True(field.Fire(1, 0.80, 0.90));
            var shell = field.Missiles.Single(m => m.Kind == MissileKindEnum.Counter);

            Run(field, 40);

            Assert.True(shell.Alive || shell.HasArrived, "the player's shell was destroyed by the player's blast");
        }

        // ------------------------------------------------------------ which battery

        [Fact]
        public void TheMiddleBatteryIsChosenOverACloserFlankBecauseItIsFaster()
        {
            // The mutation this exists for is ranking by distance. A point must be picked that is genuinely NEARER a
            // flank, or the test passes under both rules and says nothing.
            var field = Field();
            const double x = 0.30;
            const double y = 0.90;

            var toLeft = Math.Sqrt(Math.Pow(x - MissileField.SiloPositions[0], 2) +
                                   Math.Pow(y - MissileField.GroundY, 2));
            var toMiddle = Math.Sqrt(Math.Pow(x - MissileField.SiloPositions[1], 2) +
                                     Math.Pow(y - MissileField.GroundY, 2));
            Assert.True(toLeft < toMiddle, "the point has to be nearer the flank for this test to mean anything");

            Assert.Equal(1, field.BestSilo(x, y));
        }

        [Fact]
        public void ADryBatteryIsPassedOverForOneWithShellsLeft()
        {
            var field = Field();

            // Drained through the public door rather than by reaching in, which also proves shells come back.
            while (field.SiloAmmo[1] > 0)
            {
                if (!field.Fire(1, 0.80, 0.90))
                    Run(field, 20);
            }

            Assert.NotEqual(1, field.BestSilo(0.80, 0.90));
            Assert.True(field.BestSilo(0.80, 0.90) >= 0, "the flanks should still be able to answer");
        }

        [Fact]
        public void WhenEveryBatteryIsDryThereIsNoAnswerAtAll()
        {
            var field = Field();

            for (var silo = 0; silo < 3; silo++)
            {
                while (field.SiloAmmo[silo] > 0)
                {
                    if (!field.Fire(silo, 0.80, 0.90))
                        Run(field, 20);
                }
            }

            Assert.Equal(-1, field.BestSilo(0.80, 0.90));
        }

        // ------------------------------------------------------------ the swept test

        [Fact]
        public void AFireballCatchesWhatSweepsThroughItEvenWhenBothEndsAreOutside()
        {
            // The mutation: test the point the warhead landed on instead of the segment it swept. Both ends of this
            // segment are far outside the fireball and it passes straight through the middle, so a point test says
            // no twice and the warhead sails through untouched.
            var blast = new Blast(0.50, 0.50, false);
            blast.Advance(0.6);

            Assert.True(blast.Radius > 0.0 && blast.Radius < 0.30, "the fixture only works while the blast is small");

            Assert.True(blast.Catches(0.50, 0.90, 0.50, 0.10), "a segment through the centre was not caught");
            Assert.False(blast.Catches(0.50, 0.90, 0.50, 0.80), "a segment nowhere near it was caught");
        }

        [Fact]
        public void AFireballCatchesAWarheadThatCrossesItInOneBigStep()
        {
            var field = Field();
            var blast = field.Detonate(0.50, 0.60, true);
            Run(field, 40);

            // Fast enough to be clean past the fireball by the end of a single clamped step, and aimed short of the
            // ground so it has not merely arrived.
            var warhead = field.Spawn(MissileKindEnum.Icbm, 0.50, 0.75, 0.50, 0.20, 4.0);

            // Both ENDS of the step have to be outside the fireball or a point test would catch it too and this
            // proves nothing. Stated as an assertion rather than left to the reader to work out from the numbers.
            var before = Math.Abs(0.75 - blast.Y);
            var after = Math.Abs(0.35 - blast.Y);
            Assert.True(before > blast.Radius && after > blast.Radius,
                $"the fireball is {blast.Radius:F3} across and the step runs from {before:F3} to {after:F3} away " +
                "from it - one of those ends is inside, so a point test would pass this too");

            field.Advance(TimeSpan.FromMilliseconds(100));

            Assert.False(warhead.HasArrived, "it reached its target, so this is testing landing rather than catching");
            Assert.False(warhead.Alive, "it went straight through");
        }

        // ------------------------------------------------------------ the chain reaction

        [Fact]
        public void AKillLeavesAFireballThatKillsAgainOnALaterStep()
        {
            // The best thing in this game, and it is only visible because each link costs a frame. Resolve the whole
            // cascade inside one step - which an index loop over a growing list does quite happily - and six kills
            // are indistinguishable from one lucky shot.
            var field = Field(11);

            var first = field.Spawn(MissileKindEnum.Icbm, 0.840, 0.62, 0.840, MissileField.GroundY, 0.02);
            var second = field.Spawn(MissileKindEnum.Icbm, 0.885, 0.62, 0.885, MissileField.GroundY, 0.02);
            field.Detonate(0.800, 0.55, true);

            // The claim only means anything if the seeded fireball could never have reached the second warhead by
            // itself. Its entire descent is further from that centre than a blast can ever reach.
            Assert.True(0.885 - 0.800 > Blast.MaxRadius,
                "the second warhead is inside the first blast's reach, so this proves nothing");

            var firstDied = -1;
            var secondDied = -1;
            for (var step = 0; step < 400 && secondDied < 0; step++)
            {
                field.Advance(_step);

                if (firstDied < 0 && !first.Alive)
                    firstDied = step;
                if (secondDied < 0 && !second.Alive)
                    secondDied = step;
            }

            Assert.True(firstDied >= 0, "the seeded fireball never got the first warhead");
            Assert.True(secondDied >= 0, "the chain never reached the second warhead");
            Assert.True(secondDied > firstDied,
                $"both died on step {secondDied}, so the cascade resolved inside one step");
        }

        [Fact]
        public void TheCascadeSurvivesTheBlastListGrowingUnderIt()
        {
            // Every one of these dies in the same step, so the list of fireballs grows while it is being walked.
            var field = Field(3);
            for (var i = 0; i < 8; i++)
                field.Spawn(MissileKindEnum.Icbm, 0.50 + i*0.004, 0.50, 0.50, MissileField.GroundY, 0.01);

            field.Detonate(0.50, 0.50, true);

            var exception = Record.Exception(() => Run(field, 60));

            Assert.Null(exception);
        }

        [Fact]
        public void AWarheadKilledInFlightIsWorthPointsAndOneThatLandsIsWorthNothing()
        {
            var killed = Field(5);
            killed.Spawn(MissileKindEnum.Icbm, 0.50, 0.50, 0.50, MissileField.GroundY, 0.01);
            killed.Detonate(0.50, 0.50, true);
            Run(killed, 30);
            Assert.True(killed.Score > 0, "shooting one down paid nothing");

            var landed = Field(5);
            landed.Spawn(MissileKindEnum.Icbm, MissileField.CityPositions[0], 0.20,
                MissileField.CityPositions[0], MissileField.GroundY, 1.0);
            Run(landed, 30);
            Assert.False(landed.CitiesStanding[0], "the city survived, so nothing was tested");
            Assert.Equal(0, landed.Score);
        }

        // ------------------------------------------------------------ time

        [Fact]
        public void AnEnormousStepIsClampedRatherThanTeleportingEverythingThroughTheGround()
        {
            // A garbage collection, an alt-tab or a debugger breakpoint hands the next call an arbitrary amount of
            // time. Un-clamped, a wave lands all at once while the player is looking at a stalled screen.
            var field = Field();
            var warhead = field.Spawn(MissileKindEnum.Icbm, MissileField.CityPositions[0], 1.0,
                MissileField.CityPositions[0], MissileField.GroundY, 0.5);

            field.Advance(TimeSpan.FromHours(1));

            Assert.True(warhead.Alive, "it teleported into the ground");
            Assert.True(warhead.Y > 0.9, $"it fell to {warhead.Y:F3} in a single step");
            Assert.Equal(6, field.CitiesRemaining);
        }

        [Fact]
        public void AFireballGrowsThenShrinksAndIsNeverNegative()
        {
            // Sampled with irregular steps on purpose: an accumulated radius tracks a uniform sampling closely
            // enough to pass, and only comes apart when the steps vary the way real frames do.
            var blast = new Blast(0.5, 0.5, false);
            var deltas = new[] {0.004, 0.031, 0.017, 0.083, 0.009, 0.055, 0.022, 0.070};

            var peak = 0.0;
            var peakAt = 0.0;
            var age = 0.0;
            var index = 0;
            var lateRadius = -1.0;

            while (age < Blast.Lifetime.TotalSeconds*1.2)
            {
                var delta = deltas[index++%deltas.Length];
                blast.Advance(delta);
                age += delta;

                Assert.True(blast.Radius >= 0.0, $"radius went to {blast.Radius:F4} at age {age:F2}");
                Assert.True(blast.Radius <= Blast.MaxRadius + 1e-9, $"radius reached {blast.Radius:F4}");

                // Sampled near the end of its life, which is the ONLY reading that says it fades at all. A blast
                // that grows to full size and then simply holds it passes every other assertion here: the peak is
                // still the right size and is still reached early, because a value that stops rising is never
                // greater than the peak already recorded.
                if (lateRadius < 0.0 && age > Blast.Lifetime.TotalSeconds*0.9)
                    lateRadius = blast.Radius;

                if (blast.Radius <= peak)
                    continue;

                peak = blast.Radius;
                peakAt = age;
            }

            Assert.True(peak > Blast.MaxRadius*0.9, $"it only ever reached {peak:F4}");
            Assert.True(peakAt < Blast.Lifetime.TotalSeconds*0.6, "it should bloom faster than it fades");
            Assert.True(lateRadius >= 0.0 && lateRadius < Blast.MaxRadius*0.3,
                $"nine tenths of the way through its life it was still {lateRadius:F4} across, so it never faded");
            Assert.False(blast.Alive);
        }

        // ------------------------------------------------------------ waves

        [Fact]
        public void AMirvLetsItsHeadsGoOnceAndOnceOnly()
        {
            var field = Field(9);
            var parent = field.Spawn(MissileKindEnum.Mirv, 0.80, 0.65, 0.80, MissileField.GroundY, 0.10);

            // Counted by where they came from rather than by how long the test has run. Everything the field
            // launches itself starts at the very top, so filtering those out lets this run as long as it likes
            // instead of racing the wave schedule - a coupling that would have made it fail the day somebody
            // retuned the launch interval.
            int Scripted() => field.Missiles.Count(m => m.OriginY < 0.9);

            Assert.Equal(1, Scripted());

            Run(field, 40);
            Assert.True(parent.HasSplit, "it never split");

            var afterSplit = Scripted();
            Assert.InRange(afterSplit, 3, 4); // the parent plus its two or three heads

            Run(field, 60);
            Assert.Equal(afterSplit, Scripted());
            Assert.True(parent.HasSplit);
        }

        [Fact]
        public void ASmartBombSwervesAroundAFireballButOnlySoManyTimes()
        {
            var field = Field(13);
            var bomb = field.Spawn(MissileKindEnum.SmartBomb, 0.80, 0.60, 0.80, MissileField.GroundY, 0.05);
            field.Detonate(0.80, 0.45, true);
            Run(field, 40);

            Assert.True(bomb.Dodges > 0, "it flew straight into the fireball");
            Assert.True(bomb.Alive, "it did not survive its own dodge");

            // Bounded, which is what makes it beatable rather than a war of attrition.
            for (var i = 0; i < 6; i++)
            {
                field.Detonate(bomb.X, bomb.Y - 0.10, true);
                Run(field, 30);
            }

            Assert.True(bomb.Dodges <= 3, $"it dodged {bomb.Dodges} times and should stop at three");
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(7)]
        [InlineData(8)]
        [InlineData(9)]
        [InlineData(10)]
        public void NoSingleWaveIsEverAllowedToTakeMoreThanThreeCities(int seed)
        {
            // Checked as an invariant over many seeds, never as one expected outcome. Deleting the cap passes on a
            // single seed most of the time, because most waves would not have taken four cities anyway.
            var field = Field(seed);

            for (var step = 0; step < 4000 && field.Wave == 1 && !field.IsOver; step++)
            {
                field.Advance(TimeSpan.FromMilliseconds(50));

                Assert.True(field.CitiesRemaining >= 6 - MissileField.MaxCitiesLostPerWave,
                    $"wave 1 got down to {field.CitiesRemaining} cities on seed {seed}");
            }
        }

        [Fact]
        public void EveryBatteryIsRebuiltAndRearmedWhenAWaveTurnsOver()
        {
            var field = Field(2);

            // Flatten the middle battery and spend some of another's shells, so the reset has something to undo.
            field.Spawn(MissileKindEnum.Icbm, MissileField.SiloPositions[1], 0.30,
                MissileField.SiloPositions[1], MissileField.GroundY, 1.0);
            Run(field, 30);
            Assert.False(field.SilosStanding[1], "the battery should have been flattened");

            field.Fire(0, 0.30, 0.90);
            Assert.Equal(MissileField.AmmoPerSilo - 1, field.SiloAmmo[0]);

            AdvanceToWave(field, 2);

            Assert.All(field.SilosStanding, standing => Assert.True(standing));
            Assert.All(field.SiloAmmo, ammo => Assert.Equal(MissileField.AmmoPerSilo, ammo));
        }

        [Fact]
        public void TheWaveBonusPaysForUnfiredShellsAndSurvivingCities()
        {
            var field = Field(4);

            AdvanceToWave(field, 2);

            // Nothing was shot down, so every point on the board came from the tally: five a shell and a hundred a
            // city, both times the wave's multiplier, which is one for wave one.
            var expected = 3*MissileField.AmmoPerSilo*5 + field.CitiesRemaining*100;
            Assert.Equal(expected, field.Score);
        }

        [Fact]
        public void TheMultiplierRisesEveryOtherWave()
        {
            // Reaching wave three needs somebody to actually defend: left alone the field takes three cities a wave
            // and the game is over during the second, which is the rules working rather than a broken test.
            var field = Field(6);

            Assert.Equal(1, field.Multiplier);
            PlayUntilWave(field, 2);
            Assert.Equal(1, field.Multiplier);
            PlayUntilWave(field, 3);
            Assert.Equal(2, field.Multiplier);
        }

        [Fact]
        public void ThePlayerCanSurviveWavesTheyActuallyDefend()
        {
            // The end-to-end statement, and the only test here that exercises the whole loop the way a person does:
            // warheads launch, shells fly, fireballs catch them, the wave tallies up and the next one deals. It is
            // also the thing that would notice the game becoming unwinnable after a tuning change.
            var field = Field(21);

            PlayUntilWave(field, 4);

            Assert.False(field.IsOver, "the defender lost, so something has become unwinnable");
            Assert.True(field.Score > 0);
            Assert.True(field.CitiesRemaining >= 1);
        }

        // ------------------------------------------------------------ the ending

        [Fact]
        public void TheFieldKeepsMovingAfterTheLastCityFalls()
        {
            // Neither of the other endings in this arcade: the board is still in motion when the player loses, so
            // the last warheads finish their arcs onto the ruins instead of a dialog covering the best frame.
            var field = Field(8);

            foreach (var city in MissileField.CityPositions)
                field.Spawn(MissileKindEnum.Icbm, city, 0.30, city, MissileField.GroundY, 1.0);

            var bystander = field.Spawn(MissileKindEnum.Icbm, 0.05, 0.95, 0.05, MissileField.GroundY, 0.05);

            Run(field, 40);

            Assert.True(field.IsOver, "the cities should all be gone");
            Assert.Equal(0, field.CitiesRemaining);
            Assert.False(field.IsQuiet, "there is still something in the air");
            Assert.False(field.Fire(1, 0.80, 0.90), "the batteries should have stopped answering");

            var was = bystander.Y;
            Run(field, 40);
            Assert.True(bystander.Y < was, "the simulation stopped dead instead of playing out");
        }

        [Fact]
        public void TheGameEventuallyFallsQuietOnceEverythingHasLanded()
        {
            var field = Field(8);
            foreach (var city in MissileField.CityPositions)
                field.Spawn(MissileKindEnum.Icbm, city, 0.30, city, MissileField.GroundY, 1.0);

            Run(field, 40);
            Assert.True(field.IsOver);

            Run(field, 400);

            Assert.True(field.IsQuiet, "something was still moving long after the game ended");
            Assert.Contains("GAME OVER", field.Message, StringComparison.Ordinal);
        }

        /// <summary>Runs the field, with no player input at all, until it reaches a wave.</summary>
        private static void AdvanceToWave(MissileField field, int wave)
        {
            for (var step = 0; step < 8000 && field.Wave < wave && !field.IsOver; step++)
                field.Advance(TimeSpan.FromMilliseconds(50));

            Assert.True(field.Wave >= wave, $"never reached wave {wave}; stopped on {field.Wave}, over={field.IsOver}");
        }

        /// <summary>Runs the field with a defender playing it, until it reaches a wave.</summary>
        private static void PlayUntilWave(MissileField field, int wave)
        {
            var delta = TimeSpan.FromMilliseconds(50);

            for (var step = 0; step < 12_000 && field.Wave < wave && !field.IsOver; step++)
            {
                if (step%4 == 0)
                    Defend(field);

                field.Advance(delta);
            }

            Assert.True(field.Wave >= wave,
                $"never reached wave {wave}; stopped on {field.Wave} with {field.CitiesRemaining} cities, over={field.IsOver}");
        }

        /// <summary>
        ///     Plays about as well as somebody who has read the instructions: find the lowest warhead that nothing is
        ///     already going to deal with, work out where it will be by the time a shell can get there, and put one
        ///     there.
        ///     <para>
        ///         The lead is the part worth spelling out. Firing at where a warhead <i>is</i> puts the fireball
        ///         where it <i>was</i> by the time the shell arrives, which misses everything and looks for all the
        ///         world like the collision test is broken.
        ///     </para>
        /// </summary>
        private static void Defend(MissileField field)
        {
            Missile lowest = null;
            var counters = 0;

            foreach (var missile in field.Missiles)
            {
                if (!missile.Alive)
                    continue;

                if (missile.Kind == MissileKindEnum.Counter)
                {
                    counters++;
                    continue;
                }

                if (missile.Y >= MissileField.MinAimY && (lowest == null || missile.Y < lowest.Y))
                    lowest = missile;
            }

            // Leave some shells in the air rather than emptying every battery at the first thing that appears.
            if (lowest == null || counters >= 4)
                return;

            // Something already burning is going to get it, so do not spend a second shell on it.
            foreach (var blast in field.Blasts)
            {
                if (blast.Catches(lowest.X, lowest.Y, lowest.TargetX, lowest.TargetY))
                    return;
            }

            // And neither is a shell already on its way there. Checking only the fireballs is not enough and it is
            // the difference between winning and losing: a shell takes the better part of a second to arrive, so a
            // defender that only looks for fireballs keeps firing at the same warhead every tick until one finally
            // appears, and runs a battery dry on a target it had already dealt with four times over.
            foreach (var shell in field.Missiles)
            {
                if (shell.Kind != MissileKindEnum.Counter || !shell.Alive)
                    continue;

                var claimX = shell.TargetX - lowest.X;
                var claimY = shell.TargetY - lowest.Y;
                if (claimX*claimX + claimY*claimY < 0.15*0.15)
                    return;
            }

            var silo = field.BestSilo(lowest.X, lowest.Y);
            if (silo < 0)
                return;

            var dx = lowest.X - MissileField.SiloPositions[silo];
            var dy = lowest.Y - MissileField.GroundY;
            var flight = Math.Sqrt(dx*dx + dy*dy)/MissileField.CounterSpeed(silo);

            var travelled = lowest.Length <= double.Epsilon ? 0.0 : flight*lowest.Speed/lowest.Length;
            var aimX = lowest.X + (lowest.TargetX - lowest.OriginX)*travelled;
            var aimY = Math.Max(MissileField.MinAimY, lowest.Y + (lowest.TargetY - lowest.OriginY)*travelled);

            field.Fire(silo, aimX, aimY);
        }
    }
}
