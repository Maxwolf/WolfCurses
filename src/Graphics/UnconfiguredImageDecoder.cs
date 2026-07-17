// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/16/2026

using System;
using System.IO;

namespace WolfCurses.Graphics
{
    /// <summary>
    ///     What <see cref="ImageDecoders.Default" /> starts out as: a decoder that decodes nothing and exists only to
    ///     fail with an explanation. The library ships no decoder of its own — turning PNG or JPEG bytes into pixels
    ///     means a third-party dependency, and that is a choice left to the application rather than forced on every
    ///     consumer of the package.
    ///     <para>
    ///         Standing in for a real decoder rather than leaving the default null is what keeps the never-null
    ///         contract on <see cref="ImageDecoders.Default" /> honest, and turns what would otherwise be a bare
    ///         <see cref="NullReferenceException" /> at the first image load into a message that says what to do.
    ///     </para>
    /// </summary>
    internal sealed class UnconfiguredImageDecoder : IImageDecoder
    {
        /// <inheritdoc />
        public PixelBuffer Decode(Stream source)
        {
            throw new InvalidOperationException(
                "No image decoder is configured, so encoded image data cannot be turned into pixels. WolfCurses " +
                "ships without a built-in decoder to keep the package free of dependencies, so an application that " +
                "loads images must choose one and assign it at start-up, before the first image is loaded:" +
                Environment.NewLine + Environment.NewLine +
                "    ImageDecoders.Default = new StbImageDecoder();" +
                Environment.NewLine + Environment.NewLine +
                "Implement IImageDecoder over whatever image library you already use, or add the StbImageSharp " +
                "package and copy the small adapter from the example app (example/WolfCurses.Example/Graphics/). " +
                "Pixels you have already decoded need no decoder at all: AnsiImage.FromPixels takes a PixelBuffer.");
        }
    }
}
