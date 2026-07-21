using System;
using System.Collections.Generic;
using System.Linq;
using WolfCurses.Graphics;
using Xunit;

namespace WolfCurses.Tests.Graphics
{
    /// <summary>
    ///     Pins <see cref="ColorRamp" />: the smooth/stepped split, the band arithmetic, immutability, and the one
    ///     property the flag rendering is built on — <b>a stepped ramp of n stops sampled over exactly n items yields
    ///     the n stops in order</b>.
    ///     <para>
    ///         That property is not obvious and is not free. <see cref="ColorRamp.SampleIndex" /> maps item i to
    ///         i/(n-1) (end-inclusive, so the first item sits at 0 and the last at 1) while the stepped band
    ///         arithmetic multiplies by n. The two only line up because the fractional part i/(n-1) contributes stays
    ///         strictly inside a band for every interior item, and the last item's exact 1.0 clamps onto the last
    ///         stop. Any of that going wrong turns a flag into a flag with a stripe missing and a stripe doubled, so
    ///         it is checked over every stop count the presets use and then some.
    ///     </para>
    /// </summary>
    public class ColorRampTests
    {
        [Fact]
        public void Smooth_Endpoints_AreTheFirstAndLastStopExactly()
        {
            var ramp = ColorRamp.Smooth(new Rgb24(0, 0, 0), new Rgb24(255, 255, 255));

            Assert.Equal("#000000", Hex(ramp.Sample(0.0)));
            Assert.Equal("#FFFFFF", Hex(ramp.Sample(1.0)));
        }

        [Fact]
        public void Smooth_Midpoint_IsTheRoundedBlendOfTheTwoStops()
        {
            // 127.5 rounds away from zero to 128, not down to 127 — banker's rounding here would drift a gradient's
            // whole middle by one on every channel.
            var ramp = ColorRamp.Smooth(new Rgb24(0, 0, 0), new Rgb24(255, 255, 255));

            Assert.Equal("#808080", Hex(ramp.Sample(0.5)));
        }

        [Fact]
        public void Smooth_ThreeStops_LandsOnTheMiddleStopExactlyAtTheHalfway()
        {
            var ramp = ColorRamp.Smooth(new Rgb24(255, 0, 0), new Rgb24(0, 255, 0), new Rgb24(0, 0, 255));

            Assert.Equal("#FF0000", Hex(ramp.Sample(0.0)));
            Assert.Equal("#00FF00", Hex(ramp.Sample(0.5)));
            Assert.Equal("#0000FF", Hex(ramp.Sample(1.0)));
        }

        [Fact]
        public void Smooth_ThreeStops_InterpolatesBetweenTheTwoNeighboursOnly()
        {
            // A quarter of the way along a three-stop ramp is halfway between stops one and two, and the third stop
            // must not contribute at all — a ramp that averaged every stop would leak blue in here.
            var ramp = ColorRamp.Smooth(new Rgb24(255, 0, 0), new Rgb24(0, 255, 0), new Rgb24(0, 0, 255));

            Assert.Equal("#808000", Hex(ramp.Sample(0.25)));
        }

        [Fact]
        public void Smooth_OutOfRangePositions_AreClampedIntoTheRamp()
        {
            var ramp = ColorRamp.Smooth(new Rgb24(0, 0, 0), new Rgb24(255, 255, 255));

            Assert.Equal("#000000", Hex(ramp.Sample(-5.0)));
            Assert.Equal("#FFFFFF", Hex(ramp.Sample(5.0)));
        }

