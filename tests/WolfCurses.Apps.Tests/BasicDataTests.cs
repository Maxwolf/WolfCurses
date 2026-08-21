using System;
using WolfCurses.Apps.Tests.Support;
using Xunit;

namespace WolfCurses.Apps.Tests
{
    /// <summary>
    ///     DATA and READ, and the two ways of leaving a loop early. Grouped because both are about a program
    ///     stepping outside the shape it is written in.
    /// </summary>
    public class BasicDataTests
    {
        private static string Run(string source)
        {
            return RecordingBasicHost.Printed(source);
        }

        [Fact]
        public void ExitForLeavesTheLoopAtOnce()
        {
            Assert.Equal(" 1  2 ", Run("FOR I = 1 TO 9\nPRINT I;\nIF I = 2 THEN EXIT FOR\nNEXT I"),
                StringComparer.Ordinal);
        }

        [Fact]
        public void ExitForDoesNotLeaveTheLoopsFrameBehind()
        {
            // A FOR keeps its limit and step on a stack that NEXT normally takes off. Jumping past the NEXT without
            // popping would leave that frame for the next loop's NEXT to step instead of its own.
            const string program = "FOR I = 1 TO 9\nIF I = 2 THEN EXIT FOR\nNEXT I\n" +
                                   "FOR K = 1 TO 3\nPRINT K;\nNEXT K";

            Assert.Equal(" 1  2  3 ", Run(program), StringComparer.Ordinal);
        }

        [Fact]
        public void ExitForLeavesOnlyTheLoopItIsIn()
        {
            const string program = "FOR I = 1 TO 2\nFOR J = 1 TO 5\nIF J = 2 THEN EXIT FOR\nNEXT J\n" +
                                   "PRINT I;\nNEXT I";

            Assert.Equal(" 1  2 ", Run(program), StringComparer.Ordinal);
        }

        [Fact]
        public void ExitDoLeavesADoLoop()
        {
            Assert.Equal(" 1  2 ", Run("I = 1\nDO\nPRINT I;\nIF I = 2 THEN EXIT DO\nI = I + 1\nLOOP"),
                StringComparer.Ordinal);
        }

        [Fact]
        public void ReadTakesTheConstantsInTheOrderTheyWereWritten()
        {
            Assert.Equal(" 10  20  30 ", Run("READ A, B, C\nPRINT A; B; C\nEND\nDATA 10, 20, 30"),
                StringComparer.Ordinal);
        }

        [Fact]
        public void TheDataMayBeWrittenBelowTheReadThatUsesIt()
        {
            // Which is why it is gathered when the program is parsed rather than when it runs, and is how every
            // listing that uses DATA is written.
            Assert.Equal(" 7 ", Run("READ A\nPRINT A\nEND\nDATA 7"), StringComparer.Ordinal);
        }

        [Fact]
        public void DataHoldsStringsAndNumbersAndUnquotedWords()
        {
            Assert.Equal("WOLF 3 plain words", Run(
                    "READ A$, N, B$\nPRINT A$; N; B$\nEND\nDATA \"WOLF\", 3, plain words"),
                StringComparer.Ordinal);
        }

        [Fact]
        public void AnUnquotedNumberReadIntoAStringComesBackAsText()
        {
            // DATA has no types of its own, so what an item becomes is decided by the variable reading it.
            Assert.Equal("42|", Run("READ A$\nPRINT A$ + \"|\"\nEND\nDATA 42"), StringComparer.Ordinal);
            Assert.Equal(" 42 ", Run("READ N\nPRINT N\nEND\nDATA 42"), StringComparer.Ordinal);
        }

        [Fact]
        public void RestoreStartsTheDataAgain()
        {
            Assert.Equal(" 1  2  1 ", Run("READ A, B\nRESTORE\nREAD C\nPRINT A; B; C\nEND\nDATA 1, 2"),
                StringComparer.Ordinal);
        }

        [Fact]
        public void RestoreCanNameTheLineToGoBackTo()
        {
            // Which means "the DATA written from that line onward", not a jump: nothing about where the program is
            // running changes.
            const string program = "READ A\nRESTORE Second\nREAD B\nPRINT A; B\nEND\n" +
                                   "DATA 1\nSecond:\nDATA 99";

            Assert.Equal(" 1  99 ", Run(program), StringComparer.Ordinal);
        }

        [Fact]
        public void DataInsideALoopIsStillJustDataAndRunsNothing()
        {
            // DATA compiles to no statement at all, so a program does not trip over its own constants on the way
            // past them.
            Assert.Equal(" 1  2  3 ", Run("FOR I = 1 TO 3\nDATA 9\nPRINT I;\nNEXT I"), StringComparer.Ordinal);
        }

        [Theory]
        [InlineData("READ A\nEND", "Out of DATA")]
        [InlineData("READ A, B\nEND\nDATA 1", "Out of DATA")]
        [InlineData("RESTORE Nowhere\nEND\nDATA 1", "Cannot find line or label")]
        [InlineData("EXIT FOR", "only meaningful inside a FOR")]
        [InlineData("EXIT DO", "only meaningful inside a DO")]
        [InlineData("FOR I = 1 TO 2\nEXIT DO\nNEXT I", "only meaningful inside a DO")]
        public void MistakesAreReportedRatherThanIgnored(string program, string expected)
        {
            var error = RecordingBasicHost.Fails(program);

            Assert.Contains(expected, error.Reason, StringComparison.Ordinal);
        }
    }
}
