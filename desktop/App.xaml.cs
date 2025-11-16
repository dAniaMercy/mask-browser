using System.Windows;

namespace MaskBrowser.Desktop
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            // CefSharp initialization removed for build compatibility in this environment.
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // CefSharp shutdown removed.
            base.OnExit(e);
        }
    }
}

