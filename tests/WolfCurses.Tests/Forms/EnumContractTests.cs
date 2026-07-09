using WolfCurses.Tests.TestDoubles;
using WolfCurses.Window.Form;
using WolfCurses.Window.Form.Input;
using Xunit;

namespace WolfCurses.Tests.Forms
{
    /// <summary>
    ///     Pins the numeric values of the public enums; consumers persist and compare these, so silent renumbering
    ///     would be a breaking change. The gap at 2 in DialogResponse is long-shipped, hence pinned.
    /// </summary>
    public class EnumContractTests
    {
        [Fact]
        public void DialogResponse_Values()
        {
            Assert.Equal(0, (int) DialogResponse.No);
            Assert.Equal(1, (int) DialogResponse.Yes);
            Assert.Equal(3, (int) DialogResponse.Custom);
        }

        [Fact]
        public void DialogType_Values()
        {
            Assert.Equal(1, (int) DialogType.Prompt);
            Assert.Equal(2, (int) DialogType.YesNo);
            Assert.Equal(3, (int) DialogType.Custom);
        }

        [Fact]
        public void ParentWindowAttribute_ExposesParentWindowType()
        {
            var attribute = new ParentWindowAttribute(typeof(TestWindow));

            Assert.Equal(typeof(TestWindow), attribute.ParentWindow);
        }
    }
}
