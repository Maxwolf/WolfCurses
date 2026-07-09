using WolfCurses.Utility;
using Xunit;

namespace WolfCurses.Tests.Utility
{
    public class FactoryExtensionsTests
    {
        private sealed class WithDefaultCtor
        {
            public bool CtorRan { get; } = true;
        }

        private sealed class WithoutDefaultCtor
        {
            public WithoutDefaultCtor(int ignored)
            {
                _ = ignored;
            }
        }

        [Fact]
        public void NewString_Instance_ReturnsEmptyString()
        {
            Assert.Equal(string.Empty, FactoryExtensions.New<string>.Instance());
        }

        [Fact]
        public void New_TypeWithDefaultCtor_RunsConstructor()
        {
            var created = FactoryExtensions.New<WithDefaultCtor>.Instance();

            Assert.NotNull(created);
            Assert.True(created.CtorRan);
        }

        [Fact]
        public void New_ValueType_ReturnsDefault()
        {
            Assert.Equal(0, FactoryExtensions.New<int>.Instance());
        }

        [Fact]
        public void New_TypeWithoutParameterlessCtor_SilentlyReturnsNull()
        {
            // Documents current behavior on .NET 10: the fallback reflects for
            // System.Runtime.Serialization.FormatterServices inside CoreLib, does not find it, and the factory
            // degrades to a delegate that returns null - no exception is ever thrown. Any Window whose TData
            // lacks a parameterless constructor would get a null UserData from this same path.
            var created = FactoryExtensions.New<WithoutDefaultCtor>.Instance();

            Assert.Null(created);
        }
    }
}
