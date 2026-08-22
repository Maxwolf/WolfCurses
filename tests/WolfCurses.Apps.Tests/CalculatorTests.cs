using System;
using WolfCurses.Apps.Calculator;
using WolfCurses.Apps.Tests.Support;
using WolfCurses.Graphics;
using Xunit;

namespace WolfCurses.Apps.Tests
{
    /// <summary>
    ///     The calculator as a person meets it: keys in, frames out.
    ///     <para>
    ///         Where a key is comes off the screen rather than out of the layout, which is the whole point of the
    ///         control underneath: a test that computed the cell would be recomputing the arithmetic it is meant to
    ///         be checking, and would agree with a hit test that had drifted.
    ///     </para>
    /// </summary>
    [Collection("Suite")]
    public class CalculatorTests
    {
        private static DrivenSuite OpenCalculator()
        {
            var suite = new DrivenSuite();
            suite.ChooseMenuItem((int) OfficeCommandsEnum.Calculator);

            return suite;
        }

        /// <summary>What the display reads, taken from the row the chrome says it draws it on.</summary>
        private static string DisplayOf(DrivenSuite suite)
        {
            var rows = suite.Screen.Split('\n');

            // Read off the layout's own constant rather than a literal, so moving the display cannot silently make
            // every assertion here look at the wrong row.
            return rows[CalculatorChrome.DisplayRow + 1];
        }

        /// <summary>Where a key is drawn, found on screen rather than worked out.</summary>
        private static (int Row, int Column) KeyAt(DrivenSuite suite, string label)
        {
            var rows = suite.Screen.Split('\n');

            // From the keypad's first row down, so a digit in the display or on the tape above it cannot be
            // mistaken for the key of the same name.
            var top = Array.FindIndex(rows, row => row.Contains("MC", StringComparison.Ordinal));
            Assert.True(top > 0, "the keypad was not drawn:\n" + suite.Describe());

            // Matched with a space each side, which is how a centred key face is drawn and is what stops a search
            // for "+" landing inside the M+ key on the row above it.
            var face = " " + label + " ";

            for (var i = top; i < rows.Length; i++)
            {
                var at = rows[i].IndexOf(face, StringComparison.Ordinal);

                if (at >= 0)
                    return (i, at + 1);
            }

            Assert.Fail("no key labelled \"" + label + "\":\n" + suite.Describe());
            return (0, 0);
        }

        /// <summary>Clicks a key by its label.</summary>
        private static void ClickKey(DrivenSuite suite, string label)
        {
            var (row, column) = KeyAt(suite, label);
            suite.Click(row, column);
        }

        [Fact]
        public void ItOpensShowingNothingAndAFullSetOfKeys()
        {
            using var suite = OpenCalculator();

            Assert.Contains("0", DisplayOf(suite), StringComparison.Ordinal);

            foreach (var label in new[] {"MC", "CE", "7", "4", "1", "0", "=", "√"})
                Assert.NotEqual((0, 0), KeyAt(suite, label));
        }

        [Fact]
        public void TypingOnTheTopRowOfDigitsWorks()
        {
            using var suite = OpenCalculator();

            suite.PressChar('1', ConsoleKey.D1);
            suite.PressChar('2', ConsoleKey.D2);
            suite.PressChar('+', ConsoleKey.Oem1);
            suite.PressChar('8', ConsoleKey.D8);
            suite.Press(ConsoleKey.Enter);

            Assert.Contains("20", DisplayOf(suite), StringComparison.Ordinal);
        }

        [Fact]
        public void TheNumberPadWorksWithoutSendingAnyCharacterAtAll()
        {
            using var suite = OpenCalculator();

            // Deliberately harsher than the real thing: with NUM LOCK on these keys carry their digit as a
            // character too, so handling them by name is what makes them arrive whatever the console reports.
            suite.Press(ConsoleKey.NumPad1);
            suite.Press(ConsoleKey.NumPad2);
            suite.Press(ConsoleKey.Add);
            suite.Press(ConsoleKey.NumPad8);
            suite.Press(ConsoleKey.Enter);

            Assert.Contains("20", DisplayOf(suite), StringComparison.Ordinal);
        }

        [Fact]
        public void EveryNumberPadDigitIsTheDigitItSays()
        {
            using var suite = OpenCalculator();

            // One at a time and all nine of them, because an off-by-one in the arithmetic that turns a key into a
            // digit would still produce digits and would still look like it worked.
            for (var digit = 0; digit <= 9; digit++)
            {
                suite.PressChar('c', ConsoleKey.C);
                suite.Press(ConsoleKey.NumPad0 + digit);

                Assert.Contains(
                    digit.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    DisplayOf(suite),
                    StringComparison.Ordinal);
            }
        }

        [Fact]
        public void TheNumberPadOperatorsAndPointWork()
        {
            using var suite = OpenCalculator();

            suite.Press(ConsoleKey.NumPad1);
            suite.Press(ConsoleKey.Decimal);
            suite.Press(ConsoleKey.NumPad5);
            suite.Press(ConsoleKey.Multiply);
            suite.Press(ConsoleKey.NumPad4);
            suite.Press(ConsoleKey.Enter);

            Assert.Contains("6", DisplayOf(suite), StringComparison.Ordinal);
        }

