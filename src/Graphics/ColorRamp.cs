// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/20/2026

using System;
using System.Collections.Generic;

namespace WolfCurses.Graphics
{
    /// <summary>
    ///     An ordered list of color stops that a position between 0 and 1 can be looked up in. This is how the
    ///     <see cref="WolfCurses.Window.Control" /> widgets paint something other than one flat color: a progress bar
    ///     that runs green to red as it fills, a chart whose rows each take the next stripe of a flag, a sparkline
    ///     whose glyphs warm up with their value.
    ///     <para>
    ///         A ramp is either <em>smooth</em> — the stops are waypoints and everything between them is interpolated,
    ///         which is what a heat map or a gradient wants — or <em>stepped</em>, where the stops are the only colors
    ///         that ever come out and the range is cut into that many equal bands. Stepped is what a flag needs: a
    ///         flag has stripes, not a gradient, and interpolating between them would produce a muddy smear with the
    ///         actual colors visible only at the exact band centres.
    ///     </para>
    ///     <para>
    ///         <b>Instances are immutable</b> — the stops are copied in at construction and never handed out as
    ///         anything a caller can write through. That is what makes the static presets below safe to share as
    ///         cached singletons across every widget in an application, including across threads, rather than
    ///         rebuilding a private copy per bar per frame.
    ///     </para>
    /// </summary>
    public sealed class ColorRamp
    {
        /// <summary>The stops, owned outright by this instance so nothing outside can change them.</summary>
        private readonly Rgb24[] _stops;

        /// <summary>
        ///     Initializes a new smooth instance of the <see cref="ColorRamp" /> class, interpolating between the
        ///     given stops.
        /// </summary>
        /// <param name="stops">At least one color stop, in order from position 0 to position 1.</param>
        /// <exception cref="ArgumentException">No stops were given.</exception>
        public ColorRamp(params Rgb24[] stops) : this(stops, false)
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="ColorRamp" /> class.
        /// </summary>
        /// <param name="stops">At least one color stop, in order from position 0 to position 1.</param>
        /// <param name="stepped">
        ///     True to return only the stops themselves, one per equal-width band; false to interpolate between them.
        /// </param>
        /// <exception cref="ArgumentException">No stops were given.</exception>
        public ColorRamp(IEnumerable<Rgb24> stops, bool stepped)
        {
            // An empty ramp has no answer to give Sample, and a preset that threw only when first sampled would be a
            // broken singleton waiting to surface a frame at a time. Refuse it at construction instead.
            if (stops == null)
                throw new ArgumentException("A color ramp needs at least one stop.", nameof(stops));

            var copied = new List<Rgb24>(stops);
            if (copied.Count == 0)
                throw new ArgumentException("A color ramp needs at least one stop.", nameof(stops));

            _stops = copied.ToArray();
            Stops = Array.AsReadOnly(_stops);
            IsStepped = stepped;
        }

        /// <summary>The stops in order, read-only — the ramp keeps its own copy and this cannot write through to it.</summary>
        public IReadOnlyList<Rgb24> Stops { get; }

        /// <summary>
        ///     True when only the stops themselves are ever produced, each occupying an equal-width band of the
        ///     range; false when positions between stops are interpolated.
        /// </summary>
        public bool IsStepped { get; }

        /// <summary>Builds a smooth ramp that interpolates between the given stops.</summary>
        /// <param name="stops">At least one color stop, in order from position 0 to position 1.</param>
        /// <exception cref="ArgumentException">No stops were given.</exception>
        public static ColorRamp Smooth(params Rgb24[] stops)
        {
            return new ColorRamp(stops, false);
        }

        /// <summary>Builds a stepped ramp that produces only the given stops, one per equal-width band.</summary>
        /// <param name="stops">At least one color stop, in order from position 0 to position 1.</param>
        /// <exception cref="ArgumentException">No stops were given.</exception>
        public static ColorRamp Stepped(params Rgb24[] stops)
        {
            return new ColorRamp(stops, true);
        }

