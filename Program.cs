namespace YouTubeVoiceController
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // Catch unhandled exceptions on UI thread
            Application.ThreadException += (_, e) =>
                AppLogger.Error("Unhandled UI thread exception", e.Exception);

            // Catch unhandled exceptions on background threads (ThreadPool, Tasks)
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                AppLogger.Error("Fatal unhandled exception (exit imminent)",
                    e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString()));

            // Catch unobserved Task exceptions
            TaskScheduler.UnobservedTaskException += (_, e) => {
                AppLogger.Error("Unobserved Task exception", e.Exception);
                e.SetObserved(); // prevent process crash for unobserved tasks
            };

            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}