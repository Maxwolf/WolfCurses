using System;
using WolfCurses.Apps.Basic;
using Xunit;

namespace WolfCurses.Apps.Tests
{
    /// <summary>
    ///     How INPUT stops a program that is being run in slices. Pure: a screen and an interpreter, no application
    ///     around either of them.
    /// </summary>
    public class BasicInputTests
    {
        [Fact]
        public void AScreenWithNothingTypedAsksToBeComeBackToRatherThanBlocking()
        {
            // Blocking here would freeze the very screen the keystrokes have to arrive through, which is the whole
            // reason this is a signal and not a wait.
            var screen = new BasicScreen(40, 10);

            var request = Assert.Throws<BasicInputRequest>(() => screen.ReadLine("Name? "));

            Assert.Equal("Name? ", request.Prompt, StringComparer.Ordinal);
        }

        [Fact]
        public void AskingDoesNotPrintThePromptBecauseItIsAskedTwice()
        {
            // One INPUT calls ReadLine twice: once to signal, once to be answered. A host that wrote the prompt
            // would ask the question twice on screen, which is the bug this arrangement is shaped around.
            var screen = new BasicScreen(40, 10);

            Assert.Throws<BasicInputRequest>(() => screen.ReadLine("Name? "));
            screen.SupplyAnswer("Wolf");
            screen.ReadLine("Name? ");

            Assert.DoesNotContain("Name?", screen.Render(), StringComparison.Ordinal);
        }

        [Fact]
        public void OnceAnAnswerIsSuppliedTheSameCallHandsItBack()
        {
            var screen = new BasicScreen(40, 10);
            screen.SupplyAnswer("Wolf");

            Assert.Equal("Wolf", screen.ReadLine("Name? "), StringComparer.Ordinal);

            // And only once: the next INPUT has to ask again rather than being given a stale answer.
            Assert.Throws<BasicInputRequest>(() => screen.ReadLine("Again? "));
        }

        [Fact]
        public void TheWaitingStatementSaysWhereToCarryOnFrom()
        {
            // The loop that was running the program does not survive the throw, so the statement itself has to
            // carry its own position out. Without it there is no way back to the INPUT that asked.
            var screen = new BasicScreen(40, 10);
            var program = BasicProgram.Compile("PRINT \"X\"\nINPUT A$");

            var request = Assert.Throws<BasicInputRequest>(() => program.Run(new BasicRuntime(screen)));

            Assert.True(request.ResumeAt > 0,
                "the INPUT statement did not say where it was, so nothing could resume it");
        }

        [Fact]
        public void RunningTheStatementAgainCompletesItWithTheAnswer()
        {
            // The whole trick: coming back means running the statement from the top, which is safe only because
            // asking is the first thing it does.
            var screen = new BasicScreen(40, 10);
            var runtime = new BasicRuntime(screen);
            var program = BasicProgram.Compile("INPUT A$\nPRINT \"HELLO \" + A$");

            var request = Assert.Throws<BasicInputRequest>(() => program.Run(runtime));

            screen.SupplyAnswer("WOLF");
            var index = program.Step(runtime, request.ResumeAt, 100);

            Assert.False(program.IsRunning(index));
            Assert.Contains("HELLO WOLF", screen.Render(), StringComparison.Ordinal);
        }

        [Fact]
        public void BackspaceRubsOutTheCharacterBeforeTheCursor()
        {
            var screen = new BasicScreen(40, 10);

            screen.Write("ABC");
            screen.Backspace();

            var rendered = screen.Render();

            Assert.Contains("AB", rendered, StringComparison.Ordinal);
            Assert.DoesNotContain("ABC", rendered, StringComparison.Ordinal);
        }

        [Fact]
        public void BackspaceAtTheStartOfARowDoesNothingRatherThanWrappingBackwards()
        {
            var screen = new BasicScreen(40, 10);

            screen.Backspace();
            screen.Write("A");

            Assert.Contains("A", screen.Render(), StringComparison.Ordinal);
        }
    }
}
