// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/16/2026

using System;
using System.Threading;

namespace WolfCurses.Graphics
{
    /// <summary>
    ///     Holds the process-wide <see cref="IImageRenderer" /> used whenever an <see cref="AnsiImage" /> is drawn
    ///     without an explicit renderer. It starts out as <see cref="HalfBlockImageRenderer" /> — the safe choice that
    ///     works in any color terminal — and the first <see cref="SimulationApp" /> constructed upgrades it
    ///     automatically to whatever the terminal itself says it can draw (see <see cref="AutoDetect()" />), so an
    ///     application gets true-pixel pictures on a capable terminal without configuring anything. Assigning
    ///     <see cref="Default" /> yourself — before or after the simulation is created — is how an app overrides that
    ///     answer, and is how it opts into (or out of) a specific renderer:
    ///     <code>
    ///         ImageRenderers.Default = new SixelImageRenderer();
    ///     </code>
    ///     <para>
    ///         This mirrors <see cref="ImageDecoders" /> deliberately: the two seams of the graphics feature are
    ///         configured the same way, so knowing one teaches you the other.
    ///     </para>
    /// </summary>
    /// <seealso cref="ImageDecoders" />
    public static class ImageRenderers
    {
        private static IImageRenderer _default = new HalfBlockImageRenderer();

        /// <summary>
        ///     Whether the application ever assigned <see cref="Default" /> itself. A choice the host made, at any
        ///     moment, outranks the automatic one — <see cref="AutoDetect()" /> checks this and stands down.
        /// </summary>
        private static bool _defaultAssigned;

        /// <summary>
        ///     Nonzero once <see cref="AutoDetect()" /> has run (or decided not to). Interlocked rather than a bool so
        ///     two threads constructing simulations at once cannot both probe the terminal.
        /// </summary>
        private static int _autoDetectRan;

        /// <summary>
        ///     The renderer used by <see cref="AnsiImage" /> when the caller does not pass one explicitly. Never null;
        ///     assigning null throws so a mis-configured start-up fails loudly rather than at the first image drawn.
        ///     Assigning anything marks the choice as the application's own, which stops <see cref="AutoDetect()" />
        ///     from ever replacing it.
        /// </summary>
        public static IImageRenderer Default
        {
            get => _default;
            set
            {
                _default = value ?? throw new ArgumentNullException(nameof(value),
                    "The default image renderer cannot be null; assign a real IImageRenderer implementation.");
                _defaultAssigned = true;
            }
        }

        /// <summary>
        ///     Asks the terminal which graphics protocol it can draw with and installs the matching renderer as
        ///     <see cref="Default" /> — sixel or kitty where the terminal says so, otherwise leaving the half-block
        ///     default exactly as it was. <see cref="SimulationApp" />'s constructor calls this automatically, so a
        ///     consuming application never needs to: by the time the first window renders, images already draw the
        ///     best way this terminal allows, and a terminal that allows nothing special keeps drawing characters.
        ///     <para>
        ///         It runs at most once per process, and never overrules the application: if <see cref="Default" />
        ///         was ever assigned, that choice stands and this does nothing. So the only reasons to touch this
        ///         seam yourself are wanting a <em>different</em> renderer than the terminal's answer (assign
        ///         <see cref="Default" />) or drawing images <em>before</em> the simulation exists (call this first —
        ///         it is safe to call early, and the constructor's later call becomes a no-op).
        ///     </para>
        ///     <para>
        ///         The probe underneath (<see cref="AnsiConsole.ProbeGraphicsProtocol" />) reads the terminal's reply
        ///         off standard input, so this must run before the host's key-reading loop starts — which constructing
        ///         the simulation naturally is. It never throws: with input or output redirected, or a terminal that
        ///         answers nothing, it simply falls back to the environment's guess, and from there to half blocks.
        ///     </para>
        /// </summary>
        public static void AutoDetect()
        {
            AutoDetect(ProbeReadyTerminal);
        }

