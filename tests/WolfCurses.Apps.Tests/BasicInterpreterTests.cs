using System;
using WolfCurses.Apps.Basic;
using WolfCurses.Apps.Tests.Support;
using Xunit;

namespace WolfCurses.Apps.Tests
{
    /// <summary>
    ///     Whole BASIC programs, run and checked. Pure: the interpreter talks to a host that is really a string, so
    ///     none of this needs an application, a window or a terminal.
    /// </summary>
    public class BasicInterpreterTests
    {
        private static string Run(string source)
        {
            return RecordingBasicHost.Printed(source);
        }

        [Fact]
        public void ItPrintsAStringAndANumber()
        {
            Assert.Equal("HELLO", Run("PRINT \"HELLO\""), StringComparer.Ordinal);

            // A number is printed with a space where its sign would be and a space after it, which is why BASIC
            // listings line up in columns. Programs lay their screens out by counting those spaces.
            Assert.Equal(" 42 ", Run("PRINT 42"), StringComparer.Ordinal);
            Assert.Equal("-42 ", Run("PRINT -42"), StringComparer.Ordinal);
        }

        [Fact]
        public void AWholeNumberPrintsWithoutADecimalPoint()
        {
            Assert.Equal(" 3 ", Run("PRINT 6 / 2"), StringComparer.Ordinal);
            Assert.Equal(" 2.5 ", Run("PRINT 5 / 2"), StringComparer.Ordinal);
        }

        [Fact]
        public void ASemicolonHoldsTheLineAndTheAbsenceOfOneEndsIt()
        {
            // The entire difference between PRINT "A" and PRINT "A"; and the reason BASIC can draw anything.
            Assert.Equal("AB", Run("PRINT \"A\";\nPRINT \"B\""), StringComparer.Ordinal);
            Assert.Equal("A\nB", Run("PRINT \"A\"\nPRINT \"B\""), StringComparer.Ordinal);
        }

        [Fact]
        public void ArithmeticFollowsBasicsOwnPrecedence()
        {
            Assert.Equal(" 7 ", Run("PRINT 1 + 2 * 3"), StringComparer.Ordinal);
            Assert.Equal(" 9 ", Run("PRINT (1 + 2) * 3"), StringComparer.Ordinal);

            // Powers group to the right: 2 ^ 3 ^ 2 is 2 ^ 9 and not 8 ^ 2, which would be 64.
            Assert.Equal(" 512 ", Run("PRINT 2 ^ 3 ^ 2"), StringComparer.Ordinal);
            Assert.Equal("-4 ", Run("PRINT -2 ^ 2"), StringComparer.Ordinal);
        }

        [Fact]
        public void ComparisonsBindTighterThanAndSoConditionsReadAsWritten()
        {
            // If AND bound tighter, "a = 1 AND b = 2" would compare 1 to the bits of b and the whole thing would
            // quietly mean something else.
            Assert.Equal("YES", Run("A = 1\nB = 2\nIF A = 1 AND B = 2 THEN PRINT \"YES\""), StringComparer.Ordinal);
            Assert.Equal(string.Empty, Run("A = 1\nB = 3\nIF A = 1 AND B = 2 THEN PRINT \"YES\""),
                StringComparer.Ordinal);
        }

        [Fact]
        public void TruthIsMinusOneBecauseProgramsUseItArithmetically()
        {
            Assert.Equal("-1 ", Run("PRINT (1 = 1)"), StringComparer.Ordinal);
            Assert.Equal(" 0 ", Run("PRINT (1 = 2)"), StringComparer.Ordinal);
        }

        [Fact]
        public void IntegerDivisionAndModuloTruncateFirst()
        {
            Assert.Equal(" 3 ", Run("PRINT 7.9 \\ 2"), StringComparer.Ordinal);
            Assert.Equal(" 1 ", Run("PRINT 7 MOD 2"), StringComparer.Ordinal);
        }

        [Fact]
        public void StringsJoinWithPlusAndMixingTheTwoIsRefused()
        {
            Assert.Equal("AB", Run("PRINT \"A\" + \"B\""), StringComparer.Ordinal);

            // Refused rather than guessed at, which is what stops "1" + 1 quietly becoming either 2 or "11".
            var error = RecordingBasicHost.Fails("PRINT \"A\" + 1");
            Assert.Contains("Type mismatch", error.Reason, StringComparison.Ordinal);
        }

