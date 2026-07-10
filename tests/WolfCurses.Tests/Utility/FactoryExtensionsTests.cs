using System;
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

        private sealed class ConstructorThrows
        {
            public ConstructorThrows(int ignored)
            {
                throw new InvalidOperationException("constructor must be bypassed by GetUninitializedObject");
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

        [Fact]
        public void GetUninitializedObject_ReturnsNonNullInstanceOfExactType()
        {
            // Regression for the FormatterServices reflection hack that no longer resolves on modern .NET: it made
            // this path silently return null for every type, which crashed downstream object factories. It must now
            // return a real, correctly typed instance (RuntimeHelpers.GetUninitializedObject).
            var created = FactoryExtensions.New<WithoutDefaultCtor>.GetUninitializedObject(typeof(WithoutDefaultCtor));

            Assert.NotNull(created);
            Assert.IsType<WithoutDefaultCtor>(created);
        }

        [Fact]
        public void New_ConstructorHavingSideEffects_IsBypassedForTypeWithoutParameterlessCtor()
        {
            // The uninitialized-object path must skip the constructor entirely; if it fell back to Activator or the
            // ctor ran, this would throw instead of handing back a blank instance.
            var created = FactoryExtensions.New<ConstructorThrows>.Instance();

            Assert.NotNull(created);
            Assert.IsType<ConstructorThrows>(created);
        }
    }
}
