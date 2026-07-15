using System;
using WolfCurses.Tests.TestDoubles;
using Xunit;

namespace WolfCurses.Tests.Windows
{
    public class WindowTests
    {
        private static TestWindow NewWindow(out TestSimulationApp app)
        {
            app = new TestSimulationApp();
            return new TestWindow(app);
        }

        [Fact]
        public void Ctor_NonEnumCommands_ThrowsInvalidCastException()
        {
            // The generic constraint cannot express "enum"; the constructor enforces it at runtime.
            var app = new TestSimulationApp();

            Assert.Throws<InvalidCastException>(() => new BadCommandsWindow(app));
        }

        [Fact]
        public void Ctor_CreatesUserDataViaFactory()
        {
            var window = NewWindow(out _);

            Assert.NotNull(((WolfCurses.Window.IWindow) window).UserData);
            Assert.IsType<TestWindowData>(((WolfCurses.Window.IWindow) window).UserData);
        }

        [Fact]
        public void SendCommand_BeforeFirstRender_InvokesAction()
        {
            // Mappings are refreshed eagerly by AddCommand, so commands work before the window ever renders.
            var window = NewWindow(out _);
            window.AddTestCommand(TestCommandsEnum.First);

            window.SendCommand("1");

            Assert.Equal(new[] { TestCommandsEnum.First }, window.InvokedCommands);
        }

        [Fact]
        public void SendCommand_AfterRender_InvokesActionByEnumIntegerKey()
        {
            var window = NewWindow(out _);
            window.AddTestCommand(TestCommandsEnum.First);
            window.AddTestCommand(TestCommandsEnum.Second);
            window.OnRenderWindow();

            window.SendCommand("2");

            Assert.Equal(new[] { TestCommandsEnum.Second }, window.InvokedCommands);
        }

        [Theory]
        [InlineData("99")]
        [InlineData("garbage")]
        [InlineData("")]
        [InlineData("   ")]
        public void SendCommand_UnknownOrBlankInput_DoesNotInvokeOrThrow(string input)
        {
            var window = NewWindow(out _);
            window.AddTestCommand(TestCommandsEnum.First);
            window.OnRenderWindow();

            window.SendCommand(input);

            Assert.Empty(window.InvokedCommands);
        }

        [Fact]
        public void OnRenderWindow_ListsCommandDescriptions()
        {
            var window = NewWindow(out _);
            window.AddTestCommand(TestCommandsEnum.First);
            window.AddTestCommand(TestCommandsEnum.Second);

            var rendered = window.OnRenderWindow();

            // Descriptions come from ToDescriptionAttribute: enum name fallback and attribute text.
            Assert.Contains("1. First", rendered);
            Assert.Contains("2. Second command", rendered);
        }

        [Fact]
        public void AddCommand_DuplicateCommand_KeepsFirstRegistration()
        {
            // MenuChoice equality is based on the command value, so a duplicate AddCommand is ignored instead of
            // crashing the next render with a duplicate mapping key.
            var window = NewWindow(out _);
            var firstInvocations = 0;
            var secondInvocations = 0;
            window.AddTestCommand(TestCommandsEnum.First, () => firstInvocations++);
            window.AddTestCommand(TestCommandsEnum.First, () => secondInvocations++);

            var rendered = window.OnRenderWindow();
            window.SendCommand("1");

            var menuLines = rendered.Split(Environment.NewLine);
            Assert.Single(menuLines, line => line.StartsWith("  1."));
            Assert.Equal(1, firstInvocations);
            Assert.Equal(0, secondInvocations);
        }

        [Fact]
        public void RemoveWindowNextTick_SetsShouldRemoveMode_AndStopsAcceptingInput()
        {
            var window = NewWindow(out _);

            Assert.False(window.ShouldRemoveMode);
            Assert.True(window.AcceptsInput);

            window.RemoveWindowNextTick();

            Assert.True(window.ShouldRemoveMode);
            Assert.False(window.AcceptsInput);
            Assert.Null(window.CurrentForm);
        }

        [Fact]
        public void SetForm_AttachesForm_AndSendCommandRoutesToIt()
        {
            var window = NewWindow(out _);
            window.AddTestCommand(TestCommandsEnum.First);
            window.OnRenderWindow();

            window.SetForm(typeof(TestForm));
            var form = Assert.IsType<TestForm>(window.CurrentForm);

            window.SendCommand("hello");

            Assert.Equal(new[] { "hello" }, form.ReceivedInputs);
            Assert.Empty(window.InvokedCommands);
        }

        [Fact]
        public void ClearForm_DetachesForm_AndMenuRoutingResumes()
        {
            var window = NewWindow(out _);
            window.AddTestCommand(TestCommandsEnum.First);
            window.OnRenderWindow();
            window.SetForm(typeof(TestForm));

            window.ClearForm();
            Assert.Null(window.CurrentForm);

            window.OnRenderWindow();
            window.SendCommand("1");

            Assert.Equal(new[] { TestCommandsEnum.First }, window.InvokedCommands);
        }

        [Fact]
        public void OnRenderWindow_WithFormAttached_RendersFormInsteadOfMenu()
        {
            var window = NewWindow(out _);
            window.AddTestCommand(TestCommandsEnum.First);
            window.SetForm(typeof(TestForm));

            var rendered = window.OnRenderWindow();

            Assert.Contains(TestForm.RENDER_TEXT, rendered);
            Assert.DoesNotContain("1. First", rendered);
        }

        [Fact]
        public void Equals_TwoWindowsWithNullForms_AreEqual()
        {
            // Same window type with no forms attached on either side counts as equal; no null dereference.
            var app = new TestSimulationApp();
            var first = new TestWindow(app);
            var second = new TestWindow(app);

            Assert.True(first.Equals(second));
        }

        [Fact]
        public void CompareTo_WindowsWithNullForms_AreEqual()
        {
            // Same type name and both forms missing compares as equal; no null dereference.
            var app = new TestSimulationApp();
            var first = new TestWindow(app);
            var second = new TestWindow(app);

            Assert.Equal(0, first.CompareTo((WolfCurses.Window.IWindow) second));
        }

        [Fact]
        public void CompareTo_SortsByTypeNameAscending()
        {
            // Ascending contract: "SecondTestWindow" < "TestWindow" ordinally, so SecondTestWindow compares
            // less than TestWindow (previous code compared the arguments in reverse and inverted the sign).
            var app = new TestSimulationApp();
            var testWindow = new TestWindow(app);
            var secondWindow = new SecondTestWindow(app);

            Assert.True(secondWindow.CompareTo((WolfCurses.Window.IWindow) testWindow) < 0);
            Assert.True(testWindow.CompareTo((WolfCurses.Window.IWindow) secondWindow) > 0);
        }

        [Fact]
        public void ToString_ReturnsTypeName()
        {
            Assert.Equal(nameof(TestWindow), NewWindow(out _).ToString());
        }
    }
}
