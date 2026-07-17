using WolfCurses.Graphics;
using Xunit;

namespace WolfCurses.Tests.Graphics
{
    /// <summary>
    ///     Pins how a terminal's answer to the graphics probe is read. The probe's input/output half needs a real
    ///     terminal and cannot be exercised here, which makes it all the more worth testing the half that can be: given
    ///     exactly the bytes a terminal sends, what do we conclude?
    ///     <para>
    ///         The replies below are the real thing — a kitty acknowledgement, and primary device-attributes replies as
    ///         actually sent by xterm, Windows Terminal, and others.
    ///     </para>
    /// </summary>
    public class GraphicsProbeReplyTests
    {
        private const char ESC = (char) 27;

        private static AnsiGraphicsProtocolEnum Interpret(string reply,
            AnsiGraphicsProtocolEnum fallback = AnsiGraphicsProtocolEnum.None)
        {
            return AnsiConsole.InterpretGraphicsReply(reply, fallback);
        }

        [Fact]
        public void KittyAcknowledgement_MeansKitty()
        {
            // kitty answers the graphics query with the image id it was handed and an OK, then the device attributes.
            var reply = $"{ESC}_Gi=31;OK{ESC}\\{ESC}[?62;1;4c";

            Assert.Equal(AnsiGraphicsProtocolEnum.Kitty, Interpret(reply));
        }

        [Fact]
        public void KittyAcknowledgement_WinsEvenWhenTheTerminalAlsoClaimsSixel()
        {
            // WezTerm answers both. Kitty is the better protocol, so it takes precedence.
            var reply = $"{ESC}_Gi=31;OK{ESC}\\{ESC}[?62;4;6;22c";

            Assert.Equal(AnsiGraphicsProtocolEnum.Kitty, Interpret(reply));
        }

        [Fact]
        public void DeviceAttributesContainingFour_MeansSixel()
        {
            // Attribute 4 is sixel. This is xterm built with sixel support.
            var reply = $"{ESC}[?62;1;2;4;6;9;15;22;29c";

            Assert.Equal(AnsiGraphicsProtocolEnum.Sixel, Interpret(reply));
        }

        [Fact]
        public void DeviceAttributesWithoutFour_MeansDrawWithCharacters()
        {
            // A terminal that answered and did not claim sixel has told us something real: it has no sixel.
            var reply = $"{ESC}[?62;1;6;9;15;22;29c";

            Assert.Equal(AnsiGraphicsProtocolEnum.None, Interpret(reply));
        }

        [Fact]
        public void DeviceAttributesWithoutFour_DoesNotOverruleAKittyTerminalFromTheEnvironment()
        {
            // Device attributes say nothing about the kitty protocol, so a terminal the environment already identified
            // as kitty must not be downgraded just because its attributes list no sixel.
            var reply = $"{ESC}[?62;1;6c";

            Assert.Equal(AnsiGraphicsProtocolEnum.Kitty, Interpret(reply, AnsiGraphicsProtocolEnum.Kitty));
        }

        [Fact]
        public void FourAppearingOnlyAsPartOfALargerNumber_IsNotSixel()
        {
            // "14" and "40" contain a 4 but are not attribute 4; matching on the whole parameter is what prevents a
            // terminal being told it has sixel when it does not.
            var reply = $"{ESC}[?64;14;40;41c";

            Assert.Equal(AnsiGraphicsProtocolEnum.None, Interpret(reply));
        }

        [Fact]
        public void UnrecognizableReply_FallsBackToTheEnvironmentGuess()
        {
            Assert.Equal(AnsiGraphicsProtocolEnum.Sixel,
                Interpret("total nonsense", AnsiGraphicsProtocolEnum.Sixel));
        }

        [Fact]
        public void ReplyWithNoTerminator_FallsBackToTheEnvironmentGuess()
        {
            // Truncated by the timeout: the 'c' never arrived, so the attribute list cannot be trusted to be complete.
            Assert.Equal(AnsiGraphicsProtocolEnum.Sixel,
                Interpret($"{ESC}[?62;1;4", AnsiGraphicsProtocolEnum.Sixel));
        }

        [Fact]
        public void EmptyReply_FallsBackToTheEnvironmentGuess()
        {
            Assert.Equal(AnsiGraphicsProtocolEnum.Kitty, Interpret(string.Empty, AnsiGraphicsProtocolEnum.Kitty));
        }

        [Fact]
        public void WindowsTerminalWithSixel_IsRecognized()
        {
            // Windows Terminal 1.22+ reports attribute 4. This is precisely the case the environment cannot settle,
            // since Windows Terminal publishes no version — so this is the probe earning its keep.
            var reply = $"{ESC}[?61;4;6;7;14;21;22;23;24;28;32;42c";

            Assert.Equal(AnsiGraphicsProtocolEnum.Sixel, Interpret(reply));
        }

        [Fact]
        public void WindowsTerminalWithoutSixel_DrawsWithCharacters()
        {
            // The same terminal before 1.22, which is why assuming from WT_SESSION alone would have been wrong.
            var reply = $"{ESC}[?61;6;7;14;21;22;23;24;28;32;42c";

            Assert.Equal(AnsiGraphicsProtocolEnum.None, Interpret(reply));
        }
    }
}
