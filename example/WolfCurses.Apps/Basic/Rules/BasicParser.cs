// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;
using System.Globalization;

namespace WolfCurses.Apps.Basic
{
    /// <summary>
    ///     Turns tokens into a program: a flat list of statements with every jump worked out.
    ///     <para>
    ///         <b>Blocks are compiled, not nested.</b> An IF becomes a conditional jump over its body, a WHILE
    ///         becomes a test at the top and a jump back at the bottom, and a DO becomes whichever of those four
    ///         arrangements it asked for. Nothing survives into the finished program except statements and jump
    ///         targets, which is what lets a GOTO land anywhere at all: the thing it lands in the middle of no
    ///         longer exists by then.
    ///     </para>
    ///     <para>
    ///         <b>A forward jump is emitted before its target is known</b> and patched when the parser reaches the
    ///         end of the block. That is why <see cref="BasicJumpStatement.Target" /> is settable, and it is the
    ///         reason the block stack carries lists of jumps rather than addresses.
    ///     </para>
    /// </summary>
    public sealed class BasicParser
    {
        /// <summary>The blocks currently open, innermost last.</summary>
        private readonly List<Block> _blocks = new();

        /// <summary>Where each label and line number ended up.</summary>
        private readonly Dictionary<string, int> _labels = new(StringComparer.Ordinal);

        /// <summary>Jumps whose target was a name that had not been seen yet.</summary>
        private readonly List<PendingJump> _pending = new();

        /// <summary>The finished statements.</summary>
        private readonly List<BasicStatement> _statements = new();

        /// <summary>The tokens being read.</summary>
        private readonly IReadOnlyList<BasicToken> _tokens;

        /// <summary>How far through the tokens we are.</summary>
        private int _at;

        /// <summary>Initializes a new instance of the <see cref="BasicParser" /> class.</summary>
        /// <param name="tokens">The tokens to read.</param>
        private BasicParser(IReadOnlyList<BasicToken> tokens)
        {
            _tokens = tokens;
        }

        /// <summary>Parses a whole program.</summary>
        /// <param name="source">The program text.</param>
        /// <returns>The program, ready to run.</returns>
        public static BasicProgram Parse(string source)
        {
            var parser = new BasicParser(BasicLexer.Tokenize(source));
            return parser.ParseProgram();
        }

        /// <summary>The token about to be read.</summary>
        private BasicToken Current => _tokens[_at];

        /// <summary>Reads the whole token stream.</summary>
        private BasicProgram ParseProgram()
        {
            while (Current.Kind != BasicTokenKindEnum.EndOfFile)
            {
                if (Current.Kind == BasicTokenKindEnum.EndOfLine)
                {
                    _at++;
                    continue;
                }

                ParseLine();
            }

            if (_blocks.Count > 0)
                throw new BasicError("Missing " + _blocks[_blocks.Count - 1].Closer, _blocks[_blocks.Count - 1].Line);

            ResolveJumps();
            return new BasicProgram(_statements);
        }

        /// <summary>Reads one physical line: its number or label, then the statements on it.</summary>
        private void ParseLine()
        {
            // A number at the start of a line is a line number, which is a label like any other. Everywhere else a
            // number is just a number, which is why this is decided here rather than in the lexer.
            if (Current.Kind == BasicTokenKindEnum.Number)
            {
                DefineLabel(Current.Number.ToString(CultureInfo.InvariantCulture), Current.Line);
                _at++;
            }
            else if (Current.Kind == BasicTokenKindEnum.Word && _tokens[_at + 1].IsSymbol(":") &&
                     !IsStatementWord(Current.Text))
            {
                DefineLabel(Current.Text, Current.Line);
                _at += 2;
            }

            while (Current.Kind is not (BasicTokenKindEnum.EndOfLine or BasicTokenKindEnum.EndOfFile))
            {
                ParseStatement();

                if (Current.IsSymbol(":"))
                {
                    _at++;
                    continue;
                }

                break;
            }

            if (Current.Kind == BasicTokenKindEnum.EndOfLine)
                _at++;
        }

        /// <summary>
        ///     Whether a word introduces a statement, which is what stops <c>CLS:</c> being read as a label. Only
        ///     the words that can legitimately stand alone at the start of a line need listing.
        /// </summary>
        private static bool IsStatementWord(string word)
        {
            return word is "CLS" or "END" or "STOP" or "BEEP" or "RETURN" or "WEND" or "LOOP" or "NEXT" or "ELSE"
                or "PRINT" or "INPUT" or "DO" or "RANDOMIZE";
        }

