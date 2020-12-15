using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace CYKViewer
{
    /// <summary>
    /// Interaction logic for StartupPage.xaml
    /// </summary>
    public partial class StartupPage : Page
    {
        private readonly MainWindow _parentWindow;
        private static readonly string s_userDataRootDirectory =
            System.IO.Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CYKViewer", "userData");
        private static readonly string s_scriptPath =
            System.IO.Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CYKViewer", "localization.js");
        private static readonly string s_settingsPath =
            System.IO.Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CYKViewer", "settings.json");

        private Settings _settings;
        public ObservableCollection<string> Profiles { get; set; } = new ObservableCollection<string>();

        public StartupPage(MainWindow parentWindow) : this()
        {
            _parentWindow = parentWindow;
        }

        public StartupPage()
        {
            InitializeComponent();
            DirectoryInfo directoryInfo = new(s_userDataRootDirectory);
            if (!directoryInfo.Exists)
            {
                directoryInfo.Create();
            }
            foreach (DirectoryInfo userDataFolder in directoryInfo.EnumerateDirectories())
            {
                Profiles.Add(userDataFolder.Name);
            }

            profileList.ItemsSource = Profiles;
        }

        private void AddProfileButton_Click(object sender, RoutedEventArgs e)
        {
            var illegalChars = System.IO.Path.GetInvalidFileNameChars();
            string profileName = profileNameTextBox.Text;
            if (string.IsNullOrWhiteSpace(profileName))
            {
                return;
            }

            if (profileName.Intersect(illegalChars).Any())
            {
                return;
            }

            Profiles.Add(profileNameTextBox.Text);
            profileNameTextBox.Clear();
        }

        private void StartRequested(object sender, RoutedEventArgs e)
        {
            string selection = (string)profileList.SelectedItem;

            if (string.IsNullOrWhiteSpace(selection))
            {
                return;
            }

            // Ensure selection doesn't contain illegal characters.
            string pathToAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string userFolderPath = System.IO.Path.Join(pathToAppData, "CYKViewer", "userData", selection);
            GamePage gamePage = new(_parentWindow, userFolderPath, _settings);
            _ = _parentWindow.PageFrame.Navigate(gamePage);
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // Check for localization patch's update
            string onlineScript;
            using (HttpClient client = new())
            {
                onlineScript = await client.GetStringAsync("https://newbiepr.github.io/Temporary_KRTL/ShinyColors.user.js");
            }

            var onlineScriptMatch = Regex.Match(onlineScript, @"^\s*\/\/\s*@version\s*(?<version>.*?)\s*$", RegexOptions.Multiline);
            string onlineVersionString = onlineScriptMatch.Groups["version"].Value;
            Debug.WriteLine($"The version of the script from update source is {onlineVersionString}");
            bool onlineIsNewer = true;
            var scriptVersion = onlineVersionString;
            if (File.Exists(s_scriptPath))
            {
                string existingScript = await File.ReadAllTextAsync(s_scriptPath);
                var existingMatch = Regex.Match(existingScript, @"^\s*\/\/\s*@version\s*(?<version>.*?)\s*$", RegexOptions.Multiline);
                string existingVersionString = existingMatch.Groups["version"].Value;
                Debug.WriteLine($"The version of the offline script is {existingVersionString}");
                onlineIsNewer = new Version(onlineVersionString) > new Version(existingVersionString);
                if (!onlineIsNewer)
                {
                    onlineScript = existingScript;
                }
                scriptVersion = onlineIsNewer ? onlineVersionString : existingVersionString;
            }
            else
            {
                Debug.WriteLine($"The offline script does not exist.");
            }

            if (onlineIsNewer)
            {
                // The online script is newer, updating...
                Debug.WriteLine($"Updating the script...");
                await File.WriteAllTextAsync(s_scriptPath, onlineScript);
            }
            Debug.WriteLine("Update logic complete.");

            // Look for a configuration file.
            if (File.Exists(s_settingsPath))
            {
                string settingsJson = await File.ReadAllTextAsync(s_settingsPath);
                _settings = JsonSerializer.Deserialize<Settings>(settingsJson);
            }
            else
            {
                _settings = new Settings
                {
                    EnableKoreanPatch = true,
                    ScreenshotSavePath = System.IO.Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "CYKViewer")
                };
            }
            _settings.LocalizationPatchVersion = scriptVersion;
            _settings.PropertyChanged += UpdateSettingsFile;
        }

        private async void UpdateSettingsFile(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Don't care what exactly changed - just serialize the object then save it to the path.
            string settingsJson = JsonSerializer.Serialize(_settings);
            await File.WriteAllTextAsync(s_settingsPath, settingsJson);
        }
    }
}
