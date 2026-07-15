using System;
using System.Collections.Generic;
using WolfCurses.Window;

namespace WolfCurses.Tests.TestDoubles
{
    /// <summary>
    ///     Primary test window. WindowFactory creates windows via Activator.CreateInstance(type, simUnit), so the
    ///     single (SimulationApp) constructor must stay public. Lifecycle hooks are counted so tests can observe when
    ///     the window manager fires them.
    /// </summary>
    public class TestWindow : Window<TestCommandsEnum, TestWindowData>
    {
        public TestWindow(SimulationApp simUnit) : base(simUnit)
        {
        }

        public int PostCreateCount { get; private set; }

        public int ActivateCount { get; private set; }

        public int AddedCount { get; private set; }

        public List<TestCommandsEnum> InvokedCommands { get; } = new();

        /// <summary>Exposes the protected AddCommand for tests; records invocations into InvokedCommands.</summary>
        public void AddTestCommand(TestCommandsEnum command)
        {
            AddCommand(() => InvokedCommands.Add(command), command);
        }

        /// <summary>Exposes the protected AddCommand with an arbitrary action, for duplicate-command pinning tests.</summary>
        public void AddTestCommand(TestCommandsEnum command, Action action)
        {
            AddCommand(action, command);
        }

        public override void OnWindowPostCreate()
        {
            base.OnWindowPostCreate();
            PostCreateCount++;
        }

        public override void OnWindowActivate()
        {
            base.OnWindowActivate();
            ActivateCount++;
        }

        public override void OnWindowAdded()
        {
            base.OnWindowAdded();
            AddedCount++;
        }
    }
}