        /// <summary>
        ///     The color at a position along the ramp.
        /// </summary>
        /// <param name="t">
        ///     The position, 0 for the first stop and 1 for the last. Values outside that are clamped into it, and a
        ///     non-finite value (a division that produced NaN or an infinity, which is easy to arrive at from a
        ///     widget's scale arithmetic) is treated as 0 rather than propagating garbage into a color.
        /// </param>
        /// <returns>The interpolated color, or for a stepped ramp the stop whose band contains the position.</returns>
        public Rgb24 Sample(double t)
        {
            if (!double.IsFinite(t))
                t = 0.0;

            t = Math.Clamp(t, 0.0, 1.0);

            var count = _stops.Length;
            if (count == 1)
                return _stops[0];

            if (IsStepped)
            {
                // n equal-width bands. Multiplying by n rather than n-1 is what makes the bands equal; the clamp is
                // there for exactly one input, t == 1, which lands on n and belongs to the last band.
                var band = (int) (t * count);
                return _stops[Math.Clamp(band, 0, count - 1)];
            }

            var position = t * (count - 1);
            var lower = (int) Math.Floor(position);
            if (lower < 0)
                lower = 0;
            if (lower > count - 1)
                lower = count - 1;

            var upper = lower + 1;
            if (upper > count - 1)
                upper = count - 1;

            var fraction = position - lower;
            var a = _stops[lower];
            var b = _stops[upper];
            return new Rgb24(Mix(a.R, b.R, fraction), Mix(a.G, b.G, fraction), Mix(a.B, b.B, fraction));
        }

        /// <summary>
        ///     The color for item <paramref name="index" /> of <paramref name="count" /> items spread across the whole
        ///     ramp. The form the widgets actually use, because "row 3 of 11" is the question a chart asks and
        ///     working the fraction out at each call site is where off-by-one errors live.
        ///     <para>
        ///         The arithmetic is deliberately end-inclusive — the first item sits at 0 and the last at 1 — which
        ///         gives the property the flag rendering depends on: <b>a stepped ramp of n stops, sampled over
        ///         exactly n items, yields the n stops in order</b>. Item i maps to i/(n-1), which the band
        ///         arithmetic turns into i + i/(n-1); the fractional part is strictly between 0 and 1 for every
        ///         interior item, so it truncates back to i, and the last item's exact 1.0 clamps onto the last stop.
        ///     </para>
        /// </summary>
        /// <param name="index">Which item, counting from zero.</param>
        /// <param name="count">How many items there are in total.</param>
        /// <returns>The color for that item.</returns>
        public Rgb24 SampleIndex(int index, int count)
        {
            // One item (or a nonsensical count) has nowhere to be spread across, so it takes the ramp's start.
            if (count <= 1)
                return Sample(0.0);

            return Sample(index / (double) (count - 1));
        }

        /// <summary>Rounds one channel of a linear blend between two stops.</summary>
        private static byte Mix(byte from, byte to, double fraction)
        {
            var value = from + (to - from) * fraction;
            return (byte) Math.Round(value, MidpointRounding.AwayFromZero);
        }

        // Functional presets. Cached singletons rather than properties that build a ramp per call: a ramp is
        // immutable, so one instance is as good as a thousand, and these sit on the render path of every frame.

        private static readonly ColorRamp _rainbow = Smooth(
            new Rgb24(0xFF, 0x00, 0x00), // red
            new Rgb24(0xFF, 0x7F, 0x00), // orange
            new Rgb24(0xFF, 0xFF, 0x00), // yellow
            new Rgb24(0x00, 0xFF, 0x00), // green
            new Rgb24(0x00, 0x00, 0xFF), // blue
            new Rgb24(0x4B, 0x00, 0x82), // indigo
            new Rgb24(0x94, 0x00, 0xD3)); // violet

