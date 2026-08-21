using System;
using System.IO;
using WolfCurses.Apps.Basic;
using Xunit;

namespace WolfCurses.Apps.Tests
{
    /// <summary>
    ///     The programs that ship beside the executable. They are documentation as much as anything else, and a
    ///     sample that does not compile is worse than no sample: it is the first thing somebody runs.
    /// </summary>
    public class BasicSampleTests
    {
        public static TheoryData<string> Samples()
        {
            var data = new TheoryData<string>();

            foreach (var path in Directory.GetFiles(BasicLibrary.Folder, "*.bas"))
                data.Add(Path.GetFileName(path));

            return data;
        }

        [Fact]
        public void TheSamplesAreReallyShippedBesideTheProgram()
        {
            // Without this the theory below would pass by having nothing to say, which is the way a test about a
            // folder of files fails silently.
            Assert.True(Directory.Exists(BasicLibrary.Folder), "no programs folder was copied to the output");
            Assert.True(Directory.GetFiles(BasicLibrary.Folder, "*.bas").Length >= 4,
                "the sample programs were not copied beside the executable");
        }

        [Theory]
        [MemberData(nameof(Samples))]
        public void EverySampleCompiles(string name)
        {
            var source = File.ReadAllText(Path.Combine(BasicLibrary.Folder, name));

            // Compiled rather than run: one of them asks a question, and INPUT with nobody to answer it stops the
            // program by design. What is worth checking here is that the listing is valid BASIC.
            var error = Record.Exception(() => BasicProgram.Compile(source));

            Assert.True(error == null, name + " does not compile: " + error?.Message);
        }

        [Fact]
        public void TheOneThatOpensOnStartUpAlsoRunsToCompletion()
        {
            // The very first thing anybody sees, so it has to do more than parse.
            var source = File.ReadAllText(BasicLibrary.DefaultProgramPath);
            var screen = new BasicScreen(80, 25);

            BasicProgram.Compile(source).Run(new BasicRuntime(screen, 1));

            Assert.Contains("Try editing this", screen.Render(), StringComparison.Ordinal);
        }

        [Fact]
        public void TheDrawingSampleReallyPutsPixelsDown()
        {
            // A graphics program that compiles and draws nothing at all would pass every other test here.
            var source = File.ReadAllText(Path.Combine(BasicLibrary.Folder, "drawing.bas"));
            var screen = new BasicScreen(80, 25);

            BasicProgram.Compile(source).Run(new BasicRuntime(screen, 1));

            Assert.True(screen.IsGraphics, "the sample never asked for a graphics mode");

            var lit = 0;
            for (var y = 0; y < screen.ScreenHeight; y++)
            {
                for (var x = 0; x < screen.ScreenWidth; x++)
                {
                    if (screen.ColorAt(x, y) > 0)
                        lit++;
                }
            }

            Assert.True(lit > 1000, "the sample drew only " + lit + " pixels");
        }

        [Fact]
        public void TheProcedureSampleRunsAndGetsItsSumsRight()
        {
            // It is the demonstration of SUB and FUNCTION, so what it prints is the claim being made.
            var source = File.ReadAllText(Path.Combine(BasicLibrary.Folder, "procedures.bas"));
            var screen = new BasicScreen(80, 40);

            BasicProgram.Compile(source).Run(new BasicRuntime(screen, 1));

            var output = screen.Render();

            Assert.Contains("999", output, StringComparison.Ordinal);
            Assert.Contains("15", output, StringComparison.Ordinal);
            Assert.Contains("720", output, StringComparison.Ordinal);
        }
    }
}