        [Fact]
        public void ClickingTheKeysAddsUpJustAsTypingDoes()
        {
            using var suite = OpenCalculator();

            ClickKey(suite, "7");
            ClickKey(suite, "+");
            ClickKey(suite, "8");
            ClickKey(suite, "=");

            Assert.Contains("15", DisplayOf(suite), StringComparison.Ordinal);
        }

        [Fact]
        public void ClickingTheWideZeroKeyWorksAnywhereAlongIt()
        {
            using var suite = OpenCalculator();

            var (row, column) = KeyAt(suite, "0");

            ClickKey(suite, "5");

            // Three columns further along is still inside the same key, which a pad that assumed every key was the
            // same width would have got wrong.
            suite.Click(row, column + 3);

            Assert.Contains("50", DisplayOf(suite), StringComparison.Ordinal);
        }

        [Fact]
        public void AKeyThatCannotBeUsedDoesNothingWhenClicked()
        {
            using var suite = OpenCalculator();

            ClickKey(suite, "9");

            // Nothing is in the memory, so recall is greyed. Clicking it must leave the display alone rather than
            // quietly reading a zero out of an empty memory.
            ClickKey(suite, "MR");

            Assert.Contains("9", DisplayOf(suite), StringComparison.Ordinal);
        }

        [Fact]
        public void TheMemoryKeysComeToLifeOnceSomethingIsInThem()
        {
            using var suite = OpenCalculator();

            ClickKey(suite, "9");
            ClickKey(suite, "M+");
            ClickKey(suite, "C");
            ClickKey(suite, "MR");

            Assert.Contains("9", DisplayOf(suite), StringComparison.Ordinal);
        }

        [Fact]
        public void TheTapeShowsTheWorkingRatherThanOnlyTheAnswer()
        {
            using var suite = OpenCalculator();

            suite.PressChar('2', ConsoleKey.D2);
            suite.PressChar('+', ConsoleKey.Oem1);
            suite.PressChar('3', ConsoleKey.D3);
            suite.Press(ConsoleKey.Enter);

            // A calculator working left to right is doing something worth showing, which is what the tape is for.
            var screen = suite.Screen;

            Assert.Contains("Tape", screen, StringComparison.Ordinal);
            Assert.Contains("2 +", screen, StringComparison.Ordinal);
            Assert.Contains("3 =", screen, StringComparison.Ordinal);
        }

        [Fact]
        public void RubbingOutAndClearingBothWork()
        {
            using var suite = OpenCalculator();

            suite.PressChar('1', ConsoleKey.D1);
            suite.PressChar('2', ConsoleKey.D2);
            suite.PressChar('3', ConsoleKey.D3);
            suite.Press(ConsoleKey.Backspace);

            Assert.Contains("12", DisplayOf(suite), StringComparison.Ordinal);

            suite.Press(ConsoleKey.Delete);
            Assert.Contains("0", DisplayOf(suite), StringComparison.Ordinal);
        }

        [Fact]
        public void DividingByNothingSaysSoOnTheDisplay()
        {
            using var suite = OpenCalculator();

            suite.PressChar('8', ConsoleKey.D8);
            suite.PressChar('/', ConsoleKey.Divide);
            suite.PressChar('0', ConsoleKey.D0);
            suite.Press(ConsoleKey.Enter);

            Assert.Contains("divide by zero", DisplayOf(suite), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void EscapeWithNothingOpenReturnsToTheSuiteMenu()
        {
            using var suite = OpenCalculator();

            suite.Escape();

            Assert.Contains("Which application?", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void EscapeShutsAnOpenMenuRatherThanLeavingTheApplication()
        {
            using var suite = OpenCalculator();

            suite.Press(ConsoleKey.F10);
            Assert.Contains("Exit", suite.Screen, StringComparison.Ordinal);

            suite.Escape();

            Assert.Contains("MC", suite.Screen, StringComparison.Ordinal);
            Assert.DoesNotContain("Which application?", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void OpeningAMenuDrawsItOverTheKeysWithoutMovingAnything()
        {
            using var suite = OpenCalculator();

            var before = suite.Screen.Split('\n').Length;
            suite.Press(ConsoleKey.F10);

            var screen = suite.Screen;

            // Drawn over the finished rows rather than appended, which for this screen means slicing strings the
            // keypad had already styled.
            Assert.Equal(before, screen.Split('\n').Length);

            // And the tape beside the panel is untouched, which is what a full-width overlay would have lost.
            Assert.Contains("Tape", screen, StringComparison.Ordinal);
        }

        [Fact]
        public void TheWholeScreenFitsEightyByTwentyFour()
        {
            using var suite = OpenCalculator();

            AssertFits(suite.RawScreen);

            suite.Press(ConsoleKey.F10);
            AssertFits(suite.RawScreen);
        }

        /// <summary>Checks a frame against the suite's floor, measuring columns rather than characters.</summary>
        private static void AssertFits(string raw)
        {
            var rows = raw.Split('\n');

            Assert.True(rows.Length <= 24, "the screen is " + rows.Length + " rows, which is more than 24");

            foreach (var row in rows)
            {
                var width = AnsiText.VisibleLength(row.TrimEnd('\r'));

                Assert.True(width <= 80, "this row is " + width + " columns wide:\n" + row.TrimEnd('\r'));
            }
        }
    }
}
