using WolfCurses.Core;
using WolfCurses.Tests.TestDoubles;
using WolfCurses.Window.Form.Input;
using Xunit;

namespace WolfCurses.Tests.Forms
{
    /// <summary>
    ///     Dialog forms are constructed directly (bypassing the factory); OnFormPostCreate must be called manually
    ///     because that is where the prompt buffer gets built.
    /// </summary>
    public class InputFormTests
    {
        private static TestWindow NewWindow()
        {
            return new TestWindow(new TestSimulationApp());
        }

        [Theory]
        [InlineData("y")]
        [InlineData("Y")]
        [InlineData("yes")]
        [InlineData("YES")]
        [InlineData("yEs")]
        [InlineData("true")]
        [InlineData("TRUE")]
        public void OnInputBufferReturned_YesVariants_RespondYes(string input)
        {
            var form = new YesNoDialogForm(NewWindow());

            form.OnInputBufferReturned(input);

            Assert.Equal(DialogResponse.Yes, form.LastResponse);
        }

        [Theory]
        [InlineData("n")]
        [InlineData("N")]
        [InlineData("no")]
        [InlineData("NO")]
        [InlineData("false")]
        [InlineData("FALSE")]
        public void OnInputBufferReturned_NoVariants_RespondNo(string input)
        {
            var form = new YesNoDialogForm(NewWindow());

            form.OnInputBufferReturned(input);

            Assert.Equal(DialogResponse.No, form.LastResponse);
        }

        [Theory]
        [InlineData("maybe")]
        [InlineData("")]
        [InlineData("1")]
        public void OnInputBufferReturned_AnythingElse_RespondsCustom(string input)
        {
            var form = new YesNoDialogForm(NewWindow());

            form.OnInputBufferReturned(input);

            Assert.Equal(DialogResponse.Custom, form.LastResponse);
        }

        [Fact]
        public void OnInputBufferReturned_SecondCall_IsIgnored()
        {
            var form = new YesNoDialogForm(NewWindow());

            form.OnInputBufferReturned("yes");
            form.OnInputBufferReturned("no");

            Assert.Equal(1, form.ResponseCount);
            Assert.Equal(DialogResponse.Yes, form.LastResponse);
        }

        [Fact]
        public void OnRenderForm_PromptType_AppendsPressEnterSuffix()
        {
            var form = new PromptDialogForm(NewWindow());
            form.OnFormPostCreate();

            Assert.Equal(RecordingDialogForm.PROMPT_TEXT + InputManager.PRESSENTER, form.OnRenderForm());
        }

        [Fact]
        public void OnRenderForm_YesNoType_NoSuffix()
        {
            var form = new YesNoDialogForm(NewWindow());
            form.OnFormPostCreate();

            Assert.Equal(RecordingDialogForm.PROMPT_TEXT, form.OnRenderForm());
        }

        [Fact]
        public void OnRenderForm_CustomType_NoSuffix()
        {
            var form = new CustomDialogForm(NewWindow());
            form.OnFormPostCreate();

            Assert.Equal(RecordingDialogForm.PROMPT_TEXT, form.OnRenderForm());
        }

        [Fact]
        public void InputFillsBuffer_FollowsDialogType()
        {
            var window = NewWindow();

            Assert.False(new PromptDialogForm(window).InputFillsBuffer);
            Assert.True(new YesNoDialogForm(window).InputFillsBuffer);
            Assert.True(new CustomDialogForm(window).InputFillsBuffer);
        }
    }
}
