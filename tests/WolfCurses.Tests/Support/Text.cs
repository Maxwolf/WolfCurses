using System;

namespace WolfCurses.Tests.Support
{
    /// <summary>
    ///     The library emits Environment.NewLine throughout (WordWrap, MarqueeBar, menu and table rendering); these
    ///     helpers let tests write goldens with \n and stay portable across operating systems.
    /// </summary>
    internal static class Text
    {
        public static string NL => Environment.NewLine;

        public static string Norm(string value)
        {
            return value?.Replace("\r\n", "\n");
        }
    }
}