        /// <summary>Remembers where a label points, refusing two with the same name.</summary>
        private void DefineLabel(string name, int line)
        {
            if (_labels.ContainsKey(name))
                throw new BasicError("Duplicate label " + name, line);

            _labels[name] = _statements.Count;
        }

        /// <summary>Reads one statement.</summary>
        private void ParseStatement()
        {
            var token = Current;

            if (token.Kind != BasicTokenKindEnum.Word)
                throw new BasicError("Expected a statement but found " + token, token.Line);

            switch (token.Text)
            {
                case "PRINT":
                    _at++;
                    ParsePrint(token.Line);
                    return;
                case "LET":
                    _at++;
                    ParseAssignment();
                    return;
                case "INPUT":
                    _at++;
                    ParseInput(token.Line);
                    return;
                case "IF":
                    _at++;
                    ParseIf(token.Line);
                    return;
                case "ELSEIF":
                    _at++;
                    ParseElseIf(token.Line);
                    return;
                case "ELSE":
                    _at++;
                    ParseElse(token.Line);
                    return;
                case "FOR":
                    _at++;
                    ParseFor(token.Line);
                    return;
                case "NEXT":
                    _at++;
                    ParseNext(token.Line);
                    return;
                case "WHILE":
                    _at++;
                    ParseWhile(token.Line);
                    return;
                case "WEND":
                    _at++;
                    ParseWend(token.Line);
                    return;
                case "SELECT":
                    _at++;
                    ParseSelect(token.Line);
                    return;
                case "CASE":
                    _at++;
                    ParseCase(token.Line);
                    return;
                case "DO":
                    _at++;
                    ParseDo(token.Line);
                    return;
                case "LOOP":
                    _at++;
                    ParseLoop(token.Line);
                    return;
                case "GOTO":
                    _at++;
                    ParseGoto(token.Line);
                    return;
                case "GOSUB":
                    _at++;
                    ParseGosub(token.Line);
                    return;
                case "RETURN":
                    _at++;
                    Emit(new BasicReturnStatement(token.Line));
                    return;
                case "DIM":
                    _at++;
                    ParseDim(token.Line);
                    return;
                case "END":
                    _at++;

                    // END IF is the block ender; END on its own stops the program. The word after it is what says
                    // which, and nothing else distinguishes them.
                    if (Current.IsWord("IF"))
                    {
                        _at++;
                        ParseEndIf(token.Line);
                        return;
                    }

                    if (Current.IsWord("SELECT"))
                    {
                        _at++;
                        ParseEndSelect(token.Line);
                        return;
                    }

                    Emit(new BasicCommandStatement("END", Array.Empty<BasicExpression>(), token.Line));
                    return;
                case "CLS":
                case "BEEP":
                case "STOP":
                case "RANDOMIZE":
                case "LOCATE":
                case "COLOR":
                    _at++;
                    Emit(new BasicCommandStatement(token.Text, ParseArgumentList(), token.Line));
                    return;
                default:
                    ParseAssignment();
                    return;
            }
        }

        /// <summary>
        ///     Whether the current token ends the statement being read.
        ///     <para>
        ///         <b>ELSE counts, and forgetting that is a silent disaster rather than an error.</b> In
        ///         <c>IF a THEN PRINT "y" ELSE PRINT "n"</c> the ELSE is the end of the first PRINT; a PRINT that
        ///         reads on would take ELSE, PRINT and "n" as three more things to print, the whole line would
        ///         become one arm of the IF, and the program would print everything or nothing.
        ///     </para>
        /// </summary>
        private bool AtStatementEnd()
        {
            return Current.Kind is BasicTokenKindEnum.EndOfLine or BasicTokenKindEnum.EndOfFile ||
                   Current.IsSymbol(":") || Current.IsWord("ELSE");
        }

        /// <summary>Adds a statement and hands back where it landed.</summary>
        private int Emit(BasicStatement statement)
        {
            _statements.Add(statement);
            return _statements.Count - 1;
        }

        /// <summary>Reads an assignment, with or without subscripts on the left.</summary>
        private void ParseAssignment()
        {
            var token = Current;
            var target = ParseTarget();

            if (!Current.IsSymbol("="))
                throw new BasicError("Expected = in an assignment", token.Line);

            _at++;
            Emit(new BasicAssignStatement(target, ParseExpression(), token.Line));
        }

