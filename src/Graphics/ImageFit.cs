// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/16/2026

using System;

namespace WolfCurses.Graphics
{
    /// <summary>
    ///     Shared geometry and compositing for the renderers that draw real pixels (<see cref="SixelImageRenderer" />,
    ///     <see cref="KittyImageRenderer" />). They differ entirely in how they hand pixels to the terminal but not at
    ///     all in how they decide which pixels to hand over, so that part lives here once.
    ///     <para>
    ///         This is deliberately not the arithmetic <see cref="AnsiImageRenderer" /> uses. That renderer stacks two
    ///         pixels into every character row and has to correct for a cell being about twice as tall as it is wide, so
    ///         its sizing is expressed in cells with the aspect ratio folded in. A true-pixel renderer has no such
    ///         distortion to undo — a pixel is square and a picture's proportions carry through — so its only job is to
    ///         turn the cell budget the rest of the library speaks in into a pixel area.
    ///     </para>
    /// </summary>
    internal static class ImageFit
    {
        /// <summary>
        ///     Scales (and for <see cref="AnsiImageFitEnum.Cover" />, crops) the image to fill the pixel area implied by
        ///     the options' cell budget and the terminal's cell size.
        /// </summary>
        /// <param name="image">The decoded source image.</param>
        /// <param name="options">The rendering options whose fit, alignment and bounds are honored.</param>
        /// <param name="cellPixelWidth">Pixels across one terminal character cell.</param>
        /// <param name="cellPixelHeight">Pixels down one terminal character cell.</param>
        public static PixelBuffer FitToPixels(PixelBuffer image, AnsiImageOptions options, int cellPixelWidth,
            int cellPixelHeight)
        {
            var (maxColumns, maxRows) = AnsiImageRenderer.ResolveBounds(options);
            var areaWidth = Math.Max(1, maxColumns * cellPixelWidth);
            var areaHeight = Math.Max(1, maxRows * cellPixelHeight);

            switch (options.Fit)
            {
                case AnsiImageFitEnum.Stretch:
                    return image.Resize(areaWidth, areaHeight);

                case AnsiImageFitEnum.Cover:
                    return Cover(image, areaWidth, areaHeight, options);

                default:
                    // Contain and ScaleDown both scale the whole picture to sit inside the area; ScaleDown additionally
                    // refuses to enlarge, so a small image is shown at its own size rather than blown up and blurred.
                    var scale = Math.Min(areaWidth / (double) image.Width, areaHeight / (double) image.Height);
                    if (options.Fit == AnsiImageFitEnum.ScaleDown)
                        scale = Math.Min(scale, 1.0);
                    if (!double.IsFinite(scale) || scale <= 0)
                        scale = 1.0;

                    var width = Math.Max(1, (int) Math.Round(image.Width * scale, MidpointRounding.AwayFromZero));
                    var height = Math.Max(1, (int) Math.Round(image.Height * scale, MidpointRounding.AwayFromZero));
                    return image.Resize(width, height);
            }
        }

        /// <summary>
        ///     Fills the area completely without distortion: crops the source to the sub-rectangle whose proportions
        ///     match the area (anchored by the alignment options) and scales that to fill it exactly.
        /// </summary>
        private static PixelBuffer Cover(PixelBuffer image, int areaWidth, int areaHeight, AnsiImageOptions options)
        {
            var imageAspect = image.Width / (double) image.Height;
            var areaAspect = areaWidth / (double) areaHeight;
            if (!double.IsFinite(imageAspect) || imageAspect <= 0)
                imageAspect = 1.0;
            if (!double.IsFinite(areaAspect) || areaAspect <= 0)
                areaAspect = 1.0;

            int cropWidth, cropHeight;
            if (imageAspect > areaAspect)
            {
                // Source is proportionally wider than the area: keep the full height, crop the sides.
                cropHeight = image.Height;
                cropWidth = (int) Math.Round(image.Width * (areaAspect / imageAspect), MidpointRounding.AwayFromZero);
            }
            else
            {
                // Source is proportionally taller than the area: keep the full width, crop top and bottom.
                cropWidth = image.Width;
                cropHeight = (int) Math.Round(image.Height * (imageAspect / areaAspect), MidpointRounding.AwayFromZero);
            }

            cropWidth = Math.Max(1, Math.Min(cropWidth, image.Width));
            cropHeight = Math.Max(1, Math.Min(cropHeight, image.Height));

            var cropX = AnsiImageRenderer.AnchorOffset(options.HorizontalAlignment, image.Width - cropWidth);
            var cropY = AnsiImageRenderer.AnchorOffset(options.VerticalAlignment, image.Height - cropHeight);

            return image.Crop(cropX, cropY, cropWidth, cropHeight).Resize(areaWidth, areaHeight);
        }

        /// <summary>
        ///     Alpha-composites every pixel over an opaque color so nothing is left see-through, for when the caller
        ///     asked for a known backdrop instead of letting the terminal show through.
        /// </summary>
        public static PixelBuffer Flatten(PixelBuffer image, Rgb24 background)
        {
            var flattened = new PixelBuffer(image.Width, image.Height);

            for (var y = 0; y < image.Height; y++)
            {
                for (var x = 0; x < image.Width; x++)
                {
                    var source = image.GetPixel(x, y);
                    var alpha = source.A / 255.0;
                    flattened.SetPixel(x, y, new Rgba32(
                        (byte) Math.Round(source.R * alpha + background.R * (1 - alpha)),
                        (byte) Math.Round(source.G * alpha + background.G * (1 - alpha)),
                        (byte) Math.Round(source.B * alpha + background.B * (1 - alpha)),
                        255));
                }
            }

            return flattened;
        }
    }
}
