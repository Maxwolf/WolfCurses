using System;
using WolfCurses.Tests.Support;
using WolfCurses.Utility;
using Xunit;

namespace WolfCurses.Tests.Utility
{
    public class StringExtensionsTests
    {
        [Fact]
        public void Truncate_Null_ReturnsNull()
        {
            Assert.Null(((string) null).Truncate(5));
        }

        [Fact]
        public void Truncate_Empty_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, string.Empty.Truncate(5));
        }

        [Theory]
        [InlineData("abc", 3)]
        [InlineData("abc", 10)]
        public void Truncate_ShorterOrEqualToMax_ReturnsOriginal(string value, int maxLength)
        {
            Assert.Equal(value, value.Truncate(maxLength));
        }

        [Fact]
        public void Truncate_LongerThanMax_ReturnsPrefix()
        {
            Assert.Equal("abc", "abcdef".Truncate(3));
        }

        [Fact]
        public void Truncate_ZeroMax_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, "abc".Truncate(0));
        }

        [Fact]
        public void Truncate_NegativeMax_ThrowsArgumentOutOfRangeException()
        {
            // Documents current behavior: the range slice value[..maxLength] rejects negative lengths.
            Assert.Throws<ArgumentOutOfRangeException>(() => "abc".Truncate(-1));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void WordWrap_WidthLessThanOne_ReturnsInputUnchanged(int width)
        {
            Assert.Equal("hello world", "hello world".WordWrap(width));
        }

        [Fact]
        public void WordWrap_ShortLine_AppendsTrailingNewline()
        {
            Assert.Equal("abc" + Text.NL, "abc".WordWrap(10));
        }

        [Fact]
        public void WordWrap_BreaksAtWordBoundary()
        {
            Assert.Equal("aaa bbb" + Text.NL + "ccc" + Text.NL, "aaa bbb ccc".WordWrap(7));
        }

        [Fact]
        public void WordWrap_UnbreakableWord_BreaksAtWidth()
        {
            Assert.Equal("abcd" + Text.NL + "efgh" + Text.NL + "ij" + Text.NL, "abcdefghij".WordWrap(4));
        }

        [Fact]
        public void WordWrap_PreservesEmptyLines()
        {
            var input = "aaa" + Text.NL + Text.NL + "bbb";
            Assert.Equal("aaa" + Text.NL + Text.NL + "bbb" + Text.NL, input.WordWrap(10));
        }

        [Fact]
        public void WordWrap_DefaultWidthIs32()
        {
            var thirtyTwo = new string('a', 32);
            var input = thirtyTwo + " tail";
            Assert.Equal(thirtyTwo + Text.NL + "tail" + Text.NL, input.WordWrap());
        }
    }
}
