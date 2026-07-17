using Xunit;

namespace WolfCurses.Tests.Graphics
{
    /// <summary>
    ///     Collection whose tests mutate the process-global <see cref="WolfCurses.Graphics.ImageRenderers" /> state
    ///     (the default renderer and the auto-detect once-flag). Marked non-parallel so the temporary mutations can
    ///     never be observed by the rest of the suite — every other test that renders an image, or asserts what the
    ///     default renderer is, reads the same statics.
    /// </summary>
    [CollectionDefinition("RendererDefaultMutation", DisableParallelization = true)]
    public sealed class RendererDefaultMutationCollection
    {
    }
}
