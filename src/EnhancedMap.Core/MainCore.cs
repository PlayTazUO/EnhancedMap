using System;
using System.Reflection;

namespace EnhancedMap
{
    /// <summary>
    ///     Platform independent application state. The original WinForms entry point lived here;
    ///     the Avalonia application (EnhancedMap.Desktop) is now responsible for bootstrapping.
    /// </summary>
    public static class MainCore
    {
        public static bool IsRunning { get; set; }

        public static Version MapVersion => typeof(MainCore).Assembly.GetName().Version;
    }
}