        [Fact]
        public void AVariableThatWasNeverSetReadsAsZeroOrAsNothing()
        {
            Assert.Equal(" 0 ", Run("PRINT COUNT"), StringComparer.Ordinal);
            Assert.Equal("|", Run("PRINT NAME$ + \"|\""), StringComparer.Ordinal);
        }

        [Fact]
        public void ADollarMakesAWhollyDifferentVariable()
        {
            Assert.Equal(" 1 A", Run("A = 1\nA$ = \"A\"\nPRINT A; A$"), StringComparer.Ordinal);
        }

        [Fact]
        public void ForCountsUpAndDownAndHonoursItsStep()
        {
            Assert.Equal(" 1  2  3 ", Run("FOR I = 1 TO 3\nPRINT I;\nNEXT I"), StringComparer.Ordinal);
            Assert.Equal(" 3  2  1 ", Run("FOR I = 3 TO 1 STEP -1\nPRINT I;\nNEXT I"), StringComparer.Ordinal);
            Assert.Equal(" 1  3  5 ", Run("FOR I = 1 TO 5 STEP 2\nPRINT I;\nNEXT I"), StringComparer.Ordinal);
        }

        [Fact]
        public void AForWhoseStartIsAlreadyPastItsLimitRunsNoTimesAtAll()
        {
            // The behaviour every program depends on when it loops over an empty list. Testing at the bottom of the
            // loop instead would run the body once, which hides until the day the list really is empty.
            Assert.Equal("DONE", Run("FOR I = 1 TO 0\nPRINT \"BODY\"\nNEXT I\nPRINT \"DONE\""),
                StringComparer.Ordinal);
        }

        [Fact]
        public void TheLimitIsWorkedOutOnceRatherThanEveryTimeRound()
        {
            // FOR I = 1 TO N runs the number of times N said at the start, even if the body changes N. A loop that
            // re-read it would quietly do something else.
            Assert.Equal(" 1  2  3 ", Run("N = 3\nFOR I = 1 TO N\nPRINT I;\nN = 99\nNEXT I"), StringComparer.Ordinal);
        }

        [Fact]
        public void LoopsNestAndABareNextBelongsToTheInnermost()
        {
            Assert.Equal("11 12 21 22 ", Run(
                "FOR I = 1 TO 2\nFOR J = 1 TO 2\nPRINT LTRIM$(STR$(I)) + LTRIM$(STR$(J)) + \" \";\nNEXT\nNEXT"),
                StringComparer.Ordinal);
        }

        [Fact]
        public void WhileRunsWhileItsConditionHolds()
        {
            Assert.Equal(" 1  2  3 ", Run("I = 1\nWHILE I <= 3\nPRINT I;\nI = I + 1\nWEND"), StringComparer.Ordinal);
        }

        [Fact]
        public void AllFourShapesOfDoLoopWork()
        {
            Assert.Equal(" 1  2 ", Run("I = 1\nDO WHILE I <= 2\nPRINT I;\nI = I + 1\nLOOP"), StringComparer.Ordinal);
            Assert.Equal(" 1  2 ", Run("I = 1\nDO UNTIL I > 2\nPRINT I;\nI = I + 1\nLOOP"), StringComparer.Ordinal);
            Assert.Equal(" 1  2 ", Run("I = 1\nDO\nPRINT I;\nI = I + 1\nLOOP WHILE I <= 2"), StringComparer.Ordinal);
            Assert.Equal(" 1  2 ", Run("I = 1\nDO\nPRINT I;\nI = I + 1\nLOOP UNTIL I > 2"), StringComparer.Ordinal);
        }

        [Fact]
        public void ADoLoopWithItsTestAtTheBottomAlwaysRunsOnce()
        {
            // Which is the whole reason both shapes exist, and is not a detail: the top-tested form would print
            // nothing here.
            Assert.Equal(" 1 ", Run("I = 1\nDO\nPRINT I;\nI = I + 1\nLOOP WHILE I < 0"), StringComparer.Ordinal);
            Assert.Equal(string.Empty, Run("I = 1\nDO WHILE I < 0\nPRINT I;\nLOOP"), StringComparer.Ordinal);
        }

