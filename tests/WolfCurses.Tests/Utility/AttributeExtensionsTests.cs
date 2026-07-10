using System.Linq;
using System.Reflection;
using WolfCurses.Tests.TestDoubles;
using WolfCurses.Utility;
using WolfCurses.Window.Form;
using Xunit;

namespace WolfCurses.Tests.Utility
{
    public class AttributeExtensionsTests
    {
        [Fact]
        public void ToDescriptionAttribute_WithDescription_ReturnsAttributeText()
        {
            Assert.Equal("Second command", TestCommands.Second.ToDescriptionAttribute());
        }

        [Fact]
        public void ToDescriptionAttribute_WithoutDescription_FallsBackToToString()
        {
            Assert.Equal("First", TestCommands.First.ToDescriptionAttribute());
        }

        [Fact]
        public void GetAttributes_ReturnsAttributeInstances()
        {
            var attributes = typeof(TestForm).GetTypeInfo()
                .GetAttributes<ParentWindowAttribute>(false)
                .ToList();

            var attribute = Assert.Single(attributes);
            Assert.Equal(typeof(TestWindow), attribute.ParentWindow);
        }

        [Fact]
        public void GetAttributes_TypeWithoutAttribute_ReturnsEmpty()
        {
            var attributes = typeof(OrphanForm).GetTypeInfo().GetAttributes<ParentWindowAttribute>(false);

            Assert.Empty(attributes);
        }

        [Fact]
        public void GetTypesWith_ParentWindow_FindsFormsDeclaredInThisAssembly()
        {
            // Second entry-assembly canary: GetTypesWith scans Assembly.GetEntryAssembly(), which under the
            // xunit.v3 runner is this test assembly, so form discovery works exactly as in a real host app.
            var types = AttributeExtensions.GetTypesWith<ParentWindowAttribute>(false).ToList();

            Assert.Contains(typeof(TestForm), types);
            Assert.Contains(typeof(SecondTestForm), types);
            Assert.Contains(typeof(YesNoDialogForm), types);
            Assert.DoesNotContain(typeof(OrphanForm), types);
        }

        [Fact]
        public void GetTypesWith_TypeWithStackedAttributes_YieldedExactlyOnce()
        {
            // A second [ParentWindow] used to add the type twice and crash FormFactory's dictionary for every
            // SimulationApp in the assembly; stacked attributes now register the type a single time.
            var types = AttributeExtensions.GetTypesWith<ParentWindowAttribute>(false).ToList();

            Assert.Single(types, t => t == typeof(DoubleRegisteredForm));
        }
    }
}
