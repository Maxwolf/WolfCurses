using System;
using System.Diagnostics;
using System.Threading;
using WolfCurses.Apps.Tests.Support;
using Xunit;

namespace WolfCurses.Apps.Tests
{
    /// <summary>
    ///     The BASIC environment as a person meets it: pick it off the suite menu, press F5, watch a program run.
    ///     <para>
    ///         These need real elapsed time and cannot be tuned away. A running program is paced by an
    ///         <c>IntervalTimer</c> off the system tick, exactly so that a program which loops forever cannot take
    ///         the screen with it, which means spinning ticks with no clock between them runs nothing at all.
    ///     </para>
    /// </summary>
    [Collection("AppsApp")]
    public class BasicDialogTests
    {
        private static DrivenAppsApp OpenBasic()
        {
            var suite = new DrivenAppsApp();
            suite.ChooseMenuItem((int) AppsCommandsEnum.Basic);

            return suite;
        }

        /// <summary>Ticks with a real clock running until the screen says what is expected, or gives up.</summary>
        private static bool WaitFor(DrivenAppsApp suite, string expected)
        {
            var clock = Stopwatch.StartNew();

            while (clock.ElapsedMilliseconds < 4000)
            {
                if (suite.Screen.Contains(expected, StringComparison.Ordinal))
                    return true;

                suite.Tick();
                Thread.Sleep(5);
            }

            return suite.Screen.Contains(expected, StringComparison.Ordinal);
        }

        [Fact]
        public void ItIsOnTheSuiteMenuAndOpensOnAProgram()
        {
            using var suite = OpenBasic();

            Assert.Contains("welcome.bas", suite.Screen, StringComparison.Ordinal);
            Assert.Contains("F5=Run", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void FiveRunsTheProgramAndItsOutputAppears()
        {
            using var suite = OpenBasic();

            suite.Press(ConsoleKey.F5);

            // The last line the program prints, not the first. A screen that scrolls has thrown the early output
            // away by the time anything gets to look at it, which is what this test asserted the first time.
            Assert.True(WaitFor(suite, "Try editing this"),
                "the sample program produced no output:\n" + suite.Describe());
        }

        [Fact]
        public void AFinishedProgramSaysSoAndEscapeGoesBackToTheListing()
        {
            using var suite = OpenBasic();
            suite.Press(ConsoleKey.F5);

            Assert.True(WaitFor(suite, "Program finished"), "the program never finished:\n" + suite.Describe());

            // ESC out of a running program means "stop looking at this", not "leave the application", which is the
            // whole reason the form takes ESC back from the window while a screen is up.
            suite.Escape();

            // Back on the listing, which is the title bar rather than the key hints: the status line carries the
            // last message instead of the hints whenever there is one, and "Stopped." is one.
            Assert.Contains("welcome.bas", suite.Screen, StringComparison.Ordinal);
            Assert.DoesNotContain("Which application?", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void EscapeFromTheListingLeavesTheApplication()
        {
            using var suite = OpenBasic();

            suite.Escape();

            Assert.Contains("Which application?", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void AProgramThatWillNotCompileNeverShowsAnOutputScreen()
        {
            // The mistake is in the listing, which is what you want to be looking at. Showing an empty output
            // screen and the error underneath it would put the wrong thing in front of you.
            using var suite = OpenBasic();

            suite.PressChar('I', ConsoleKey.NoName);
            suite.PressChar('F', ConsoleKey.NoName);
            suite.Press(ConsoleKey.Enter);
            suite.Press(ConsoleKey.F5);

            Assert.Contains("Line 1", suite.Screen, StringComparison.Ordinal);

            // Still the listing: the title bar is there and no output screen replaced it.
            Assert.Contains("welcome.bas", suite.Screen, StringComparison.Ordinal);
        }

        /// <summary>Types a line into whatever is collecting characters, a key at a time.</summary>
        private static void Type(DrivenAppsApp suite, string text)
        {
            foreach (var character in text)
                suite.PressChar(character, ConsoleKey.NoName);
        }

        /// <summary>Replaces the listing with a program of the test's own.</summary>
        private static void ReplaceProgram(DrivenAppsApp suite, params string[] lines)
        {
            suite.Press(ConsoleKey.A, ConsoleModifiers.Control);

            for (var i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                    suite.Press(ConsoleKey.Enter);

                Type(suite, lines[i]);
            }
        }

        [Fact]
        public void AProgramThatAsksAQuestionWaitsForTheAnswerAndThenCarriesOn()
        {
            // The whole INPUT arrangement end to end: the program stops, the keystrokes arrive through the same
            // screen it stopped on, and running the statement again completes it.
            using var suite = OpenBasic();

            ReplaceProgram(suite, "INPUT A$", "PRINT \"HELLO \" + A$");
            suite.Press(ConsoleKey.F5);

            Assert.True(WaitFor(suite, "Type an answer"), "the program never asked:\n" + suite.Describe());

            Type(suite, "WOLF");
            suite.Press(ConsoleKey.Enter);

            Assert.True(WaitFor(suite, "HELLO WOLF"), "the answer never reached the program:\n" + suite.Describe());
        }

        [Fact]
        public void AWaitingProgramEchoesWhatIsTypedAndBackspaceTakesItBack()
        {
            using var suite = OpenBasic();

            ReplaceProgram(suite, "INPUT A$", "PRINT \"[\" + A$ + \"]\"");
            suite.Press(ConsoleKey.F5);

            Assert.True(WaitFor(suite, "Type an answer"), "the program never asked:\n" + suite.Describe());

            Type(suite, "WOLX");
            suite.Press(ConsoleKey.Backspace);
            Type(suite, "F");
            suite.Press(ConsoleKey.Enter);

            Assert.True(WaitFor(suite, "[WOLF]"), "backspace did not take the character back:\n" + suite.Describe());
        }

        [Fact]
        public void EscapeLeavesAProgramThatIsWaitingForAnAnswer()
        {
            // A program stopped on INPUT is still a program somebody has to be able to get out of.
            using var suite = OpenBasic();

            ReplaceProgram(suite, "INPUT A$", "PRINT A$");
            suite.Press(ConsoleKey.F5);

            Assert.True(WaitFor(suite, "Type an answer"), "the program never asked:\n" + suite.Describe());

            suite.Escape();

            Assert.Contains("Stopped", suite.Screen, StringComparison.Ordinal);
            Assert.DoesNotContain("Which application?", suite.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void TypingEditsTheProgramRatherThanTheCommandPromptUnderneath()
        {
            using var suite = OpenBasic();

            suite.PressChar('X', ConsoleKey.NoName);

            // The asterisk is the modified marker on the title, and it is proof the character reached the document
            // rather than the input buffer the suite menu reads.
            Assert.Contains("welcome.bas *", suite.Screen, StringComparison.Ordinal);
        }
    }
}
