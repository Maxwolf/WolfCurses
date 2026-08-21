// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;

namespace WolfCurses.Apps.Basic
{
    /// <summary>
    ///     GET and PUT: lift a rectangle of the screen into an array, and stamp it back down somewhere else. What
    ///     every BASIC game moves a sprite with.
    ///     <para>
    ///         <b>The array holds a width, a height, and then one colour number per pixel.</b> The machines packed
    ///         the bits according to the video mode, and a program that reads those bytes back out by hand would
    ///         want that packing; nothing sensible does, and imitating it would tie the array's shape to hardware
    ///         this has none of. A program that DIMs its array too small is told so, which is what would have
    ///         happened anyway.
    ///     </para>
    ///     <para>
    ///         <b>PUT defaults to XOR, and that is not an arbitrary choice.</b> Drawing the same sprite twice in the
    ///         same place with XOR puts the screen back exactly as it was, which is how a sprite moves without
    ///         anything having to remember what was underneath it.
    ///     </para>
    /// </summary>
    public sealed class BasicImageStatement : BasicStatement
    {
        /// <summary>The array to read from or write to.</summary>
        private readonly string _array;

        /// <summary>The coordinates.</summary>
        private readonly IReadOnlyList<BasicExpression> _arguments;

        /// <summary>PSET, PRESET, AND, OR or XOR, for PUT.</summary>
        private readonly string _action;

        /// <summary>Whether this is a GET.</summary>
        private readonly bool _capture;

        /// <summary>Initializes a new instance of the <see cref="BasicImageStatement" /> class.</summary>
        /// <param name="capture">TRUE for GET, FALSE for PUT.</param>
        /// <param name="arguments">The coordinates.</param>
        /// <param name="array">The array to read from or write to.</param>
        /// <param name="action">How PUT combines the sprite with what is already there.</param>
        /// <param name="line">The source line.</param>
        public BasicImageStatement(bool capture, IReadOnlyList<BasicExpression> arguments, string array,
            string action, int line) : base(line)
        {
            _capture = capture;
            _arguments = arguments;
            _array = array;
            _action = string.IsNullOrEmpty(action) ? "XOR" : action;
        }

        /// <inheritdoc />
        public override int Execute(BasicRuntime runtime, int index)
        {
            if (_capture)
                Capture(runtime);
            else
                Stamp(runtime);

            return index + 1;
        }

        /// <summary>Lifts the rectangle into the array.</summary>
        private void Capture(BasicRuntime runtime)
        {
            var x0 = Argument(runtime, 0);
            var y0 = Argument(runtime, 1);
            var x1 = Argument(runtime, 2);
            var y1 = Argument(runtime, 3);

            var left = Math.Min(x0, x1);
            var top = Math.Min(y0, y1);
            var width = Math.Abs(x1 - x0) + 1;
            var height = Math.Abs(y1 - y0) + 1;

            runtime.WriteElement(_array, new[] {0}, new BasicValue(width), Line);
            runtime.WriteElement(_array, new[] {1}, new BasicValue(height), Line);

            var at = 2;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    runtime.WriteElement(_array, new[] {at}, new BasicValue(
                        runtime.Host.PixelAt(left + x, top + y)), Line);

                    at++;
                }
            }
        }

        /// <summary>Stamps the array back onto the screen.</summary>
        private void Stamp(BasicRuntime runtime)
        {
            var left = Argument(runtime, 0);
            var top = Argument(runtime, 1);

            var width = (int) runtime.ReadElement(_array, new[] {0}, Line).AsNumber(Line);
            var height = (int) runtime.ReadElement(_array, new[] {1}, Line).AsNumber(Line);

            if (width <= 0 || height <= 0)
                return;

            var at = 2;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var stored = (int) runtime.ReadElement(_array, new[] {at}, Line).AsNumber(Line);
                    at++;

                    var target = runtime.Host.PixelAt(left + x, top + y);
                    if (target < 0)
                        continue;

                    runtime.Host.Plot(left + x, top + y, Combine(target, stored));
                }
            }
        }

        /// <summary>How the sprite's colour and the screen's are put together.</summary>
        private int Combine(int screen, int sprite)
        {
            return _action switch
            {
                "PSET" => sprite,
                "PRESET" => 15 - sprite,
                "AND" => screen & sprite,
                "OR" => screen | sprite,
                _ => screen ^ sprite
            };
        }

        /// <summary>One coordinate, truncated to a pixel.</summary>
        private int Argument(BasicRuntime runtime, int position)
        {
            if (position >= _arguments.Count || _arguments[position] == null)
                return 0;

            return (int) Math.Truncate(_arguments[position].Evaluate(runtime).AsNumber(Line));
        }
    }
}