        private static readonly ColorRamp _heat = Smooth(
            new Rgb24(0x00, 0x00, 0x00), // cold: black
            new Rgb24(0xFF, 0x00, 0x00), // red
            new Rgb24(0xFF, 0xA5, 0x00), // orange
            new Rgb24(0xFF, 0xFF, 0x00), // yellow
            new Rgb24(0xFF, 0xFF, 0xFF)); // hot: white

        private static readonly ColorRamp _cool = Smooth(
            new Rgb24(0x00, 0xFF, 0xFF), // cyan
            new Rgb24(0x00, 0x88, 0xFF), // sky
            new Rgb24(0x00, 0x00, 0xFF), // blue
            new Rgb24(0x4B, 0x00, 0x82)); // indigo

        private static readonly ColorRamp _traffic = Smooth(
            new Rgb24(0x00, 0xC8, 0x53), // go
            new Rgb24(0xFF, 0xD6, 0x00), // caution
            new Rgb24(0xD5, 0x00, 0x00)); // stop

        private static readonly ColorRamp _monochrome = Smooth(
            new Rgb24(0x00, 0x00, 0x00),
            new Rgb24(0xFF, 0xFF, 0xFF));

        /// <summary>The full spectrum, red through violet. Decorative; use it with <see cref="ColorRampModeEnum.Spread" />.</summary>
        public static ColorRamp Rainbow => _rainbow;

        /// <summary>
        ///     Black through red and orange to white, the classic black-body heat map. Reads as "more is hotter", so
        ///     it belongs on a <see cref="ColorRampModeEnum.Level" /> gauge.
        /// </summary>
        public static ColorRamp Heat => _heat;

        /// <summary>Cyan through blue to indigo, the cold counterpart to <see cref="Heat" />.</summary>
        public static ColorRamp Cool => _cool;

        /// <summary>
        ///     Green through amber to red. The one ramp whose meaning everybody already knows, which is why a
        ///     <see cref="ColorRampModeEnum.Level" /> gauge wearing it needs no legend.
        /// </summary>
        public static ColorRamp Traffic => _traffic;

        /// <summary>
        ///     Black to white. Useful when color would be noise but a gradient still carries the shape — and the one
        ///     preset that survives <see cref="AnsiColorModeEnum.Grayscale" /> unchanged.
        /// </summary>
        public static ColorRamp Monochrome => _monochrome;

        // Pride flag palettes. NB: none of these have a formal standards-body spec — the values are the dominant
        // community convention, sourced from the Wikimedia Commons SVGs and flagcolorcodes.com. Where two conventions
        // exist they usually differ by 1-2/255, which is invisible once a terminal quantises it; the disagreements
        // that ARE visible are noted on the flag concerned. All of these are stepped: a flag has stripes, and a
        // smooth ramp would show the actual colors only at the exact centre of each band.

        private static readonly ColorRamp _prideRainbow = Stepped(
            new Rgb24(0xE4, 0x03, 0x03), // red
            new Rgb24(0xFF, 0x8C, 0x00), // orange
            new Rgb24(0xFF, 0xED, 0x00), // yellow
            new Rgb24(0x00, 0x80, 0x26), // green
            new Rgb24(0x00, 0x4D, 0xFF), // blue
            new Rgb24(0x75, 0x07, 0x87)); // violet

        private static readonly ColorRamp _prideProgress = Stepped(
            new Rgb24(0x00, 0x00, 0x00), // chevron: black
            new Rgb24(0x61, 0x39, 0x15), // chevron: brown
            new Rgb24(0x74, 0xD7, 0xEE), // chevron: light blue
            new Rgb24(0xFF, 0xAF, 0xC8), // chevron: pink
            new Rgb24(0xFF, 0xFF, 0xFF), // chevron: white
            new Rgb24(0xE4, 0x03, 0x03), // red
            new Rgb24(0xFF, 0x8C, 0x00), // orange
            new Rgb24(0xFF, 0xED, 0x00), // yellow
            new Rgb24(0x00, 0x80, 0x26), // green
            new Rgb24(0x00, 0x4D, 0xFF), // blue
            new Rgb24(0x75, 0x07, 0x87)); // violet