        /// <summary>Reads somewhere a value can be put.</summary>
        private BasicTarget ParseTarget()
        {
            var token = Current;
            if (token.Kind != BasicTokenKindEnum.Word)
                throw new BasicError("Expected a variable but found " + token, token.Line);

            _at++;

            if (!Current.IsSymbol("("))
                return new BasicTarget(token.Text, null);

            return new BasicTarget(token.Text, ParseBracketedList(token.Line));
        }

        /// <summary>Reads PRINT and the punctuation that decides its layout.</summary>
        private void ParsePrint(int line)
        {
            var items = new List<BasicExpression>();
            var separators = new List<char>();

            while (!AtStatementEnd())
            {
                if (Current.IsSymbol(";") || Current.IsSymbol(","))
                {
                    // A separator with no expression in front of it still counts, which is what makes PRINT , move
                    // a zone and PRINT ; on its own hold the line.
                    if (items.Count == separators.Count)
                    {
                        items.Add(new BasicLiteralExpression(BasicValue.EmptyString, line));
                        separators.Add(Current.Text[0]);
                    }
                    else
                    {
                        separators.Add(Current.Text[0]);
                    }

                    _at++;
                    continue;
                }

                items.Add(ParseExpression());

                if (items.Count > separators.Count + 1)
                    separators.Add('\0');
            }

            Emit(new BasicPrintStatement(items, separators, line));
        }

        /// <summary>Reads INPUT, with the prompt string BASIC lets it carry.</summary>
        private void ParseInput(int line)
        {
            var prompt = "? ";

            if (Current.Kind == BasicTokenKindEnum.String)
            {
                prompt = Current.Text;
                _at++;

                // A semicolon after the prompt adds the question mark, a comma does not. That is the whole of the
                // difference and programs use it to ask a question without one.
                if (Current.IsSymbol(";"))
                {
                    prompt += "? ";
                    _at++;
                }
                else if (Current.IsSymbol(","))
                {
                    _at++;
                }
            }

            var targets = new List<BasicTarget> {ParseTarget()};
            while (Current.IsSymbol(","))
            {
                _at++;
                targets.Add(ParseTarget());
            }

            Emit(new BasicInputStatement(prompt, targets, line));
        }

        /// <summary>
        ///     Reads IF, which is two statements wearing one name: the single line form runs its body immediately,
        ///     and the block form opens a block that ELSE and END IF close.
        /// </summary>
        private void ParseIf(int line)
        {
            var condition = ParseExpression();

            if (!Current.IsWord("THEN"))
                throw new BasicError("Expected THEN", line);

            _at++;

            // Nothing after THEN means a block; anything after it means the single line form, where the body is
            // whatever is left on this line.
            if (Current.Kind is BasicTokenKindEnum.EndOfLine or BasicTokenKindEnum.EndOfFile)
            {
                var blockJump = new BasicJumpStatement(condition, false, line);
                Emit(blockJump);
                _blocks.Add(Block.If(blockJump, line));
                return;
            }

            var jump = new BasicJumpStatement(condition, false, line);
            Emit(jump);

            ParseInlineBody(line);

            if (!Current.IsWord("ELSE"))
            {
                jump.Target = _statements.Count;
                return;
            }

            _at++;

            var skipElse = new BasicJumpStatement(null, true, line);
            Emit(skipElse);
            jump.Target = _statements.Count;

            ParseInlineBody(line);
            skipElse.Target = _statements.Count;
        }

        /// <summary>
        ///     The body of a single line IF: statements up to the end of the line or an ELSE. A bare line number
        ///     there is an implied GOTO, which is how most old listings are written.
        /// </summary>
        private void ParseInlineBody(int line)
        {
            if (Current.Kind == BasicTokenKindEnum.Number)
            {
                // "IF x THEN 100" means GOTO 100. Old listings are written this way throughout, and reading it as
                // an expression statement instead would silently do nothing at all.
                var target = Current.Number.ToString(CultureInfo.InvariantCulture);
                _at++;

                var implied = new BasicJumpStatement(null, true, line);
                Emit(implied);
                _pending.Add(new PendingJump(implied, null, target, line));
                return;
            }

            while (!AtStatementEnd() || Current.IsSymbol(":"))
            {
                if (Current.IsSymbol(":"))
                {
                    _at++;
                    continue;
                }

                ParseStatement();

                if (!Current.IsSymbol(":"))
                    break;

                _at++;
            }
        }

