using System.Windows;
using CefSharp;
using CefSharp.Wpf;

namespace MaskBrowser.Desktop
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var settings = new CefSettings
            {
                BrowserSubprocessPath = @"CefSharp.BrowserSubprocess.exe"
            };

            Cef.Initialize(settings, performDependencyCheck: true, browserProcessHandler: null);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Cef.Shutdown();
            base.OnExit(e);
        }
    }
}

