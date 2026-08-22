using System;
using System.Linq;
using WolfCurses.Apps.Calculator;
using Xunit;

namespace WolfCurses.Apps.Tests
{
    /// <summary>
    ///     The desk calculator's own logic, with no screen anywhere near it.
    ///     <para>
    ///         Almost every assertion here is an exact answer rather than "it produced a number", because a
    ///         calculator that is wrong still produces a number. The interesting cases are the ones where two
    ///         defensible answers exist and only one is what a desk calculator gives: chaining without precedence,
    ///         percent taking its meaning from the pending operator, and equals repeating itself.
    ///     </para>
    /// </summary>
    public class CalculatorEngineTests
    {
        /// <summary>Presses a run of keys, which reads far closer to using one than a page of method calls.</summary>
        private static CalculatorEngine Press(string keys)
        {
            var engine = new CalculatorEngine();

            foreach (var key in keys)
            {
                switch (key)
                {
                    case ' ':
                        break;
                    case '.':
                        engine.Point();
                        break;
                    case '+':
                        engine.Operator(CalculatorOperatorEnum.Add);
                        break;
                    case '-':
                        engine.Operator(CalculatorOperatorEnum.Subtract);
                        break;
                    case '*':
                        engine.Operator(CalculatorOperatorEnum.Multiply);
                        break;
                    case '/':
                        engine.Operator(CalculatorOperatorEnum.Divide);
                        break;
                    case '=':
                        engine.Equals();
                        break;
                    case '%':
                        engine.Percent();
                        break;
                    default:
                        engine.Digit(key);
                        break;
                }
            }

            return engine;
        }

        [Fact]
        public void ItStartsAtNothing()
        {
            var engine = new CalculatorEngine();

            Assert.Equal("0", engine.Display);
            Assert.Null(engine.Error);
            Assert.False(engine.HasMemory);
            Assert.Empty(engine.Tape);
        }

        [Fact]
        public void TypingBuildsANumberRatherThanStackingZeroes()
        {
            Assert.Equal("305", Press("305").Display);
            Assert.Equal("0", Press("000").Display);
            Assert.Equal("7", Press("0007").Display);
        }

        [Fact]
        public void TheFourOperationsGiveTheseExactAnswers()
        {
            Assert.Equal("5", Press("2+3=").Display);
            Assert.Equal("7", Press("10-3=").Display);
            Assert.Equal("42", Press("6*7=").Display);
            Assert.Equal("4", Press("12/3=").Display);
        }

        [Fact]
        public void ItWorksLeftToRightWithNoPrecedence()
        {
            // Twenty, not fourteen. This is the decision the whole class turns on: pressing an operator finishes
            // whatever was pending before starting the next, which is what a desk calculator has always done and
            // what the paper tape exists to make visible.
            Assert.Equal("20", Press("2+3*4=").Display);
            Assert.Equal("14", Press("3*4+2=").Display);
        }

        [Fact]
        public void ADecimalSumComesOutExactly()
        {
            // The reason the arithmetic is decimal. In double this is 0.30000000000000004, and a calculator that
            // says so is a broken calculator however defensible the floating point.
            Assert.Equal("0.3", Press("0.1+0.2=").Display);
            Assert.Equal("0.03", Press("0.1*0.3=").Display);
        }

        [Fact]
        public void APointCanOnlyBeTypedOnce()
        {
            Assert.Equal("1.5", Press("1.5").Display);
            Assert.Equal("1.55", Press("1.5.5").Display);
        }

        [Fact]
        public void ATrailingPointStaysOnTheDisplayWhileItIsBeingTyped()
        {
            var engine = Press("12.");

            // The display is what was typed, not what the number is: rubbing out a point somebody has only just
            // pressed would be the display arguing with them.
            Assert.Equal("12.", engine.Display);
            Assert.Equal(12m, engine.Value);
        }