        /// <summary>Reads ELSEIF, which closes the previous arm and opens another.</summary>
        private void ParseElseIf(int line)
        {
            var block = Open(BlockKindEnum.If, line);
            var condition = ParseExpression();

            if (!Current.IsWord("THEN"))
                throw new BasicError("Expected THEN", line);

            _at++;

            var skip = new BasicJumpStatement(null, true, line);
            Emit(skip);
            block.Exits.Add(skip);

            block.Pending.Target = _statements.Count;

            var next = new BasicJumpStatement(condition, false, line);
            Emit(next);
            block.Pending = next;
        }

        /// <summary>Reads ELSE.</summary>
        private void ParseElse(int line)
        {
            var block = Open(BlockKindEnum.If, line);

            var skip = new BasicJumpStatement(null, true, line);
            Emit(skip);
            block.Exits.Add(skip);

            block.Pending.Target = _statements.Count;
            block.Pending = null;
        }

        /// <summary>Reads END IF, which is where every arm of the IF lands.</summary>
        private void ParseEndIf(int line)
        {
            var block = Open(BlockKindEnum.If, line);

            if (block.Pending != null)
                block.Pending.Target = _statements.Count;

            foreach (var exit in block.Exits)
                exit.Target = _statements.Count;

            _blocks.RemoveAt(_blocks.Count - 1);
        }

        /// <summary>Reads FOR.</summary>
        private void ParseFor(int line)
        {
            var variable = Current;
            if (variable.Kind != BasicTokenKindEnum.Word)
                throw new BasicError("Expected a loop variable", line);

            _at++;

            if (!Current.IsSymbol("="))
                throw new BasicError("Expected = in FOR", line);

            _at++;
            var start = ParseExpression();

            if (!Current.IsWord("TO"))
                throw new BasicError("Expected TO in FOR", line);

            _at++;
            var limit = ParseExpression();

            BasicExpression step = null;
            if (Current.IsWord("STEP"))
            {
                _at++;
                step = ParseExpression();
            }

            var statement = new BasicForStatement(variable.Text, start, limit, step, line);
            Emit(statement);
            _blocks.Add(Block.For(statement, line));
        }

        /// <summary>Reads NEXT and finishes the loop it belongs to.</summary>
        private void ParseNext(int line)
        {
            var block = Open(BlockKindEnum.For, line);

            string variable = null;
            if (Current.Kind == BasicTokenKindEnum.Word && !IsStatementWord(Current.Text))
            {
                variable = Current.Text;
                _at++;
            }

            Emit(new BasicNextStatement(variable, line));

            // The FOR jumps here when the loop is over before it starts, which is the statement after the NEXT.
            block.Loop.ExitIndex = _statements.Count;
            _blocks.RemoveAt(_blocks.Count - 1);
        }

        /// <summary>Reads WHILE.</summary>
        private void ParseWhile(int line)
        {
            var top = _statements.Count;
            var condition = ParseExpression();

            var exit = new BasicJumpStatement(condition, false, line);
            Emit(exit);

            _blocks.Add(Block.While(top, exit, line));
        }

        /// <summary>Reads WEND.</summary>
        private void ParseWend(int line)
        {
            var block = Open(BlockKindEnum.While, line);

            var back = new BasicJumpStatement(null, true, line) {Target = block.Top};
            Emit(back);

            block.Pending.Target = _statements.Count;
            _blocks.RemoveAt(_blocks.Count - 1);
        }

        /// <summary>Reads DO, in both of the shapes that put their test at the top.</summary>
        private void ParseDo(int line)
        {
            var top = _statements.Count;
            BasicJumpStatement exit = null;

            if (Current.IsWord("WHILE") || Current.IsWord("UNTIL"))
            {
                var until = Current.IsWord("UNTIL");
                _at++;

                // DO WHILE leaves when the condition fails and DO UNTIL leaves when it holds, which is the only
                // difference between them and is expressed by which way round the jump is asked.
                exit = new BasicJumpStatement(ParseExpression(), until, line);
                Emit(exit);
            }

            _blocks.Add(Block.Do(top, exit, line));
        }

