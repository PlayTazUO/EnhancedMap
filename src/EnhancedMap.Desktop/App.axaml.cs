using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using EnhancedMap.Core;
using EnhancedMap.Diagnostic;

namespace EnhancedMap.Desktop
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            MainCore.IsRunning = true;

            Logger.MessageWrited += (sender, message) => Console.WriteLine(message.ComposeMsg());
            NotificationManager.Notified += (severity, message) => Console.WriteLine($"[{severity}] {message}");

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.ShutdownRequested += (sender, e) =>
                {
                    MainCore.IsRunning = false;
                    FilesManager.Save();
                    HouseReader.SaveXml();
                };

                desktop.MainWindow = new MainWindow();
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