        /// <summary>
        ///     The decision behind <see cref="AutoDetect()" />, with the terminal conversation passed in so tests can
        ///     exercise the once-only and application-choice-wins rules without a terminal to talk to.
        /// </summary>
        /// <param name="probe">Asks the terminal (or pretends to) which protocol it understands.</param>
        internal static void AutoDetect(Func<AnsiGraphicsProtocolEnum> probe)
        {
            // Whatever happens below, it happens once: the first caller claims the flag and everyone after — every
            // further SimulationApp constructed in this process — finds it claimed and leaves immediately.
            if (Interlocked.Exchange(ref _autoDetectRan, 1) != 0)
                return;

            // The application already chose. Its reasons are its own; detection has nothing to add.
            if (_defaultAssigned)
                return;

            // None means the default already draws the right way, so the instance is left completely untouched —
            // deliberately not replaced with an equivalent one, so nothing observable changes on the terminals
            // (and test hosts) where detection finds nothing.
            var protocol = probe();
            if (protocol == AnsiGraphicsProtocolEnum.None)
                return;

            // Not the setter: this is the automatic answer filling in an unmade choice, not the application making
            // one, and recording it as the latter would be a lie the flag above acts on.
            _default = For(protocol);
        }

        /// <summary>True once <see cref="AutoDetect()" /> has run; lets tests see that start-up reached it.</summary>
        internal static bool AutoDetectHasRun => _autoDetectRan != 0;

        /// <summary>
        ///     Returns <see cref="AutoDetect()" /> and <see cref="Default" /> to their process-start state: half
        ///     blocks, no application choice recorded, detection not yet run. Test-only, and only safe there because
        ///     the tests that use it live in a collection that runs while nothing else does.
        /// </summary>
        internal static void ResetAutoDetectForTests()
        {
            _default = new HalfBlockImageRenderer();
            _defaultAssigned = false;
            _autoDetectRan = 0;
        }

        /// <summary>
        ///     The real terminal conversation: make sure escape sequences will be interpreted rather than printed,
        ///     then ask. Enabling first matters on classic conhost, where the probe's query would otherwise be drawn
        ///     on screen as garbage and its answer would never come — and it is gated on a real terminal being
        ///     attached at both ends, so a redirected or hostless process is not reconfigured for a conversation
        ///     that is not going to happen.
        /// </summary>
        private static AnsiGraphicsProtocolEnum ProbeReadyTerminal()
        {
            try
            {
                if (!Console.IsInputRedirected && !Console.IsOutputRedirected)
                    AnsiConsole.Enable();
            }
            catch
            {
                // A console that cannot even say whether it is redirected is one the probe will fall back on anyway.
            }

            return AnsiConsole.ProbeGraphicsProtocol();
        }

        /// <summary>
        ///     The best renderer for the terminal the application is actually running in, worked out from the
        ///     environment by <see cref="AnsiConsole.DetectGraphicsProtocol()" />. Assign it once at start-up and every
        ///     image drawn afterwards uses it:
        ///     <code>
        ///         ImageRenderers.Default = ImageRenderers.ForCurrentTerminal();
        ///     </code>
        ///     <para>
        ///         Where nothing better is known to be available this is <see cref="HalfBlockImageRenderer" />, which
        ///         works in any terminal that can show color at all — so this is always safe to call, including with
        ///         output redirected or on a console that has never heard of a graphics protocol.
        ///     </para>
        /// </summary>
        /// <seealso cref="AnsiConsole.ProbeGraphicsProtocol" />
        public static IImageRenderer ForCurrentTerminal()
        {
            return For(AnsiConsole.DetectGraphicsProtocol());
        }

        /// <summary>
        ///     The renderer that draws with a given protocol. Pass the result of
        ///     <see cref="AnsiConsole.ProbeGraphicsProtocol" /> to use the answer the terminal itself gave rather than
        ///     the environment's guess:
        ///     <code>
        ///         ImageRenderers.Default = ImageRenderers.For(AnsiConsole.ProbeGraphicsProtocol());
        ///     </code>
        /// </summary>
        /// <param name="protocol">
        ///     The protocol to draw with. <see cref="AnsiGraphicsProtocolEnum.None" /> — and any value that is not a
        ///     protocol this library knows — yields <see cref="HalfBlockImageRenderer" />, so a caller can hand this
        ///     whatever detection returned without checking it first.
        /// </param>
        public static IImageRenderer For(AnsiGraphicsProtocolEnum protocol)
        {
            switch (protocol)
            {
                case AnsiGraphicsProtocolEnum.Sixel:
                    return new SixelImageRenderer();
                case AnsiGraphicsProtocolEnum.Kitty:
                    return new KittyImageRenderer();
                default:
                    return new HalfBlockImageRenderer();
            }
        }
    }
}
