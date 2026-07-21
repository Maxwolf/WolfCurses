// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/20/2026

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WolfCurses.Graphics;
using WolfCurses.Utility;
using WolfCurses.Window;
using WolfCurses.Window.Control;
using WolfCurses.Window.Form;

namespace WolfCurses.Example.Demos
{
    /// <summary>
    ///     Every pride flag the library ships a <see cref="ColorRamp" /> for, drawn one at a time. The LEFT and RIGHT
    ///     arrow keys walk the list and ENTER returns to the menu.
    ///     <para>
    ///         <b>Nothing here is a flag renderer.</b> Each flag is an ordinary <see cref="BarChart" /> with its labels
    ///         empty, its values all equal, its separator turned off and its values hidden — which leaves nothing on
    ///         the row but a full-width bar — carrying the flag's ramp in
    ///         <see cref="ColorRampModeEnum.Spread" />, where row <c>i</c> of <c>n</c> takes
    ///         <see cref="ColorRamp.SampleIndex" />. A stepped ramp answers that with one stop per band, so the chart
    ///         lays its stripes out in order and the picture falls out of the widget the example app already had. That
    ///         is the point worth taking away: <em>Spread over a stepped ramp is a flag</em>, and this screen is the
    ///         proof rather than a special case built beside it.
    ///     </para>
    ///     <para>
    ///         Several rows are drawn per stripe (as many as the console has room for, up to four) because one row per
    ///         stripe reads as a chart with a color problem — a flag needs the bands to have some height before the eye
    ///         stops counting them and starts seeing a flag. The row count is always an exact multiple of the stop
    ///         count so no stripe ends up a row thicker than its neighbours.
    ///     </para>
    ///     <para>
    ///         On a terminal that reports no color at all — <c>NO_COLOR</c>, a pipe, an old console — the widgets emit
    ///         no escape sequences whatsoever and this screen honestly degrades to a solid block of glyphs. It says so
    ///         rather than leaving the reader to wonder, since a colorless pride flag is the one failure mode this demo
    ///         cannot show its way out of.
    ///     </para>
    /// </summary>
    [ParentWindow(typeof (ExampleWindow))]
    public sealed class PrideFlagDialog : Form<ExampleWindowInfo>
    {
        /// <summary>Most rows any one stripe is drawn with, however tall the console is.</summary>
        private const int MaximumRowsPerStripe = 4;

        /// <summary>
        ///     The flags, in the order the arrow keys walk them. Notes credit the designer and the year, which for
        ///     these flags is not decoration: several are commonly misattributed, and the ramps themselves carry the
        ///     longer story about which hex values are the right ones.
        /// </summary>
        private static readonly Flag[] _flags =
        {
            new Flag("Rainbow", "Six-stripe rainbow — Gilbert Baker, 1978; this six-color form since 1979.",
                ColorRamp.PrideRainbow),
            new Flag("Progress Pride",
                "Daniel Quasar, 2018 (CC0). The chevron cannot be drawn in bands, so it is stacked on top.",
                ColorRamp.PrideProgress),
            new Flag("Transgender",
                "Monica Helms, 1999. Palindromic on purpose: correct whichever way up it is flown.",
                ColorRamp.PrideTrans),
            new Flag("Bisexual",
                "Michael Page, 1998. Three colors over five stops: the lavender band is a narrow 2:1:2 overlap.",
                ColorRamp.PrideBisexual),
            new Flag("Lesbian", "Five-stripe form; seven-stripe original by Emily Gwen, 2018, reduced by taqwomen.",
                ColorRamp.PrideLesbian),
            new Flag("Pansexual", "Anonymous, before 2010. Three equal stripes.", ColorRamp.PridePansexual),
            new Flag("Asexual", "AVEN community vote, 2010. Four equal stripes.", ColorRamp.PrideAsexual),
            new Flag("Non-binary", "Kye Rowan, 2014 — not the genderqueer flag, which is Marilyn Roxie's, 2011.",
                ColorRamp.PrideNonBinary),
            new Flag("Demisexual",
                "Community-authored. Black triangle stacked like the Progress chevron; its purple is not the ace flag's.",
                ColorRamp.PrideDemisexual),
            new Flag("Aromantic", "Cameron Whimsy, 2014 — the five-stripe flag, not the older four-stripe.",
                ColorRamp.PrideAromantic),
            new Flag("Aromantic Asexual", "The aroace flag: two warm stripes over two blues.",
                ColorRamp.PrideAroace),
            new Flag("Genderqueer", "Marilyn Roxie, 2011 — the flag the non-binary one is most often confused with.",
                ColorRamp.PrideGenderqueer),
            new Flag("Genderfluid", "JJ Poole, 2012. Vivid community values; the Commons SVG uses muted near-variants.",
                ColorRamp.PrideGenderfluid),
            new Flag("Agender", "Salem X, 2014. Seven stripes, vertically symmetric — correct either way up.",
                ColorRamp.PrideAgender),
            new Flag("Polysexual", "Tumblr user samlin, 2012. Not the polyamory or the pansexual flag.",
                ColorRamp.PridePolysexual),
            new Flag("Omnisexual", "Pastelmemer, 2015. The dark middle stripe is a near-black indigo, not black.",
                ColorRamp.PrideOmnisexual),
            new Flag("Abrosexual", "Community-authored, around 2015. Green through white to pink.",
                ColorRamp.PrideAbrosexual)
        };

