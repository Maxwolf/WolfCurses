// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 12/31/2015@4:49 AM

using System;
using System.Reflection;

namespace WolfCurses.Window.Menu
{
    /// <summary>
    ///     Defines a choice in the dynamic action selection system for a given game mode. This is intended to be used by a
    ///     wrapper for menu choices that aggregates all of the possible actions a given game mode can make while it is active
    ///     in the simulation.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class MenuChoice<T> : IMenuChoice<T>, IEquatable<MenuChoice<T>>
        where T : struct, IComparable, IFormattable, IConvertible
    {
        /// <summary>Initializes a new instance of the <see cref="MenuChoice{T}" /> class.</summary>
        /// <param name="command">The command.</param>
        /// <param name="action">The action.</param>
        /// <param name="description">The description.</param>
        /// <exception cref="InvalidCastException"></exception>
        public MenuChoice(T command, Action action, string description)
        {
            // Complain the generics implemented is not of an enum type.
            if (!typeof (T).GetTypeInfo().IsEnum)
            {
                throw new InvalidCastException("T must be an enumerated type!");
            }

            Command = command;
            Action = action;
            Description = description;
        }

        /// <summary>
        ///     Gets or sets the command.
        /// </summary>
        public T Command { get; set; }

        /// <summary>
        ///     Gets or sets the description.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        ///     Gets or sets the action.
        /// </summary>
        public Action Action { get; set; }

        /// <summary>
        ///     Two menu choices are the same choice when they map the same command value; description and action are
        ///     presentation details of that command, not identity.
        /// </summary>
        public bool Equals(MenuChoice<T> other)
        {
            if (other == null)
                return false;

            return Command.Equals(other.Command);
        }

        /// <summary>Determines whether the specified object is equal to the current menu choice.</summary>
        /// <param name="obj">The object to compare with the current menu choice.</param>
        /// <returns>TRUE if the specified object is a menu choice for the same command.</returns>
        public override bool Equals(object obj)
        {
            return Equals(obj as MenuChoice<T>);
        }

        /// <summary>
        ///     Serves as a hash function for a particular type.
        /// </summary>
        /// <returns>
        ///     A hash code for the current menu choice.
        /// </returns>
        public override int GetHashCode()
        {
            return Command.GetHashCode();
        }
    }
}