using WolfCurses.Controls;
using WolfCurses.Graphics;
using WolfCurses.Tests.TestDoubles;
using WolfCurses.Window.Control;
using WolfCurses.Window.Form;
using WolfCurses.Window.Form.Input;
using Xunit;

namespace WolfCurses.Tests.Forms
{
    /// <summary>
    ///     Pins the numeric values of the public enums; consumers persist and compare these, so silent renumbering
    ///     (including reordering the members of an implicit-valued enum) would be a breaking change. Renaming the
    ///     enum types themselves is fine (they were all given the *Enum suffix in the 2026-07 naming-convention
    ///     pass) — the serialized surface is the numeric values, which must not move. The gap at 2 in
    ///     DialogResponseEnum is long-shipped, hence pinned.
    /// </summary>
    public class EnumContractTests
    {
        [Fact]
        public void DialogResponse_Values()
        {
            Assert.Equal(0, (int) DialogResponseEnum.No);
            Assert.Equal(1, (int) DialogResponseEnum.Yes);
            Assert.Equal(3, (int) DialogResponseEnum.Custom);
        }

        [Fact]
        public void DialogType_Values()
        {
            Assert.Equal(1, (int) DialogTypeEnum.Prompt);
            Assert.Equal(2, (int) DialogTypeEnum.YesNo);
            Assert.Equal(3, (int) DialogTypeEnum.Custom);
        }

        [Fact]
        public void FileDialogMode_Values()
        {
            Assert.Equal(0, (int) FileDialogModeEnum.OpenFile);
            Assert.Equal(1, (int) FileDialogModeEnum.SelectFolder);
        }

        [Fact]
        public void FileDialogEntryKind_Values()
        {
            Assert.Equal(0, (int) FileDialogEntryKindEnum.ParentDirectory);
            Assert.Equal(1, (int) FileDialogEntryKindEnum.Drive);
            Assert.Equal(2, (int) FileDialogEntryKindEnum.Directory);
            Assert.Equal(3, (int) FileDialogEntryKindEnum.File);
        }

        [Fact]
        public void AnsiImageFit_Values()
        {
            Assert.Equal(0, (int) AnsiImageFitEnum.Contain);
            Assert.Equal(1, (int) AnsiImageFitEnum.Cover);
            Assert.Equal(2, (int) AnsiImageFitEnum.Stretch);
            Assert.Equal(3, (int) AnsiImageFitEnum.ScaleDown);
        }

        [Fact]
        public void AnsiColorMode_Values()
        {
            Assert.Equal(0, (int) AnsiColorModeEnum.Auto);
            Assert.Equal(1, (int) AnsiColorModeEnum.TrueColor);
            Assert.Equal(2, (int) AnsiColorModeEnum.Palette256);
            Assert.Equal(3, (int) AnsiColorModeEnum.Grayscale);
            Assert.Equal(4, (int) AnsiColorModeEnum.None);
        }

        [Fact]
        public void AnsiAlignment_Values()
        {
            Assert.Equal(0, (int) AnsiHorizontalAlignmentEnum.Left);
            Assert.Equal(1, (int) AnsiHorizontalAlignmentEnum.Center);
            Assert.Equal(2, (int) AnsiHorizontalAlignmentEnum.Right);
            Assert.Equal(0, (int) AnsiVerticalAlignmentEnum.Top);
            Assert.Equal(1, (int) AnsiVerticalAlignmentEnum.Middle);
            Assert.Equal(2, (int) AnsiVerticalAlignmentEnum.Bottom);
        }

        [Fact]
        public void MessageBoxResult_Values()
        {
            Assert.Equal(0, (int) MessageBoxResultEnum.Ok);
            Assert.Equal(1, (int) MessageBoxResultEnum.Yes);
            Assert.Equal(2, (int) MessageBoxResultEnum.No);
            Assert.Equal(3, (int) MessageBoxResultEnum.Cancel);
        }

        [Fact]
        public void MessageBoxButtons_Values()
        {
            Assert.Equal(0, (int) MessageBoxButtonsEnum.Ok);
            Assert.Equal(1, (int) MessageBoxButtonsEnum.YesNo);
            Assert.Equal(2, (int) MessageBoxButtonsEnum.YesNoCancel);
        }

        [Fact]
        public void Box_Values()
        {
            Assert.Equal(0, (int) BoxBorderEnum.Single);
            Assert.Equal(1, (int) BoxBorderEnum.Double);
            Assert.Equal(2, (int) BoxBorderEnum.Rounded);
            Assert.Equal(3, (int) BoxBorderEnum.Ascii);
            Assert.Equal(4, (int) BoxBorderEnum.None);
            Assert.Equal(0, (int) BoxAlignmentEnum.Left);
            Assert.Equal(1, (int) BoxAlignmentEnum.Center);
            Assert.Equal(2, (int) BoxAlignmentEnum.Right);
        }

        [Fact]
        public void CommandEnums_NonePinnedToZero()
        {
            Assert.Equal(0, (int) FileDialogCommandsEnum.None);
            Assert.Equal(0, (int) MessageBoxCommandsEnum.None);
            Assert.Equal(0, (int) SelectListCommandsEnum.None);
            Assert.Equal(0, (int) TextInputCommandsEnum.None);
        }

        [Fact]
        public void ParentWindowAttribute_ExposesParentWindowType()
        {
            var attribute = new ParentWindowAttribute(typeof(TestWindow));

            Assert.Equal(typeof(TestWindow), attribute.ParentWindow);
        }
    }
}
