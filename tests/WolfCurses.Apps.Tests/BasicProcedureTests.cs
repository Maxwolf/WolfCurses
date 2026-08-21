using System;
using WolfCurses.Apps.Tests.Support;
using Xunit;

namespace WolfCurses.Apps.Tests
{
    /// <summary>
    ///     SUBs and FUNCTIONs. Most of these are about scope rather than about calling, because local-by-default is
    ///     the whole reason procedures are worth having: a SUB that borrows the caller's loop counter is the bug
    ///     they exist to prevent.
    /// </summary>
    public class BasicProcedureTests
    {
        private static string Run(string source)
        {
            return RecordingBasicHost.Printed(source);
        }

        [Fact]
        public void AProcedureBodyIsJumpedOverRatherThanFallenInto()
        {
            // The bodies live in the same statement list as everything else, so without a jump the program would
            // simply run down into its own procedures on the way past.
            const string program = "PRINT \"BEFORE\"\nSUB Quiet\nPRINT \"INSIDE\"\nEND SUB\nPRINT \"AFTER\"";

            Assert.Equal("BEFORE\nAFTER", Run(program), StringComparer.Ordinal);
        }

        [Fact]
        public void ASubCanBeCalledWithOrWithoutTheWordCall()
        {
            const string program = "CALL Greet\nGreet\nEND\nSUB Greet\nPRINT \"HI\"\nEND SUB";

            Assert.Equal("HI\nHI", Run(program), StringComparer.Ordinal);
        }

        [Fact]
        public void ArgumentsArriveUnderTheParameterNames()
        {
            const string program = "Add 2, 3\nEND\nSUB Add (A, B)\nPRINT A + B\nEND SUB";

            Assert.Equal(" 5 ", Run(program), StringComparer.Ordinal);
        }

        [Fact]
        public void ArgumentsAreWorkedOutInTheCallersScopeBeforeTheProcedureExists()
        {
            // The parameter here is called X and so is the caller's variable. If the arguments were evaluated after
            // the new scope was pushed, X would already be the uninitialised local and this would print zero.
            const string program = "X = 7\nShow X\nEND\nSUB Show (X)\nPRINT X\nEND SUB";

            Assert.Equal(" 7 ", Run(program), StringComparer.Ordinal);
        }

        [Fact]
        public void AProcedureVariableIsItsOwnAndDoesNotDisturbTheCaller()
        {
            // THE reason for all of this. Both use I, and the caller's must survive the call.
            const string program = "I = 99\nCount\nPRINT I\nEND\n" +
                                   "SUB Count\nFOR I = 1 TO 3\nNEXT I\nEND SUB";

            Assert.Equal(" 99 ", Run(program), StringComparer.Ordinal);
        }

        [Fact]
        public void SharedIsHowAProcedureReachesOutside()
        {
            const string program = "TOTAL = 1\nBump\nPRINT TOTAL\nEND\n" +
                                   "SUB Bump\nSHARED TOTAL\nTOTAL = TOTAL + 41\nEND SUB";

            Assert.Equal(" 42 ", Run(program), StringComparer.Ordinal);
        }

        [Fact]
        public void WithoutSharedTheOuterVariableIsNotEvenVisible()
        {
            // Not an error, just a different variable that happens to have the same name, which is what makes
            // forgetting SHARED produce a quiet zero rather than a complaint.
            const string program = "TOTAL = 10\nLook\nEND\nSUB Look\nPRINT TOTAL\nEND SUB";

            Assert.Equal(" 0 ", Run(program), StringComparer.Ordinal);
        }

        [Fact]
        public void AFunctionReturnsByAssigningToItsOwnName()
        {
            const string program = "PRINT Double(21)\nEND\nFUNCTION Double (N)\nDouble = N * 2\nEND FUNCTION";

            Assert.Equal(" 42 ", Run(program), StringComparer.Ordinal);
        }

        [Fact]
        public void AFunctionThatNeverAssignsToItselfReturnsNothingInParticular()
        {
            Assert.Equal(" 0 ", Run("PRINT Empty(1)\nEND\nFUNCTION Empty (N)\nEND FUNCTION"),
                StringComparer.Ordinal);

            Assert.Equal("|", Run("PRINT Blank$(1) + \"|\"\nEND\nFUNCTION Blank$ (N)\nEND FUNCTION"),
                StringComparer.Ordinal);
        }

        [Fact]
        public void AFunctionCanBeUsedAnywhereAValueCanBe()
        {
            const string program = "PRINT Twice(3) + Twice(4)\nEND\nFUNCTION Twice (N)\nTwice = N * 2\nEND FUNCTION";

            Assert.Equal(" 14 ", Run(program), StringComparer.Ordinal);
        }

        [Fact]
        public void AStringFunctionWorksTheSameWay()
        {
            const string program = "PRINT Shout$(\"hello\")\nEND\n" +
                                   "FUNCTION Shout$ (S$)\nShout$ = UCASE$(S$) + \"!\"\nEND FUNCTION";

            Assert.Equal("HELLO!", Run(program), StringComparer.Ordinal);
        }