        private static readonly ColorRamp _prideTrans = Stepped(
            new Rgb24(0x5B, 0xCE, 0xFA), // light blue
            new Rgb24(0xF5, 0xA9, 0xB8), // pink
            new Rgb24(0xFF, 0xFF, 0xFF), // white
            new Rgb24(0xF5, 0xA9, 0xB8), // pink
            new Rgb24(0x5B, 0xCE, 0xFA)); // light blue

        private static readonly ColorRamp _prideBisexual = Stepped(
            new Rgb24(0xD6, 0x02, 0x70), // magenta (upper two fifths)
            new Rgb24(0xD6, 0x02, 0x70), // magenta
            new Rgb24(0x9B, 0x4F, 0x96), // lavender (the middle fifth)
            new Rgb24(0x00, 0x38, 0xA8), // royal blue (lower two fifths)
            new Rgb24(0x00, 0x38, 0xA8)); // royal blue

        private static readonly ColorRamp _prideLesbian = Stepped(
            new Rgb24(0xD5, 0x2D, 0x00), // dark orange
            new Rgb24(0xFF, 0x9A, 0x56), // light orange
            new Rgb24(0xFF, 0xFF, 0xFF), // white
            new Rgb24(0xD3, 0x62, 0xA4), // pink
            new Rgb24(0xA3, 0x02, 0x62)); // dark rose

        private static readonly ColorRamp _pridePansexual = Stepped(
            new Rgb24(0xFF, 0x21, 0x8C), // magenta
            new Rgb24(0xFF, 0xD8, 0x00), // yellow
            new Rgb24(0x21, 0xB1, 0xFF)); // cyan

        private static readonly ColorRamp _prideAsexual = Stepped(
            new Rgb24(0x00, 0x00, 0x00), // black
            new Rgb24(0xA3, 0xA3, 0xA3), // grey
            new Rgb24(0xFF, 0xFF, 0xFF), // white
            new Rgb24(0x80, 0x00, 0x80)); // purple

        private static readonly ColorRamp _prideNonBinary = Stepped(
            new Rgb24(0xFF, 0xF4, 0x33), // yellow
            new Rgb24(0xFF, 0xFF, 0xFF), // white
            new Rgb24(0x9B, 0x59, 0xD0), // purple
            new Rgb24(0x2D, 0x2D, 0x2D)); // black

        private static readonly ColorRamp _prideDemisexual = Stepped(
            new Rgb24(0x00, 0x00, 0x00), // black triangle, stacked first like the Progress chevron
            new Rgb24(0xFF, 0xFF, 0xFF), // white
            new Rgb24(0x6E, 0x00, 0x70), // purple — demisexual-specific, NOT the asexual flag's 800080
            new Rgb24(0xD2, 0xD2, 0xD2)); // gray — D2D2D2, NOT the asexual flag's A3A3A3

        private static readonly ColorRamp _prideAromantic = Stepped(
            new Rgb24(0x3D, 0xA5, 0x42), // dark green
            new Rgb24(0xA7, 0xD3, 0x79), // light green
            new Rgb24(0xFF, 0xFF, 0xFF), // white
            new Rgb24(0xA9, 0xA9, 0xA9), // grey (CSS darkgray)
            new Rgb24(0x00, 0x00, 0x00)); // black

        private static readonly ColorRamp _prideAgender = Stepped(
            new Rgb24(0x00, 0x00, 0x00), // black
            new Rgb24(0xB9, 0xB9, 0xB9), // gray
            new Rgb24(0xFF, 0xFF, 0xFF), // white
            new Rgb24(0xB8, 0xF4, 0x83), // mint green (the center of a vertically symmetric flag)
            new Rgb24(0xFF, 0xFF, 0xFF), // white
            new Rgb24(0xB9, 0xB9, 0xB9), // gray
            new Rgb24(0x00, 0x00, 0x00)); // black

