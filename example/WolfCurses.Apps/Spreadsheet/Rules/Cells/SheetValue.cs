// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System.Globalization;

namespace WolfCurses.Apps.Spreadsheet
{
    /// <summary>
    ///     What a cell is worth: a number, some text, an error, or nothing.
    ///     <para>
    ///         <b>An error is an ordinary value here rather than an exception</b>, which is the decision that keeps
    ///         a sheet drawable. One cell dividing by zero must show <c>#DIV/0!</c> in that cell and leave the other
    ///         four hundred alone; throwing would take the whole screen down, and this screen is redrawn a thousand
    ///         times a second.
    ///     </para>
    /// </summary>
    public readonly struct SheetValue
    {
        /// <summary>Initializes a new instance of the <see cref="SheetValue" /> struct.</summary>
        /// <param name="kind">Which sort of value it is.</param>
        /// <param name="number">The number, when it is one.</param>
        /// <param name="text">The text, or the error's name.</param>
        private SheetValue(SheetValueKindEnum kind, double number, string text)
        {
            Kind = kind;
            Number = number;
            Text = text;
        }

        /// <summary>An empty cell.</summary>
        public static SheetValue Empty => new(SheetValueKindEnum.Empty, 0d, string.Empty);

        /// <summary>Which sort of value it is.</summary>
        public SheetValueKindEnum Kind { get; }

        /// <summary>The number, which is zero for anything that is not one.</summary>
        public double Number { get; }

        /// <summary>The text, or the error's name.</summary>
        public string Text { get; }

        /// <summary>Whether this is a number, which is what arithmetic and charts ask before using it.</summary>
        public bool IsNumber => Kind == SheetValueKindEnum.Number;

        /// <summary>Whether working it out went wrong.</summary>
        public bool IsError => Kind == SheetValueKindEnum.Error;

        /// <summary>Whether nothing was typed in the cell at all.</summary>
        public bool IsEmpty => Kind == SheetValueKindEnum.Empty;

        /// <summary>A number.</summary>
        /// <param name="number">Its value.</param>
        /// <returns>The value.</returns>
        public static SheetValue FromNumber(double number)
        {
            return new SheetValue(SheetValueKindEnum.Number, number, null);
        }

        /// <summary>Some text.</summary>
        /// <param name="text">The text.</param>
        /// <returns>The value.</returns>
        public static SheetValue FromText(string text)
        {
            return new SheetValue(SheetValueKindEnum.Text, 0d, text ?? string.Empty);
        }

        /// <summary>An error.</summary>
        /// <param name="name">Its short name, such as <c>#REF!</c>.</param>
        /// <returns>The value.</returns>
        public static SheetValue FromError(string name)
        {
            return new SheetValue(SheetValueKindEnum.Error, 0d, name);
        }

        /// <summary>
        ///     What the cell shows.
        ///     <para>
        ///         Numbers are formatted with the invariant culture rather than the machine's, which is the same
        ///         choice the file format makes: a sheet saved on one computer and opened on another must not
        ///         change what its numbers mean, and a decimal comma inside a comma separated file would do exactly
        ///         that.
        ///     </para>
        /// </summary>
        /// <returns>The text to draw.</returns>
        public string Display()
        {
            switch (Kind)
            {
                case SheetValueKindEnum.Number:
                    // Two decimal places at most, and none at all when the number is whole, so a column of round
                    // figures does not turn into a column of trailing zeroes.
                    return Number.ToString("0.##", CultureInfo.InvariantCulture);

                case SheetValueKindEnum.Empty:
                    return string.Empty;

                default:
                    return Text ?? string.Empty;
            }
        }

        /// <summary>What the cell shows, so a value can be used where a string is expected.</summary>
        /// <returns>The text to draw.</returns>
        public override string ToString()
        {
            return Display();
        }
    }
}
