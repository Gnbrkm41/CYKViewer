using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

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

        private static readonly JsonSerializerOptions s_serializerOptions = new()
        {
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
        };

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
            using HttpClient client = new();
            // Check for the app's update
            Version currentVersion = Assembly.GetExecutingAssembly().GetName().Version;

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/Gnbrkm41/CYKViewer/releases/latest");
            request.Headers.Add("User-Agent", $"Gnbrkm41-CYKViewer-v" + currentVersion.ToString(3));
            request.Headers.Add("Accept", "application/vnd.github.v3+json");

            Version latestVersion = null;
            try
            {
                HttpResponseMessage response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                GithubRelease deserializedResponse = await response.Content.ReadFromJsonAsync<GithubRelease>();
                string latestRelease = deserializedResponse.TagName;
                latestVersion = new Version(latestRelease.TrimStart('v'));
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"Update check failed: {ex}");
            }

            if (latestVersion != null)
            {
                Debug.WriteLine($"Latest release is {latestVersion}");
                if (latestVersion > currentVersion)
                {
                    openReleasesPageButton.Visibility = Visibility.Visible;
                }
            }

            // Check for localization patch's update
            string onlineScript = null;
            try
            {
                onlineScript = await client.GetStringAsync("https://newbiepr.github.io/Temporary_KRTL/ShinyColors.user.js");
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"Script update check failed: {ex}");
            }

            string scriptVersion = null;
            if (onlineScript != null)
            {
                Match onlineScriptMatch = Regex.Match(onlineScript, @"^\s*\/\/\s*@version\s*(?<version>.*?)\s*$", RegexOptions.Multiline);
                string onlineVersionString = onlineScriptMatch.Groups["version"].Value;
                Debug.WriteLine($"The version of the script from update source is {onlineVersionString}");
                bool onlineIsNewer = true;
                scriptVersion = onlineVersionString;
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
            }

            _settings = await ReadSettings();

            _settings.LocalizationPatchVersion = scriptVersion;
            _settings.PropertyChanged += UpdateSettingsFile;
        }

        private static async Task<Settings> ReadSettings()
        {
            Settings settings;
            // Look for a configuration file.
            if (File.Exists(s_settingsPath))
            {
                string settingsJson = await File.ReadAllTextAsync(s_settingsPath);
                settings = JsonSerializer.Deserialize<Settings>(settingsJson, s_serializerOptions);

                // Added in 1.0.3 - if null (does not exist), set a default value of 1.0x
                settings.GameScreenSize ??= new GameScreenSize(1.0);
            }
            else
            {
                settings = new Settings
                {
                    EnableKoreanPatch = true,
                    ScreenshotSavePath = System.IO.Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "CYKViewer"),
                    GameScreenSize = new GameScreenSize(1.0)
                };
            }

            return settings;
        }

        private async void UpdateSettingsFile(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Don't care what exactly changed - just serialize the object then save it to the path.
            string settingsJson = JsonSerializer.Serialize(_settings, s_serializerOptions);
            await File.WriteAllTextAsync(s_settingsPath, settingsJson);
        }

        private void OpenReleasesPage(object sender, RoutedEventArgs e)
        {
            ProcessStartInfo info = new("https://github.com/Gnbrkm41/CYKViewer/releases/latest")
            {
                UseShellExecute = true
            };
            Process.Start(info).Dispose();
        }
    }

    class GithubRelease
    {
        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("tag_name")]
        public string TagName { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }
    }
}