        [Fact]
        public void ABlockIfPicksExactlyOneArm()
        {
            const string program = "A = 2\nIF A = 1 THEN\nPRINT \"ONE\"\nELSEIF A = 2 THEN\nPRINT \"TWO\"\n" +
                                   "ELSE\nPRINT \"OTHER\"\nEND IF\nPRINT \"AFTER\"";

            Assert.Equal("TWO\nAFTER", Run(program), StringComparer.Ordinal);
        }

        [Fact]
        public void ASingleLineIfCanCarryAnElse()
        {
            Assert.Equal("NO", Run("A = 0\nIF A THEN PRINT \"YES\" ELSE PRINT \"NO\""), StringComparer.Ordinal);
            Assert.Equal("YES", Run("A = 1\nIF A THEN PRINT \"YES\" ELSE PRINT \"NO\""), StringComparer.Ordinal);
        }

        [Fact]
        public void GotoJumpsToALineNumberOrToALabel()
        {
            Assert.Equal("END", Run("10 GOTO 30\n20 PRINT \"SKIPPED\"\n30 PRINT \"END\""), StringComparer.Ordinal);
            Assert.Equal("END", Run("GOTO Finish\nPRINT \"SKIPPED\"\nFinish:\nPRINT \"END\""), StringComparer.Ordinal);
        }

        [Fact]
        public void AnIfWithABareLineNumberIsAnImpliedGoto()
        {
            // How most old listings are written, and reading it as anything else silently does nothing.
            Assert.Equal("JUMPED", Run("10 IF 1 THEN 30\n20 PRINT \"NO\"\n30 PRINT \"JUMPED\""),
                StringComparer.Ordinal);
        }

        [Fact]
        public void GosubComesBackToJustAfterItself()
        {
            const string program = "GOSUB Greet\nPRINT \"BACK\"\nEND\nGreet:\nPRINT \"HELLO\"\nRETURN";

            Assert.Equal("HELLO\nBACK", Run(program), StringComparer.Ordinal);
        }

        [Fact]
        public void SubroutinesNestBecauseTheReturnAddressesAreAStack()
        {
            const string program = "GOSUB Outer\nEND\n" +
                                   "Outer:\nPRINT \"A\";\nGOSUB Inner\nPRINT \"C\";\nRETURN\n" +
                                   "Inner:\nPRINT \"B\";\nRETURN";

            Assert.Equal("ABC", Run(program), StringComparer.Ordinal);
        }

        [Fact]
        public void ArraysAreDimensionedInclusivelyAndStartAtZero()
        {
            // DIM A(2) has three elements, and a program that indexes A(2) after dimensioning it that way would be
            // one short otherwise.
            Assert.Equal(" 10  20  30 ", Run("DIM A(2)\nA(0) = 10\nA(1) = 20\nA(2) = 30\nPRINT A(0); A(1); A(2)"),
                StringComparer.Ordinal);
        }

        [Fact]
        public void ATwoDimensionalArrayWorks()
        {
            Assert.Equal(" 7 ", Run("DIM G(2, 2)\nG(1, 2) = 7\nPRINT G(1, 2)"), StringComparer.Ordinal);
        }

        [Fact]
        public void AStringArrayStartsAsEmptyStringsRatherThanAsNumbers()
        {
            Assert.Equal("|", Run("DIM N$(2)\nPRINT N$(1) + \"|\""), StringComparer.Ordinal);
        }

        [Fact]
        public void InputFillsSeveralVariablesFromOneAnswer()
        {
            var host = new RecordingBasicHost();
            host.Answer("7, Wolf");

            BasicProgram.Compile("INPUT \"Name\"; A, B$\nPRINT A; B$").Run(new BasicRuntime(host, 1));

            Assert.Equal(" 7 Wolf", host.Output.TrimEnd('\n'), StringComparer.Ordinal);
            Assert.Equal("Name? ", host.Prompts[0], StringComparer.Ordinal);
        }

        [Fact]
        public void TheStringFunctionsCountFromOne()
        {
            // MID$ is the one where an off-by-one silently returns the wrong characters rather than failing.
            Assert.Equal("WOLF", Run("PRINT LEFT$(\"WOLFCURSES\", 4)"), StringComparer.Ordinal);
            Assert.Equal("CURSES", Run("PRINT RIGHT$(\"WOLFCURSES\", 6)"), StringComparer.Ordinal);
            Assert.Equal("CUR", Run("PRINT MID$(\"WOLFCURSES\", 5, 3)"), StringComparer.Ordinal);
            Assert.Equal("CURSES", Run("PRINT MID$(\"WOLFCURSES\", 5)"), StringComparer.Ordinal);
            Assert.Equal(" 5 ", Run("PRINT INSTR(\"WOLFCURSES\", \"C\")"), StringComparer.Ordinal);

            // Not found is zero, which is why a BASIC program tests INSTR against zero and not against minus one.
            Assert.Equal(" 0 ", Run("PRINT INSTR(\"WOLF\", \"Z\")"), StringComparer.Ordinal);
        }

