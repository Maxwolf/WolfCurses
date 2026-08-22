// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;
using System.Globalization;

namespace WolfCurses.Apps.Calculator
{
    /// <summary>
    ///     A desk calculator: what is on the display, what is waiting to happen to it, what is in the memory, and
    ///     the paper tape of how it got there. No console anywhere in here, so all of it can be driven from a test.
    ///     <para>
    ///         <b>It works left to right with no precedence, which is the decision most worth knowing.</b>
    ///         <c>2 + 3 x 4</c> comes to twenty here, not fourteen: pressing an operator finishes whatever was
    ///         pending before starting the next, which is what every desk calculator and adding machine has always
    ///         done. It is not an expression evaluator and does not pretend to be one, and the tape is there so
    ///         that the working is visible rather than surprising.
    ///     </para>
    ///     <para>
    ///         <b>The arithmetic is <see cref="decimal" />, not <see cref="double" />.</b> A calculator whose
    ///         0.1 + 0.2 comes to 0.30000000000000004 is a broken calculator, however defensible the floating point
    ///         is. Decimal is exact for the numbers people type, which is the whole population of numbers a desk
    ///         calculator ever sees. The spreadsheet next door uses double on purpose and for the opposite reason:
    ///         it is doing science on columns, not adding up receipts.
    ///     </para>
    ///     <para>
    ///         <b>What is on the display is a string, not a number.</b> Somebody typing "1.50" has typed something
    ///         a decimal cannot remember, and a display rebuilt from the value would rub out their trailing zero
    ///         while they were still typing it.
    ///     </para>
    /// </summary>
    public sealed class CalculatorEngine
    {
        /// <summary>How many digits may be typed, which is about what a desk calculator's display holds.</summary>
        public const int MaximumDigits = 15;

        /// <summary>How many lines of tape are kept before the oldest are dropped.</summary>
        private const int MaximumTape = 500;

        /// <summary>The paper tape, oldest first.</summary>
        private readonly List<CalculatorTapeLine> _tape = new();

        /// <summary>What is on the display, exactly as it should be read.</summary>
        private string _entry = "0";

        /// <summary>Whether the display is being typed into, as opposed to showing an answer.</summary>
        private bool _typing;

        /// <summary>The running total, which the pending operator is waiting to apply something to.</summary>
        private decimal _accumulator;

        /// <summary>What is waiting for a second number.</summary>
        private CalculatorOperatorEnum _pending = CalculatorOperatorEnum.None;

        /// <summary>What the last equals did, so that pressing it again does the same thing.</summary>
        private CalculatorOperatorEnum _repeatOperator = CalculatorOperatorEnum.None;

        /// <summary>What the last equals did it with.</summary>
        private decimal _repeatOperand;

        /// <summary>What is on the display: a number, or the reason there is not one.</summary>
        public string Display => Error ?? Group(_entry);

        /// <summary>What went wrong, or null. While this is set every key but the two clears is refused.</summary>
        public string Error { get; private set; }

        /// <summary>The operator waiting for a second number, for the screen to show.</summary>
        public CalculatorOperatorEnum Pending => _pending;

        /// <summary>What is in the memory.</summary>
        public decimal Memory { get; private set; }

        /// <summary>Whether the memory holds anything, which is what a recall key asks before lighting up.</summary>
        public bool HasMemory => Memory != 0m;

        /// <summary>The paper tape, oldest first.</summary>
        public IReadOnlyList<CalculatorTapeLine> Tape => _tape;

        /// <summary>
        ///     What is on the display as a number. A part-typed number such as "0." is worth zero rather than
        ///     refusing to parse, since somebody is still in the middle of typing it.
        /// </summary>
        public decimal Value
        {
            get
            {
                var text = _entry.EndsWith(".", StringComparison.Ordinal)
                    ? _entry.Substring(0, _entry.Length - 1)
                    : _entry;

                return decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                    ? value
                    : 0m;
            }
        }

        /// <summary>Types a digit.</summary>
        /// <param name="digit">One of '0' to '9'; anything else is ignored.</param>
        public void Digit(char digit)
        {
            if (Error != null || digit < '0' || digit > '9')
                return;

            StartTyping();

            if (DigitCount(_entry) >= MaximumDigits)
                return;

            // A fresh entry reads "0", and the first digit replaces it rather than sitting after it, or every
            // number typed would begin with a nought.
            _entry = _entry == "0" ? digit.ToString() : _entry + digit;
        }

        /// <summary>Types a decimal point, which a number may have only one of.</summary>
        public void Point()
        {
            if (Error != null)
                return;

            StartTyping();

            if (!_entry.Contains('.', StringComparison.Ordinal))
                _entry += ".";
        }

        /// <summary>
        ///     Rubs out the last thing typed.
        ///     <para>
        ///         Only while something is being typed: rubbing a digit off an <i>answer</i> would leave a number
        ///         that nothing computed, which is then indistinguishable from one that something did.
        ///     </para>
        /// </summary>
        public void Backspace()
        {
            if (Error != null || !_typing)
                return;

            _entry = _entry.Length <= 1 ? "0" : _entry.Substring(0, _entry.Length - 1);

            if (_entry == "-" || _entry.Length == 0)
                _entry = "0";
        }

