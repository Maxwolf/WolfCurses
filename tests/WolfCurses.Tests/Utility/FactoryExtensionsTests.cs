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
        public void New_TypeWithoutParameterlessCtor_CreatesUninitializedInstance()
        {
            // The fallback uses RuntimeHelpers.GetUninitializedObject, so a Window whose TData lacks a
            // parameterless constructor still gets a (constructor-skipped) UserData instance instead of null.
            var created = FactoryExtensions.New<WithoutDefaultCtor>.Instance();

            Assert.NotNull(created);
        }
    }
}
