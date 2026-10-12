using System;
using EnhancedMap.Diagnostic;

namespace EnhancedMap.Core
{
    /// <summary>
    ///     The original UOClientManager attached to the Ultima Online client window using Win32
    ///     (SendMessage / FindWindow / DWM). That Windows-only interop has been removed for the
    ///     cross-platform port. This minimal replacement keeps the API surface the rest of the code
    ///     still calls, routing system messages through the chat/logging layers.
    /// </summary>
    public static class UOClientManager
    {
        public static bool IsAttached => false;

        public static IntPtr hWnd => IntPtr.Zero;

        public static void Initialize(IntPtr handle)
        {
        }

        public static void AttachToClient()
        {
        }

        public static void SysMessage(string message, int hue)
        {
            Logger.Log(message);
            ChatManager.Add("System", message);
        }
    }
}
