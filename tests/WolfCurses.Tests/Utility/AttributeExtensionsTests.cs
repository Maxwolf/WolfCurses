using System.Collections.Generic;
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

        [Fact]
        public void GetTypesWith_ExplicitAssembly_ScansThatAssemblyRegardlessOfEntry()
        {
            // The single-assembly overload is the seam that fixes the entry-assembly-only limitation: it finds the
            // forms in whatever assembly it is handed, not just Assembly.GetEntryAssembly().
            var types = AttributeExtensions
                .GetTypesWith<ParentWindowAttribute>(typeof(TestForm).Assembly, false)
                .ToList();

            Assert.Contains(typeof(TestForm), types);
            Assert.Contains(typeof(SecondTestForm), types);
            Assert.DoesNotContain(typeof(OrphanForm), types);
        }

        [Fact]
        public void GetTypesWith_ExplicitAssembly_WithoutMatchingTypes_ReturnsEmpty()
        {
            // Proves it honors the assembly it is given rather than silently falling back to the entry assembly: the
            // core runtime assembly defines no [ParentWindow] forms even though the entry (test) assembly does. (The
            // WolfCurses library assembly is no longer a valid "empty" target — it now ships the file dialog's form.)
            var types = AttributeExtensions
                .GetTypesWith<ParentWindowAttribute>(typeof(object).Assembly, false)
                .ToList();

            Assert.Empty(types);
        }

        [Fact]
        public void GetTypesWith_NullAssembly_ReturnsEmpty()
        {
            var types = AttributeExtensions.GetTypesWith<ParentWindowAttribute>((Assembly) null, false);

            Assert.Empty(types);
        }

        [Fact]
        public void GetTypesWith_AssemblyCollection_UnionsAcrossDistinctAssemblies()
        {
            // The library assembly contributes its built-in file dialog form and the test assembly its test forms;
            // unioning them must not drop or duplicate anything, so the test assembly's TestForm still appears once.
            var assemblies = new[] { typeof(SimulationApp).Assembly, typeof(TestForm).Assembly };

            var types = AttributeExtensions.GetTypesWith<ParentWindowAttribute>(assemblies, false).ToList();

            Assert.Contains(typeof(TestForm), types);
            Assert.Single(types, t => t == typeof(TestForm));
        }

        [Fact]
        public void GetTypesWith_AssemblyCollection_DedupesRepeatedAssembly()
        {
            // The common host case is entry assembly == app assembly. Passing the same assembly twice must still
            // yield each type once, or FormFactory's dictionary Add would throw on the duplicate key.
            var assemblies = new[] { typeof(TestForm).Assembly, typeof(TestForm).Assembly };

            var types = AttributeExtensions.GetTypesWith<ParentWindowAttribute>(assemblies, false).ToList();

            Assert.Single(types, t => t == typeof(TestForm));
            Assert.Single(types, t => t == typeof(DoubleRegisteredForm));
        }

        [Fact]
        public void GetTypesWith_AssemblyCollection_SkipsNullEntries()
        {
            var assemblies = new[] { null, typeof(TestForm).Assembly };

            var types = AttributeExtensions.GetTypesWith<ParentWindowAttribute>(assemblies, false).ToList();

            Assert.Contains(typeof(TestForm), types);
        }

        [Fact]
        public void GetTypesWith_NullAssemblyCollection_ReturnsEmpty()
        {
            var types = AttributeExtensions.GetTypesWith<ParentWindowAttribute>((IEnumerable<Assembly>) null, false);

            Assert.Empty(types);
        }
    }
}
