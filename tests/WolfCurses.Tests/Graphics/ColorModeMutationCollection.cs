using Xunit;

namespace WolfCurses.Tests.Graphics
{
    /// <summary>
    ///     Collection whose tests mutate the process-global <see cref="WolfCurses.Graphics.AnsiConsole.ForcedColorMode" />,
    ///     which <see cref="WolfCurses.Graphics.AnsiConsole.DetectColorMode" /> reads. Marked non-parallel so the
    ///     temporary override can never be observed by the rest of the suite — anything left at
    ///     <see cref="WolfCurses.Graphics.AnsiColorModeEnum.Auto" /> resolves through that same static.
    /// </summary>
    [CollectionDefinition("ColorModeMutation", DisableParallelization = true)]
    public sealed class ColorModeMutationCollection
    {
    }
}
