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

        [Fact]
        public void SeededCtor_ExposesTheSeed()
        {
            var random = new Randomizer(1337);

            Assert.Equal(1337, random.RandomSeed);
        }

        [Fact]
        public void SeededCtor_SameSeed_ProducesIdenticalSequence()
        {
            // The whole point of the seed overload: two independently constructed randomizers with the same seed
            // yield the exact same draws, so downstream simulations become reproducible.
            var a = new Randomizer(42);
            var b = new Randomizer(42);

            for (var i = 0; i < ITERATIONS; i++)
            {
                Assert.Equal(a.Next(), b.Next());
                Assert.Equal(a.Next(0, 100), b.Next(0, 100));
                Assert.Equal(a.NextDouble(), b.NextDouble());
                Assert.Equal(a.NextBool(), b.NextBool());
            }
        }

        [Fact]
        public void SeededCtor_RecordedAutoSeed_ReplaysSameSequence()
        {
            // A session seeded from the clock can be replayed later by feeding its RandomSeed back in.
            var original = new Randomizer();
            var firstDraws = new List<int>();
            for (var i = 0; i < 20; i++)
                firstDraws.Add(original.Next());

            var replay = new Randomizer(original.RandomSeed);
            for (var i = 0; i < 20; i++)
                Assert.Equal(firstDraws[i], replay.Next());
        }
    }
}
