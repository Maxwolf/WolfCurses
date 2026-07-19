// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/18/2026

using System;
using WolfCurses.Tests.TestDoubles;
using Xunit;

namespace WolfCurses.Tests.Core
{
    /// <summary>
    ///     Covers the whole <see cref="ConsoleKeyInfo" /> travelling from <c>SendConsoleKey</c> to the form, not just
    ///     the <see cref="ConsoleKey" /> inside it.
    ///     <para>
    ///         The motivating bug is shifted punctuation: on a US keyboard '&lt;' is Shift+',' and both report
    ///         <see cref="ConsoleKey.OemComma" />, so a form handed only the key cannot tell the 1990 Oregon Trail
    ///         hunting minigame's novice aiming keys ('&lt;' '&gt;') from its expert ones (',' '.') and binding both
    ///         double-fires on every press. The character and modifiers were in the console's hands all along; these
    ///         pin that they are no longer dropped on the way in, and that everything written against the bare-key
    ///         overloads keeps hearing exactly what it always heard.
    ///     </para>
    /// </summary>
    public class KeyInfoRoutingTests
    {
        private static (TestSimulationApp app, KeyInfoRecordingForm form) NewAppWithKeyInfoForm()
        {
            var app = new TestSimulationApp();
            app.WindowManager.Add(typeof(TestWindow));
            app.WindowManager.FocusedWindow.SetForm(typeof(KeyInfoRecordingForm));
            return (app, (KeyInfoRecordingForm) app.WindowManager.FocusedWindow.CurrentForm);
        }

        [Fact]
        public void AShiftedKeyReachesTheFormWithCharacterAndModifiersIntact()
        {
            var (app, form) = NewAppWithKeyInfoForm();

            app.InputManager.SendConsoleKey(new ConsoleKeyInfo('<', ConsoleKey.OemComma, true, false, false));
            app.OnTick(false);

            var received = Assert.Single(form.ReceivedKeyInfos);
            Assert.Equal('<', received.KeyChar);
            Assert.Equal(ConsoleKey.OemComma, received.Key);
            Assert.Equal(ConsoleModifiers.Shift, received.Modifiers);
        }

        [Fact]
        public void ShiftedAndUnshiftedFormsOfTheSameKeyArriveDistinguishable()
        {
            // The double-fire bug in one test: ',' and '<' are the same ConsoleKey, so a form binding novice keys to
            // one and expert keys to the other must be able to tell which was pressed — KeyChar is the discriminator.
            var (app, form) = NewAppWithKeyInfoForm();

            app.InputManager.SendConsoleKey(new ConsoleKeyInfo(',', ConsoleKey.OemComma, false, false, false));
            app.InputManager.SendConsoleKey(new ConsoleKeyInfo('<', ConsoleKey.OemComma, true, false, false));
            app.OnTick(false);

            Assert.Equal(2, form.ReceivedKeyInfos.Count);
            Assert.All(form.ReceivedKeyInfos, keyInfo => Assert.Equal(ConsoleKey.OemComma, keyInfo.Key));
            Assert.Equal(',', form.ReceivedKeyInfos[0].KeyChar);
            Assert.Equal('<', form.ReceivedKeyInfos[1].KeyChar);
            Assert.NotEqual(form.ReceivedKeyInfos[0].Modifiers, form.ReceivedKeyInfos[1].Modifiers);
        }

        [Fact]
        public void TheLegacyBareKeySendArrivesWithNoCharacterOrModifiers()
        {
            // SendKeyPress(ConsoleKey) synthesises the info it never had. A form listening for the full info still
            // hears the key; it just learns nothing that was not sent.
            var (app, form) = NewAppWithKeyInfoForm();

            app.InputManager.SendKeyPress(ConsoleKey.LeftArrow);
            app.OnTick(false);

            var received = Assert.Single(form.ReceivedKeyInfos);
            Assert.Equal(ConsoleKey.LeftArrow, received.Key);
            Assert.Equal('\0', received.KeyChar);
            Assert.Equal((ConsoleModifiers) 0, received.Modifiers);
        }

        [Fact]
        public void AFormOverridingOnlyTheBareKeyOverloadHearsShiftedKeysExactlyAsBefore()
        {
            // The additive contract from the form's side: the full info rides past an old form untouched, arriving
            // through the default forwarding as the same bare ConsoleKey it always got.
            var app = new TestSimulationApp();
            app.WindowManager.Add(typeof(TestWindow));
            app.WindowManager.FocusedWindow.SetForm(typeof(KeyRecordingForm));
            var form = (KeyRecordingForm) app.WindowManager.FocusedWindow.CurrentForm;

            app.InputManager.SendConsoleKey(new ConsoleKeyInfo('<', ConsoleKey.OemComma, true, false, false));
            app.OnTick(false);

            Assert.Equal(new[] {ConsoleKey.OemComma}, form.ReceivedKeys);
        }

        [Fact]
        public void AWindowOverridingTheBareKeyOverloadStillFires_AndItsFormStillGetsTheFullInfo()
        {
            // The subtle half of the additive contract. The bare-key overload stays the single routing point so a
            // window override written before ConsoleKeyInfo existed keeps intercepting every key — and its base call
            // must still deliver the whole info to the form rather than a reconstruction with the character stripped.
            var app = new LegacyKeyWindowSimulationApp();
            app.WindowManager.Add(typeof(LegacyKeyWindow));
            var window = (LegacyKeyWindow) app.WindowManager.FocusedWindow;
            window.SetForm(typeof(LegacyKeyWindowForm));
            var form = (LegacyKeyWindowForm) window.CurrentForm;

            app.InputManager.SendConsoleKey(new ConsoleKeyInfo('<', ConsoleKey.OemComma, true, false, false));
            app.OnTick(false);

            Assert.Equal(new[] {ConsoleKey.OemComma}, window.ReceivedKeys);
            var received = Assert.Single(form.ReceivedKeyInfos);
            Assert.Equal('<', received.KeyChar);
            Assert.Equal(ConsoleModifiers.Shift, received.Modifiers);
        }

        [Fact]
        public void CallingTheWindowsBareKeyOverloadDirectlyStillReachesTheForm()
        {
            // A host (or test) that talks straight to the window with only a ConsoleKey has nothing better to offer,
            // so the form receives the synthesised no-character info instead of nothing at all.
            var (app, form) = NewAppWithKeyInfoForm();

            app.WindowManager.FocusedWindow.OnKeyPressed(ConsoleKey.RightArrow);

            var received = Assert.Single(form.ReceivedKeyInfos);
            Assert.Equal(ConsoleKey.RightArrow, received.Key);
            Assert.Equal('\0', received.KeyChar);
        }
    }
}
