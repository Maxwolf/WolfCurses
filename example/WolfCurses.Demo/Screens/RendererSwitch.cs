// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/17/2026

using WolfCurses.Graphics;

namespace WolfCurses.Demo.Screens
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

        /// <summary>
        ///     Names what is drawing, and what pressing TAB would get instead.
        ///     <para>
        ///         <b>Both names are read off the renderers themselves</b>, which is worth stating because this
        ///         method used to do neither. It matched on the three built-in classes, so a third-party renderer -
        ///         precisely what the <c>IImageRenderer</c> seam exists to allow - came out as its own class name;
        ///         and it named the alternative from a hard-coded literal, so changing what TAB offers would have
        ///         made the label quietly lie. <see cref="IImageRenderer.Name" /> is a default interface member, so
        ///         every renderer answers whether or not it was written to.
        ///     </para>
        ///     <para>
        ///         Asked of the objects rather than by re-running detection: the probe's answer is what was
        ///         installed and what would actually draw, and re-detecting from environment variables would
        ///         disagree with it on exactly the terminals worth knowing about. That is the same lesson
        ///         <c>DemoWindow.DescribeRenderer</c> already records; this was the copy the sweep missed.
        ///     </para>
        /// </summary>
        public string Describe()
        {
            var probed = ImageRenderers.Default.Name;
            var alternative = DemoImages.AnimationRenderer.Name;

            // Naming the alternative as well as the current one, because a reader who does not already know what the
            // probe found cannot tell what TAB is going to do, and on a plain terminal the honest answer is "nothing".
            return Forced
                ? $"{alternative} (TAB: {probed})"
                : $"{probed} (TAB: {alternative})";
        }
    }
}
