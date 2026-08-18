using System;
using System.Collections.Generic;
using WolfCurses.Core;
using Xunit;

namespace WolfCurses.Tests.Core
{
    /// <summary>
    ///     The shuffle. Small enough to be obvious and wrong often enough to be worth pinning.
    /// </summary>
    public class RandomizerShuffleTests
    {
        [Fact]
        public void EveryItemIsStillThereAfterwards()
        {
            var random = new Randomizer(7);
            var items = new List<int>();
            for (var i = 0; i < 52; i++)
                items.Add(i);

            random.Shuffle(items);
            items.Sort();

            for (var i = 0; i < 52; i++)
                Assert.Equal(i, items[i]);
        }

        [Fact]
        public void TheSameSeedShufflesTheSameWay()
        {
            var first = Deal(new Randomizer(11));
            var second = Deal(new Randomizer(11));
            var other = Deal(new Randomizer(12));

            Assert.Equal(first, second);
            Assert.NotEqual(first, other);
        }

        [Fact]
        public void ItActuallyChangesTheOrder()
        {
            var random = new Randomizer(3);
            var items = Deal(new Randomizer(3));
            var before = new List<int>(items);

            random.Shuffle(items);

            Assert.NotEqual(before, items);
        }

        [Fact]
        public void NothingAndOneThingAreLeftAloneRatherThanRefused()
        {
            var random = new Randomizer(1);

            var empty = new List<int>();
            var single = new List<string> {"only"};

            var thrown = Record.Exception(() =>
            {
                random.Shuffle(empty);
                random.Shuffle(single);
                random.Shuffle<int>(null);
            });

            Assert.Null(thrown);
            Assert.Empty(empty);
            Assert.Equal("only", single[0]);
        }

        [Fact]
        public void EveryPositionCanHoldEveryItem()
        {
            // The cheap check that the loop covers the whole list. A shuffle that skipped index zero - the classic
            // off-by-one in the descending loop - would leave one item pinned to its starting position forever, and
            // would still look thoroughly shuffled at every other index.
            const int size = 6;
            var random = new Randomizer(29);
            var seen = new HashSet<(int Item, int Position)>();

            for (var trial = 0; trial < 400; trial++)
            {
                var items = new List<int>();
                for (var i = 0; i < size; i++)
                    items.Add(i);

                random.Shuffle(items);

                for (var position = 0; position < size; position++)
                    seen.Add((items[position], position));
            }

            Assert.Equal(size*size, seen.Count);
        }

        [Fact]
        public void EveryOrderingComesUpAboutAsOftenAsEveryOther()
        {
            // THE test, and the reason this is in the library rather than in each caller. The naive shuffle - swap
            // each item with a random index anywhere in the whole list - passes every check above and is not
            // uniform: it reaches n^n equally likely outcomes for n! orderings, and those do not divide. For three
            // items that is 27 outcomes over 6 orderings, so two of them come up 5/27 of the time and four come up
            // 4/27. The bias is perfectly visible in a few thousand trials and completely invisible by eye.
            const int trials = 60_000;
            var random = new Randomizer(97);
            var counts = new Dictionary<string, int>();

            for (var trial = 0; trial < trials; trial++)
            {
                var items = new List<string> {"a", "b", "c"};
                random.Shuffle(items);
                var key = string.Concat(items);
                counts[key] = counts.TryGetValue(key, out var seen) ? seen + 1 : 1;
            }

            Assert.Equal(6, counts.Count);

            // Each ordering should be a sixth of the trials. Three per cent either way is far tighter than the
            // naive shuffle's worst case (5/27 is 12.5% high) and far looser than sampling noise at this count.
            var expected = trials / 6.0;
            foreach (var (ordering, count) in counts)
            {
                Assert.True(Math.Abs(count - expected) < expected*0.03,
                    $"\"{ordering}\" came up {count} times, expected about {expected:F0}");
            }
        }

        private static List<int> Deal(Randomizer random)
        {
            var items = new List<int>();
            for (var i = 0; i < 52; i++)
                items.Add(i);

            random.Shuffle(items);
            return items;
        }
    }
}
