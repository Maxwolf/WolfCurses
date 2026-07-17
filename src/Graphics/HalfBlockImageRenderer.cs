// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/16/2026

namespace WolfCurses.Graphics
{
    /// <summary>
    ///     The default <see cref="IImageRenderer" />: draws the image as colored half-block characters (or a brightness
    ///     ramp in <see cref="AnsiColorModeEnum.None" />) by delegating to <see cref="AnsiImageRenderer" />. Because it
    ///     produces nothing but ordinary character cells and SGR color escapes, it works in any terminal that can show
    ///     color at all, which is why it is the default rather than a true-pixel protocol.
    ///     <para>
    ///         This is a thin instance wrapper: <see cref="AnsiImageRenderer" /> stays a static class so existing
    ///         callers keep working, and this type is what lets it be handed around as an interface (stored in
    ///         <see cref="ImageRenderers.Default" />, passed to <see cref="AnsiImage.ToAnsi(AnsiImageOptions,IImageRenderer)" />,
    ///         and so on). It holds no state, so one instance can be shared freely.
    ///     </para>
    /// </summary>
    public sealed class HalfBlockImageRenderer : IImageRenderer
    {
        /// <inheritdoc />
        public string Render(PixelBuffer image, AnsiImageOptions options = null)
        {
            return AnsiImageRenderer.Render(image, options);
        }
    }
}
