using WolfCurses.Graphics;
using Xunit;

namespace WolfCurses.Tests.Graphics
{
    /// <summary>
    ///     <see cref="AnsiConsole.SupportsPictures" />, which answers "will a true-pixel payload survive to this
    ///     terminal at all".
    ///     <para>
    ///         In the non-parallel collection because it moves <see cref="AnsiConsole.ForcedColorMode" />, which
    ///         every widget on <c>Auto</c> reads — the same reason the forced-colour tests live there.
    ///     </para>
    /// </summary>
    [Collection("ColorModeMutation")]
    public class AnsiConsoleSupportsPicturesTests
    {
        [Fact]
        public void NoColourMeansNoPicture()
        {
            // A picture is nothing but colour, and neither true-pixel renderer consults the colour mode itself - so
            // without this check a NO_COLOR environment would grey every widget on screen and leave the photographs
            // in full colour.
            //
            // Asked of the internal overload, which is the only way this clause can be tested at all: a test host has
            // no console to enable virtual terminal processing on, so the public method answers false whatever the
            // colour mode is and a test written against it passes with the colour check deleted. It shipped that way
            // for one mutation round.
            var previous = AnsiConsole.ForcedColorMode;

            try
            {
                AnsiConsole.ForcedColorMode = AnsiColorModeEnum.None;
                Assert.False(AnsiConsole.SupportsPictures(true));

                AnsiConsole.ForcedColorMode = AnsiColorModeEnum.TrueColor;
                Assert.True(AnsiConsole.SupportsPictures(true));
            }
            finally
            {
                AnsiConsole.ForcedColorMode = previous;
            }
        }

        [Fact]
        public void NoVirtualTerminalMeansNoPictureHoweverMuchColourThereIs()
        {
            // The half that is easy to forget: where escapes cannot be interpreted the presenter strips them from
            // every row before writing it, so a true-pixel payload row goes out BLANK and the application shows an
            // empty rectangle with nothing anywhere reporting a problem.
            var previous = AnsiConsole.ForcedColorMode;

            try
            {
                AnsiConsole.ForcedColorMode = AnsiColorModeEnum.TrueColor;
                Assert.False(AnsiConsole.SupportsPictures(false));
            }
            finally
            {
                AnsiConsole.ForcedColorMode = previous;
            }
        }

        [Fact]
        public void TheColourHalfIsReadLiveRatherThanCached()
        {
            // Only the virtual-terminal half is cached, because only that half cannot change. Forcing a colour mode
            // at run time - which is exactly what the example app's renderer picker does - has to take effect at
            // once, so caching the whole answer would break the feature that motivated it.
            var previous = AnsiConsole.ForcedColorMode;

            try
            {
                AnsiConsole.ForcedColorMode = AnsiColorModeEnum.None;
                Assert.False(AnsiConsole.SupportsPictures());

                AnsiConsole.ForcedColorMode = AnsiColorModeEnum.TrueColor;
                var withColour = AnsiConsole.SupportsPictures();

                AnsiConsole.ForcedColorMode = AnsiColorModeEnum.None;
                Assert.False(AnsiConsole.SupportsPictures());

                AnsiConsole.ForcedColorMode = AnsiColorModeEnum.TrueColor;
                Assert.Equal(withColour, AnsiConsole.SupportsPictures());
            }
            finally
            {
                AnsiConsole.ForcedColorMode = previous;
            }
        }

        [Fact]
        public void TheAnswerIsStableWhileNothingChanges()
        {
            var first = AnsiConsole.SupportsPictures();

            for (var i = 0; i < 50; i++)
                Assert.Equal(first, AnsiConsole.SupportsPictures());
        }
    }
}
