using System;
using WolfCurses.Graphics;

namespace WolfCurses.Demo.Tests.Support
{
    /// <summary>
    ///     Puts back the two process-wide globals the demo's <b>Force render type</b> menu moves.
    ///     <para>
    ///         <b>The leak is what makes this necessary rather than tidy.</b> A forced renderer or colour mode
    ///         outlives the application that set it - <c>Destroy</c> knows nothing about either - so a test that
    ///         forces sixel and does not put it back hands every later test in the assembly a sixel default, and
    ///         the one that fails is whichever came next. Same reasoning as the library's own
    ///         <c>RendererDefaultMutation</c> collection, and the collection alone is not enough: serialising the
    ///         tests stops them racing, it does not stop one of them changing the world for the rest.
    ///     </para>
    /// </summary>
    public sealed class RenderTypeScope : IDisposable
    {
        /// <summary>What was drawing before anything was forced.</summary>
        private readonly IImageRenderer _renderer;

        /// <summary>What the colour mode override was before anything was forced, usually nothing.</summary>
        private readonly AnsiColorModeEnum? _colorMode;

        /// <summary>Initializes a new instance of the <see cref="RenderTypeScope" /> class.</summary>
        public RenderTypeScope()
        {
            _renderer = ImageRenderers.Default;
            _colorMode = AnsiConsole.ForcedColorMode;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            ImageRenderers.Default = _renderer;
            AnsiConsole.ForcedColorMode = _colorMode;
        }
    }
}
