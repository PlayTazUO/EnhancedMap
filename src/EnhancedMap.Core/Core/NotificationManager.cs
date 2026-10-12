using System;

namespace EnhancedMap.Core
{
    /// <summary>
    ///     Cross-platform replacement for the MessageBox popups that used to live in the core layer.
    ///     The UI layer subscribes and shows an appropriate dialog.
    /// </summary>
    public static class NotificationManager
    {
        public enum Severity
        {
            Info,
            Warning,
            Error
        }

        public static event Action<Severity, string> Notified;

        public static void Notify(Severity severity, string message)
        {
            Notified?.Invoke(severity, message);
        }

        public static void Info(string message)
        {
            Notify(Severity.Info, message);
        }

        public static void Warn(string message)
        {
            Notify(Severity.Warning, message);
        }

        public static void Error(string message)
        {
            Notify(Severity.Error, message);
        }
    }
}
