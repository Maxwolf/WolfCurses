using System.Collections.Generic;
using WolfCurses.Core;
using Xunit;

namespace WolfCurses.Tests.Core
{
    /// <summary>
    ///     Randomizer seeds from the wall clock with no injection seam, so only bounds and distribution shape can be
    ///     asserted, never exact sequences.
    /// </summary>
    public class RandomizerTests
    {
        private const int ITERATIONS = 1000;

        [Fact]
        public void Next_Returns0Through59()
        {
            var random = new Randomizer();

            for (var i = 0; i < ITERATIONS; i++)
                Assert.InRange(random.Next(), 0, 59);
        }

        [Fact]
        public void NextMax_StaysBelowMax()
        {
            var random = new Randomizer();

            for (var i = 0; i < ITERATIONS; i++)
                Assert.InRange(random.Next(10), 0, 9);
        }

        [Fact]
        public void NextMinMax_StaysWithinRange()
        {
            var random = new Randomizer();

            for (var i = 0; i < ITERATIONS; i++)
                Assert.InRange(random.Next(5, 15), 5, 14);
        }

        [Fact]
        public void NextDouble_StaysInUnitInterval()
        {
            var random = new Randomizer();

            for (var i = 0; i < ITERATIONS; i++)
                Assert.InRange(random.NextDouble(), 0.0, 1.0);
        }

        [Fact]
        public void NextBool_ProducesBothValues()
        {
            var random = new Randomizer();
            var seen = new HashSet<bool>();

            for (var i = 0; i < ITERATIONS && seen.Count < 2; i++)
                seen.Add(random.NextBool());

            Assert.Equal(2, seen.Count);
        }
    }
}
