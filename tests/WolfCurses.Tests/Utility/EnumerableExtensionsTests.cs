using System.Linq;
using WolfCurses.Utility;
using Xunit;

namespace WolfCurses.Tests.Utility
{
    /// <summary>
    ///     Shuffle orders by fresh GUIDs and is not seedable, so these tests assert membership and size, never order.
    /// </summary>
    public class EnumerableExtensionsTests
    {
        private static readonly int[] _source = { 1, 2, 3, 4, 5 };

        [Fact]
        public void PickRandom_Single_ReturnsItemFromSource()
        {
            var picked = _source.PickRandom(1).ToList();

            var item = Assert.Single(picked);
            Assert.Contains(item, _source);
        }

        [Fact]
        public void PickRandom_Count_ReturnsThatManyDistinctItemsAllFromSource()
        {
            var picked = _source.PickRandom(3).ToList();

            Assert.Equal(3, picked.Count);
            Assert.Equal(3, picked.Distinct().Count());
            Assert.All(picked, item => Assert.Contains(item, _source));
        }

        [Fact]
        public void PickRandom_CountExceedsSource_ReturnsAllItems()
        {
            var picked = _source.PickRandom(99).OrderBy(x => x);

            Assert.Equal(_source, picked);
        }

        [Fact]
        public void PickRandom_EmptySource_ReturnsEmpty()
        {
            Assert.Empty(System.Array.Empty<int>().PickRandom(3));
        }

        [Fact]
        public void Shuffle_ReturnsSameMultiset()
        {
            // .NET 10 introduced System.Linq Shuffle, so the library extension is called explicitly.
            var source = new[] { 1, 2, 2, 3, 3, 3 };

            var shuffled = EnumerableExtensions.Shuffle(source).OrderBy(x => x);

            Assert.Equal(source.OrderBy(x => x), shuffled);
        }

        [Fact]
        public void Shuffle_Empty_ReturnsEmpty()
        {
            Assert.Empty(EnumerableExtensions.Shuffle(System.Array.Empty<int>()));
        }
    }
}
