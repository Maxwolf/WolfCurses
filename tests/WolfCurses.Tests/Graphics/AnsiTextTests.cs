using WolfCurses.Graphics;
using Xunit;

namespace WolfCurses.Tests.Graphics
{
    /// <summary>
    ///     AnsiText is the library's one escape walk, made public because keeping it internal is what produced the
    ///     divergent copies it now replaces. The grammar itself is already pinned in detail by
    ///     <see cref="Core.ConsolePresenterTests" />, which since the move exercises this type through the
    ///     presenter's forwarders; what these add is the part that is new — that it is reachable at all, that the
    ///     measure and the strip cannot disagree, and that plain text costs nothing.
    /// </summary>
    public class AnsiTextTests
    {
        /// <summary>Written as an escape rather than typed: a literal ESC in a source file is invisible in every editor.</summary>
        private const char Esc = '\u001b';

        private const char Bel = '\a';

        /// <summary>
        ///     One of every shape the walk has a branch for: plain, CSI, true-colour CSI, an OSC string terminated
        ///     both ways, a CSI whose final byte is not a letter (bracketed paste — the shape the regular expression
        ///     this replaced got wrong), a short ESC with an intermediate byte, and a bare trailing ESC.
        /// </summary>
        private static readonly string[] _shapes =
        {
            string.Empty,
            "plain",
            $"{Esc}[31mred{Esc}[0m",
            $"{Esc}[38;2;255;0;0mtrue colour{Esc}[0m",
            $"{Esc}]8;;https://example.com{Bel}link{Esc}]8;;{Bel}",
            $"{Esc}]8;;https://example.com{Esc}\\link{Esc}]8;;{Esc}\\",
            $"{Esc}[200~bracketed{Esc}[201~",
            $"{Esc}(Bcharset",
            $"trailing{Esc}"
        };

        [Fact]
        public void IsPublic_AndReachableFromOutsideTheLibrary()
        {
            // The whole point of the change, and the only assertion that fails if someone "tidies" the type back to
            // internal: InternalsVisibleTo would let every other test here compile either way.
            Assert.True(typeof(AnsiText).IsPublic);
            Assert.True(typeof(AnsiText).GetMethod(nameof(AnsiText.VisibleLength)).IsPublic);
            Assert.True(typeof(AnsiText).GetMethod(nameof(AnsiText.StripEscapes)).IsPublic);
        }

        [Fact]
        public void StrippedLengthAlwaysEqualsVisibleLength()
        {
            // The divergence trap, asserted on the public surface. Two walks that answer differently is the failure
            // this type exists to make impossible, and it is invisible in any single-sided test.
            foreach (var shape in _shapes)
                Assert.Equal(AnsiText.VisibleLength(shape), AnsiText.StripEscapes(shape).Length);
        }

        [Fact]
        public void EscapesAreZeroWidth_AndTheirBodiesAreNotCounted()
        {
            Assert.Equal(3, AnsiText.VisibleLength($"{Esc}[31mred{Esc}[0m"));
            Assert.Equal(4, AnsiText.VisibleLength($"{Esc}]8;;https://example.com{Esc}\\link{Esc}]8;;{Esc}\\"));
            Assert.Equal(9, AnsiText.VisibleLength($"{Esc}[200~bracketed"));
            Assert.Equal(7, AnsiText.VisibleLength($"{Esc}(Bcharset"));
            Assert.Equal(8, AnsiText.VisibleLength($"trailing{Esc}"));
        }

        [Fact]
        public void PlainText_IsReturnedByReferenceWithoutAllocating()
        {
            // The fast path exists because the overwhelmingly common row carries no styling at all, and a widget's
            // render walks every row of every frame.
            const string plain = "no escapes here";
            Assert.Same(plain, AnsiText.StripEscapes(plain));
            Assert.Equal(plain.Length, AnsiText.VisibleLength(plain));
        }

        [Fact]
        public void NullAndEmpty_AreAnsweredRatherThanThrown()
        {
            // A public helper reached from layout code, where a null row is an ordinary thing to have.
            Assert.Equal(0, AnsiText.VisibleLength(null));
            Assert.Equal(0, AnsiText.VisibleLength(string.Empty));
            Assert.Null(AnsiText.StripEscapes(null));
            Assert.Equal(string.Empty, AnsiText.StripEscapes(string.Empty));
        }

        [Fact]
        public void ThePresenterForwardsToThisTypeRatherThanKeepingItsOwnWalk()
        {
            // Pins that the forwarders stay forwarders. If someone reintroduces a private walk in ConsolePresenter
            // this keeps passing only for as long as the two agree — which is exactly the state that rots silently,
            // so the pairing is asserted across the whole escape-shape table rather than on one string.
            foreach (var shape in _shapes)
            {
                Assert.Equal(AnsiText.VisibleLength(shape), ConsolePresenter.VisibleLength(shape));
                Assert.Equal(AnsiText.StripEscapes(shape), ConsolePresenter.StripEscapes(shape));
            }
        }
    }
}