        private static readonly ColorRamp _prideGenderfluid = Stepped(
            new Rgb24(0xFF, 0x76, 0xA4), // pink
            new Rgb24(0xFF, 0xFF, 0xFF), // white
            new Rgb24(0xC0, 0x11, 0xD7), // magenta (usually called "purple")
            new Rgb24(0x00, 0x00, 0x00), // black
            new Rgb24(0x2F, 0x3C, 0xBE)); // blue

        private static readonly ColorRamp _prideGenderqueer = Stepped(
            new Rgb24(0xB5, 0x7E, 0xDC), // lavender
            new Rgb24(0xFF, 0xFF, 0xFF), // white
            new Rgb24(0x4A, 0x81, 0x23)); // dark chartreuse green

        private static readonly ColorRamp _pridePolysexual = Stepped(
            new Rgb24(0xF6, 0x1C, 0xB9), // pink
            new Rgb24(0x07, 0xD5, 0x69), // green
            new Rgb24(0x1C, 0x92, 0xF6)); // blue

        private static readonly ColorRamp _prideOmnisexual = Stepped(
            new Rgb24(0xFF, 0x9B, 0xCD), // light pink
            new Rgb24(0xFF, 0x53, 0xBE), // pink
            new Rgb24(0x20, 0x00, 0x44), // dark indigo (200044, not the also-seen 260046)
            new Rgb24(0x66, 0x5E, 0xFF), // blue
            new Rgb24(0x8C, 0xA6, 0xFF)); // light blue

        private static readonly ColorRamp _prideAroace = Stepped(
            new Rgb24(0xE2, 0x8C, 0x00), // orange
            new Rgb24(0xEC, 0xCD, 0x00), // yellow
            new Rgb24(0xFF, 0xFF, 0xFF), // white
            new Rgb24(0x62, 0xAE, 0xDC), // light blue
            new Rgb24(0x20, 0x38, 0x56)); // dark blue

        private static readonly ColorRamp _prideAbrosexual = Stepped(
            new Rgb24(0x65, 0xC2, 0x86), // dark green
            new Rgb24(0xB4, 0xE4, 0xCC), // light green
            new Rgb24(0xFF, 0xFF, 0xFF), // white
            new Rgb24(0xE7, 0x96, 0xB7), // light pink
            new Rgb24(0xD9, 0x44, 0x6E)); // dark pink

        /// <summary>
        ///     The six-stripe rainbow pride flag (1979 to present): red, orange, yellow, green, blue, violet.
        ///     <para>
        ///         <b>Blue and violet travel together and must not be mixed across conventions.</b> This ramp uses
        ///         <c>004DFF</c>/<c>750787</c>, the pair Wikimedia Commons' <c>Gay_Pride_Flag.svg</c> carried from
        ///         2011 until April 2023 and the pair its Progress Pride SVG still carries — so the two ramps here
        ///         agree with each other and with the values the wider web scraped over those twelve years. The
        ///         widely-seen navy <c>24408E</c> pairs with violet <c>732982</c>, and both belong to the 2017
        ///         Philadelphia "More Color, More Pride" flag (the Gay Flag of South Africa reuses that same
        ///         palette, which is why it often gets the credit). Taking the blue from one set and the violet from
        ///         the other yields a combination no source actually publishes — the exact mistake this ramp shipped
        ///         with for a day.
        ///     </para>
        ///     <para>
        ///         Commons recolored its six-stripe file in April 2023 to <c>E50000 FF8D00 FFEE00 028121 004CFF
        ///         770088</c>. That set is deliberately <em>not</em> used here: it would disagree with the rainbow
        ///         band inside <see cref="PrideProgress" /> (never recolored), and its pedigree is weaker than what
        ///         it replaced — the file's own talk page records the earlier values being derived by averaging
        ///         colors out of street photographs, and the replacement sourced to a blog post. Don't "helpfully"
        ///         sync this to Commons.
        ///     </para>
        ///     <para>
        ///         Gilbert Baker's own 1978 eight-stripe flag uses an entirely different and much more muted
        ///         Pantone-derived palette; the two sets are not interchangeable.
        ///     </para>
        /// </summary>
        public static ColorRamp PrideRainbow => _prideRainbow;