        [Fact]
        public void EqualsPressedAgainRepeatsTheLastOperation()
        {
            var engine = Press("2+3=");

            Assert.Equal("5", engine.Display);

            engine.Equals();
            Assert.Equal("8", engine.Display);

            engine.Equals();
            Assert.Equal("11", engine.Display);
        }

        [Fact]
        public void ANewNumberAfterEqualsIsStillRepeatedOnByEquals()
        {
            var engine = Press("2+3=");

            engine.Digit('1');
            engine.Digit('0');
            engine.Equals();

            // Ten plus the remembered three, which is what a desk calculator does with a fresh number after an
            // equals and is the whole reason the operand is remembered rather than only the operator.
            Assert.Equal("13", engine.Display);
        }

        [Fact]
        public void TwoOperatorsInARowChangeYourMindRatherThanComputing()
        {
            var engine = new CalculatorEngine();

            engine.Digit('2');
            engine.Operator(CalculatorOperatorEnum.Add);
            engine.Operator(CalculatorOperatorEnum.Multiply);
            engine.Digit('3');
            engine.Equals();

            // Six. Folding on the second press would apply the pending plus to its own left-hand side, quietly
            // making the two a four before the multiply ever happened.
            Assert.Equal("6", engine.Display);
        }

        [Fact]
        public void PercentTakesItsMeaningFromTheOperatorWaitingForIt()
        {
            // The behaviour that surprises everybody, and the reason the key is worth having: a discount off a
            // total without typing the total twice.
            Assert.Equal("220", Press("200+10%=").Display);
            Assert.Equal("180", Press("200-10%=").Display);

            // With a times or a divide there is nothing for a percentage to be *of*, so it is a hundredth.
            Assert.Equal("20", Press("200*10%=").Display);
        }

        [Fact]
        public void DividingByNothingIsAnErrorYouHaveToClear()
        {
            var engine = Press("8/0=");

            Assert.NotNull(engine.Error);
            Assert.Equal(engine.Error, engine.Display);

            // Every key is refused until it is cleared, or the error would quietly become part of a later sum.
            engine.Digit('5');
            Assert.NotNull(engine.Error);

            engine.ClearEntry();
            Assert.Null(engine.Error);
            Assert.Equal("0", engine.Display);
        }

        [Fact]
        public void TheRootOfANegativeNumberIsRefusedRatherThanInvented()
        {
            var engine = Press("9");
            engine.Negate();
            engine.SquareRoot();

            Assert.NotNull(engine.Error);
        }

        [Fact]
        public void TheOtherSingleNumberKeysGiveTheseAnswers()
        {
            var root = Press("81");
            root.SquareRoot();
            Assert.Equal("9", root.Display);

            var square = Press("12");
            square.Square();
            Assert.Equal("144", square.Display);

            var reciprocal = Press("4");
            reciprocal.Reciprocal();
            Assert.Equal("0.25", reciprocal.Display);

            var byZero = Press("0");
            byZero.Reciprocal();
            Assert.NotNull(byZero.Error);
        }

        [Fact]
        public void TheSignKeyFlipsItBackAndForth()
        {
            var engine = Press("42");

            engine.Negate();
            Assert.Equal("-42", engine.Display);

            engine.Negate();
            Assert.Equal("42", engine.Display);

            // Nothing has no sign, so the key does nothing rather than producing a negative zero.
            var zero = new CalculatorEngine();
            zero.Negate();
            Assert.Equal("0", zero.Display);
        }

        [Fact]
        public void RubbingOutWorksOnWhatIsBeingTypedAndNotOnAnAnswer()
        {
            var typed = Press("123");
            typed.Backspace();
            Assert.Equal("12", typed.Display);

            typed.Backspace();
            typed.Backspace();
            typed.Backspace();
            Assert.Equal("0", typed.Display);

            // An answer with a digit rubbed off it is a number nothing computed, which is then indistinguishable
            // from one that something did.
            var answered = Press("2+3=");
            answered.Backspace();
            Assert.Equal("5", answered.Display);
        }