        /// <summary>The screen's heading. Bold and a mid cyan, both legible on a light terminal and a dark one.</summary>
        private static readonly TextStyle _headingStyle = new TextStyle(new TextColor(ConsoleColor.DarkCyan), null,
            true);

        /// <summary>The note under each flag.</summary>
        private static readonly TextStyle _noteStyle = new TextStyle(new TextColor(ConsoleColor.DarkCyan));

        /// <summary>The position counter and the key hints, which should read as quieter than the flag.</summary>
        private static readonly TextStyle _dimStyle = new TextStyle(new TextColor(ConsoleColor.DarkGray));

        /// <summary>
        ///     The one chart every flag is drawn with, reconfigured in place rather than rebuilt per flag. Everything
        ///     that makes it look like data is switched off here; only the width, the ramp and the rows change.
        /// </summary>
        private readonly BarChart _chart = new BarChart
        {
            ShowValues = false,
            ShowTrack = false,
            Separator = string.Empty,
            RampMode = ColorRampModeEnum.Spread
        };

        /// <summary>
        ///     The frame around the flag, titled with its name. The border is deliberately a quiet gray rather than
        ///     one of the flag's own colors — a frame borrowing a stripe's color reads as a fourteenth stripe.
        /// </summary>
        private readonly Box _frame = new Box
        {
            BorderStyle = ConsoleColor.DarkGray,
            TitleStyle = new TextStyle(bold: true)
        };

        private bool _colorless;
        private string _current = string.Empty;
        private int _index;

        /// <summary>Initializes a new instance of the <see cref="PrideFlagDialog" /> class.</summary>
        /// <param name="window">The parent window.</param>
        // ReSharper disable once UnusedMember.Global
        public PrideFlagDialog(IWindow window) : base(window)
        {
        }

        /// <inheritdoc />
        public override void OnFormPostCreate()
        {
            base.OnFormPostCreate();

            // Asked once, here, rather than per frame: the answer is process-cached anyway and it is only used to
            // decide whether to print a caveat, which is not a question worth re-asking a thousand times a second.
            _colorless = AnsiConsole.DetectColorMode() == AnsiColorModeEnum.None;

            Build();

            ParentWindow.PromptText = "LEFT/RIGHT arrow keys change the flag, ENTER or ESC returns to the menu";
        }

        /// <inheritdoc />
        public override void OnKeyPressed(ConsoleKey key)
        {
            base.OnKeyPressed(key);

            // An arrow key carries no character, so it never lands in the input buffer and can only arrive here.
            // ENTER and BACKSPACE never do — the input manager consumes them as buffer control before a key press is
            // ever queued — which is why leaving is handled in OnInputBufferReturned and not by a case below.
            switch (key)
            {
                case ConsoleKey.LeftArrow:
                    _index = (_index + _flags.Length - 1) % _flags.Length;
                    break;
                case ConsoleKey.RightArrow:
                    _index = (_index + 1) % _flags.Length;
                    break;
                default:
                    return;
            }

            Build();
        }

        /// <inheritdoc />
        public override string OnRenderForm()
        {
            // A pure read of what Build already composed: rendering a flag means asking the chart for a few dozen
            // colored rows, and doing that in here would do it on every one of the ~1000 system ticks a second.
            var flag = _flags[_index];
            var width = ProseWidth();

            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine(Wrap(_headingStyle, "Pride flags — every one of them is a BarChart", width));
            sb.AppendLine();
            sb.AppendLine(_current);
            sb.AppendLine(Wrap(_noteStyle, flag.Note, width));

            if (_colorless)
                sb.AppendLine(Wrap(_dimStyle,
                    "This terminal reports no color support, so the widgets emit no escapes and the stripes are bare.",
                    width));

            sb.Append(Wrap(_dimStyle, string.Format(CultureInfo.InvariantCulture,
                "[{0} of {1}]  LEFT/RIGHT changes the flag, ENTER returns to the menu",
                _index + 1, _flags.Length), width));

            return sb.ToString();
        }

