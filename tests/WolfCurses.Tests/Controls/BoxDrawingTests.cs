using System.Collections.Generic;
using WolfCurses.Window.Control;
using Xunit;

namespace WolfCurses.Tests.Controls
{
    /// <summary>
    ///     The junction table. A pure lookup, so it is tested exhaustively — all sixteen connection patterns in all
    ///     five styles is eighty cases, which is small enough to state outright rather than sample.
    /// </summary>
    public class BoxDrawingTests
    {
        [Theory]
        [InlineData(false, false, false, false, '─')] // an island: nothing to follow
        [InlineData(false, false, false, true, '─')] // one connection takes the whole line for its axis
        [InlineData(false, false, true, false, '─')]
        [InlineData(false, true, false, false, '│')]
        [InlineData(true, false, false, false, '│')]
        [InlineData(false, false, true, true, '─')]
        [InlineData(true, true, false, false, '│')]
        [InlineData(false, true, false, true, '┌')]
        [InlineData(false, true, true, false, '┐')]
        [InlineData(true, false, false, true, '└')]
        [InlineData(true, false, true, false, '┘')]
        [InlineData(false, true, true, true, '┬')]
        [InlineData(true, false, true, true, '┴')]
        [InlineData(true, true, false, true, '├')]
        [InlineData(true, true, true, false, '┤')]
        [InlineData(true, true, true, true, '┼')]
        public void EverySingleLinePatternHasItsGlyph(bool up, bool down, bool left, bool right, char expected)
        {
            Assert.Equal(expected, BoxDrawing.Junction(up, down, left, right));
        }

        [Theory]
        [InlineData(false, true, false, true, '╔')]
        [InlineData(false, true, true, false, '╗')]
        [InlineData(true, false, false, true, '╚')]
        [InlineData(true, false, true, false, '╝')]
        [InlineData(false, true, true, true, '╦')]
        [InlineData(true, false, true, true, '╩')]
        [InlineData(true, true, false, true, '╠')]
        [InlineData(true, true, true, false, '╣')]
        [InlineData(true, true, true, true, '╬')]
        [InlineData(true, true, false, false, '║')]
        [InlineData(false, false, true, true, '═')]
        public void TheDoubleLineSetIsTheSameShapesInItsOwnAlphabet(bool up, bool down, bool left, bool right,
            char expected)
        {
            Assert.Equal(expected, BoxDrawing.Junction(up, down, left, right, BoxBorderEnum.Double));
        }

        [Fact]
        public void RoundingChangesTheCornersAndNothingElse()
        {
            // A corner is the only thing there is to round, so every tee, cross and straight run must come back
            // byte-identical to the single-line answer. Anything else means the table was copied rather than edited.
            for (var mask = 0; mask < 16; mask++)
            {
                var (up, down, left, right) = Unpack(mask);
                var single = BoxDrawing.Junction(up, down, left, right);
                var rounded = BoxDrawing.Junction(up, down, left, right, BoxBorderEnum.Rounded);

                var isCorner = (up ^ down) && (left ^ right);
                if (isCorner)
                    Assert.NotEqual(single, rounded);
                else
                    Assert.Equal(single, rounded);
            }
        }

        [Fact]
        public void EveryStyleAnswersEveryPatternWithAGlyphOfItsOwnAlphabet()
        {
            // The property that makes the type safe to switch styles under: no pattern falls through to a default,
            // and no style borrows a character from another - which is the failure a hand-written switch produces
            // when one case is forgotten and lands on the single-line fallback.
            var alphabets = new Dictionary<BoxBorderEnum, string>
            {
                [BoxBorderEnum.Single] = "─│┌┐└┘├┤┬┴┼",
                [BoxBorderEnum.Double] = "═║╔╗╚╝╠╣╦╩╬",
                [BoxBorderEnum.Rounded] = "─│╭╮╰╯├┤┬┴┼",
                [BoxBorderEnum.Ascii] = "-|+"
            };

            foreach (var (border, alphabet) in alphabets)
            {
                for (var mask = 0; mask < 16; mask++)
                {
                    var (up, down, left, right) = Unpack(mask);
                    var glyph = BoxDrawing.Junction(up, down, left, right, border);

                    Assert.True(alphabet.IndexOf(glyph) >= 0,
                        $"{border} answered pattern {mask} with '{glyph}', which is not one of its own glyphs");
                }
            }
        }

        [Fact]
        public void TheSameConnectionsGiveTheSameShapeInEveryStyle()
        {
            // The reason single-connection cells take a full line rather than one of Unicode's half-line stubs: the
            // stubs have no double-line counterpart, so using them would make one style draw a different picture from
            // another for identical input. Shape here means "which of the eleven roles", compared across styles.
            var single = new List<int>();
            var doubled = new List<int>();

            for (var mask = 0; mask < 16; mask++)
            {
                var (up, down, left, right) = Unpack(mask);
                single.Add("─│┌┐└┘├┤┬┴┼".IndexOf(BoxDrawing.Junction(up, down, left, right)));
                doubled.Add("═║╔╗╚╝╠╣╦╩╬".IndexOf(BoxDrawing.Junction(up, down, left, right, BoxBorderEnum.Double)));
            }

            Assert.Equal(single, doubled);
        }

        [Fact]
        public void NoBorderMeansNoGlyphAtAll()
        {
            // Exactly what BoxBorderEnum.None means on Box, so a caller switching a style to None gets a blank
            // network rather than a network drawn in some fallback alphabet.
            for (var mask = 0; mask < 16; mask++)
            {
                var (up, down, left, right) = Unpack(mask);
                Assert.Equal(' ', BoxDrawing.Junction(up, down, left, right, BoxBorderEnum.None));
            }
        }

        [Fact]
        public void TheParametersAreVerticalThenHorizontal()
        {
            // Guards the argument ORDER, which no other test here can see: every assertion above would pass just as
            // happily if up and left were swapped throughout, since the table would be swapped with them. A corner
            // opening up and right is the elbow at the bottom-left of a run, and that is a fact about the signature.
            Assert.Equal('└', BoxDrawing.Junction(true, false, false, true));
            Assert.Equal('┐', BoxDrawing.Junction(false, true, true, false));
        }

        private static (bool Up, bool Down, bool Left, bool Right) Unpack(int mask)
        {
            return ((mask & 8) != 0, (mask & 4) != 0, (mask & 2) != 0, (mask & 1) != 0);
        }
    }
}
