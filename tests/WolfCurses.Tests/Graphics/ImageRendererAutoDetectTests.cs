using WolfCurses.Graphics;
using WolfCurses.Tests.TestDoubles;
using Xunit;

namespace WolfCurses.Tests.Graphics
{
    /// <summary>
    ///     Covers the automatic renderer detection that <see cref="SimulationApp" />'s constructor triggers: it runs
    ///     once per process, installs what the terminal answered, and never overrules a renderer the application
    ///     assigned itself. The terminal conversation is passed in as a delegate throughout — the real probe needs a
    ///     terminal to talk to, and (worse) in a test process attached to a capable one it would genuinely install a
    ///     sixel or kitty renderer process-wide.
    ///     <para>
    ///         Every test here rearms the once-flag and swaps the process-wide default, which is exactly what the rest
    ///         of the suite must never observe — hence the non-parallel collection, and each test restoring the
    ///         process-start state on the way out even when it fails.
    ///     </para>
    /// </summary>
    [Collection("RendererDefaultMutation")]
    public sealed class ImageRendererAutoDetectTests
    {
        [Fact]
        public void AutoDetect_InstallsTheRendererForWhatTheProbeFound()
        {
            try
            {
                ImageRenderers.ResetAutoDetectForTests();

                ImageRenderers.AutoDetect(() => AnsiGraphicsProtocolEnum.Sixel);

                Assert.IsType<SixelImageRenderer>(ImageRenderers.Default);
            }
            finally
            {
                ImageRenderers.ResetAutoDetectForTests();
            }
        }

        [Fact]
        public void AutoDetect_WhenTheAnswerIsNone_LeavesTheDefaultInstanceUntouched()
        {
            try
            {
                ImageRenderers.ResetAutoDetectForTests();
                var before = ImageRenderers.Default;

                ImageRenderers.AutoDetect(() => AnsiGraphicsProtocolEnum.None);

                // The same instance, not an equivalent replacement: on the terminals (and test hosts) where
                // detection finds nothing, nothing observable may change at all.
                Assert.Same(before, ImageRenderers.Default);
            }
            finally
            {
                ImageRenderers.ResetAutoDetectForTests();
            }
        }

        [Fact]
        public void AutoDetect_NeverOverrulesARendererTheApplicationChose()
        {
            try
            {
                ImageRenderers.ResetAutoDetectForTests();
                var chosen = new HalfBlockImageRenderer();
                ImageRenderers.Default = chosen;

                // The terminal says kitty; the application already said otherwise. The application wins — this is
                // what makes assigning Default before creating the simulation the documented override.
                ImageRenderers.AutoDetect(() => AnsiGraphicsProtocolEnum.Kitty);

                Assert.Same(chosen, ImageRenderers.Default);
            }
            finally
            {
                ImageRenderers.ResetAutoDetectForTests();
            }
        }

        [Fact]
        public void AutoDetect_RunsOncePerProcess_SoALaterCallChangesNothing()
        {
            try
            {
                ImageRenderers.ResetAutoDetectForTests();

                ImageRenderers.AutoDetect(() => AnsiGraphicsProtocolEnum.Sixel);

                // Every SimulationApp constructed after the first calls this again; none of them may re-probe.
                ImageRenderers.AutoDetect(() => AnsiGraphicsProtocolEnum.Kitty);

                Assert.IsType<SixelImageRenderer>(ImageRenderers.Default);
            }
            finally
            {
                ImageRenderers.ResetAutoDetectForTests();
            }
        }

        [Fact]
        public void AutoDetect_AnAssignmentAfterDetection_StillWins()
        {
            try
            {
                ImageRenderers.ResetAutoDetectForTests();
                ImageRenderers.AutoDetect(() => AnsiGraphicsProtocolEnum.Sixel);

                var chosen = new HalfBlockImageRenderer();
                ImageRenderers.Default = chosen;

                Assert.Same(chosen, ImageRenderers.Default);
            }
            finally
            {
                ImageRenderers.ResetAutoDetectForTests();
            }
        }

        [Fact]
        public void ConstructingASimulationApp_RunsDetection()
        {
            TestSimulationApp app = null;
            try
            {
                ImageRenderers.ResetAutoDetectForTests();

                // The real path, real probe included: in a test host input/output are redirected, so the probe
                // falls straight back to the environment guess without touching the console.
                app = new TestSimulationApp();

                Assert.True(ImageRenderers.AutoDetectHasRun);
            }
            finally
            {
                app?.Destroy();
                ImageRenderers.ResetAutoDetectForTests();
            }
        }
    }
}