        /// <summary>
        ///     The Progress Pride flag (Daniel Quasar, 2018), rendered as eleven horizontal stripes.
        ///     <para>
        ///         The chevron's light blue and pink are <c>74D7EE</c>/<c>FFAFC8</c>, which differ slightly from
        ///         <see cref="PrideTrans" />'s <c>5BCEFA</c>/<c>F5A9B8</c> — so copying the trans values here is a
        ///         real (and common) way to get the flag subtly wrong. Whether the difference was <em>intended</em>
        ///         is undocumented, and this doc used to claim it was: the values come from the Wikimedia Commons
        ///         SVG, built in turn from Quasar's usage PDF, which is now offline and which contemporaneous
        ///         references say quoted no hex or Pantone values at all. Quasar's own description says the trans
        ///         flag's stripes were <em>moved</em> into the chevron, which if anything argues for sameness. The
        ///         values are kept because they are what every downstream table carries; the reason is not claimed.
        ///     </para>
        ///     <para>
        ///         A chevron cannot be drawn with horizontal bands at all, so this stacks the chevron's own
        ///         outer-to-inner reading order (black at the flag's edge through to white at the arrow's point) above
        ///         the rainbow. That ordering is a rendering convention of this library, not part of Quasar's design,
        ///         though it happens to reproduce the Philadelphia flag's black-and-brown-above-red logic. The design
        ///         is released CC0, so there is no attribution obligation — crediting Quasar is simply good manners.
        ///     </para>
        /// </summary>
        public static ColorRamp PrideProgress => _prideProgress;

        /// <summary>
        ///     The transgender pride flag (Monica Helms, 1999): light blue, pink, white, pink, light blue. The
        ///     palindromic order is intentional — Helms designed it so it is correct whichever way up it is flown.
        /// </summary>
        public static ColorRamp PrideTrans => _prideTrans;

        /// <summary>
        ///     The bisexual pride flag (Michael Page, 1998): magenta, lavender, royal blue.
        ///     <para>
        ///         Three colors but <b>five</b> stops, because the flag's proportions are 2:1:2 and the narrow
        ///         lavender band is the entire meaning of the design — it is the overlap of the pink and the blue.
        ///         Widening it to a third would make this read as a generic three-stripe flag. Duplicating the outer
        ///         stops is simply how a stepped ramp, which only knows equal bands, expresses unequal ones.
        ///     </para>
        ///     <para>
        ///         So stop count is <em>per flag</em>, and callers should read <see cref="Stops" /> rather than
        ///         assume any particular number: this one needs five to say what it means where
        ///         <see cref="PridePansexual" /> needs three. That is not a special case to work around — a ramp
        ///         drawn into fewer rows than it has stops still shows as many of its colors as the space allows
        ///         (three rows here give all three colors; five give the true 2:1:2), so the only thing an exact
        ///         multiple of <c>Stops.Count</c> buys is stripes of even thickness.
        ///     </para>
        /// </summary>
        public static ColorRamp PrideBisexual => _prideBisexual;

