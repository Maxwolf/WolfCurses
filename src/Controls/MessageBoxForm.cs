// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System;
using WolfCurses.Window;
using WolfCurses.Window.Control;
using WolfCurses.Window.Form;

namespace WolfCurses.Controls
{
    /// <summary>
    ///     Draws the message (framed with a <see cref="Box" />) and turns the user's answer into a
    ///     <see cref="MessageBoxResultEnum" />. An OK dialog is dismissed by any ENTER; a yes/no dialog accepts Y/N (and
    ///     spelled-out variants) and ignores anything else until a valid answer is given; a yes/no/cancel dialog also
    ///     accepts C.
    /// </summary>
    [ParentWindow(typeof (MessageBoxWindow))]
    public sealed class MessageBoxForm : Form<MessageBoxData>
    {
        /// <summary>Initializes a new instance of the <see cref="MessageBoxForm" /> class.</summary>
        /// <param name="window">The parent window.</param>
        // ReSharper disable once UnusedMember.Global
        public MessageBoxForm(IWindow window) : base(window)
        {
        }

        private MessageBoxData Data => UserData;

        /// <inheritdoc />
        public override string OnRenderForm()
        {
            var data = Data;
            if (data == null || !data.Initialized)
            {
                ParentWindow.PromptText = "Opening...";
                return Environment.NewLine + "Opening...";
            }

            var title = data.Buttons == MessageBoxButtonsEnum.Ok ? "Message" : "Confirm";
            var boxed = new Box {Title = title, Padding = 1}.Render(data.Message);

            ParentWindow.PromptText = PromptFor(data.Buttons);

            return Environment.NewLine + boxed;
        }

        /// <inheritdoc />
        public override void OnInputBufferReturned(string input)
        {
            var data = Data;
            if (data == null || !data.Initialized)
                return;

            var answer = (input ?? string.Empty).Trim().ToUpperInvariant();

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
                    return "[Y]es  [N]o";
                case MessageBoxButtonsEnum.YesNoCancel:
                    return "[Y]es  [N]o  [C]ancel";
                default:
                    return "Press ENTER to continue";
            }
        }
    }
}
