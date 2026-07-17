using System.Collections.Generic;
using WolfCurses.Graphics;
using Xunit;

namespace WolfCurses.Tests.Graphics
{
    /// <summary>
    ///     Pins which terminals are recognized as speaking a true-pixel protocol, and — just as importantly — which are
    ///     deliberately not. Getting this wrong in the permissive direction dumps raw escape sequences on a user's
    ///     screen, so the rule throughout is that anything unproven falls back to drawing with characters.
    ///     <para>
    ///         The environment is passed in rather than set on the process, so these stay deterministic and can run in
    ///         parallel with everything else.
    ///     </para>
    /// </summary>
    public class GraphicsProtocolDetectionTests
    {
        /// <summary>Detects against a made-up environment, as though standard output were a real terminal.</summary>
        private static AnsiGraphicsProtocolEnum Detect(params (string Name, string Value)[] variables)
        {
            var environment = new Dictionary<string, string>();
            foreach (var (name, value) in variables)
                environment[name] = value;

            return AnsiConsole.DetectGraphicsProtocol(
                name => environment.TryGetValue(name, out var value) ? value : null,
                outputRedirected: false);
        }

        [Fact]
        public void OutputRedirected_DrawsWithCharacters()
        {
            // No terminal behind a pipe or a file, so there is nothing to draw pixels on.
            var protocol = AnsiConsole.DetectGraphicsProtocol(_ => "xterm-kitty", outputRedirected: true);

            Assert.Equal(AnsiGraphicsProtocolEnum.None, protocol);
        }

        [Fact]
        public void EmptyEnvironment_DrawsWithCharacters()
        {
            Assert.Equal(AnsiGraphicsProtocolEnum.None, Detect());
        }

        [Fact]
        public void DumbTerminal_DrawsWithCharacters()
        {
            Assert.Equal(AnsiGraphicsProtocolEnum.None, Detect(("TERM", "dumb")));
        }

        [Theory]
        [InlineData("KITTY_WINDOW_ID", "1")]
        [InlineData("GHOSTTY_RESOURCES_DIR", "/usr/share/ghostty")]
        [InlineData("WEZTERM_PANE", "0")]
        public void TerminalsIdentifiedByTheirOwnVariable_UseKitty(string name, string value)
        {
            Assert.Equal(AnsiGraphicsProtocolEnum.Kitty, Detect((name, value)));
        }

        [Theory]
        [InlineData("xterm-kitty")]
        [InlineData("xterm-ghostty")]
        public void KittyAndGhosttyTerm_UseKitty(string term)
        {
            Assert.Equal(AnsiGraphicsProtocolEnum.Kitty, Detect(("TERM", term)));
        }

        [Theory]
        [InlineData("WezTerm")]
        [InlineData("ghostty")]
        public void KittySpeakingTermPrograms_UseKitty(string termProgram)
        {
            Assert.Equal(AnsiGraphicsProtocolEnum.Kitty, Detect(("TERM_PROGRAM", termProgram)));
        }

        [Theory]
        [InlineData("foot")]
        [InlineData("foot-extra")]
        [InlineData("mlterm")]
        [InlineData("contour")]
        [InlineData("yaft-256color")]
        public void SixelOnlyTerminals_UseSixel(string term)
        {
            Assert.Equal(AnsiGraphicsProtocolEnum.Sixel, Detect(("TERM", term)));
        }

        [Fact]
        public void WezTerm_PrefersKittyOverSixelEvenThoughItSpeaksBoth()
        {
            // Kitty carries full color and alpha with no palette reduction, so where both are on offer it wins.
            Assert.Equal(AnsiGraphicsProtocolEnum.Kitty,
                Detect(("TERM_PROGRAM", "WezTerm"), ("TERM", "wezterm")));
        }

        [Fact]
        public void ITerm2_Version3OrLater_UsesSixel()
        {
            Assert.Equal(AnsiGraphicsProtocolEnum.Sixel,
                Detect(("TERM_PROGRAM", "iTerm.app"), ("TERM_PROGRAM_VERSION", "3.4.19")));
        }

        [Fact]
        public void ITerm2_BeforeVersion3_DrawsWithCharacters()
        {
            Assert.Equal(AnsiGraphicsProtocolEnum.None,
                Detect(("TERM_PROGRAM", "iTerm.app"), ("TERM_PROGRAM_VERSION", "2.9.20150512")));
        }

        [Fact]
        public void ITerm2_WithoutAVersion_DrawsWithCharacters()
        {
            // An unknown version is not evidence of a new one.
            Assert.Equal(AnsiGraphicsProtocolEnum.None, Detect(("TERM_PROGRAM", "iTerm.app")));
        }

        [Theory]
        [InlineData("7800", AnsiGraphicsProtocolEnum.Sixel)] // VTE 0.78 turned sixel on.
        [InlineData("8000", AnsiGraphicsProtocolEnum.Sixel)]
        [InlineData("7600", AnsiGraphicsProtocolEnum.None)] // Older VTE has no sixel to speak of.
        [InlineData("not-a-version", AnsiGraphicsProtocolEnum.None)]
        public void Vte_IsJudgedByItsVersion(string vteVersion, AnsiGraphicsProtocolEnum expected)
        {
            Assert.Equal(expected, Detect(("VTE_VERSION", vteVersion)));
        }

        [Theory]
        [InlineData("220400", AnsiGraphicsProtocolEnum.Sixel)] // Konsole gained sixel in 22.04.
        [InlineData("230800", AnsiGraphicsProtocolEnum.Sixel)]
        [InlineData("210400", AnsiGraphicsProtocolEnum.None)]
        public void Konsole_IsJudgedByItsVersion(string konsoleVersion, AnsiGraphicsProtocolEnum expected)
        {
            Assert.Equal(expected, Detect(("KONSOLE_VERSION", konsoleVersion)));
        }

        [Fact]
        public void PlainXterm_DrawsWithCharacters()
        {
            // xterm only has sixel when it was compiled with it and started in a sixel-capable emulation, and TERM
            // cannot tell us either way. Only a probe settles this one.
            Assert.Equal(AnsiGraphicsProtocolEnum.None, Detect(("TERM", "xterm-256color")));
        }

        [Fact]
        public void WindowsTerminal_DrawsWithCharactersBecauseItsVersionIsUnknowable()
        {
            // Windows Terminal has had sixel since 1.22 but publishes no version to the environment, and assuming the
            // newer one would fill an older terminal with garbage. Only a probe settles this one.
            Assert.Equal(AnsiGraphicsProtocolEnum.None, Detect(("WT_SESSION", "some-guid")));
        }

        [Theory]
        [InlineData("TMUX", "/tmp/tmux-1000/default,123,0")]
        [InlineData("STY", "1234.pts-0.host")]
        public void InsideAMultiplexer_DrawsWithCharacters(string name, string value)
        {
            // The terminal a picture would have to survive is the multiplexer, which rewrites escape sequences and
            // needs per-user passthrough configuration to let graphics by. What is underneath does not settle it.
            Assert.Equal(AnsiGraphicsProtocolEnum.None, Detect(("TERM", "xterm-kitty"), (name, value)));
        }

        [Theory]
        [InlineData("screen-256color")]
        [InlineData("tmux-256color")]
        public void MultiplexerTerm_DrawsWithCharacters(string term)
        {
            Assert.Equal(AnsiGraphicsProtocolEnum.None, Detect(("TERM", term), ("KITTY_WINDOW_ID", "1")));
        }
    }
}