        /// <summary>
        ///     The five-stripe lesbian pride flag: dark orange, light orange, white, pink, dark rose.
        ///     <para>
        ///         The seven-stripe original is Emily Gwen's (2018); the five-stripe reduction used here is credited
        ///         to the Tumblr user taqwomen. Dropping stripes two and six leaves <c>FF9A56</c> as the surviving
        ///         orange — the burnt <c>EF7627</c> belongs to the seven-stripe version and looks visibly wrong here.
        ///         Note this is the most widely adopted lesbian flag rather than the only one.
        ///     </para>
        /// </summary>
        public static ColorRamp PrideLesbian => _prideLesbian;

        /// <summary>
        ///     The pansexual pride flag (anonymous, before 2010): magenta, yellow, cyan, in three equal stripes.
        ///     Because it began life as a JPEG, slightly resampled variants circulate; these are the cleaned-up
        ///     canonical values.
        /// </summary>
        public static ColorRamp PridePansexual => _pridePansexual;

        /// <summary>
        ///     The asexual pride flag (AVEN community vote, 2010): black, grey, white, purple, in four equal stripes.
        ///     Its purple is exactly CSS <c>purple</c>, which happens to land cleanly on the xterm-256 palette.
        /// </summary>
        public static ColorRamp PrideAsexual => _prideAsexual;

        /// <summary>
        ///     The non-binary pride flag (Kye Rowan, 2014): yellow, white, purple, black, in four equal stripes.
        ///     <para>
        ///         The values here are the Wikimedia Commons SVG's, for consistency with the other flags in this
        ///         class; a widely circulated alternative convention differs by one or two per channel, which is
        ///         invisible after quantisation. Do not confuse this with the genderqueer flag (Marilyn Roxie, 2011),
        ///         which is a different flag and is commonly attributed here by mistake.
        ///     </para>
        /// </summary>
        public static ColorRamp PrideNonBinary => _prideNonBinary;

        /// <summary>
        ///     The demisexual pride flag: a black triangle over white, purple and grey stripes.
        ///     <para>
        ///         Like <see cref="PrideProgress" /> this flag has an element bands cannot draw — a solid black
        ///         triangle on the hoist — so it is stacked first as a single stripe, then the three horizontal
        ///         stripes top to bottom (white, purple, grey). The real flag's stripes are unequal (a thin middle
        ///         purple); four equal stops keep every color but not that proportion.
        ///     </para>
        ///     <para>
        ///         <b>It borrows the asexual flag's symbolism but not its exact hex, and that is the trap.</b> The
        ///         purple is <c>6E0070</c>, not the asexual flag's <c>800080</c>, and the grey is <c>D2D2D2</c>, not
        ///         its <c>A3A3A3</c> — several palette sites conflate the two. The flag is community-authored with no
        ///         single documented designer.
        ///     </para>
        /// </summary>
        public static ColorRamp PrideDemisexual => _prideDemisexual;

        /// <summary>
        ///     The aromantic pride flag (Cameron Whimsy, 2014): dark green, light green, white, grey, black in five
        ///     equal stripes.
        ///     <para>
        ///         This is the current five-stripe flag, not the earlier four-stripe green/yellow/orange/black it
        ///         replaced — a common misattribution. Values are the Wikimedia Commons SVG's (<c>3DA542</c>,
        ///         <c>A7D379</c>, grey the CSS <c>darkgray</c> <c>A9A9A9</c>); Wikipedia's infobox lists a variant
        ///         differing by a point or two that no SVG source actually publishes.
        ///     </para>
        /// </summary>
        public static ColorRamp PrideAromantic => _prideAromantic;

        /// <summary>
        ///     The agender pride flag (Salem X, 2014): black, grey, white, mint green, then white, grey, black — seven
        ///     stripes, vertically symmetric, so it reads the same either way up.
        ///     <para>
        ///         The grey <c>B9B9B9</c> and green <c>B8F483</c> are the Wikimedia Commons SVG's; flagcolorcodes.com
        ///         is the outlier here with raster-sampled approximations (<c>BCC4C7</c>/<c>B7F684</c>) that are not
        ///         the source values. The green is deliberately the complement of a gender-associated purple.
        ///     </para>
        /// </summary>
        public static ColorRamp PrideAgender => _prideAgender;

