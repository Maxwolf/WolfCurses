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
        public void SendCommand_BeforeOnRenderWindow_DoesNothing()
        {
            // Documents surprising current behavior: command mappings are built only inside OnRenderWindow, so
            // input sent before the first render is silently dropped.
            var window = NewWindow(out _);
            window.AddTestCommand(TestCommands.First);

            window.SendCommand("1");

            Assert.Empty(window.InvokedCommands);
        }

        [Fact]
        public void SendCommand_AfterRender_InvokesActionByEnumIntegerKey()
        {
            var window = NewWindow(out _);
            window.AddTestCommand(TestCommands.First);
            window.AddTestCommand(TestCommands.Second);
            window.OnRenderWindow();

            window.SendCommand("2");

            Assert.Equal(new[] { TestCommands.Second }, window.InvokedCommands);
        }

        [Theory]
        [InlineData("99")]
        [InlineData("garbage")]
        [InlineData("")]
        [InlineData("   ")]
        public void SendCommand_UnknownOrBlankInput_DoesNotInvokeOrThrow(string input)
        {
            var window = NewWindow(out _);
            window.AddTestCommand(TestCommands.First);
            window.OnRenderWindow();

            window.SendCommand(input);

            Assert.Empty(window.InvokedCommands);
        }

        [Fact]
        public void OnRenderWindow_ListsCommandDescriptions()
        {
            var window = NewWindow(out _);
            window.AddTestCommand(TestCommands.First);
            window.AddTestCommand(TestCommands.Second);

            var rendered = window.OnRenderWindow();

            // Descriptions come from ToDescriptionAttribute: enum name fallback and attribute text.
            Assert.Contains("1. First", rendered);
            Assert.Contains("2. Second command", rendered);
        }

        [Fact]
        public void OnRenderWindow_DuplicateAddCommand_ThrowsArgumentException()
        {
            // Documents a known bug: MenuChoice does not override Equals, so AddCommand's Contains dedup never
            // matches and the second registration survives until RefreshCommandMappings hits a duplicate key.
            var window = NewWindow(out _);
            window.AddTestCommand(TestCommands.First, () => { });
            window.AddTestCommand(TestCommands.First, () => { });

            Assert.Throws<ArgumentException>(() => window.OnRenderWindow());
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
            window.AddTestCommand(TestCommands.First);
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
            window.AddTestCommand(TestCommands.First);
            window.OnRenderWindow();
            window.SetForm(typeof(TestForm));

            window.ClearForm();
            Assert.Null(window.CurrentForm);

            window.OnRenderWindow();
            window.SendCommand("1");

            Assert.Equal(new[] { TestCommands.First }, window.InvokedCommands);
        }

        [Fact]
        public void OnRenderWindow_WithFormAttached_RendersFormInsteadOfMenu()
        {
            var window = NewWindow(out _);
            window.AddTestCommand(TestCommands.First);
            window.SetForm(typeof(TestForm));

            var rendered = window.OnRenderWindow();

            Assert.Contains(TestForm.RENDER_TEXT, rendered);
            Assert.DoesNotContain("1. First", rendered);
        }

        [Fact]
        public void Equals_TwoWindowsWithNullForms_ThrowsNullReferenceException()
        {
            // Documents a known bug: instance Equals dereferences Form unconditionally.
            var app = new TestSimulationApp();
            var first = new TestWindow(app);
            var second = new TestWindow(app);

            Assert.Throws<NullReferenceException>(() => first.Equals(second));
        }

        [Fact]
        public void CompareTo_WindowsWithNullForms_ThrowsNullReferenceException()
        {
            // Documents a known bug: CompareTo dereferences CurrentForm when type names match.
            var app = new TestSimulationApp();
            var first = new TestWindow(app);
            var second = new TestWindow(app);

            Assert.Throws<NullReferenceException>(() => first.CompareTo((WolfCurses.Window.IWindow) second));
        }

        [Fact]
        public void ToString_ReturnsTypeName()
        {
            Assert.Equal(nameof(TestWindow), NewWindow(out _).ToString());
        }
    }
}
