using System;
using WolfCurses.Games.Battlezone;
using Xunit;

namespace WolfCurses.Games.Tests.Battlezone
{
    /// <summary>
    ///     Driving a tank from a keyboard that can only report one key at a time.
    /// </summary>
    public class TankControlsTests
    {
        private static readonly TimeSpan _frame = TimeSpan.FromMilliseconds(33);

        [Fact]
        public void SteeringDoesNotStopTheTank()
        {
            // THE bug a play-test found in about ten seconds, and the reason this class exists at all. An operating
            // system repeats only the most recently pressed key, so with the throttle on a held axis, holding
            // forward and then pressing left silences the forward repeats entirely - the forward axis is correctly
            // inferred to have been released, and the tank stops dead every time the player tries to turn.
            var controls = new TankControls();
            controls.Shift(1);

            for (var i = 0; i < 200; i++)
            {
                controls.PressTurn(-1);
                controls.TurnFor(_frame, BattleField.PlayerTurnRate);

                Assert.Equal(1, controls.Gear);
            }
        }

        [Fact]
        public void TheGearStepsThroughReverseStopAndAheadAndNoFurther()
        {
            var controls = new TankControls();
            Assert.Equal(0, controls.Gear);

            controls.Shift(1);
            Assert.Equal(1, controls.Gear);

            controls.Shift(1);
            controls.Shift(1);
            Assert.Equal(1, controls.Gear);

            controls.Shift(-1);
            Assert.Equal(0, controls.Gear);

            controls.Shift(-1);
            Assert.Equal(-1, controls.Gear);

            controls.Shift(-1);
            controls.Shift(-1);
            Assert.Equal(-1, controls.Gear);
        }

        [Fact]
        public void TheGearStaysWhereItWasPut()
        {
            // The whole point of a gear rather than a held key: nothing has to keep saying so.
            var controls = new TankControls();
            controls.Shift(1);

            for (var i = 0; i < 500; i++)
                controls.TurnFor(_frame, BattleField.PlayerTurnRate);

            Assert.Equal(1, controls.Gear);
        }

        [Fact]
        public void TheBrakeStopsWithoutSteppingThroughTheGears()
        {
            var controls = new TankControls();
            controls.Shift(1);

            controls.Halt();

            Assert.Equal(0, controls.Gear);
        }

        [Fact]
        public void ATapTurnsForAWhileAndThenStopsOnItsOwn()
        {
            // A press buys a fixed amount of turning, paid out over the frames that follow - so a tap is a precise
            // nudge, which is what aiming at four hundred units actually needs.
            var controls = new TankControls();
            controls.PressTurn(1);

            var turning = 0;
            for (var i = 0; i < 200; i++)
            {
                if (controls.TurnFor(_frame, BattleField.PlayerTurnRate) != 0)
                    turning++;
            }

            var expected = TankControls.TurnPerPress/BattleField.PlayerTurnRate/_frame.TotalSeconds;

            Assert.InRange(turning, (int) expected, (int) expected + 2);
            Assert.Equal(0.0, controls.TurnDebt);
        }

        [Fact]
        public void AHeldKeyTurnsWithoutEverStopping()
        {
            // Once the operating system starts repeating, a press arrives about every frame, and the nudges overlap
            // into one continuous turn.
            var controls = new TankControls();

            for (var i = 0; i < 300; i++)
            {
                controls.PressTurn(-1);
                Assert.Equal(-1, controls.TurnFor(_frame, BattleField.PlayerTurnRate));
            }
        }

        [Fact]
        public void LettingGoCoastsByAboutOneNudgeAndNoMore()
        {
            // The cap is the coast-after-release, so it is kept to barely more than a single press. Without it a
            // held turn builds a debt that keeps being paid out long after the key is released, and the tank sails
            // straight past whatever it was being lined up on.
            var controls = new TankControls();

            for (var i = 0; i < 300; i++)
            {
                controls.PressTurn(1);
                controls.TurnFor(_frame, BattleField.PlayerTurnRate);
            }

            Assert.True(controls.TurnDebt <= TankControls.MaxTurnDebt,
                $"a held turn had built up {controls.TurnDebt:F2} radians of debt");

            var coasted = 0;
            while (controls.TurnFor(_frame, BattleField.PlayerTurnRate) != 0)
            {
                coasted++;
                Assert.True(coasted < 100, "it never stopped turning");
            }

            Assert.True(coasted*BattleField.PlayerTurnRate*_frame.TotalSeconds <= TankControls.MaxTurnDebt + 1e-9,
                $"it coasted for {coasted} frames after the key was let go");
        }

        [Fact]
        public void TurningTheOtherWaySpendsWhatWasOwedRatherThanAddingToIt()
        {
            // A correction has to take effect on the next frame, not after finishing the turn it is correcting.
            var controls = new TankControls();

            controls.PressTurn(1);
            controls.PressTurn(1);
            controls.PressTurn(-1);

            Assert.Equal(-1, controls.TurnFor(_frame, BattleField.PlayerTurnRate));
            Assert.True(controls.TurnDebt <= TankControls.TurnPerPress);
        }

        [Fact]
        public void NothingPressedIsNothingTurned()
        {
            var controls = new TankControls();

            for (var i = 0; i < 50; i++)
                Assert.Equal(0, controls.TurnFor(_frame, BattleField.PlayerTurnRate));
        }

        [Fact]
        public void ResettingPutsItBackToAStandstill()
        {
            var controls = new TankControls();
            controls.Shift(1);
            controls.PressTurn(1);

            controls.Reset();

            Assert.Equal(0, controls.Gear);
            Assert.Equal(0.0, controls.TurnDebt);
            Assert.Equal(0, controls.TurnFor(_frame, BattleField.PlayerTurnRate));
        }

        [Fact]
        public void ATankCanDriveAndTurnThroughAWholeGame()
        {
            // End to end against the real world, which is what the report was actually about: the tank has to both
            // go somewhere and end up pointing somewhere else.
            var field = new BattleField(new WolfCurses.Core.Randomizer(9));
            var controls = new TankControls();
            controls.Shift(1);

            // Summed frame by frame rather than read off the heading at the end. Six seconds of holding right is
            // most of a full circle, so the final heading has wrapped past pi and comes back as a small negative
            // number - a tank that turned three hundred and forty degrees looked like one that had barely moved,
            // which is a statement about angles and not about steering.
            var turned = 0.0;
            var previous = field.PlayerHeading;

            for (var i = 0; i < 200; i++)
            {
                controls.PressTurn(1);
                field.Advance(_frame, controls.TurnFor(_frame, BattleField.PlayerTurnRate), controls.Gear);

                turned += Math.Abs(BattleField.WrapAngle(field.PlayerHeading - previous));
                previous = field.PlayerHeading;
            }

            var moved = Math.Sqrt(field.PlayerX*field.PlayerX + field.PlayerZ*field.PlayerZ);

            Assert.True(moved > 20.0, $"the tank only got {moved:F1} units from where it started");
            Assert.True(turned > 1.0, $"it only turned {turned:F2} radians altogether");
        }
    }
}
