// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/17/2026

using System;
using WolfCurses.Tests.TestDoubles;
using Xunit;

namespace WolfCurses.Tests.Core
{
    /// <summary>
    ///     Covers key presses: the path that lets a form hear a key the input buffer cannot hold.
    ///     <para>
    ///         The buffer takes characters, and an arrow key has none, so before this existed an arrow key reaching the
    ///         host was simply dropped and nothing could be steered. These check that a key gets from
    ///         <c>InputManager.SendKeyPress</c> to the focused window's current form, and that the buffer's own rules —
    ///         which are about text — do not quietly eat it on the way.
    ///     </para>
    /// </summary>
    public class KeyPressRoutingTests
    {
        private static (TestSimulationApp app, KeyRecordingForm form) NewAppWithKeyForm()
        {
            var app = new TestSimulationApp();
            app.WindowManager.Add(typeof(TestWindow));
            app.WindowManager.FocusedWindow.SetForm(typeof(KeyRecordingForm));
            return (app, (KeyRecordingForm) app.WindowManager.FocusedWindow.CurrentForm);
        }

        [Fact]
        public void AKeyPressReachesTheFocusedWindowsForm()
        {
            var (app, form) = NewAppWithKeyForm();

            app.InputManager.SendKeyPress(ConsoleKey.LeftArrow);
            app.OnTick(false);

            Assert.Equal(new[] {ConsoleKey.LeftArrow}, form.ReceivedKeys);
        }

        [Fact]
        public void KeyPressesArriveInOrderAndNoneAreLostToATickWithNothingElseInIt()
        {
            // The trap this is really about: the command dispatch bails out early when no commands are queued, so key
            // handling placed after it would only ever run on ticks that happened to carry a command — which, for a
            // screen driven entirely by arrow keys, is never.
            var (app, form) = NewAppWithKeyForm();

            app.InputManager.SendKeyPress(ConsoleKey.UpArrow);
            app.InputManager.SendKeyPress(ConsoleKey.RightArrow);
            app.InputManager.SendKeyPress(ConsoleKey.DownArrow);
            app.OnTick(false);

            Assert.Equal(new[] {ConsoleKey.UpArrow, ConsoleKey.RightArrow, ConsoleKey.DownArrow}, form.ReceivedKeys);
        }

        [Fact]
        public void TheSameKeyHeldDownIsReportedEveryTime()
        {
            // Not merely a detail: the command queue drops a command identical to one already waiting, which is right
            // for commands and would make holding an arrow key move something exactly once. Key presses get their own
            // queue for this reason, and this is what says so.
            var (app, form) = NewAppWithKeyForm();

            for (var i = 0; i < 5; i++)
                app.InputManager.SendKeyPress(ConsoleKey.RightArrow);

            app.OnTick(false);

            Assert.Equal(5, form.ReceivedKeys.Count);
            Assert.All(form.ReceivedKeys, key => Assert.Equal(ConsoleKey.RightArrow, key));
        }

        [Fact]
        public void EveryQueuedKeyIsSpentOnOneTickRatherThanOnePerTick()
        {
            // Commands are dispatched one a tick, deliberately. Keys are not: anything holding a key down produces them
            // faster than a slow host loop consumes them, and a queue that drains slower than it fills is a control
            // scheme that drifts further behind the longer it is used.
            var (app, form) = NewAppWithKeyForm();

            app.InputManager.SendKeyPress(ConsoleKey.A);
            app.InputManager.SendKeyPress(ConsoleKey.B);
            app.OnTick(false);

            Assert.Equal(2, form.ReceivedKeys.Count);

            // And nothing lingers to be delivered a second time.
            app.OnTick(false);
            Assert.Equal(2, form.ReceivedKeys.Count);
        }

        [Fact]
        public void AKeyPressDoesNotTouchTheInputBuffer()
        {
            // They are separate paths on purpose. A form being steered by the arrow keys should not find its prompt
            // filling up with them.
            var (app, form) = NewAppWithKeyForm();

            app.InputManager.SendKeyPress(ConsoleKey.LeftArrow);
            app.InputManager.SendKeyPress(ConsoleKey.Spacebar);
            app.OnTick(false);

            Assert.Equal(string.Empty, app.InputManager.InputBuffer);
            Assert.Equal(2, form.ReceivedKeys.Count);
        }

        [Fact]
        public void AWindowWithNoFormSwallowsKeyPressesQuietly()
        {
            // Window.OnKeyPressed forwards to the current form and there may not be one. Nothing to assert but the
            // absence of an exception, which is the whole point.
            var app = new TestSimulationApp();
            app.WindowManager.Add(typeof(TestWindow));

            app.InputManager.SendKeyPress(ConsoleKey.Escape);
            app.OnTick(false);

            Assert.Null(app.WindowManager.FocusedWindow.CurrentForm);
        }

        [Fact]
        public void KeysGoToWhicheverWindowHasFocusNow()
        {
            // What makes a modal dialog pause a game for free: put a window on top and the keys follow it, so the form
            // underneath stops hearing them without having to be told to stop listening.
            var (app, form) = NewAppWithKeyForm();

            app.InputManager.SendKeyPress(ConsoleKey.LeftArrow);
            app.OnTick(false);
            Assert.Single(form.ReceivedKeys);

            app.WindowManager.Add(typeof(SecondTestWindow));
            app.InputManager.SendKeyPress(ConsoleKey.RightArrow);
            app.OnTick(false);

            Assert.Single(form.ReceivedKeys);
        }

        [Fact]
        public void KeysPressedBeforeAnyWindowExists_AreDroppedWithoutHanging()
        {
            // The dispatch loop dequeues before the null-conditional dispatch, deliberately:
            // `FocusedWindow?.OnKeyPressed(_keyQueue.Dequeue())` never evaluates the dequeue when there is no window,
            // so the queue stayed full and the loop span forever. Unreachable while hosts only sent keys at windows
            // they could see; the automatic console read made it real, because a key can arrive during the first
            // second of a session, before OnFirstTick has attached anything. A regression here does not fail — it
            // hangs, which is the loudest a test can be.
            var app = new TestSimulationApp();
            app.InputManager.SendKeyPress(ConsoleKey.A);

            app.OnTick(false);

            // And the key was spent, not saved: a window appearing later hears nothing stale.
            app.WindowManager.Add(typeof(TestWindow));
            app.WindowManager.FocusedWindow.SetForm(typeof(KeyRecordingForm));
            app.OnTick(false);

            Assert.Empty(((KeyRecordingForm) app.WindowManager.FocusedWindow.CurrentForm).ReceivedKeys);
        }

        [Fact]
        public void ClearingTheQueueDropsKeysThatNeverGotDelivered()
        {
            // Session reset: a key pressed at the old simulation has nothing to say to the new one.
            var (app, form) = NewAppWithKeyForm();

            app.InputManager.SendKeyPress(ConsoleKey.LeftArrow);
            app.InputManager.ClearQueue();
            app.OnTick(false);

            Assert.Empty(form.ReceivedKeys);
        }
    }
}
