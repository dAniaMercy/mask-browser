using CefSharp.Wpf;
using System.Windows;

namespace MaskBrowser.Desktop
{
    public class CefBrowser : ChromiumWebBrowser
    {
        public int ProfileId { get; set; }
        public string ProfileName { get; set; } = string.Empty;

        public CefBrowser(int profileId, string profileName) : base()
        {
            ProfileId = profileId;
            ProfileName = profileName;
            Address = "https://google.com";
            
            // Configure browser settings
            BrowserSettings = new CefSharp.BrowserSettings
            {
                WebSecurity = CefState.Disabled,
                FileAccessFromFileUrls = CefState.Enabled,
                UniversalAccessFromFileUrls = CefState.Enabled
            };
        }

        public void NavigateToUrl(string url)
        {
            Address = url;
        }
    }
}

