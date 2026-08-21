using System;
using System.Linq;
using WolfCurses.Apps.Basic;
using WolfCurses.Apps.Tests.Support;
using Xunit;

namespace WolfCurses.Apps.Tests
{
    /// <summary>
    ///     Moving pixels about with GET and PUT, and the little language PLAY takes. The music is pure arithmetic
    ///     and is tested as such; nothing here makes a noise.
    /// </summary>
    public class BasicSpriteSoundTests
    {
        private static BasicScreen Drawn(string source)
        {
            var screen = new BasicScreen(80, 25);
            BasicProgram.Compile(source).Run(new BasicRuntime(screen, 1));

            return screen;
        }

        [Fact]
        public void GetLiftsARectangleAndPutStampsItBackSomewhereElse()
        {
            const string program = "SCREEN 13\nDIM S(200)\nLINE (0, 0)-(4, 4), 15, BF\nGET (0, 0)-(4, 4), S\n" +
                                   "LINE (0, 0)-(4, 4), 0, BF\nPUT (20, 20), S, PSET";

            var screen = Drawn(program);

            Assert.Equal(15, screen.ColorAt(22, 22));
            Assert.Equal(15, screen.ColorAt(20, 20));
            Assert.Equal(15, screen.ColorAt(24, 24));

            // Where it came from was rubbed out, so what is on screen came from the array rather than never having
            // moved at all.
            Assert.Equal(0, screen.ColorAt(2, 2));
        }

        [Fact]
        public void PuttingTheSameSpriteTwiceWithXorPutsTheScreenBackAsItWas()
        {
            // The whole reason XOR is the default: a sprite moves without anything having to remember what was
            // underneath it.
            const string setup = "SCREEN 13\nDIM S(100)\nPSET (0, 0), 5\nGET (0, 0)-(0, 0), S\n";

            Assert.Equal(5, Drawn(setup + "PUT (20, 20), S").ColorAt(20, 20));
            Assert.Equal(0, Drawn(setup + "PUT (20, 20), S\nPUT (20, 20), S").ColorAt(20, 20));
        }

        [Fact]
        public void PutTakesTheOtherWaysOfCombiningToo()
        {
            const string setup = "SCREEN 13\nDIM S(100)\nPSET (0, 0), 12\nGET (0, 0)-(0, 0), S\n" +
                                 "PSET (20, 20), 10\n";

            Assert.Equal(12, Drawn(setup + "PUT (20, 20), S, PSET").ColorAt(20, 20));
            Assert.Equal(12 | 10, Drawn(setup + "PUT (20, 20), S, OR").ColorAt(20, 20));
            Assert.Equal(12 & 10, Drawn(setup + "PUT (20, 20), S, AND").ColorAt(20, 20));
            Assert.Equal(15 - 12, Drawn(setup + "PUT (20, 20), S, PRESET").ColorAt(20, 20));
        }

        [Fact]
        public void TheArrayHoldsItsOwnSizeSoPutKnowsWhatItIsStamping()
        {
            const string program = "SCREEN 13\nDIM S(200)\nGET (0, 0)-(3, 1), S\nPRINT S(0); S(1)";

            var host = new RecordingBasicHost();
            BasicProgram.Compile(program).Run(new BasicRuntime(host, 1));

            // Four across and two down, counted inclusively as every BASIC rectangle is.
            Assert.Contains(" 4  2 ", host.Output, StringComparison.Ordinal);
        }

        [Fact]
        public void ASpriteStampedPartlyOffTheScreenLosesOnlyThePartThatIsOff()
        {
            const string program = "SCREEN 13\nDIM S(200)\nLINE (0, 0)-(4, 4), 9, BF\nGET (0, 0)-(4, 4), S\n" +
                                   "PUT (317, 10), S, PSET";

            var screen = Drawn(program);

            Assert.Equal(9, screen.ColorAt(318, 12));
            Assert.Equal(-1, screen.ColorAt(320, 12));
        }

        [Theory]
        [InlineData("SCREEN 13\nGET (0,0)-(1,1), S", "has not been dimensioned")]
        [InlineData("SCREEN 13\nDIM S(3)\nGET (0,0)-(9,9), S", "Subscript out of range")]
        [InlineData("SCREEN 13\nDIM S(9)\nGET (0,0), S", "Expected -")]
        [InlineData("SOUND 440", "Expected a duration")]
        public void MistakesAreReportedRatherThanIgnored(string program, string expected)
        {
            var error = RecordingBasicHost.Fails(program);

            Assert.Contains(expected, error.Reason, StringComparison.Ordinal);
        }