        /// <summary>Reads LOOP, in all three of its endings.</summary>
        private void ParseLoop(int line)
        {
            var block = Open(BlockKindEnum.Do, line);

            if (Current.IsWord("WHILE") || Current.IsWord("UNTIL"))
            {
                var until = Current.IsWord("UNTIL");
                _at++;

                var back = new BasicJumpStatement(ParseExpression(), !until, line) {Target = block.Top};
                Emit(back);
            }
            else
            {
                var back = new BasicJumpStatement(null, true, line) {Target = block.Top};
                Emit(back);
            }

            if (block.Pending != null)
                block.Pending.Target = _statements.Count;

            _blocks.RemoveAt(_blocks.Count - 1);
        }

        /// <summary>Reads SELECT CASE, which works its value out once and leaves it for the CASE tests.</summary>
        private void ParseSelect(int line)
        {
            if (!Current.IsWord("CASE"))
                throw new BasicError("Expected CASE after SELECT", line);

            _at++;
            Emit(new BasicSelectStatement(ParseExpression(), line));
            _blocks.Add(Block.Select(line));
        }

        /// <summary>Reads one CASE, closing whichever arm came before it.</summary>
        private void ParseCase(int line)
        {
            var block = Open(BlockKindEnum.Select, line);

            if (block.Started)
            {
                // The arm above this one runs to the end of the construct rather than falling into this test, which
                // is the whole difference between SELECT CASE and a switch that needs breaking out of.
                var skip = new BasicJumpStatement(null, true, line);
                Emit(skip);
                block.Exits.Add(skip);

                if (block.Pending != null)
                    block.Pending.Target = _statements.Count;
            }

            block.Started = true;

            if (Current.IsWord("ELSE"))
            {
                _at++;
                block.Pending = null;
                return;
            }

            var next = new BasicJumpStatement(ParseCaseTests(line), false, line);
            Emit(next);
            block.Pending = next;
        }

        /// <summary>Reads END SELECT, where every arm lands and the selected value is thrown away.</summary>
        private void ParseEndSelect(int line)
        {
            var block = Open(BlockKindEnum.Select, line);

            if (block.Pending != null)
                block.Pending.Target = _statements.Count;

            foreach (var exit in block.Exits)
                exit.Target = _statements.Count;

            // Emitted after the patching, so every path lands on it and the value is discarded exactly once.
            Emit(new BasicEndSelectStatement(line));
            _blocks.RemoveAt(_blocks.Count - 1);
        }

        /// <summary>Reads the comma separated tests of one CASE into a single condition.</summary>
        private BasicExpression ParseCaseTests(int line)
        {
            BasicExpression condition = null;

            while (true)
            {
                var test = ParseCaseTest(line);

                // OR rather than anything cleverer: a comparison is 0 or -1, so a bitwise OR of two of them is
                // exactly the logical one, and the ordinary conditional jump then runs the whole construct.
                condition = condition == null
                    ? test
                    : new BasicBinaryExpression("OR", condition, test, line);

                if (!Current.IsSymbol(","))
                    return condition;

                _at++;
            }
        }

        /// <summary>Reads one CASE test: a value, a range, or a comparison introduced by IS.</summary>
        private BasicExpression ParseCaseTest(int line)
        {
            if (Current.IsWord("IS"))
            {
                _at++;

                if (Current.Kind != BasicTokenKindEnum.Symbol ||
                    Current.Text is not ("=" or "<>" or "<" or ">" or "<=" or ">="))
                    throw new BasicError("Expected a comparison after IS", line);

                var op = Current.Text;
                _at++;

                return new BasicBinaryExpression(op, new BasicSelectValueExpression(line), ParseExpression(), line);
            }

            var first = ParseExpression();

            if (!Current.IsWord("TO"))
                return new BasicBinaryExpression("=", new BasicSelectValueExpression(line), first, line);

            _at++;
            var last = ParseExpression();

            return new BasicBinaryExpression("AND",
                new BasicBinaryExpression(">=", new BasicSelectValueExpression(line), first, line),
                new BasicBinaryExpression("<=", new BasicSelectValueExpression(line), last, line), line);
        }

        /// <summary>Reads GOTO.</summary>
        private void ParseGoto(int line)
        {
            var target = ParseJumpTarget(line);
            var jump = new BasicJumpStatement(null, true, line);

            Emit(jump);
            _pending.Add(new PendingJump(jump, null, target, line));
        }

        /// <summary>Reads GOSUB.</summary>
        private void ParseGosub(int line)
        {
            var target = ParseJumpTarget(line);
            var jump = new BasicGosubStatement(line);

            Emit(jump);
            _pending.Add(new PendingJump(null, jump, target, line));
        }

