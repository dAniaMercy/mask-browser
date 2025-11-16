using System;
using System.Windows;
using System.Windows.Controls;

namespace MaskBrowser.Desktop
{
    // Placeholder browser implementation using WPF WebBrowser.
    // Replace with CefSharp-based implementation when CefSharp native packages and platform are configured.
    public class CefBrowser : UserControl
    {
        private readonly WebBrowser _webBrowser;

        public int ProfileId { get; set; }
        public string ProfileName { get; set; } = string.Empty;

        public string Address
        {
            get => _webBrowser.Source?.ToString() ?? string.Empty;
            set
            {
                if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
                {
                    _webBrowser.Source = uri;
                }
                else
                {
                    try { _webBrowser.Navigate(value); } catch { }
                }
            }
        }

        // Parameterless constructor for XAML
        public CefBrowser() : this(0, string.Empty)
        {
        }

        public CefBrowser(int profileId, string profileName)
        {
            ProfileId = profileId;
            ProfileName = profileName;

            _webBrowser = new WebBrowser();
            Content = _webBrowser;
            Address = "https://google.com";
        }

        public void NavigateToUrl(string url)
        {
            Address = url;
        }
    }
}

