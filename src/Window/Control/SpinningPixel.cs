// Created by Ron 'Maxwolf' McDowell (ron.mcdowell@gmail.com) 
// Timestamp 12/31/2015@4:49 AM

using System.Collections.Generic;

namespace WolfCurses.Window.Control
{
    /// <summary>
    ///     Old school spinning pixel progress, normally used to show the thread is not locked by some running process.
    /// </summary>
    internal sealed class SpinningPixel
    {
        private readonly List<string> _animation;

        private int _counter;

        /// <summary>
        ///     Initializes a new instance of the <see cref="SpinningPixel" /> class.
        /// </summary>
        public SpinningPixel()
        {
            _animation = new List<string> {"/", "-", @"\", "|"};
            _counter = 0;
        }

        /// <summary>
        ///     prints the character found in the animation according to the current index
        /// </summary>
        /// <returns>
        ///     The <see cref="string" />.
        /// </returns>
        public string Step()
        {
            var barText = _animation[_counter];
            _counter++;
            if (_counter == _animation.Count)
                _counter = 0;

            return barText;
        }
    }
}