// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System;
using WolfCurses.Window;

namespace WolfCurses.Controls
{
    /// <summary>
    ///     Holds the message, button set, and result callback of a <see cref="MessageBox" />. It is the
    ///     <see cref="WindowData" /> of the <see cref="MessageBoxWindow" />.
    /// </summary>
    public sealed class MessageBoxData : WindowData
    {
        /// <summary>The message shown to the user.</summary>
        public string Message { get; private set; }

        /// <summary>Which buttons are offered.</summary>
        public MessageBoxButtons Buttons { get; private set; }

        /// <summary>Invoked with the pressed button when the user answers.</summary>
        public Action<MessageBoxResult> OnResult { get; private set; }

        /// <summary>True once <see cref="Initialize" /> has run.</summary>
        public bool Initialized { get; private set; }

        /// <summary>Configures the dialog.</summary>
        public void Initialize(string message, MessageBoxButtons buttons, Action<MessageBoxResult> onResult)
        {
            Message = message ?? string.Empty;
            Buttons = buttons;
            OnResult = onResult;
            Initialized = true;
        }
    }
}