        [Fact]
        public void ConcertPitchIsWhereItShouldBeAndEverySemitoneFollowsFromIt()
        {
            // Four hundred and forty hertz is the A above middle C. Get the reference wrong and every note is in
            // the wrong place while the tune still sounds like a tune, which is why this is asserted absolutely.
            var state = new BasicMusicState();
            var notes = BasicMusic.Parse("A", state);

            Assert.Equal(440d, notes[0].Frequency, 2);

            Assert.Equal(261.63d, BasicMusic.Parse("O4C", new BasicMusicState())[0].Frequency, 2);
            Assert.Equal(880d, BasicMusic.Parse("O5A", new BasicMusicState())[0].Frequency, 2);
        }

        [Fact]
        public void SharpsAndFlatsMoveOneSemitoneEitherWay()
        {
            var sharp = BasicMusic.Parse("C#", new BasicMusicState())[0].Frequency;
            var plus = BasicMusic.Parse("C+", new BasicMusicState())[0].Frequency;
            var flat = BasicMusic.Parse("D-", new BasicMusicState())[0].Frequency;

            Assert.Equal(sharp, plus, 5);
            Assert.Equal(sharp, flat, 5);
        }

        [Fact]
        public void ANoteLastsAsLongAsTheTempoAndItsLengthSay()
        {
            // At 120 quarter notes a minute a quarter note is half a second, and a dot adds half of what there was.
            var state = new BasicMusicState();
            var notes = BasicMusic.Parse("C C8 C4.", state);

            Assert.Equal(500d, notes[0].Milliseconds, 1);
            Assert.Equal(250d, notes[1].Milliseconds, 1);
            Assert.Equal(750d, notes[2].Milliseconds, 1);
        }

        [Fact]
        public void TempoAndLengthAndOctaveCarryOnIntoTheNextPlayString()
        {
            // Which is why they live on the running program: a tune is very often set up on one line and played on
            // another, and starting fresh would play it at the wrong speed.
            var state = new BasicMusicState();

            BasicMusic.Parse("T60 L8 O5", state);
            var notes = BasicMusic.Parse("A", state);

            Assert.Equal(60, state.Tempo);
            Assert.Equal(8, state.Length);
            Assert.Equal(880d, notes[0].Frequency, 2);
            Assert.Equal(500d, notes[0].Milliseconds, 1);
        }

        [Fact]
        public void AngleBracketsStepAnOctaveAndStopAtTheEnds()
        {
            var state = new BasicMusicState();

            BasicMusic.Parse(">", state);
            Assert.Equal(5, state.Octave);

            BasicMusic.Parse("<<", state);
            Assert.Equal(3, state.Octave);

            BasicMusic.Parse("<<<<<<<<", state);
            Assert.Equal(0, state.Octave);
        }

        [Fact]
        public void ARestIsANoteWithNoPitch()
        {
            // Expressed as a frequency of zero rather than a second kind of note, so a caller walks one list.
            var notes = BasicMusic.Parse("P4", new BasicMusicState());

            Assert.Equal(0d, notes[0].Frequency);
            Assert.Equal(500d, notes[0].Milliseconds, 1);
        }

        [Fact]
        public void TheArticulationCommandsAreReadAndDropped()
        {
            // Nothing here can express how notes join up, and refusing them would make somebody's tune an error.
            var notes = BasicMusic.Parse("MB ML C", new BasicMusicState());

            Assert.Single(notes);
        }

        [Fact]
        public void PlayAndSoundBothReachTheScreen()
        {
            var host = RecordingBasicHost.Run("SOUND 440, 18.2\nPLAY \"CDE\"");

            Assert.Equal(4, host.Notes.Count);

            // Eighteen point two ticks is one second, and rounding that rate to eighteen or twenty would make every
            // tune the wrong length.
            Assert.Equal(440d, host.Notes[0].Frequency, 2);
            Assert.Equal(1000d, host.Notes[0].Milliseconds, 1);
        }

        [Fact]
        public void SomethingPlayCannotReadIsReportedRatherThanIgnored()
        {
            var error = RecordingBasicHost.Fails("PLAY \"CXE\"");

            Assert.Contains("PLAY does not understand", error.Reason, StringComparison.Ordinal);
        }
    }
}
