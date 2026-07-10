using System.Collections.Generic;
using WolfCurses.Window;
using WolfCurses.Window.Form;

namespace WolfCurses.Tests.TestDoubles
{
    /// <summary>
    ///     Records inputs like <see cref="TestForm" /> but reports InputFillsBuffer=false, so the simulation is NOT
    ///     accepting input while it is attached. Used to prove stale buffer text cannot leak through as a command.
    /// </summary>
    [ParentWindow(typeof(TestWindow))]
    public sealed class NonFillingRecordingForm : Form<TestWindowData>
    {
        public NonFillingRecordingForm(IWindow window) : base(window)
        {
        }

        public List<string> ReceivedInputs { get; } = new();

        public override bool InputFillsBuffer => false;

        public override string OnRenderForm()
        {
            return "NONFILLING";
        }

        public override void OnInputBufferReturned(string input)
        {
            ReceivedInputs.Add(input);
        }
    }

    /// <summary>
    ///     Destroys the owning simulation from inside input dispatch, which happens during the InputManager module
    ///     tick — the mid-tick teardown scenario. The app reference travels through the shared user data object.
    /// </summary>
    [ParentWindow(typeof(TestWindow))]
    public sealed class DestroyOnInputForm : Form<TestWindowData>
    {
        public DestroyOnInputForm(IWindow window) : base(window)
        {
        }

        public override string OnRenderForm()
        {
            return "DESTROYER";
        }

        public override void OnInputBufferReturned(string input)
        {
            UserData.App?.Destroy();
        }
    }

    /// <summary>
    ///     Carries the same [ParentWindow] attribute twice (AllowMultiple permits it); GetTypesWith must still yield
    ///     the type exactly once or FormFactory's dictionary Add would break every SimulationApp in this assembly.
    /// </summary>
    [ParentWindow(typeof(TestWindow))]
    [ParentWindow(typeof(TestWindow))]
    public sealed class DoubleRegisteredForm : Form<TestWindowData>
    {
        public DoubleRegisteredForm(IWindow window) : base(window)
        {
        }

        public override string OnRenderForm()
        {
            return "DOUBLE";
        }

        public override void OnInputBufferReturned(string input)
        {
        }
    }

    /// <summary>
    ///     Abstract form registered via [ParentWindow]; FormFactory must refuse to instantiate it with a descriptive
    ///     exception rather than returning null for SetForm to dereference.
    /// </summary>
    [ParentWindow(typeof(TestWindow))]
    public abstract class AbstractRegisteredForm : Form<TestWindowData>
    {
        protected AbstractRegisteredForm(IWindow window) : base(window)
        {
        }
    }
}