        /// <summary>Reads the name or number a jump is aimed at.</summary>
        private string ParseJumpTarget(int line)
        {
            var token = Current;

            if (token.Kind == BasicTokenKindEnum.Number)
            {
                _at++;
                return token.Number.ToString(CultureInfo.InvariantCulture);
            }

            if (token.Kind == BasicTokenKindEnum.Word)
            {
                _at++;
                return token.Text;
            }

            throw new BasicError("Expected a line number or label", line);
        }

        /// <summary>Reads DIM, which may dimension several arrays at once.</summary>
        private void ParseDim(int line)
        {
            while (true)
            {
                var name = Current;
                if (name.Kind != BasicTokenKindEnum.Word)
                    throw new BasicError("Expected an array name", line);

                _at++;

                var bounds = Current.IsSymbol("(")
                    ? ParseBracketedList(line)
                    : new List<BasicExpression>();

                Emit(new BasicDimStatement(name.Text, bounds, line));

                if (!Current.IsSymbol(","))
                    return;

                _at++;
            }
        }

        /// <summary>Reads a bracketed, comma separated list of expressions.</summary>
        private List<BasicExpression> ParseBracketedList(int line)
        {
            if (!Current.IsSymbol("("))
                throw new BasicError("Expected (", line);

            _at++;
            var items = new List<BasicExpression>();

            if (Current.IsSymbol(")"))
            {
                _at++;
                return items;
            }

            items.Add(ParseExpression());
            while (Current.IsSymbol(","))
            {
                _at++;
                items.Add(ParseExpression());
            }

            if (!Current.IsSymbol(")"))
                throw new BasicError("Expected )", line);

            _at++;
            return items;
        }

        /// <summary>Reads the unbracketed argument list a command statement carries.</summary>
        private List<BasicExpression> ParseArgumentList()
        {
            var items = new List<BasicExpression>();

            while (!AtStatementEnd())
            {
                if (Current.IsSymbol(","))
                {
                    // A missing argument between commas is allowed and means "leave this one alone", which is how
                    // COLOR sets a background without touching the foreground.
                    items.Add(null);
                    _at++;
                    continue;
                }

                items.Add(ParseExpression());

                if (!Current.IsSymbol(","))
                    break;

                _at++;
            }

            return items;
        }

        /// <summary>The innermost block, insisting it is the kind the statement expects.</summary>
        private Block Open(BlockKindEnum kind, int line)
        {
            if (_blocks.Count == 0 || _blocks[_blocks.Count - 1].Kind != kind)
            {
                // Named the way BASIC has always named them, because "NEXT without FOR" is a phrase people
                // recognise and go looking for, and a generic complaint about blocks is not.
                throw new BasicError(kind switch
                {
                    BlockKindEnum.For => "NEXT without FOR",
                    BlockKindEnum.While => "WEND without WHILE",
                    BlockKindEnum.Do => "LOOP without DO",
                    BlockKindEnum.Select => "CASE without SELECT CASE",
                    _ => "ELSE or END IF without IF"
                }, line);
            }

            return _blocks[_blocks.Count - 1];
        }

        /// <summary>Points every jump at the label it named, now that every label is known.</summary>
        private void ResolveJumps()
        {
            foreach (var pending in _pending)
            {
                if (!_labels.TryGetValue(pending.Target, out var index))
                    throw new BasicError("Cannot find line or label " + pending.Target, pending.Line);

                if (pending.Jump != null)
                    pending.Jump.Target = index;
                else
                    pending.Gosub.Target = index;
            }
        }

        /// <summary>
        ///     Reads an expression.
        ///     <para>
        ///         Precedence is spelled out as one method per level rather than as a table, in BASIC's own order:
        ///         OR and XOR bind loosest, then AND, then NOT, then the comparisons, then plus and minus, then MOD,
        ///         then integer division, then multiply and divide, then a sign, and <c>^</c> binds tightest of all.
        ///         <b>The comparisons binding tighter than AND is the one that matters</b>, because it is what makes
        ///         <c>IF a = 1 AND b = 2</c> mean what it reads as instead of comparing 1 to the bits of b.
        ///     </para>
        /// </summary>
        /// <returns>The expression.</returns>
        private BasicExpression ParseExpression()
        {
            return ParseOr();
        }

