using WolfCurses.Utility;

namespace WolfCurses.Tests.TestDoubles
{
    /// <summary>
    ///     Menu command enum for <see cref="TestWindow" />. Second carries the library's custom description attribute
    ///     so tests can cover both the attribute and the ToString fallback paths of ToDescriptionAttribute.
    /// </summary>
    public enum TestCommandsEnum
    {
        First = 1,

        [Description("Second command")]
        Second = 2,

        Third = 3
    }
}