        [Fact]
        public void AskingForMoreCharactersThanThereAreIsClampedRatherThanRefused()
        {
            Assert.Equal("WOLF", Run("PRINT LEFT$(\"WOLF\", 99)"), StringComparer.Ordinal);
            Assert.Equal(string.Empty, Run("PRINT MID$(\"WOLF\", 99)"), StringComparer.Ordinal);
        }

        [Fact]
        public void IntAndFixDisagreeOnNegativeNumbersAndProgramsUseTheDifference()
        {
            Assert.Equal("-3 ", Run("PRINT INT(-2.5)"), StringComparer.Ordinal);
            Assert.Equal("-2 ", Run("PRINT FIX(-2.5)"), StringComparer.Ordinal);
        }

        [Fact]
        public void CommandsReachTheHostRatherThanAConsole()
        {
            var host = RecordingBasicHost.Run("CLS\nLOCATE 5, 10\nCOLOR 4, 1\nBEEP");

            Assert.Equal(1, host.Clears);
            Assert.Equal((5, 10), host.Cursor);
            Assert.Equal((4, 1), host.Colors);
            Assert.Equal(1, host.Beeps);
        }

        [Fact]
        public void EndStopsTheProgramWhereItStands()
        {
            Assert.Equal("BEFORE", Run("PRINT \"BEFORE\"\nEND\nPRINT \"AFTER\""), StringComparer.Ordinal);
        }

        [Fact]
        public void SeveralStatementsCanShareALineWithColons()
        {
            Assert.Equal(" 1  2 ", Run("A = 1 : B = 2 : PRINT A; B;"), StringComparer.Ordinal);
        }

        [Fact]
        public void CommentsAreIgnoredInBothSpellings()
        {
            Assert.Equal("HI", Run("REM this does nothing\nPRINT \"HI\" ' nor does this"), StringComparer.Ordinal);
        }

        [Fact]
        public void SelectCasePicksTheArmThatMatches()
        {
            const string program = "A = 2\nSELECT CASE A\nCASE 1\nPRINT \"ONE\"\nCASE 2\nPRINT \"TWO\"\n" +
                                   "CASE 3\nPRINT \"THREE\"\nEND SELECT\nPRINT \"AFTER\"";

            Assert.Equal("TWO\nAFTER", Run(program), StringComparer.Ordinal);
        }

        [Fact]
        public void OneCaseCanListSeveralValues()
        {
            const string program = "SELECT CASE A\nCASE 1, 2, 3\nPRINT \"SMALL\"\nCASE ELSE\n" +
                                   "PRINT \"OTHER\"\nEND SELECT";

            Assert.Equal("OTHER", Run("A = 9\n" + program), StringComparer.Ordinal);
            Assert.Equal("SMALL", Run("A = 2\n" + program), StringComparer.Ordinal);
        }

        [Fact]
        public void CaseIsTakesAComparisonAndCaseToTakesARange()
        {
            const string program = "SELECT CASE A\nCASE IS < 0\nPRINT \"NEGATIVE\"\nCASE 0\nPRINT \"ZERO\"\n" +
                                   "CASE 1 TO 9\nPRINT \"DIGIT\"\nCASE ELSE\nPRINT \"BIG\"\nEND SELECT";

            Assert.Equal("NEGATIVE", Run("A = -5\n" + program), StringComparer.Ordinal);
            Assert.Equal("ZERO", Run("A = 0\n" + program), StringComparer.Ordinal);
            Assert.Equal("DIGIT", Run("A = 7\n" + program), StringComparer.Ordinal);
            Assert.Equal("BIG", Run("A = 99\n" + program), StringComparer.Ordinal);

            // The ends of a range are included, which is what TO means and is the off-by-one worth pinning.
            Assert.Equal("DIGIT", Run("A = 1\n" + program), StringComparer.Ordinal);
            Assert.Equal("DIGIT", Run("A = 9\n" + program), StringComparer.Ordinal);
        }