        /// <summary>OR and XOR, the loosest binding of the lot.</summary>
        private BasicExpression ParseOr()
        {
            var left = ParseAnd();

            while (Current.IsWord("OR") || Current.IsWord("XOR"))
            {
                var op = Current;
                _at++;
                left = new BasicBinaryExpression(op.Text, left, ParseAnd(), op.Line);
            }

            return left;
        }

        /// <summary>AND.</summary>
        private BasicExpression ParseAnd()
        {
            var left = ParseNot();

            while (Current.IsWord("AND"))
            {
                var op = Current;
                _at++;
                left = new BasicBinaryExpression("AND", left, ParseNot(), op.Line);
            }

            return left;
        }

        /// <summary>NOT, which is a prefix and binds looser than any comparison.</summary>
        private BasicExpression ParseNot()
        {
            if (!Current.IsWord("NOT"))
                return ParseComparison();

            var op = Current;
            _at++;

            return new BasicUnaryExpression("NOT", ParseNot(), op.Line);
        }

        /// <summary>The comparisons.</summary>
        private BasicExpression ParseComparison()
        {
            var left = ParseAdditive();

            while (Current.Kind == BasicTokenKindEnum.Symbol &&
                   Current.Text is "=" or "<>" or "<" or ">" or "<=" or ">=")
            {
                var op = Current;
                _at++;
                left = new BasicBinaryExpression(op.Text, left, ParseAdditive(), op.Line);
            }

            return left;
        }

        /// <summary>Addition, subtraction, and the joining of strings.</summary>
        private BasicExpression ParseAdditive()
        {
            var left = ParseModulo();

            while (Current.IsSymbol("+") || Current.IsSymbol("-"))
            {
                var op = Current;
                _at++;
                left = new BasicBinaryExpression(op.Text, left, ParseModulo(), op.Line);
            }

            return left;
        }

        /// <summary>MOD.</summary>
        private BasicExpression ParseModulo()
        {
            var left = ParseIntegerDivide();

            while (Current.IsWord("MOD"))
            {
                var op = Current;
                _at++;
                left = new BasicBinaryExpression("MOD", left, ParseIntegerDivide(), op.Line);
            }

            return left;
        }

        /// <summary>Integer division, which binds tighter than MOD and looser than ordinary division.</summary>
        private BasicExpression ParseIntegerDivide()
        {
            var left = ParseMultiplicative();

            while (Current.IsSymbol("\\"))
            {
                var op = Current;
                _at++;
                left = new BasicBinaryExpression("BACKSLASH", left, ParseMultiplicative(), op.Line);
            }

            return left;
        }

        /// <summary>Multiplication and division.</summary>
        private BasicExpression ParseMultiplicative()
        {
            var left = ParseUnary();

            while (Current.IsSymbol("*") || Current.IsSymbol("/"))
            {
                var op = Current;
                _at++;
                left = new BasicBinaryExpression(op.Text, left, ParseUnary(), op.Line);
            }

            return left;
        }

        /// <summary>A leading sign.</summary>
        private BasicExpression ParseUnary()
        {
            if (!Current.IsSymbol("-") && !Current.IsSymbol("+"))
                return ParsePower();

            var op = Current;
            _at++;

            return new BasicUnaryExpression(op.Text, ParseUnary(), op.Line);
        }

        /// <summary>
        ///     Raising to a power, which binds tightest and <b>groups to the right</b>: 2 ^ 3 ^ 2 is 2 ^ 9 and not
        ///     8 ^ 2. Grouping it leftwards like the other operators gives 64 instead of 512.
        /// </summary>
        private BasicExpression ParsePower()
        {
            var left = ParsePrimary();

            if (!Current.IsSymbol("^"))
                return left;

            var op = Current;
            _at++;

            return new BasicBinaryExpression("^", left, ParseUnary(), op.Line);
        }

