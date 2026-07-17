// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/17/2026

using WolfCurses.Graphics;

namespace WolfCurses.Example.Demos
{
    /// <summary>
    ///     Flips a demo between whatever the terminal answered the startup probe with and plain half blocks, so the
    ///     difference between them can be watched rather than taken on trust.
    ///     <para>
    ///         <b>It opens on the probe's answer</b>, which is the honest thing for a demo to do: this app exists to
    ///         show what the library does on the terminal it finds itself in, and quietly substituting a cheaper
    ///         renderer would be showing something else. Since the 2026-07-17 rework every renderer holds 30fps on
    ///         these scenes, so the number that moves when TAB is pressed is <b>ms/frame</b>, not fps: the same sprite
    ///         frame costs about <b>21ms in sixel against 5ms in half blocks</b> (it was ~205ms before the rework,
    ///         when sixel upscaled the canvas to 1.6 million terminal pixels on the CPU and then quantized every one
    ///         of them; now it palettes the ~100K source pixels and stretches runs arithmetically). The gap is still
    ///         real — sixel does many times the work for its ten-by-twenty real pixels per cell — it just fits inside
    ///         the frame budget now.
    ///     </para>
    ///     <para>
    ///         On a terminal the probe found nothing on, both sides of the switch are half blocks and pressing TAB will
    ///         do nothing at all — which is why this names both of them rather than printing a hopeful label.
    ///     </para>
    /// </summary>
    /// <seealso cref="DemoImages.AnimationRenderer" />
    internal sealed class RendererSwitch
    {
        /// <summary>
        ///     Whether half blocks have been forced, as opposed to whatever the probe installed. False to begin with:
        ///     a demo shows what this terminal actually does until asked otherwise.
        /// </summary>
        public bool Forced { get; private set; }

        /// <summary>The renderer to draw with right now.</summary>
        public IImageRenderer Current => Forced ? DemoImages.AnimationRenderer : ImageRenderers.Default;

        /// <summary>Swaps to the other one.</summary>
        public void Toggle()
        {
            Forced = !Forced;
        }

        /// <summary>Names what is drawing, and what pressing TAB would get instead.</summary>
        public string Describe()
        {
            var probed = Name(ImageRenderers.Default);

            // Naming the alternative as well as the current one, because a reader who does not already know what the
            // probe found cannot tell what TAB is going to do, and on a plain terminal the honest answer is "nothing".
            return Forced
                ? $"half blocks (TAB: {probed})"
                : $"{probed} (TAB: half blocks)";
        }

        /// <summary>
        ///     What a renderer is, in a word. Asks the object rather than re-running detection: the probe's answer is
        ///     what was installed and what would draw, and re-detecting from environment variables would disagree with
        ///     it on exactly the terminals worth knowing about.
        /// </summary>
        private static string Name(IImageRenderer renderer)
        {
            return renderer switch
            {
                KittyImageRenderer => "kitty",
                SixelImageRenderer => "sixel",
                HalfBlockImageRenderer => "half blocks",
                var other => other.GetType().Name
            };
        }
    }
}