        [Theory]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NegativeInfinity)]
        public void Sample_NonFinitePosition_IsTreatedAsZeroRatherThanPropagatingGarbage(double t)
        {
            // A widget's scale arithmetic reaches NaN and infinity easily (a flat series divides by a zero range), and
            // a color built out of NaN is whatever the cast happens to do. Note infinity deliberately does NOT clamp
            // to the far end; every non-finite input means "no usable position", which is the ramp's start.
            var ramp = ColorRamp.Smooth(new Rgb24(0, 0, 0), new Rgb24(255, 255, 255));

            Assert.Equal("#000000", Hex(ramp.Sample(t)));
        }

        [Fact]
        public void SingleStop_AlwaysReturnsThatStopWhicheverKindOfRamp()
        {
            var smooth = ColorRamp.Smooth(new Rgb24(1, 2, 3));
            var stepped = ColorRamp.Stepped(new Rgb24(1, 2, 3));

            Assert.Equal("#010203", Hex(smooth.Sample(0.0)));
            Assert.Equal("#010203", Hex(smooth.Sample(0.5)));
            Assert.Equal("#010203", Hex(smooth.Sample(1.0)));
            Assert.Equal("#010203", Hex(stepped.Sample(1.0)));
        }

        [Fact]
        public void Stepped_CutsTheRangeIntoEqualBandsAndNeverInterpolates()
        {
            // Four stops means four quarter-wide bands, and the boundaries at .25/.50/.75 are exact in binary so this
            // asserts them directly. Only the stops themselves may ever come out.
            var ramp = ColorRamp.Stepped(
                new Rgb24(10, 0, 0), new Rgb24(20, 0, 0), new Rgb24(30, 0, 0), new Rgb24(40, 0, 0));

            Assert.Equal("#0A0000", Hex(ramp.Sample(0.0)));
            Assert.Equal("#0A0000", Hex(ramp.Sample(0.2)));
            Assert.Equal("#140000", Hex(ramp.Sample(0.25)));
            Assert.Equal("#140000", Hex(ramp.Sample(0.4)));
            Assert.Equal("#1E0000", Hex(ramp.Sample(0.5)));
            Assert.Equal("#1E0000", Hex(ramp.Sample(0.7)));
            Assert.Equal("#280000", Hex(ramp.Sample(0.75)));
            Assert.Equal("#280000", Hex(ramp.Sample(0.9)));
        }

        [Fact]
        public void Stepped_PositionOne_LandsOnTheLastStopRatherThanFallingOffTheEnd()
        {
            // t == 1 multiplies to exactly n, one past the last band. This is the single input the clamp exists for,
            // and without it a flag's last stripe would be an IndexOutOfRangeException at render time.
            var ramp = ColorRamp.Stepped(
                new Rgb24(10, 0, 0), new Rgb24(20, 0, 0), new Rgb24(30, 0, 0), new Rgb24(40, 0, 0));

            Assert.Equal("#280000", Hex(ramp.Sample(1.0)));
        }

        [Fact]
        public void Stepped_OutOfRangePositions_AreClampedToTheEndBands()
        {
            var ramp = ColorRamp.Stepped(new Rgb24(10, 0, 0), new Rgb24(20, 0, 0));

            Assert.Equal("#0A0000", Hex(ramp.Sample(-1.0)));
            Assert.Equal("#140000", Hex(ramp.Sample(2.0)));
        }

        [Fact]
        public void SampleIndex_SteppedRampOverExactlyItsOwnStopCount_YieldsTheStopsInOrder()
        {
            // The flag property, checked over every stop count the presets use (3, 4, 5, 6, 11) and every size either
            // side of them, because it is floating-point arithmetic that only happens to be exact.
            for (var n = 2; n <= 16; n++)
            {
                var stops = new Rgb24[n];
                for (var i = 0; i < n; i++)
                    stops[i] = new Rgb24((byte) (i * 10), 0, 0);

                var ramp = ColorRamp.Stepped(stops);

                for (var i = 0; i < n; i++)
                    Assert.Equal(Hex(stops[i]), Hex(ramp.SampleIndex(i, n)));
            }
        }

        [Fact]
        public void SampleIndex_EveryPridePreset_PaintsItsOwnStripesInOrder()
        {
            // The same property against the real presets, which is what a bar chart of Stops.Count rows will do.
            foreach (var ramp in PridePresets())
            {
                for (var i = 0; i < ramp.Stops.Count; i++)
                    Assert.Equal(Hex(ramp.Stops[i]), Hex(ramp.SampleIndex(i, ramp.Stops.Count)));
            }
        }

        [Fact]
        public void SampleIndex_FirstAndLastItem_SitAtTheVeryEndsOfTheRamp()
        {
            var ramp = ColorRamp.Smooth(new Rgb24(0, 0, 0), new Rgb24(255, 255, 255));

            Assert.Equal("#000000", Hex(ramp.SampleIndex(0, 5)));
            Assert.Equal("#FFFFFF", Hex(ramp.SampleIndex(4, 5)));
        }

        [Fact]
        public void SampleIndex_CountOfOneOrLess_TakesTheRampsStart()
        {
            // One item has nowhere to be spread across, and a zero or negative count is a caller bug that must not
            // become a divide by zero mid-frame.
            var ramp = ColorRamp.Smooth(new Rgb24(0, 0, 0), new Rgb24(255, 255, 255));

            Assert.Equal("#000000", Hex(ramp.SampleIndex(0, 1)));
            Assert.Equal("#000000", Hex(ramp.SampleIndex(0, 0)));
            Assert.Equal("#000000", Hex(ramp.SampleIndex(3, -2)));
        }

        [Fact]
        public void SampleIndex_IndexOutsideTheCount_IsClampedRatherThanThrowing()
        {
            var ramp = ColorRamp.Smooth(new Rgb24(0, 0, 0), new Rgb24(255, 255, 255));

            Assert.Equal("#FFFFFF", Hex(ramp.SampleIndex(99, 4)));
            Assert.Equal("#000000", Hex(ramp.SampleIndex(-99, 4)));
        }

        [Fact]
        public void Constructor_NoStops_ThrowsRatherThanBuildingARampWithNoAnswer()
        {
            // A preset that threw only when first sampled would be a broken singleton surfacing one frame at a time.
            Assert.Throws<ArgumentException>(() => new ColorRamp());
            Assert.Throws<ArgumentException>(() => ColorRamp.Smooth());
            Assert.Throws<ArgumentException>(() => ColorRamp.Stepped());
            Assert.Throws<ArgumentException>(() => new ColorRamp(new Rgb24[0], false));
        }

        [Fact]
        public void Constructor_NullStops_Throws()
        {
            Assert.Throws<ArgumentException>(() => ColorRamp.Smooth(null));
            Assert.Throws<ArgumentException>(() => new ColorRamp((IEnumerable<Rgb24>) null, false));
        }

        [Fact]
        public void Constructor_CopiesTheStopsSoLaterMutationOfTheCallersArrayCannotReachTheRamp()
        {
            // This is what makes the cached preset singletons safe to share across every widget in an application.
            var stops = new[] {new Rgb24(0, 0, 0), new Rgb24(255, 255, 255)};
            var ramp = ColorRamp.Smooth(stops);

            stops[0] = new Rgb24(255, 0, 0);

            Assert.Equal("#000000", Hex(ramp.Sample(0.0)));
            Assert.Equal("#000000", Hex(ramp.Stops[0]));
        }

        [Fact]
        public void Stops_AreExposedReadOnlyAndCannotBeWrittenThrough()
        {
            var ramp = ColorRamp.Smooth(new Rgb24(0, 0, 0), new Rgb24(255, 255, 255));

            var list = Assert.IsAssignableFrom<IList<Rgb24>>(ramp.Stops);

            Assert.True(list.IsReadOnly);
            Assert.Throws<NotSupportedException>(() => { list[0] = new Rgb24(255, 0, 0); });
        }

        [Fact]
        public void Stops_ReportTheOrderAndCountTheyWereBuiltWith()
        {
            var ramp = ColorRamp.Stepped(new Rgb24(1, 0, 0), new Rgb24(2, 0, 0), new Rgb24(3, 0, 0));

            Assert.Equal(3, ramp.Stops.Count);
            Assert.Equal("#010000", Hex(ramp.Stops[0]));
            Assert.Equal("#020000", Hex(ramp.Stops[1]));
            Assert.Equal("#030000", Hex(ramp.Stops[2]));
        }

        [Fact]
        public void ParamsConstructorAndFactories_SetTheRightKind()
        {
            Assert.False(new ColorRamp(new Rgb24(0, 0, 0), new Rgb24(1, 1, 1)).IsStepped);
            Assert.False(ColorRamp.Smooth(new Rgb24(0, 0, 0), new Rgb24(1, 1, 1)).IsStepped);
            Assert.True(ColorRamp.Stepped(new Rgb24(0, 0, 0), new Rgb24(1, 1, 1)).IsStepped);
            Assert.True(new ColorRamp(new[] {new Rgb24(0, 0, 0)}, true).IsStepped);
        }

        [Fact]
        public void FunctionalPresets_AreNonNullSmoothAndTheExpectedLength()
        {
            var presets = new (string Name, ColorRamp Ramp, int Stops)[]
            {
                ("Rainbow", ColorRamp.Rainbow, 7),
                ("Heat", ColorRamp.Heat, 5),
                ("Cool", ColorRamp.Cool, 4),
                ("Traffic", ColorRamp.Traffic, 3),
                ("Monochrome", ColorRamp.Monochrome, 2)
            };

            foreach (var preset in presets)
            {
                Assert.NotNull(preset.Ramp);
                Assert.Equal(preset.Stops, preset.Ramp.Stops.Count);
                Assert.False(preset.Ramp.IsStepped);
            }
        }

        [Fact]
        public void PridePresets_AreNonNullSteppedAndTheExpectedLength()
        {
            // Stepped because a flag has stripes: a smooth ramp would show the real colors only at the exact centre
            // of each band and read as a muddy smear everywhere else.
            var presets = new (string Name, ColorRamp Ramp, int Stops)[]
            {
                ("PrideRainbow", ColorRamp.PrideRainbow, 6),
                ("PrideProgress", ColorRamp.PrideProgress, 11),
                ("PrideTrans", ColorRamp.PrideTrans, 5),
                ("PrideBisexual", ColorRamp.PrideBisexual, 5),
                ("PrideLesbian", ColorRamp.PrideLesbian, 5),
                ("PridePansexual", ColorRamp.PridePansexual, 3),
                ("PrideAsexual", ColorRamp.PrideAsexual, 4),
                ("PrideNonBinary", ColorRamp.PrideNonBinary, 4),
                ("PrideDemisexual", ColorRamp.PrideDemisexual, 4),
                ("PrideAromantic", ColorRamp.PrideAromantic, 5),
                ("PrideAgender", ColorRamp.PrideAgender, 7),
                ("PrideGenderfluid", ColorRamp.PrideGenderfluid, 5),
                ("PrideGenderqueer", ColorRamp.PrideGenderqueer, 3),
                ("PridePolysexual", ColorRamp.PridePolysexual, 3),
                ("PrideOmnisexual", ColorRamp.PrideOmnisexual, 5),
                ("PrideAroace", ColorRamp.PrideAroace, 5),
                ("PrideAbrosexual", ColorRamp.PrideAbrosexual, 5)
            };

            foreach (var preset in presets)
            {
                Assert.NotNull(preset.Ramp);
                Assert.Equal(preset.Stops, preset.Ramp.Stops.Count);
                Assert.True(preset.Ramp.IsStepped);
            }
        }

        [Fact]
        public void Presets_AreCachedSingletonsRatherThanARampPerCall()
        {
            // They sit on the render path of every frame, and a ramp is immutable, so one instance is as good as a
            // thousand.
            Assert.Same(ColorRamp.Rainbow, ColorRamp.Rainbow);
            Assert.Same(ColorRamp.Traffic, ColorRamp.Traffic);
            Assert.Same(ColorRamp.PrideRainbow, ColorRamp.PrideRainbow);
            Assert.Same(ColorRamp.PrideProgress, ColorRamp.PrideProgress);
        }

        [Fact]
        public void PrideRainbow_TakesBlueAndVioletFromTheSameConvention()
        {
            // The two ends of this ramp are the ones that go wrong, and they go wrong together: 004DFF pairs with
            // 750787 (the pre-2023 Wikimedia Commons file, and what the Progress SVG still carries), while the navy
            // 24408E pairs with 732982 (the 2017 Philadelphia flag). One from each column is a combination no source
            // publishes — which is exactly what this ramp shipped with before this test existed.
            var stops = ColorRamp.PrideRainbow.Stops;

            Assert.Equal("#E40303", Hex(stops[0]));
            Assert.Equal("#FF8C00", Hex(stops[1]));
            Assert.Equal("#FFED00", Hex(stops[2]));
            Assert.Equal("#008026", Hex(stops[3]));
            Assert.Equal("#004DFF", Hex(stops[4]));
            Assert.Equal("#750787", Hex(stops[5]));
        }

        [Fact]
        public void PrideProgress_RainbowBandMatchesPrideRainbowExactly()
        {
            // The two ramps are sourced from sibling Commons SVGs, so a "fix" applied to one and not the other shows
            // up on screen as two different violets in the same demo.
            var rainbow = ColorRamp.PrideRainbow.Stops;
            var progress = ColorRamp.PrideProgress.Stops;

            for (var i = 0; i < rainbow.Count; i++)
                Assert.Equal(Hex(rainbow[i]), Hex(progress[progress.Count - rainbow.Count + i]));
        }

        [Fact]
        public void PrideProgress_ChevronDoesNotReuseTheTransFlagsHexValues()
        {
            // The chevron tints genuinely differ from the trans flag's, so copying those over is a real way to get
            // this subtly wrong. Whether the difference was intended is undocumented (Quasar's usage PDF is offline
            // and reportedly quoted no hex at all) — this pins the values, not a claim about intent.
            var progress = ColorRamp.PrideProgress.Stops;
            var trans = ColorRamp.PrideTrans.Stops;

            Assert.Equal("#000000", Hex(progress[0]));
            Assert.Equal("#613915", Hex(progress[1]));
            Assert.Equal("#74D7EE", Hex(progress[2]));
            Assert.Equal("#FFAFC8", Hex(progress[3]));
            Assert.Equal("#FFFFFF", Hex(progress[4]));

            Assert.NotEqual(Hex(trans[0]), Hex(progress[2]));
            Assert.NotEqual(Hex(trans[1]), Hex(progress[3]));
        }

        [Fact]
        public void PrideProgress_StacksTheChevronAboveAnIntactRainbow()
        {
            var progress = ColorRamp.PrideProgress.Stops;
            var rainbow = ColorRamp.PrideRainbow.Stops;

            for (var i = 0; i < rainbow.Count; i++)
                Assert.Equal(Hex(rainbow[i]), Hex(progress[5 + i]));
        }

        [Fact]
        public void PrideTrans_IsPalindromicSoItIsCorrectWhicheverWayUpItIsFlown()
        {
            var stops = ColorRamp.PrideTrans.Stops;

            Assert.Equal("#5BCEFA", Hex(stops[0]));
            Assert.Equal("#F5A9B8", Hex(stops[1]));
            Assert.Equal("#FFFFFF", Hex(stops[2]));
            Assert.Equal(Hex(stops[1]), Hex(stops[3]));
            Assert.Equal(Hex(stops[0]), Hex(stops[4]));
        }

        [Fact]
        public void PrideBisexual_DuplicatesItsOuterStopsToKeepTheNarrowLavenderBand()
        {
            // The flag is 2:1:2 and the narrow lavender IS the meaning — it is the overlap of the pink and the blue.
            // A stepped ramp only knows equal bands, so duplicating the outer stops is how it expresses unequal ones;
            // five rows renders it exactly.
            var stops = ColorRamp.PrideBisexual.Stops;

            Assert.Equal("#D60270", Hex(stops[0]));
            Assert.Equal("#D60270", Hex(stops[1]));
            Assert.Equal("#9B4F96", Hex(stops[2]));
            Assert.Equal("#0038A8", Hex(stops[3]));
            Assert.Equal("#0038A8", Hex(stops[4]));
        }

        [Fact]
        public void PrideLesbian_UsesTheFiveStripeOrangeNotTheSevenStripeBurntOne()
        {
            // EF7627 belongs to Emily Gwen's seven-stripe original; the five-stripe reduction drops that stripe and
            // leaves FF9A56, a visibly different soft peach.
            var stops = ColorRamp.PrideLesbian.Stops;

            Assert.Equal("#D52D00", Hex(stops[0]));
            Assert.Equal("#FF9A56", Hex(stops[1]));
            Assert.Equal("#FFFFFF", Hex(stops[2]));
            Assert.Equal("#D362A4", Hex(stops[3]));
            Assert.Equal("#A30262", Hex(stops[4]));
        }

        [Fact]
        public void PridePansexualAsexualAndNonBinary_CarryTheirCanonicalStripes()
        {
            var pansexual = ColorRamp.PridePansexual.Stops;
            Assert.Equal("#FF218C", Hex(pansexual[0]));
            Assert.Equal("#FFD800", Hex(pansexual[1]));
            Assert.Equal("#21B1FF", Hex(pansexual[2]));

            var asexual = ColorRamp.PrideAsexual.Stops;
            Assert.Equal("#000000", Hex(asexual[0]));
            Assert.Equal("#A3A3A3", Hex(asexual[1]));
            Assert.Equal("#FFFFFF", Hex(asexual[2]));
            Assert.Equal("#800080", Hex(asexual[3])); // exactly CSS purple

            // The Wikimedia Commons SVG values (Kye Rowan, 2014), not the alt convention that differs by 1-2/255.
            var nonBinary = ColorRamp.PrideNonBinary.Stops;
            Assert.Equal("#FFF433", Hex(nonBinary[0]));
            Assert.Equal("#FFFFFF", Hex(nonBinary[1]));
            Assert.Equal("#9B59D0", Hex(nonBinary[2]));
            Assert.Equal("#2D2D2D", Hex(nonBinary[3]));
        }

        [Fact]
        public void PrideDemisexual_StacksTheBlackTriangleAboveWhitePurpleGrey()
        {
            // The triangle cannot be drawn in bands, so it is stacked first exactly as the Progress chevron is.
            var stops = ColorRamp.PrideDemisexual.Stops;

            Assert.Equal("#000000", Hex(stops[0])); // triangle
            Assert.Equal("#FFFFFF", Hex(stops[1]));
            Assert.Equal("#6E0070", Hex(stops[2]));
            Assert.Equal("#D2D2D2", Hex(stops[3]));
        }

        [Fact]
        public void PrideDemisexual_DoesNotBorrowTheAsexualFlagsPurpleOrGrey()
        {
            // The flag shares the ace flag's symbolism but not its hex — 6E0070 not 800080, D2D2D2 not A3A3A3 — and
            // conflating them is the documented, common way to get it wrong. Pinned against the asexual flag itself so
            // a future "these are both purple, unify them" edit fails here.
            var demi = ColorRamp.PrideDemisexual.Stops;
            var ace = ColorRamp.PrideAsexual.Stops;

            Assert.Equal("#6E0070", Hex(demi[2]));
            Assert.Equal("#D2D2D2", Hex(demi[3]));
            Assert.NotEqual(Hex(ace[3]), Hex(demi[2])); // ace purple 800080 != demi purple
            Assert.NotEqual(Hex(ace[1]), Hex(demi[3])); // ace grey A3A3A3 != demi grey
        }

        [Fact]
        public void TheNewStripeFlags_CarryTheirCanonicalColorsInOrder()
        {
            var aromantic = ColorRamp.PrideAromantic.Stops;
            Assert.Equal(new[] {"#3DA542", "#A7D379", "#FFFFFF", "#A9A9A9", "#000000"},
                aromantic.Select(Hex).ToArray());

            // Vertically symmetric: the outer three colors mirror around the central green.
            var agender = ColorRamp.PrideAgender.Stops;
            Assert.Equal(new[] {"#000000", "#B9B9B9", "#FFFFFF", "#B8F483", "#FFFFFF", "#B9B9B9", "#000000"},
                agender.Select(Hex).ToArray());

            var genderfluid = ColorRamp.PrideGenderfluid.Stops;
            Assert.Equal(new[] {"#FF76A4", "#FFFFFF", "#C011D7", "#000000", "#2F3CBE"},
                genderfluid.Select(Hex).ToArray());

            var genderqueer = ColorRamp.PrideGenderqueer.Stops;
            Assert.Equal(new[] {"#B57EDC", "#FFFFFF", "#4A8123"}, genderqueer.Select(Hex).ToArray());

            var polysexual = ColorRamp.PridePolysexual.Stops;
            Assert.Equal(new[] {"#F61CB9", "#07D569", "#1C92F6"}, polysexual.Select(Hex).ToArray());

            // The dark middle stripe is the near-black indigo 200044, not the too-red 260046 that also circulates.
            var omnisexual = ColorRamp.PrideOmnisexual.Stops;
            Assert.Equal(new[] {"#FF9BCD", "#FF53BE", "#200044", "#665EFF", "#8CA6FF"},
                omnisexual.Select(Hex).ToArray());

            var aroace = ColorRamp.PrideAroace.Stops;
            Assert.Equal(new[] {"#E28C00", "#ECCD00", "#FFFFFF", "#62AEDC", "#203856"},
                aroace.Select(Hex).ToArray());

            var abrosexual = ColorRamp.PrideAbrosexual.Stops;
            Assert.Equal(new[] {"#65C286", "#B4E4CC", "#FFFFFF", "#E796B7", "#D9446E"},
                abrosexual.Select(Hex).ToArray());
        }

        [Fact]
        public void Traffic_SweepsGreenThroughAmberToRedAcrossTheRange()
        {
            // The one ramp whose meaning everybody already knows, which is why a Level gauge wearing it needs no
            // legend — so the direction it runs in is contract, not decoration.
            Assert.Equal("#00C853", Hex(ColorRamp.Traffic.Sample(0.0)));
            Assert.Equal("#FFD600", Hex(ColorRamp.Traffic.Sample(0.5)));
            Assert.Equal("#D50000", Hex(ColorRamp.Traffic.Sample(1.0)));
        }

        [Fact]
        public void Monochrome_SurvivesGrayscaleUnchangedBecauseItIsAlreadyGray()
        {
            Assert.Equal("#000000", Hex(ColorRamp.Monochrome.Sample(0.0)));
            Assert.Equal("#FFFFFF", Hex(ColorRamp.Monochrome.Sample(1.0)));
        }

        [Fact]
        public void PrideBisexual_ShowsEveryColorItCanFitAtAnyRowCount()
        {
            // Stop count is per flag: this one needs five stops to say three colors, because the lavender band is a
            // narrow 2:1:2 overlap rather than a third of the flag. That is not something a caller has to work
            // around — a layout with fewer rows than the ramp has stops still shows as many of its colors as the
            // space allows, so the only thing an exact multiple of Stops.Count buys is even stripe thickness.
            var ramp = ColorRamp.PrideBisexual;
            var colors = ramp.Stops.Select(Hex).Distinct().ToList();

            Assert.Equal(5, ramp.Stops.Count);
            Assert.Equal(new[] {"#D60270", "#9B4F96", "#0038A8"}, colors);

            // Three rows is the fewest that can show all three, and it does — the narrow middle stop is not lost
            // just because the ramp declares it once and its neighbours twice.
            Assert.Equal(colors, Enumerable.Range(0, 3).Select(i => Hex(ramp.SampleIndex(i, 3))).ToList());

            // Five rows is the flag proper: 2:1:2, in order.
            Assert.Equal(new[] {"#D60270", "#D60270", "#9B4F96", "#0038A8", "#0038A8"},
                Enumerable.Range(0, 5).Select(i => Hex(ramp.SampleIndex(i, 5))).ToArray());

            // And nothing ever comes back out of order or off the ends, however little room there is.
            for (var rows = 1; rows <= 12; rows++)
            {
                var drawn = Enumerable.Range(0, rows).Select(i => Hex(ramp.SampleIndex(i, rows))).ToList();
                Assert.All(drawn, c => Assert.Contains(c, colors));
                Assert.Equal(drawn.OrderBy(c => colors.IndexOf(c)).ToList(), drawn);
            }
        }

        /// <summary>The eight flag presets, as the property-based tests want them.</summary>
        private static IEnumerable<ColorRamp> PridePresets()
        {
            yield return ColorRamp.PrideRainbow;
            yield return ColorRamp.PrideProgress;
            yield return ColorRamp.PrideTrans;
            yield return ColorRamp.PrideBisexual;
            yield return ColorRamp.PrideLesbian;
            yield return ColorRamp.PridePansexual;
            yield return ColorRamp.PrideAsexual;
            yield return ColorRamp.PrideNonBinary;
            yield return ColorRamp.PrideDemisexual;
            yield return ColorRamp.PrideAromantic;
            yield return ColorRamp.PrideAgender;
            yield return ColorRamp.PrideGenderfluid;
            yield return ColorRamp.PrideGenderqueer;
            yield return ColorRamp.PridePolysexual;
            yield return ColorRamp.PrideOmnisexual;
            yield return ColorRamp.PrideAroace;
            yield return ColorRamp.PrideAbrosexual;
        }

        /// <summary>Renders a triple as an uppercase hex string, which gives readable assertion failures.</summary>
        private static string Hex(Rgb24 color)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }
    }
}