        [Fact]
        public void AFunctionCanCallItself()
        {
            // Which works only because each call gets its own locals: a shared N would be overwritten by the
            // innermost call and the unwinding would multiply by the wrong numbers.
            const string program = "PRINT Fact(5)\nEND\n" +
                                   "FUNCTION Fact (N)\nIF N <= 1 THEN\nFact = 1\nELSE\nFact = N * Fact(N - 1)\n" +
                                   "END IF\nEND FUNCTION";

            Assert.Equal(" 120 ", Run(program), StringComparer.Ordinal);
        }

        [Fact]
        public void ASubCanCallAnotherSub()
        {
            const string program = "Outer\nEND\nSUB Outer\nPRINT \"A\";\nInner\nPRINT \"C\";\nEND SUB\n" +
                                   "SUB Inner\nPRINT \"B\";\nEND SUB";

            Assert.Equal("ABC", Run(program), StringComparer.Ordinal);
        }

        [Fact]
        public void ExitSubLeavesEarly()
        {
            const string program = "Stop2\nEND\nSUB Stop2\nPRINT \"IN\";\nEXIT SUB\nPRINT \"NEVER\";\nEND SUB";

            Assert.Equal("IN", Run(program), StringComparer.Ordinal);
        }

        [Fact]
        public void ExitSubFromInsideALoopDoesNotLeaveTheLoopBehind()
        {
            // The subtle one. Leaving a procedure has to put back whatever it had open, or the abandoned FOR frame
            // is still on the stack and the caller's own NEXT steps that one instead of its own.
            const string program = "Leaky\nFOR I = 1 TO 3\nPRINT I;\nNEXT I\nEND\n" +
                                   "SUB Leaky\nFOR J = 1 TO 10\nIF J = 2 THEN EXIT SUB\nNEXT J\nEND SUB";

            Assert.Equal(" 1  2  3 ", Run(program), StringComparer.Ordinal);
        }

        [Fact]
        public void AProcedureGetsItsOwnArraysUnlessTheyAreShared()
        {
            const string shared = "DIM A(2)\nA(0) = 5\nFill\nPRINT A(0)\nEND\n" +
                                  "SUB Fill\nSHARED A()\nA(0) = 9\nEND SUB";

            Assert.Equal(" 9 ", Run(shared), StringComparer.Ordinal);

            const string ownItsOwn = "DIM A(2)\nA(0) = 5\nFill\nPRINT A(0)\nEND\n" +
                                     "SUB Fill\nDIM A(2)\nA(0) = 9\nEND SUB";

            Assert.Equal(" 5 ", Run(ownItsOwn), StringComparer.Ordinal);
        }

        [Fact]
        public void DeclareLinesAreToleratedBecauseQBasicWritesThem()
        {
            // Nothing here needs them, since a call is resolved when it runs. Refusing them would make somebody's
            // own saved listing fail to load for no reason.
            const string program = "DECLARE SUB Greet (N$)\nGreet \"WOLF\"\nEND\n" +
                                   "SUB Greet (N$)\nPRINT \"HI \" + N$\nEND SUB";

            Assert.Equal("HI WOLF", Run(program), StringComparer.Ordinal);
        }

        [Theory]
        [InlineData("Missing\nEND", "Undefined subprogram MISSING")]
        [InlineData("SUB A\nSUB B\nEND SUB\nEND SUB", "cannot be declared inside another")]
        [InlineData("SUB A\nPRINT 1", "Missing END SUB")]
        [InlineData("FUNCTION F\nPRINT 1", "Missing END FUNCTION")]
        [InlineData("EXIT SUB", "only meaningful inside")]
        [InlineData("SHARED X", "only meaningful inside")]
        [InlineData("Show \"text\"\nEND\nSUB Show (N)\nPRINT N\nEND SUB", "Type mismatch in argument 1")]
        [InlineData("Show 1, 2\nEND\nSUB Show (N)\nPRINT N\nEND SUB", "Too many arguments")]
        [InlineData("SUB A\nEND SUB\nSUB A\nEND SUB", "There is already a SUB")]
        public void MistakesAreReportedRatherThanIgnored(string program, string expected)
        {
            var error = RecordingBasicHost.Fails(program);

            Assert.Contains(expected, error.Reason, StringComparison.Ordinal);
        }

        [Fact]
        public void AFunctionThatNeverReturnsIsCaughtRatherThanHangingTheScreen()
        {
            // A FUNCTION is the one thing that cannot be stepped: an expression has nowhere to suspend to, so the
            // body runs to completion inside the statement that used it. Hence the cap.
            var error = RecordingBasicHost.Fails("PRINT Forever(1)\nEND\n" +
                                                 "FUNCTION Forever (N)\nDO\nLOOP\nEND FUNCTION");

            Assert.Contains("ran for too long", error.Reason, StringComparison.Ordinal);
        }
    }
}
