using System.Windows;
using System.Windows.Controls;
using System.Net.Http;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using MaskBrowser.Desktop.Models;
using MaskBrowser.Desktop.Services;

namespace MaskBrowser.Desktop
{
    public partial class MainWindow : Window
    {
        private readonly ApiService _apiService;
        private ObservableCollection<BrowserProfile> _profiles;

        public MainWindow()
        {
            InitializeComponent();
            _apiService = new ApiService();
            _profiles = new ObservableCollection<BrowserProfile>();
            ProfileListBox.ItemsSource = _profiles;
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var email = EmailTextBox.Text;
                var password = PasswordBox.Password;

                if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
                {
                    ShowError("Заполните все поля");
                    return;
                }

                var result = await _apiService.LoginAsync(email, password);
                if (result.Success)
                {
                    LoginView.Visibility = Visibility.Collapsed;
                    ProfileView.Visibility = Visibility.Visible;
                    await LoadProfilesAsync();
                }
                else
                {
                    ShowError(result.Message);
                }
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка: {ex.Message}");
            }
        }

        private async void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Open registration window
            ShowError("Регистрация пока не реализована");
        }

        private async void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            _apiService.Logout();
            LoginView.Visibility = Visibility.Visible;
            ProfileView.Visibility = Visibility.Collapsed;
            _profiles.Clear();
        }

        private async void CreateProfileButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Open create profile dialog
            var profile = await _apiService.CreateProfileAsync("New Profile", new BrowserConfig());
            if (profile != null)
            {
                await LoadProfilesAsync();
            }
        }

        private async void StartProfile_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var profile = button?.Tag as BrowserProfile;
            if (profile != null)
            {
                await _apiService.StartProfileAsync(profile.Id);
                await LoadProfilesAsync();
            }
        }

        private async void StopProfile_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var profile = button?.Tag as BrowserProfile;
            if (profile != null)
            {
                await _apiService.StopProfileAsync(profile.Id);
                await LoadProfilesAsync();
            }
        }

        private async void DeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var profile = button?.Tag as BrowserProfile;
            if (profile != null)
            {
                var result = MessageBox.Show($"Удалить профиль {profile.Name}?", 
                    "Подтверждение", MessageBoxButton.YesNo);
                if (result == MessageBoxResult.Yes)
                {
                    await _apiService.DeleteProfileAsync(profile.Id);
                    await LoadProfilesAsync();
                }
            }
        }

        private async Task LoadProfilesAsync()
        {
            try
            {
                var profiles = await _apiService.GetProfilesAsync();
                _profiles.Clear();
                foreach (var profile in profiles)
                {
                    _profiles.Add(profile);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки профилей: {ex.Message}", "Ошибка");
            }
        }

        private void ShowError(string message)
        {
            ErrorTextBlock.Text = message;
            ErrorTextBlock.Visibility = Visibility.Visible;
        }

        private void ProfileButton_Click(object sender, RoutedEventArgs e)
        {
            // Show profile view
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            // Show settings view
        }
    }
}