        /// <summary>Changes the sign of what is on the display, whether it was typed or worked out.</summary>
        public void Negate()
        {
            if (Error != null || _entry == "0")
                return;

            _entry = _entry.StartsWith("-", StringComparison.Ordinal)
                ? _entry.Substring(1)
                : "-" + _entry;
        }

        /// <summary>
        ///     Starts an operation, finishing whatever was already pending first. That folding is what makes the
        ///     calculator work left to right: by the time the second operator is pressed the first has happened.
        /// </summary>
        /// <param name="op">Which operation.</param>
        public void Operator(CalculatorOperatorEnum op)
        {
            if (Error != null || op == CalculatorOperatorEnum.None)
                return;

            // Two operators in a row with no number between them is somebody changing their mind, not an
            // instruction. Folding here instead would apply the pending operation to its own left-hand side, so
            // pressing plus twice after a two would quietly make it four.
            if (!_typing && _pending != CalculatorOperatorEnum.None)
            {
                _pending = op;
                return;
            }

            Write(Display, Symbol(op));
            Fold();

            if (Error != null)
                return;

            _pending = op;
            _typing = false;
            _repeatOperator = CalculatorOperatorEnum.None;
        }

        /// <summary>
        ///     Works out the answer.
        ///     <para>
        ///         Pressing it again repeats the last operation on the answer, so 2 + 3 = = = counts up in threes.
        ///         That is what a desk calculator does and it is genuinely useful, which is why it is worth the one
        ///         extra pair of fields it costs.
        ///     </para>
        /// </summary>
        public void Equals()
        {
            if (Error != null)
                return;

            if (_pending != CalculatorOperatorEnum.None)
            {
                _repeatOperator = _pending;
                _repeatOperand = Value;
            }
            else if (_repeatOperator == CalculatorOperatorEnum.None)
            {
                // Nothing pending and nothing to repeat: equals on a bare number is that number.
                _accumulator = Value;
                return;
            }

            Write(Display, "=");

            _accumulator = _pending == CalculatorOperatorEnum.None ? Value : _accumulator;

            var result = Apply(_accumulator, _repeatOperator, _repeatOperand);

            _pending = CalculatorOperatorEnum.None;

            if (Error != null)
                return;

            _accumulator = result;
            Show(result);
            Write(Display, string.Empty, true);
        }

        /// <summary>
        ///     Turns the display into a percentage of what it is about to be applied to.
        ///     <para>
        ///         <b>What that means depends on the pending operator, which surprises people every time.</b> With
        ///         a plus or a minus waiting, 200 + 10 % is 200 + 10% <i>of 200</i>, which is 220: that is what the
        ///         key is for, and it is why a discount can be worked out without typing the total twice. With a
        ///         times or a divide, or nothing at all, it is simply a hundredth.
        ///     </para>
        /// </summary>
        public void Percent()
        {
            if (Error != null)
                return;

            var value = _pending is CalculatorOperatorEnum.Add or CalculatorOperatorEnum.Subtract
                ? _accumulator * Value / 100m
                : Value / 100m;

            Show(value);
        }

        /// <summary>The square root of the display, which a negative number does not have.</summary>
        public void SquareRoot()
        {
            if (Error != null)
                return;

            if (Value < 0m)
            {
                Error = "Cannot root a negative";
                return;
            }

            // Through double and back, because decimal has no root of its own. The loss is real and is why the
            // result is rounded to the display's own precision rather than shown to twenty-nine digits of noise.
            Show(Math.Round((decimal) Math.Sqrt((double) Value), 10));
        }

        /// <summary>The display times itself.</summary>
        public void Square()
        {
            if (Error != null)
                return;

            Show(Apply(Value, CalculatorOperatorEnum.Multiply, Value));
        }

        /// <summary>One divided by the display.</summary>
        public void Reciprocal()
        {
            if (Error != null)
                return;

            Show(Apply(1m, CalculatorOperatorEnum.Divide, Value));
        }

        /// <summary>Clears the display and leaves everything else, which is what a mistyped number needs.</summary>
        public void ClearEntry()
        {
            Error = null;
            Show(0m);
            _typing = false;
        }

        /// <summary>Clears everything but the memory and the tape, which is what a fresh sum needs.</summary>
        public void ClearAll()
        {
            Error = null;
            _accumulator = 0m;
            _pending = CalculatorOperatorEnum.None;
            _repeatOperator = CalculatorOperatorEnum.None;
            _repeatOperand = 0m;

            Show(0m);
            _typing = false;

            Write("0", "C");
        }

        /// <summary>Throws the tape away, which is the only thing that does.</summary>
        public void ClearTape()
        {
            _tape.Clear();
        }

        /// <summary>Empties the memory.</summary>
        public void MemoryClear()
        {
            Memory = 0m;
            Write("0", "MC");
        }

        /// <summary>Puts the memory on the display.</summary>
        public void MemoryRecall()
        {
            if (Error != null)
                return;

            Show(Memory);
            _typing = false;
        }