        /// <summary>A literal, a bracketed expression, a variable, an array element or a function call.</summary>
        private BasicExpression ParsePrimary()
        {
            var token = Current;

            switch (token.Kind)
            {
                case BasicTokenKindEnum.Number:
                    _at++;
                    return new BasicLiteralExpression(new BasicValue(token.Number), token.Line);
                case BasicTokenKindEnum.String:
                    _at++;
                    return new BasicLiteralExpression(new BasicValue(token.Text), token.Line);
                case BasicTokenKindEnum.Word:
                    _at++;

                    // A name with a bracket after it is either an array element or a function call, and BASIC does
                    // not say which. It is not decided here: BasicCallExpression asks at run time, because a
                    // program may dimension an array inside a branch the parser has not read yet.
                    if (Current.IsSymbol("("))
                        return new BasicCallExpression(token.Text, ParseBracketedList(token.Line), token.Line);

                    if (BasicFunctions.IsBare(token.Text))
                        return new BasicCallExpression(token.Text, Array.Empty<BasicExpression>(), token.Line);

                    return new BasicVariableExpression(token.Text, token.Line);
                default:
                    if (token.IsSymbol("("))
                    {
                        _at++;
                        var inner = ParseExpression();

                        if (!Current.IsSymbol(")"))
                            throw new BasicError("Expected )", token.Line);

                        _at++;
                        return inner;
                    }

                    throw new BasicError("Expected a value but found " + token, token.Line);
            }
        }

        /// <summary>What kind of block is open.</summary>
        private enum BlockKindEnum
        {
            /// <summary>An IF waiting for its END IF.</summary>
            If,

            /// <summary>A FOR waiting for its NEXT.</summary>
            For,

            /// <summary>A WHILE waiting for its WEND.</summary>
            While,

            /// <summary>A DO waiting for its LOOP.</summary>
            Do,

            /// <summary>A SELECT CASE waiting for its END SELECT.</summary>
            Select
        }

        /// <summary>A block the parser is inside, and the jumps that still need pointing somewhere.</summary>
        private sealed class Block
        {
            /// <summary>The jumps that leave this block, all landing just past its end.</summary>
            public List<BasicJumpStatement> Exits { get; } = new();

            /// <summary>What kind of block it is.</summary>
            public BlockKindEnum Kind { get; private init; }

            /// <summary>The line it opened on, for the error when it is never closed.</summary>
            public int Line { get; private init; }

            /// <summary>The FOR statement, when this is a FOR.</summary>
            public BasicForStatement Loop { get; private init; }

            /// <summary>The jump waiting to be told where the current arm ends.</summary>
            public BasicJumpStatement Pending { get; set; }

            /// <summary>
            ///     Whether a SELECT CASE has had a CASE yet, which is what tells the first one from the rest: only
            ///     the ones after the first have a previous arm to close off.
            /// </summary>
            public bool Started { get; set; }

            /// <summary>The first statement of the body, for the loops that jump back to it.</summary>
            public int Top { get; private init; }

            /// <summary>What has to be written to close it.</summary>
            public string Closer => Kind switch
            {
                BlockKindEnum.If => "END IF",
                BlockKindEnum.For => "NEXT",
                BlockKindEnum.While => "WEND",
                BlockKindEnum.Select => "END SELECT",
                _ => "LOOP"
            };

            /// <summary>An open IF.</summary>
            public static Block If(BasicJumpStatement pending, int line)
            {
                return new Block {Kind = BlockKindEnum.If, Pending = pending, Line = line};
            }

            /// <summary>An open FOR.</summary>
            public static Block For(BasicForStatement loop, int line)
            {
                return new Block {Kind = BlockKindEnum.For, Loop = loop, Line = line};
            }

            /// <summary>An open WHILE.</summary>
            public static Block While(int top, BasicJumpStatement exit, int line)
            {
                return new Block {Kind = BlockKindEnum.While, Top = top, Pending = exit, Line = line};
            }

            /// <summary>An open DO.</summary>
            public static Block Do(int top, BasicJumpStatement exit, int line)
            {
                return new Block {Kind = BlockKindEnum.Do, Top = top, Pending = exit, Line = line};
            }

            /// <summary>An open SELECT CASE.</summary>
            public static Block Select(int line)
            {
                return new Block {Kind = BlockKindEnum.Select, Line = line};
            }
        }

        /// <summary>A jump that named a label the parser had not reached yet.</summary>
        private sealed class PendingJump
        {
            /// <summary>Initializes a new instance of the <see cref="PendingJump" /> class.</summary>
            public PendingJump(BasicJumpStatement jump, BasicGosubStatement gosub, string target, int line)
            {
                Jump = jump;
                Gosub = gosub;
                Target = target;
                Line = line;
            }

            /// <summary>The GOSUB, when it is one.</summary>
            public BasicGosubStatement Gosub { get; }

            /// <summary>The jump, when it is one.</summary>
            public BasicJumpStatement Jump { get; }

            /// <summary>The line that wrote it.</summary>
            public int Line { get; }

            /// <summary>The label or line number it named.</summary>
            public string Target { get; }
        }
    }
}
