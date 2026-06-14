using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace GestureFlowWPF
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                LogException("AppDomain.CurrentDomain.UnhandledException", args.ExceptionObject as Exception);
            };

            DispatcherUnhandledException += (sender, args) =>
            {
                LogException("DispatcherUnhandledException", args.Exception);
                args.Handled = true; // Keep app alive — don't let UI exceptions crash the process
            };

            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                LogException("TaskScheduler.UnobservedTaskException", args.Exception);
                args.SetObserved();
            };

            base.OnStartup(e);
        }

        private void LogException(string source, Exception? ex)
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_crash.log");
                using (var writer = new StreamWriter(logPath, true))
                {
                    writer.WriteLine($"[{DateTime.Now}] Source: {source}");
                    if (ex != null)
                    {
                        writer.WriteLine($"Exception: {ex.GetType().FullName}");
                        writer.WriteLine($"Message: {ex.Message}");
                        writer.WriteLine($"StackTrace:\n{ex.StackTrace}");
                        if (ex.InnerException != null)
                        {
                            writer.WriteLine($"InnerException: {ex.InnerException.Message}");
                            writer.WriteLine($"InnerStackTrace:\n{ex.InnerException.StackTrace}");
                        }
                    }
                    else
                    {
                        writer.WriteLine("Exception object is null.");
                    }
                    writer.WriteLine(new string('-', 80));
                }
            }
            catch
            {
                // Ignore logger errors
            }
        }
    }
}
