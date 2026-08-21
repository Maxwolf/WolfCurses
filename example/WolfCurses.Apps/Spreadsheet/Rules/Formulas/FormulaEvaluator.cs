// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WolfCurses.Apps.Spreadsheet
{
    /// <summary>
    ///     Works out what a formula comes to: <c>=SUM(B5:B16)/12</c>, <c>=B5-C5-D5</c>, <c>=ROUND(C5*1.2, 2)</c>.
    ///     <para>
    ///         Recursive descent straight down the text, working the answer out as it goes rather than building a
    ///         tree first. There is no second pass to build one for, since a sheet is re-read whenever a cell
    ///         changes, and the version without a tree is a great deal easier to follow: the grammar is the call
    ///         stack, so the precedence of times over plus is the fact that <see cref="ParseTerm" /> is called by
    ///         <see cref="ParseExpression" /> and not the other way about.
    ///     </para>
    ///     <para>
    ///         <b>Nothing in here throws.</b> Every failure comes back as an error value, because this is called
    ///         from inside a screen that redraws a thousand times a second and one mistyped cell must not be able to
    ///         stop the other four hundred being drawn.
    ///     </para>
    ///     <para>
    ///         A range is only meaningful as an argument to a function, which is why it is looked for there and
    ///         refused anywhere else: <c>=B5:B16</c> alone is not a number, and returning the first cell of it
    ///         quietly would be a worse answer than saying so.
    ///     </para>
    /// </summary>
    internal sealed class FormulaEvaluator
    {
        /// <summary>The sheet the cell references are read from.</summary>
        private readonly Sheet _sheet;

        /// <summary>The formula, without its leading equals sign.</summary>
        private readonly string _text;

        /// <summary>How far along the text has been read.</summary>
        private int _at;

        /// <summary>Initializes a new instance of the <see cref="FormulaEvaluator" /> class.</summary>
        /// <param name="sheet">The sheet to read cells from.</param>
        /// <param name="text">The formula, without its leading equals sign.</param>
        private FormulaEvaluator(Sheet sheet, string text)
        {
            _sheet = sheet;
            _text = text ?? string.Empty;
        }

        /// <summary>Works out a formula.</summary>
        /// <param name="sheet">The sheet its cell references are read from.</param>
        /// <param name="formula">The formula, without its leading equals sign.</param>
        /// <returns>What it comes to, or an error value saying why it does not come to anything.</returns>
        public static SheetValue Evaluate(Sheet sheet, string formula)
        {
            var evaluator = new FormulaEvaluator(sheet, formula);
            var value = evaluator.ParseExpression();

            if (value.IsError)
                return value;

            evaluator.SkipSpace();

            // Anything left over means the formula was only partly understood, which is worse than not understood:
            // "=1+2)" would otherwise quietly come to three.
            return evaluator._at < evaluator._text.Length ? SheetValue.FromError(FormulaErrors.Syntax) : value;
        }

        /// <summary>Adding and taking away, which bind least tightly and so are read first.</summary>
        /// <returns>The value.</returns>
        private SheetValue ParseExpression()
        {
            var left = ParseTerm();

            while (!left.IsError)
            {
                SkipSpace();

                var op = Peek();
                if (op != '+' && op != '-')
                    break;

                _at++;
                left = Arithmetic(left, ParseTerm(), op);
            }

            return left;
        }

        /// <summary>Multiplying and dividing, which bind more tightly than adding.</summary>
        /// <returns>The value.</returns>
        private SheetValue ParseTerm()
        {
            var left = ParseFactor();

            while (!left.IsError)
            {
                SkipSpace();

                var op = Peek();
                if (op != '*' && op != '/')
                    break;

                _at++;
                left = Arithmetic(left, ParseFactor(), op);
            }

            return left;
        }

        /// <summary>
        ///     Raising to a power, which binds most tightly of the operators and goes the other way: two to the
        ///     three to the two is two to the ninth, not sixty-four. Right association is why this calls itself
        ///     rather than looping the way the two above it do.
        /// </summary>
        /// <returns>The value.</returns>
        private SheetValue ParseFactor()
        {
            var left = ParseUnary();

            if (left.IsError)
                return left;

            SkipSpace();

            if (Peek() != '^')
                return left;

            _at++;
            return Arithmetic(left, ParseFactor(), '^');
        }

        /// <summary>A leading sign, which is a thing done to one value rather than between two.</summary>
        /// <returns>The value.</returns>
        private SheetValue ParseUnary()
        {
            SkipSpace();

            var sign = Peek();
            if (sign != '-' && sign != '+')
                return ParsePrimary();

            _at++;

            var value = ParseUnary();
            if (value.IsError)
                return value;

            if (!TryNumber(value, out var number))
                return SheetValue.FromError(FormulaErrors.Value);

            return SheetValue.FromNumber(sign == '-' ? -number : number);
        }

        /// <summary>A number, a piece of text, a bracketed expression, a function call, or a cell.</summary>
        /// <returns>The value.</returns>
        private SheetValue ParsePrimary()
        {
            SkipSpace();

            if (_at >= _text.Length)
                return SheetValue.FromError(FormulaErrors.Syntax);

            var character = _text[_at];

            if (character == '(')
            {
                _at++;

                var inner = ParseExpression();
                if (inner.IsError)
                    return inner;

                SkipSpace();

                if (Peek() != ')')
                    return SheetValue.FromError(FormulaErrors.Syntax);

                _at++;
                return inner;
            }

            if (character == '"')
                return ReadString();

            if (char.IsDigit(character) || character == '.')
                return ReadNumber();

            if (char.IsLetter(character))
                return ReadNameOrCell();

            return SheetValue.FromError(FormulaErrors.Syntax);
        }

        /// <summary>Reads a quoted piece of text, in which two quotes mean one.</summary>
        /// <returns>The value.</returns>
        private SheetValue ReadString()
        {
            _at++;

            var sb = new StringBuilder();

            while (_at < _text.Length)
            {
                if (_text[_at] == '"')
                {
                    if (_at + 1 < _text.Length && _text[_at + 1] == '"')
                    {
                        sb.Append('"');
                        _at += 2;
                        continue;
                    }

                    _at++;
                    return SheetValue.FromText(sb.ToString());
                }

                sb.Append(_text[_at]);
                _at++;
            }

            // Ran off the end with the quote still open.
            return SheetValue.FromError(FormulaErrors.Syntax);
        }

        /// <summary>Reads a number written out in the formula itself.</summary>
        /// <returns>The value.</returns>
        private SheetValue ReadNumber()
        {
            var start = _at;
            var seenPoint = false;

            while (_at < _text.Length)
            {
                if (char.IsDigit(_text[_at]))
                {
                    _at++;
                    continue;
                }

                // One decimal point only, so that "1.2.3" is refused rather than quietly read as 1.2.
                if (_text[_at] == '.' && !seenPoint)
                {
                    seenPoint = true;
                    _at++;
                    continue;
                }

                break;
            }

            // The invariant culture, matching how the file stores its numbers: a machine set to a decimal comma
            // must still read a sheet written on one that is not.
            return double.TryParse(_text.Substring(start, _at - start), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var number)
                ? SheetValue.FromNumber(number)
                : SheetValue.FromError(FormulaErrors.Syntax);
        }

        /// <summary>Reads something beginning with a letter, which is either a function call or a cell reference.</summary>
        /// <returns>The value.</returns>
        private SheetValue ReadNameOrCell()
        {
            var name = ReadIdentifier();

            SkipSpace();

            if (Peek() == '(')
            {
                _at++;
                return ParseCall(name);
            }

            // A range on its own is not a value. Answering with its first cell would be a plausible wrong number,
            // which is the worst kind.
            if (Peek() == ':')
                return SheetValue.FromError(FormulaErrors.Value);

            if (!CellAddress.TryParse(name, out var address))
                return SheetValue.FromError(FormulaErrors.Name);

            if (address.Row >= _sheet.RowCount || address.Column >= _sheet.ColumnCount)
                return SheetValue.FromError(FormulaErrors.Reference);

            return _sheet.GetValue(address);
        }

        /// <summary>Reads the arguments of a function call and applies it, having already eaten the opening bracket.</summary>
        /// <param name="name">The function's name.</param>
        /// <returns>The value.</returns>
        private SheetValue ParseCall(string name)
        {
            var arguments = new List<SheetValue>();

            SkipSpace();

            if (Peek() == ')')
            {
                _at++;
                return FormulaFunctions.Apply(name, arguments);
            }

            while (true)
            {
                SkipSpace();

                // A range flattens into however many values it covers, so SUM never learns what a range is: it is
                // handed a list of values either way.
                if (TryReadRange(out var range))
                {
                    if (range.LastRow >= _sheet.RowCount || range.LastColumn >= _sheet.ColumnCount)
                        return SheetValue.FromError(FormulaErrors.Reference);

                    foreach (var cell in range.Cells())
                        arguments.Add(_sheet.GetValue(cell));
                }
                else
                {
                    var value = ParseExpression();
                    if (value.IsError)
                        return value;

                    arguments.Add(value);
                }

                SkipSpace();

                if (Peek() == ',')
                {
                    _at++;
                    continue;
                }

                if (Peek() != ')')
                    return SheetValue.FromError(FormulaErrors.Syntax);

                _at++;
                break;
            }

            return FormulaFunctions.Apply(name, arguments);
        }

        /// <summary>
        ///     Reads a range if one starts here, and leaves the position exactly where it found it if not.
        ///     <para>
        ///         The rewind is the whole method. <c>B5</c> could be the start of <c>B5:B16</c> or a cell on its
        ///         own, and there is no way to know without reading past it; without putting the position back, the
        ///         <c>B5</c> in <c>SUM(B5, 2)</c> would already have been eaten by the attempt.
        ///     </para>
        /// </summary>
        /// <param name="range">The range found.</param>
        /// <returns>TRUE when a range really did start here.</returns>
        private bool TryReadRange(out CellRange range)
        {
            range = new CellRange(CellAddress.Origin);

            var mark = _at;

            if (_at < _text.Length && char.IsLetter(_text[_at]))
            {
                var first = ReadIdentifier();

                if (Peek() == ':')
                {
                    _at++;

                    if (_at < _text.Length && char.IsLetter(_text[_at]))
                    {
                        var second = ReadIdentifier();

                        if (CellAddress.TryParse(first, out var from) && CellAddress.TryParse(second, out var to))
                        {
                            range = new CellRange(from, to);
                            return true;
                        }
                    }
                }
            }

            _at = mark;
            return false;
        }

        /// <summary>Reads a run of letters and digits, which covers both a function's name and a cell's.</summary>
        /// <returns>The text read.</returns>
        private string ReadIdentifier()
        {
            var start = _at;

            while (_at < _text.Length && char.IsLetterOrDigit(_text[_at]))
                _at++;

            return _text.Substring(start, _at - start);
        }

        /// <summary>Does one piece of arithmetic, or says why it could not.</summary>
        /// <param name="left">The value on the left.</param>
        /// <param name="right">The value on the right.</param>
        /// <param name="op">Which operation.</param>
        /// <returns>The value.</returns>
        private static SheetValue Arithmetic(SheetValue left, SheetValue right, char op)
        {
            if (left.IsError)
                return left;

            if (right.IsError)
                return right;

            if (!TryNumber(left, out var a) || !TryNumber(right, out var b))
                return SheetValue.FromError(FormulaErrors.Value);

            switch (op)
            {
                case '+':
                    return SheetValue.FromNumber(a + b);

                case '-':
                    return SheetValue.FromNumber(a - b);

                case '*':
                    return SheetValue.FromNumber(a * b);

                case '^':
                    return SheetValue.FromNumber(Math.Pow(a, b));

                default:
                    // Caught rather than allowed to produce infinity, which would then spread silently through
                    // every total the cell takes part in.
                    return b == 0d
                        ? SheetValue.FromError(FormulaErrors.DivideByZero)
                        : SheetValue.FromNumber(a / b);
            }
        }

        /// <summary>
        ///     The number a value counts as in arithmetic. An empty cell is zero, which is what makes
        ///     <c>=B5-C5</c> work on a half-filled row; text is not a number at all and is refused.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="number">Its number.</param>
        /// <returns>TRUE when it can be used as one.</returns>
        private static bool TryNumber(SheetValue value, out double number)
        {
            number = value.Number;

            return value.IsNumber || value.IsEmpty;
        }

        /// <summary>Steps over any spaces.</summary>
        private void SkipSpace()
        {
            while (_at < _text.Length && char.IsWhiteSpace(_text[_at]))
                _at++;
        }

        /// <summary>The character at the current position, or nothing when the text has run out.</summary>
        /// <returns>The character.</returns>
        private char Peek()
        {
            return _at < _text.Length ? _text[_at] : '\0';
        }
    }
}
