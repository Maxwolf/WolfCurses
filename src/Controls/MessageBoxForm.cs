// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System;
using System.Collections.Generic;
using System.Text;
using WolfCurses.Window;
using WolfCurses.Window.Control;
using WolfCurses.Window.Form;

namespace WolfCurses.Controls
{
    /// <summary>
    ///     Draws the message (framed with a <see cref="Box" />) with a button bar under it, and turns the user's
    ///     answer into a <see cref="MessageBoxResultEnum" />. Two answering styles are both always live: Left/Right
    ///     move a highlight along the buttons and ENTER presses the highlighted one (the first button is highlighted
    ///     from the start, so a bare ENTER takes the dialog's default), while the typed style keeps its original
    ///     meanings — an OK dialog is dismissed by any submitted line, a yes/no dialog accepts Y/N (and spelled-out
    ///     variants), a yes/no/cancel dialog also C.
    /// </summary>
    [ParentWindow(typeof (MessageBoxWindow))]
    public sealed class MessageBoxForm : Form<MessageBoxData>
    {
        /// <summary>
        ///     The Left/Right highlight along the button bar. Horizontal on purpose: the vertical axis stays
        ///     unconsumed, and the first button is selected from the first frame so ENTER always has an answer.
        /// </summary>
        private readonly ListNavigator _navigator = new(horizontal: true) {Wrap = true};

        /// <summary>Initializes a new instance of the <see cref="MessageBoxForm" /> class.</summary>
        /// <param name="window">The parent window.</param>
        // ReSharper disable once UnusedMember.Global
        public MessageBoxForm(IWindow window) : base(window)
        {
        }

        private MessageBoxData Data => UserData;

        /// <summary>Keeps the highlight sized to the button set and parked on the first button until moved.</summary>
        private void SyncHighlight(MessageBoxData data)
        {
            _navigator.Resize(ButtonsFor(data.Buttons).Count);
            if (!_navigator.HasSelection)
                _navigator.Select(0);
        }

        /// <inheritdoc />
        public override void OnKeyPressed(ConsoleKey key)
        {
            var data = Data;
            if (data == null || !data.Initialized)
                return;

            SyncHighlight(data);
            _navigator.HandleKey(key);
        }

        /// <inheritdoc />
        public override string OnRenderForm()
        {
            var data = Data;
            if (data == null || !data.Initialized)
            {
                ParentWindow.PromptText = "Opening...";
                return Environment.NewLine + "Opening...";
            }

            SyncHighlight(data);

            var buttons = ButtonsFor(data.Buttons);
            var bar = new StringBuilder();
            for (var i = 0; i < buttons.Count; i++)
            {
                if (i > 0)
                    bar.Append("  ");
                bar.Append(DecorateButton(buttons[i].Label, _navigator.HasSelection && _navigator.Index == i));
            }

            var title = data.Buttons == MessageBoxButtonsEnum.Ok ? "Message" : "Confirm";
            var boxed = new Box {Title = title, Padding = 1}.Render(
                data.Message + Environment.NewLine + Environment.NewLine + bar);

            ParentWindow.PromptText = PromptFor(data.Buttons);

            return Environment.NewLine + boxed;
        }

        /// <summary>
        ///     Draws one button: bracketed and space-padded at rest, the brackets closing into angle markers around
        ///     the highlighted one — same width either way, so the bar never shifts as the highlight moves — plus
        ///     inverse video when the environment allows escapes.
        /// </summary>
        private static string DecorateButton(string label, bool highlighted)
        {
            return highlighted
                ? ListNavigator.Emphasize($"[>{label}<]")
                : $"[ {label} ]";
        }

        /// <inheritdoc />
        public override void OnInputBufferReturned(string input)
        {
            var data = Data;
            if (data == null || !data.Initialized)
                return;

            var answer = (input ?? string.Empty).Trim().ToUpperInvariant();

            // ENTER with nothing typed presses the highlighted button. For an OK dialog this is the same "any line
            // dismisses" it always was (OK is the only button); for the question dialogs it is new — they used to
            // ignore an empty line — and it is what makes the visible highlight honest, since a cursor that ENTER
            // does not press is a lie. Typed answers below still win whenever something was actually typed.
            if (answer.Length == 0)
            {
                SyncHighlight(data);
                Respond(data, ButtonsFor(data.Buttons)[_navigator.Index].Result);
                return;
            }

            switch (data.Buttons)
            {
                case MessageBoxButtonsEnum.Ok:
                    // Any submitted line (ENTER) dismisses an acknowledgement.
                    Respond(data, MessageBoxResultEnum.Ok);
                    break;
                case MessageBoxButtonsEnum.YesNo:
                    if (IsYes(answer))
                        Respond(data, MessageBoxResultEnum.Yes);
                    else if (IsNo(answer))
                        Respond(data, MessageBoxResultEnum.No);
                    break;
                case MessageBoxButtonsEnum.YesNoCancel:
                    if (IsYes(answer))
                        Respond(data, MessageBoxResultEnum.Yes);
                    else if (IsNo(answer))
                        Respond(data, MessageBoxResultEnum.No);
                    else if (answer is "C" or "CANCEL")
                        Respond(data, MessageBoxResultEnum.Cancel);
                    break;
            }
        }

        /// <summary>
        ///     The buttons a dialog flavor shows, in bar order, each with the result pressing it reports. The first
        ///     entry is the default the opening highlight sits on.
        /// </summary>
        private static IReadOnlyList<(string Label, MessageBoxResultEnum Result)> ButtonsFor(
            MessageBoxButtonsEnum buttons)
        {
            switch (buttons)
            {
                case MessageBoxButtonsEnum.YesNo:
                    return new[]
                    {
                        ("Yes", MessageBoxResultEnum.Yes),
                        ("No", MessageBoxResultEnum.No)
                    };
                case MessageBoxButtonsEnum.YesNoCancel:
                    return new[]
                    {
                        ("Yes", MessageBoxResultEnum.Yes),
                        ("No", MessageBoxResultEnum.No),
                        ("Cancel", MessageBoxResultEnum.Cancel)
                    };
                default:
                    return new[] {("OK", MessageBoxResultEnum.Ok)};
            }
        }

        private void Respond(MessageBoxData data, MessageBoxResultEnum result)
        {
            var callback = data.OnResult;
            ParentWindow.RemoveWindowNextTick();
            callback?.Invoke(result);
        }

        private static bool IsYes(string answer)
        {
            return answer is "Y" or "YES" or "TRUE";
        }

        private static bool IsNo(string answer)
        {
            return answer is "N" or "NO" or "FALSE";
        }

        private static string PromptFor(MessageBoxButtonsEnum buttons)
        {
            switch (buttons)
            {
                case MessageBoxButtonsEnum.YesNo:
                    return "Arrows + ENTER, or [Y]es  [N]o";
                case MessageBoxButtonsEnum.YesNoCancel:
                    return "Arrows + ENTER, or [Y]es  [N]o  [C]ancel";
                default:
                    return "Press ENTER to continue";
            }
        }
    }
}
