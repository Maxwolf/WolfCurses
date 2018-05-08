// Created by Ron 'Maxwolf' McDowell (ron.mcdowell@gmail.com) 
// Timestamp 12/31/2015@4:49 AM

using System;
using System.Text;

namespace WolfCurses.Window.Control
{
    /// <summary>
    ///     Progress bar that is drawn in characters and is a ping-pong marquee action bouncing back and fourth.
    /// </summary>
    public sealed class MarqueeBar
    {
        private string _bar;

        private readonly string _blankPointer;

        private int _counter;

        private Direction _currdir;

        private readonly string _pointer;

        /// <summary>
        ///     Initializes a new instance of the <see cref="MarqueeBar" /> class.
        /// </summary>
        public MarqueeBar()
        {
            _bar = "|                         |";
            _pointer = "***";
            _blankPointer = BlankPointer();
            _currdir = Direction.Right;
            _counter = 1;
        }

        /// <summary>
        ///     sets the attribute blankPointer with a empty string the same length that the pointer
        /// </summary>
        /// <returns>A string filled with space characters</returns>
        private string BlankPointer()
        {
            var blank = new StringBuilder();
            for (var cont = 0; cont < _pointer.Length; cont++)
                blank.Append(" ");
            return blank.ToString();
        }

        /// <summary>
        ///     reset the bar to its original state
        /// </summary>
        private void ClearBar()
        {
            _bar = _bar.Replace(_pointer, _blankPointer);
        }

        /// <summary>remove the previous pointer and place it in a new position</summary>
        /// <param name="start">start index</param>
        /// <param name="end">end index</param>
        private void PlacePointer(int start, int end)
        {
            ClearBar();
            _bar = _bar.Remove(start, end);
            _bar = _bar.Insert(start, _pointer);
        }

        /// <summary>
        ///     prints the progress bar according to pointers and current Direction
        /// </summary>
        /// <returns>
        ///     The <see cref="string" />.
        /// </returns>
        public string Step()
        {
            if (_currdir == Direction.Right)
            {
                PlacePointer(_counter, _pointer.Length);
                _counter++;
                if (_counter + _pointer.Length == _bar.Length)
                    _currdir = Direction.Left;
            }
            else
            {
                PlacePointer(_counter - _pointer.Length, _pointer.Length);
                _counter--;
                if (_counter == _pointer.Length)
                    _currdir = Direction.Right;
            }

            return _bar + Environment.NewLine;
        }

        /// <summary>
        ///     The direction.
        /// </summary>
        private enum Direction
        {
            /// <summary>
            ///     The right.
            /// </summary>
            Right,

            /// <summary>
            ///     The left.
            /// </summary>
            Left
        };
    }
}