        /// <summary>Replaces the memory with the display.</summary>
        public void MemoryStore()
        {
            if (Error != null)
                return;

            Memory = Value;
            Write(Display, "MS");
        }

        /// <summary>Adds the display to the memory.</summary>
        public void MemoryAdd()
        {
            if (Error != null)
                return;

            Memory += Value;
            Write(Display, "M+");
        }

        /// <summary>Takes the display off the memory.</summary>
        public void MemorySubtract()
        {
            if (Error != null)
                return;

            Memory -= Value;
            Write(Display, "M-");
        }

        /// <summary>The sign a key and a tape line write for an operation.</summary>
        /// <param name="op">The operation.</param>
        /// <returns>Its symbol.</returns>
        public static string Symbol(CalculatorOperatorEnum op)
        {
            return op switch
            {
                CalculatorOperatorEnum.Add => "+",
                CalculatorOperatorEnum.Subtract => "-",
                CalculatorOperatorEnum.Multiply => "×",
                CalculatorOperatorEnum.Divide => "÷",
                _ => string.Empty
            };
        }

        /// <summary>Finishes whatever operation was pending, using the display as its second number.</summary>
        private void Fold()
        {
            if (_pending == CalculatorOperatorEnum.None)
            {
                _accumulator = Value;
                return;
            }

            var result = Apply(_accumulator, _pending, Value);

            if (Error != null)
                return;

            _accumulator = result;
            Show(result);
        }

        /// <summary>Does one piece of arithmetic, or sets the error saying why it could not.</summary>
        /// <param name="left">The number on the left.</param>
        /// <param name="op">The operation.</param>
        /// <param name="right">The number on the right.</param>
        /// <returns>The result, or zero when it failed.</returns>
        private decimal Apply(decimal left, CalculatorOperatorEnum op, decimal right)
        {
            try
            {
                switch (op)
                {
                    case CalculatorOperatorEnum.Add:
                        return left + right;

                    case CalculatorOperatorEnum.Subtract:
                        return left - right;

                    case CalculatorOperatorEnum.Multiply:
                        return left * right;

                    case CalculatorOperatorEnum.Divide:
                        if (right == 0m)
                        {
                            Error = "Cannot divide by zero";
                            return 0m;
                        }

                        return left / right;

                    default:
                        return right;
                }
            }
            catch (OverflowException)
            {
                // Decimal is exact and therefore finite: it has no infinity to spill into the way a double does,
                // so the alternative to catching this is the whole program going down over a long multiplication.
                Error = "Number is too large";
                return 0m;
            }
        }

        /// <summary>Puts an answer on the display, which is then no longer being typed into.</summary>
        /// <param name="value">The answer.</param>
        private void Show(decimal value)
        {
            _entry = value.ToString("0.##########", CultureInfo.InvariantCulture);
            _typing = false;
        }

        /// <summary>Begins a new number when the display was showing an answer.</summary>
        private void StartTyping()
        {
            if (_typing)
                return;

            _entry = "0";
            _typing = true;
        }

        /// <summary>Adds a line to the tape, dropping the oldest when it gets long.</summary>
        /// <param name="value">The number.</param>
        /// <param name="mark">What was done to it.</param>
        /// <param name="isTotal">Whether it is an answer.</param>
        private void Write(string value, string mark, bool isTotal = false)
        {
            _tape.Add(new CalculatorTapeLine(value, mark, isTotal));

            // A tape that grew forever would be a memory leak wearing a paper hat: this screen can be left running.
            if (_tape.Count > MaximumTape)
                _tape.RemoveRange(0, _tape.Count - MaximumTape);
        }

        /// <summary>How many digits a typed number has, which is what the entry limit counts.</summary>
        /// <param name="entry">The typed number.</param>
        /// <returns>The digit count.</returns>
        private static int DigitCount(string entry)
        {
            var digits = 0;

            foreach (var character in entry)
            {
                if (character >= '0' && character <= '9')
                    digits++;
            }

            return digits;
        }

        /// <summary>
        ///     Puts separators into the whole part of a number and leaves the rest exactly as typed.
        ///     <para>
        ///         The fractional part is never reformatted, which is the point: somebody half way through typing
        ///         "1.50" has a trailing zero and a trailing point that a number cannot remember, and rebuilding
        ///         the display from the value would rub them out from under them.
        ///     </para>
        /// </summary>
        /// <param name="entry">The number as it should be read.</param>
        /// <returns>The number with separators.</returns>
        private static string Group(string entry)
        {
            var negative = entry.StartsWith("-", StringComparison.Ordinal);
            var body = negative ? entry.Substring(1) : entry;

            var point = body.IndexOf('.', StringComparison.Ordinal);
            var whole = point < 0 ? body : body.Substring(0, point);
            var rest = point < 0 ? string.Empty : body.Substring(point);

            if (whole.Length > 3 &&
                decimal.TryParse(whole, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                whole = value.ToString("#,##0", CultureInfo.InvariantCulture);

            return (negative ? "-" : string.Empty) + whole + rest;
        }
    }
}