        /// <inheritdoc />
        public override void OnInputBufferReturned(string input)
        {
            // Any submitted line (ENTER) closes the demo and returns to the menu.
            ClearForm();
        }

        /// <summary>
        ///     Composes the currently selected flag once, so every frame until the next arrow key is a string copy.
        ///     <para>
        ///         The row count is <c>stripes × rowsPerStripe</c> and never anything else. That matters more than it
        ///         looks: <see cref="ColorRamp.SampleIndex" /> spreads the rows end-inclusively across the ramp, and an
        ///         exact multiple is what makes every band come out the same number of rows thick — a row count that
        ///         merely happened to be close would leave one stripe a row fatter than the rest, which on a flag is
        ///         immediately visible even though on a chart it would be invisible.
        ///     </para>
        /// </summary>
        private void Build()
        {
            var flag = _flags[_index];
            var stripes = flag.Ramp.Stops.Count;

            // The Progress flag is eleven stripes tall, so the height budget has to be shared out rather than fixed:
            // a console with room for two rows each still shows the whole flag, where a hard-coded three would run
            // the bottom off the screen.
            var available = Math.Max(stripes, SafeWindowHeight() - 12);
            var rowsPerStripe = Math.Clamp(available / stripes, 1, MaximumRowsPerStripe);

            _chart.Width = Math.Clamp(SafeWindowWidth() - 10, 20, 72);
            _chart.Ramp = flag.Ramp;

            // Equal values, so every bar is the full width and the chart is nothing but color. The label is empty on
            // every row, which collapses the label column to zero and leaves the stripes flush against the border.
            var rows = new List<BarChartValue>(stripes * rowsPerStripe);
            for (var row = 0; row < stripes * rowsPerStripe; row++)
                rows.Add(new BarChartValue(string.Empty, 1d));

            _frame.Title = flag.Name;

            // The box measures its content ignoring escape sequences, which is the only reason a frame can be drawn
            // around several kilobytes of color and still come out square.
            _current = _frame.Render(_chart.Render(rows));
        }

        /// <summary>
        ///     How wide the prose under the flag is allowed to be. The flag itself has been sized to the console since
        ///     the first draft; the sentences beside it were fixed literals, several of them 90-odd characters, and
        ///     <see cref="WolfCurses.ConsolePresenter" /> deliberately turns auto-wrap off for the frame — so on an
        ///     80-column console those rows were not reflowed, they were <em>truncated</em>, and the bisexual flag's
        ///     note lost the "2:1:2" it exists to explain. Two columns of slack rather than one so the row never ends
        ///     exactly on the last cell.
        /// </summary>
        private static int ProseWidth()
        {
            return Math.Max(20, SafeWindowWidth() - 2);
        }

        /// <summary>
        ///     Word-wraps a line of prose to the console and styles it one row at a time.
        ///     <para>
        ///         Wrapping first and styling second is the whole point: an escape sequence has length but no width,
        ///         so wrapping already-styled text would count its bytes as characters, and a style opened before the
        ///         wrap would bleed across the <see cref="Environment.NewLine" /> the wrap inserts.
        ///     </para>
        /// </summary>
        /// <param name="style">The style each produced row is wrapped in.</param>
        /// <param name="text">The prose to wrap.</param>
        /// <param name="width">The widest a row may be.</param>
        /// <returns>The wrapped, styled rows, with no trailing newline.</returns>
        private static string Wrap(TextStyle style, string text, int width)
        {
            var sb = new StringBuilder();
            foreach (var line in text.WordWrap(width).Split(Environment.NewLine))
            {
                if (line.Length == 0)
                    continue;

                if (sb.Length > 0)
                    sb.AppendLine();

                sb.Append(style.Apply(line));
            }

            return sb.ToString();
        }

        private static int SafeWindowWidth()
        {
            try
            {
                return Console.WindowWidth;
            }
            catch
            {
                return 80;
            }
        }

        private static int SafeWindowHeight()
        {
            try
            {
                return Console.WindowHeight;
            }
            catch
            {
                return 24;
            }
        }

        /// <summary>One entry in the list: what the flag is called, who made it, and the ramp that draws it.</summary>
        private readonly struct Flag
        {
            public Flag(string name, string note, ColorRamp ramp)
            {
                Name = name;
                Note = note;
                Ramp = ramp;
            }

            /// <summary>The flag's name, used as the box title.</summary>
            public string Name { get; }

            /// <summary>A single line of credit and context shown under the flag.</summary>
            public string Note { get; }

            /// <summary>The stepped ramp whose stops are the flag's stripes, top to bottom.</summary>
            public ColorRamp Ramp { get; }
        }
    }
}