        /// <summary>
        ///     The genderfluid pride flag (JJ Poole, 2012): pink, white, magenta, black, blue in five equal stripes.
        ///     <para>
        ///         These are the vivid community-convention values (<c>FF76A4</c>, <c>C011D7</c>, <c>2F3CBE</c>) that
        ///         flagcolorcodes.com and schemecolor.com agree on. The Wikimedia Commons SVG carries muted
        ///         near-variants instead (off-white and near-black rather than pure <c>FFFFFF</c>/<c>000000</c>); the
        ///         vivid set is used to sit with the rest of this class. The middle stripe is a magenta despite being
        ///         universally called "purple".
        ///     </para>
        /// </summary>
        public static ColorRamp PrideGenderfluid => _prideGenderfluid;

        /// <summary>
        ///     The genderqueer pride flag (Marilyn Roxie, 2011): lavender, white, dark chartreuse green in three equal
        ///     stripes. <b>Not</b> the non-binary flag (<see cref="PrideNonBinary" />, Kye Rowan, 2014), which is the
        ///     one it is most often confused with. The green is <c>4A8123</c> from the Commons SVG.
        /// </summary>
        public static ColorRamp PrideGenderqueer => _prideGenderqueer;

        /// <summary>
        ///     The polysexual pride flag (Tumblr user samlin, 2012): pink, green, blue in three equal stripes.
        ///     <para>
        ///         Values are the Wikimedia Commons SVG's (<c>F61CB9</c>, <c>07D569</c>, <c>1C92F6</c>);
        ///         flagcolorcodes.com and schemecolor.com carry a brighter competing sampling that is off by more than
        ///         quantization would hide. Not the polyamory flag (a different design) or the pansexual flag
        ///         (<see cref="PridePansexual" />, pink/yellow/cyan).
        ///     </para>
        /// </summary>
        public static ColorRamp PridePolysexual => _pridePolysexual;

        /// <summary>
        ///     The omnisexual pride flag (Pastelmemer, 2015): light pink, pink, dark indigo, blue, light blue in five
        ///     equal stripes.
        ///     <para>
        ///         Nearly every stripe has competing conventions; the outer four here follow the Wikimedia Commons SVG
        ///         and the dark middle stripe is <c>200044</c> (flagcolorcodes.com and colorswall.com), not the
        ///         slightly-too-red <c>260046</c> that also circulates. The middle is a very dark blue-violet, not
        ///         black. Designed in 2015, not the 2018 sometimes given.
        ///     </para>
        /// </summary>
        public static ColorRamp PrideOmnisexual => _prideOmnisexual;

        /// <summary>
        ///     The aromantic-asexual (aroace) pride flag: orange, yellow, white, light blue, dark blue in five equal
        ///     stripes. Values are the Wikimedia Commons SVG's (<c>E28C00</c>, <c>ECCD00</c>, <c>62AEDC</c>,
        ///     <c>203856</c>), matched exactly by flagcolorcodes.com; a differently-sampled Tumblr palette circulates
        ///     and should not be mixed in.
        /// </summary>
        public static ColorRamp PrideAroace => _prideAroace;

        /// <summary>
        ///     The abrosexual pride flag: dark green, light green, white, light pink, dark pink in five equal stripes.
        ///     <para>
        ///         Community-authored (mogai/Tumblr, around 2015) with no reliably documented designer. The green is
        ///         <c>65C286</c> from the Wikimedia Commons SVG — the file the Wikipedia article uses — not the lighter
        ///         <c>76CB93</c> flagcolorcodes.com and color-hex publish. The Commons SVG stores its fills
        ///         bottom-to-top, which is reversed here to read top-to-bottom.
        ///     </para>
        /// </summary>
        public static ColorRamp PrideAbrosexual => _prideAbrosexual;
    }
}