        [Fact]
        public void OnlyOneArmRunsAndTheValueDoesNotChangeUnderneathIt()
        {
            // Two things at once, and both are what separate SELECT CASE from a switch that falls through: the
            // matching arm does not run into the next one, and an arm that changes the selected variable does not
            // make a later CASE match, because the value was taken once at the top.
            const string program = "X = 1\nSELECT CASE X\nCASE 1\nX = 2\nPRINT \"ONE\"\nCASE 2\n" +
                                   "PRINT \"TWO\"\nEND SELECT";

            Assert.Equal("ONE", Run(program), StringComparer.Ordinal);
        }

        [Fact]
        public void NothingMatchingAndNoElseSimplyCarriesOn()
        {
            const string program = "SELECT CASE 99\nCASE 1\nPRINT \"ONE\"\nEND SELECT\nPRINT \"AFTER\"";

            Assert.Equal("AFTER", Run(program), StringComparer.Ordinal);
        }

        [Fact]
        public void SelectCaseWorksOnStringsToo()
        {
            const string program = "SELECT CASE A$\nCASE \"YES\"\nPRINT \"AGREED\"\nCASE ELSE\n" +
                                   "PRINT \"DECLINED\"\nEND SELECT";

            Assert.Equal("AGREED", Run("A$ = \"YES\"\n" + program), StringComparer.Ordinal);
            Assert.Equal("DECLINED", Run("A$ = \"NO\"\n" + program), StringComparer.Ordinal);
        }

        [Fact]
        public void ASelectInsideASelectTestsItsOwnValue()
        {
            // Which is why the selected values are a stack rather than one slot.
            const string program = "A = 1\nB = 2\nSELECT CASE A\nCASE 1\nSELECT CASE B\nCASE 1\n" +
                                   "PRINT \"INNER ONE\"\nCASE 2\nPRINT \"INNER TWO\"\nEND SELECT\n" +
                                   "PRINT \"OUTER ONE\"\nEND SELECT";

            Assert.Equal("INNER TWO\nOUTER ONE", Run(program), StringComparer.Ordinal);
        }

        [Theory]
        [InlineData("PRINT 1 / 0", "Division by zero")]
        [InlineData("NEXT I", "NEXT without FOR")]
        [InlineData("RETURN", "RETURN without GOSUB")]
        [InlineData("GOTO Nowhere", "Cannot find line or label")]
        [InlineData("A = 1\nPRINT A(1)", "has not been dimensioned")]
        [InlineData("DIM A(2)\nPRINT A(9)", "Subscript out of range")]
        [InlineData("IF 1 THEN\nPRINT 1", "Missing END IF")]
        [InlineData("A$ = 1", "Type mismatch")]
        [InlineData("CASE 1", "CASE without SELECT CASE")]
        [InlineData("SELECT CASE 1\nCASE 1\nPRINT 1", "Missing END SELECT")]
        public void MistakesAreReportedRatherThanIgnored(string program, string expected)
        {
            var error = RecordingBasicHost.Fails(program);

            Assert.Contains(expected, error.Reason, StringComparison.Ordinal);
        }

        [Fact]
        public void AnErrorNamesTheLineItHappenedOn()
        {
            // The line is what the user is looking at in the editor, and an interpreter that loses track of it can
            // only say that something went wrong somewhere.
            var error = RecordingBasicHost.Fails("PRINT 1\nPRINT 2\nPRINT 1 / 0");

            Assert.Equal(3, error.Line);
            Assert.Contains("Line 3", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AProgramThatNeverStopsIsCaughtRatherThanHangingTheSuite()
        {
            var error = Assert.Throws<BasicError>(() =>
                BasicProgram.Compile("Again:\nGOTO Again")
                    .Run(new BasicRuntime(new RecordingBasicHost(), 1), 5000));

            Assert.Contains("too long", error.Reason, StringComparison.Ordinal);
        }

        [Fact]
        public void SteppingRunsABoundedNumberOfStatementsAndComesBack()
        {
            // What lets a screen stay alive while a program loops forever inside it, which is what a game does
            // between frames and what Run deliberately cannot do.
            var program = BasicProgram.Compile("Again:\nGOTO Again");
            var runtime = new BasicRuntime(new RecordingBasicHost(), 1);

            var index = program.Step(runtime, 0, 10);

            Assert.True(program.IsRunning(index));
        }
    }
}