        [Fact]
        public void ClearEntryLeavesTheSumAloneAndClearAllDoesNot()
        {
            var entry = Press("2+9");
            entry.ClearEntry();
            entry.Digit('3');
            entry.Equals();

            // The pending plus survived, so this is still two plus something.
            Assert.Equal("5", entry.Display);

            var all = Press("2+9");
            all.ClearAll();
            all.Digit('3');
            all.Equals();

            Assert.Equal("3", all.Display);
        }

        [Fact]
        public void TheMemoryKeysAddUpSeparatelyFromTheDisplay()
        {
            var engine = Press("10");
            engine.MemoryAdd();

            Assert.True(engine.HasMemory);
            Assert.Equal(10m, engine.Memory);

            engine.ClearAll();
            engine.Digit('4');
            engine.MemorySubtract();
            Assert.Equal(6m, engine.Memory);

            engine.ClearAll();
            engine.MemoryRecall();
            Assert.Equal("6", engine.Display);

            engine.MemoryClear();
            Assert.False(engine.HasMemory);
        }

        [Fact]
        public void StoringReplacesTheMemoryWhereAddingAccumulates()
        {
            var engine = Press("10");
            engine.MemoryAdd();
            engine.MemoryAdd();
            Assert.Equal(20m, engine.Memory);

            engine.MemoryStore();
            Assert.Equal(10m, engine.Memory);
        }

        [Fact]
        public void BigNumbersGetSeparatorsAndTypedDecimalsDoNot()
        {
            Assert.Equal("1,234,567", Press("1234567").Display);
            Assert.Equal("1,234.50", Press("1234.50").Display);

            // Fewer than four digits needs none, and a negative keeps its sign in front of them.
            Assert.Equal("999", Press("999").Display);

            var negative = Press("1234");
            negative.Negate();
            Assert.Equal("-1,234", negative.Display);
        }

        [Fact]
        public void ThereIsALimitToHowMuchCanBeTyped()
        {
            var engine = new CalculatorEngine();

            for (var i = 0; i < 40; i++)
                engine.Digit('9');

            // Counted in digits rather than in characters, so the separators the display adds do not eat into it.
            Assert.Equal(CalculatorEngine.MaximumDigits,
                engine.Display.Count(character => character >= '0' && character <= '9'));
        }

        [Fact]
        public void TheTapeRecordsTheWorkingAndMarksTheAnswer()
        {
            var engine = Press("2+3=");

            Assert.Equal(3, engine.Tape.Count);
            Assert.Equal(new[] {"2", "3", "5"}, engine.Tape.Select(line => line.Value).ToArray());
            Assert.Equal(new[] {"+", "=", string.Empty}, engine.Tape.Select(line => line.Mark).ToArray());

            // Only the answer is a total, which is what lets the screen pick answers out of the working.
            Assert.Equal(new[] {false, false, true}, engine.Tape.Select(line => line.IsTotal).ToArray());
        }

        [Fact]
        public void TheTapeIsTheOnlyThingClearTapeClears()
        {
            var engine = Press("2+3=");
            engine.MemoryAdd();

            engine.ClearTape();

            Assert.Empty(engine.Tape);
            Assert.Equal("5", engine.Display);
            Assert.Equal(5m, engine.Memory);
        }

        [Fact]
        public void TheTapeDoesNotGrowForever()
        {
            var engine = new CalculatorEngine();

            // A screen somebody leaves running would otherwise be a memory leak wearing a paper hat.
            for (var i = 0; i < 2000; i++)
            {
                engine.Digit('1');
                engine.Operator(CalculatorOperatorEnum.Add);
            }

            Assert.True(engine.Tape.Count <= 500, "the tape grew to " + engine.Tape.Count + " lines");
        }
    }
}
