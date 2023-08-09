using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
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
    /// Interaction logic for StartupControl.xaml
    /// </summary>
    public partial class StartupControl : UserControl
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

        public StartupControl(MainWindow parentWindow) : this()
        {
            _parentWindow = parentWindow;
        }

        public StartupControl()
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

            if (setDefaultCheckBox.IsChecked == true)
            {
                _settings.DefaultProfile = selection;
            }
            else if (_settings.DefaultProfile == selection)
            {
                // It was set to the default profile, however the checkbox was unchecked (presumably, manually)
                _settings.DefaultProfile = null;
            }

            string pathToAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string userFolderPath = System.IO.Path.Join(pathToAppData, "CYKViewer", "userData", selection);
            GameControl gameControl = new(_parentWindow, userFolderPath, _settings);
            _parentWindow.Content = gameControl;
        }

        public static async Task<Version> GetLatestClientVersionAsync(HttpClient client, Version currentVersion)
        {
            Debug.WriteLine("Checking for updates");
            HttpRequestMessage request = new(HttpMethod.Get, "https://api.github.com/repos/Gnbrkm41/CYKViewer/releases/latest");
            request.Headers.Add("User-Agent", $"Gnbrkm41-CYKViewer-v" + currentVersion.ToString(3));
            request.Headers.Add("Accept", "application/vnd.github.v3+json");

            Version latestVersion = null;
            try
            {
                HttpResponseMessage response = await client.SendAsync(request);
                _ = response.EnsureSuccessStatusCode();
                GithubRelease deserializedResponse = await response.Content.ReadFromJsonAsync<GithubRelease>();
                string latestRelease = deserializedResponse.TagName;
                latestVersion = new Version(latestRelease.TrimStart('v'));
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"Update check failed: {ex}");
            }
            catch (TaskCanceledException tcEx)
            {
                // Timeout (100s by default). Not sure what to do about it - it's unlikely, but we don't want to crash the app
                // Maybe it'll succeed next time. For now, let's ignore
                Debug.WriteLine($"Timeout while checking for client updates: {tcEx}");
            }

            return latestVersion;
        }

        public static Version GetScriptVersion(string script)
        {
            Match match = Regex.Match(script, @"\s*\/\/\s*@version\s*(?<version>.*?)\s*$", RegexOptions.Multiline);
            if (!match.Success)
            {
                return null;
            }

            Group group = match.Groups["version"];
            if (!group.Success)
            {
                return null;
            }

            string rawVersion = group.Value;

            return !Version.TryParse(rawVersion, out Version version) ? null : version;
        }

        public static string GetUpdateUrl(string script)
        {
            // When updating, also change the update URL.
            Match updateUrlMatch = Regex.Match(script, @"^\s*\/\/\s*@updateURL\s*(?<updateUrl>.*?)\s*$", RegexOptions.Multiline);
            if (!updateUrlMatch.Success)
            {
                return null;
            }

            Group updateUrlGroup = updateUrlMatch.Groups["updateUrl"];
            if (!updateUrlGroup.Success)
            {
                return null;
            }

            string rawUrl = updateUrlGroup.Value;

            // Perform basic validation - probably not good enough, but gives us a hint on whether the captured URL is valid.
            return Uri.TryCreate(rawUrl, UriKind.Absolute, out _) ? rawUrl : null;
        }

        private async void ControlLoaded(object sender, RoutedEventArgs e)
        {
            Version currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
            Debug.WriteLine($"The client's version is {currentVersion.ToString(3)}");
            // Load the settings first, so it doesn't need to wait for the update logic to finish
            // in case it's slow to load data from GitHub
            _settings = ReadSettings(true);
            _settings.ClientVersion = currentVersion.ToString(3);
            _settings.PropertyChanged += UpdateSettingsFile;
            
            using HttpClient client = new();

            Version latestVersion = await GetLatestClientVersionAsync(client, currentVersion);

            if (latestVersion != null)
            {
                Debug.WriteLine($"Latest release is {latestVersion}");
                if (latestVersion > currentVersion)
                {
                    openReleasesPageButton.Visibility = Visibility.Visible;
                    _settings.ClientVersion += " (업데이트 가능)";
                }
            }

            // Check for localization patch's update
            string onlineScript = null;
            try
            {
                onlineScript = await client.GetStringAsync(_settings.ScriptUpdateUrl);
            }
            catch (HttpRequestException hrEx)
            {
                _ = MessageBox.Show(_parentWindow, $"패치 스크립트 업데이트에 실패했습니다. {Environment.NewLine}주소: {_settings.ScriptUpdateUrl}{Environment.NewLine}메시지: {hrEx}", "업데이트 중 오류 발생");
                Debug.WriteLine($"Script update check failed: {hrEx}");
            }
            catch (InvalidOperationException ioEx)
            {
                _ = MessageBox.Show(_parentWindow, $"패치 스크립트 업데이트 주소가 올바르지 않습니다. {Environment.NewLine}주소: {_settings.ScriptUpdateUrl}{Environment.NewLine}메시지: {ioEx}", "업데이트 중 오류 발생");
            }
            catch (ArgumentException ae)
            {
                _ = MessageBox.Show(_parentWindow, $"패치 스크립트 업데이트 주소가 올바르지 않습니다. {Environment.NewLine}주소: {_settings.ScriptUpdateUrl}{Environment.NewLine}메시지: {ae}", "업데이트 중 오류 발생");
            }
            catch (TaskCanceledException tcEx)
            {
                // Timeout (100s). Not sure what to do about it - it's unlikely, but we don't want to crash the app
                // Maybe it'll succeed next time. For now, let's ignore
                Debug.WriteLine($"Timeout while checking for script updates: {tcEx}");
            }

            Version scriptVersion = null;
            if (onlineScript != null)
            {
                bool onlineIsNewer = true;
                Version onlineScriptVersion = GetScriptVersion(onlineScript);
                Debug.WriteLine($"The version of the script from update source is {onlineScriptVersion?.ToString(3) ?? "null"}");
                if (File.Exists(s_scriptPath))
                {
                    string offlineScript = await File.ReadAllTextAsync(s_scriptPath);
                    Version offlineScriptVersion = GetScriptVersion(offlineScript);
                    Debug.WriteLine($"The version of the offline script is {offlineScriptVersion?.ToString(3) ?? "null"}");
                    if (onlineScriptVersion is not null && offlineScriptVersion is not null && onlineScriptVersion > offlineScriptVersion)
                    {
                        onlineScript = offlineScript;
                        scriptVersion = onlineScriptVersion;
                    }
                    else
                    {
                        scriptVersion = offlineScriptVersion;
                        onlineIsNewer = false;
                    }
                }
                else
                {
                    Debug.WriteLine($"The offline script does not exist.");
                    scriptVersion = onlineScriptVersion;
                }

                if (onlineIsNewer)
                {
                    // The online script is newer, updating...
                    Debug.WriteLine($"Updating the script...");
                    await File.WriteAllTextAsync(s_scriptPath, onlineScript);

                    _settings.ScriptUpdated = true;
                    // When updating, also change the update URL.
                    string updateUrl = GetUpdateUrl(onlineScript);
                    if (updateUrl != null)
                    {
                        _settings.ScriptUpdateUrl = updateUrl;
                    }
                    else
                    {
                        // no clue what to do if it's invalid.
                        Debug.WriteLine($"Failed to obtain a valid URL from the new script.");
                    }
                }
                Debug.WriteLine("Update logic complete.");
            }

            _settings.LocalizationPatchVersion = scriptVersion is null ? "알 수 없음" : scriptVersion.ToString(3);
        }

        public static Settings ReadSettings(bool createNew)
        {
            Settings settings = null;
            // Look for a configuration file.
            if (File.Exists(s_settingsPath))
            {
                string settingsJson = File.ReadAllText(s_settingsPath);
                settings = JsonSerializer.Deserialize<Settings>(settingsJson, s_serializerOptions);

                // Added in 1.0.3 - if null (does not exist), set a default value of 1.0x
                settings.GameScreenSize ??= new GameScreenSize(1.0);

                // Added in 1.0.4 - if null (does not exist), set the default value to the GitLab update link
                settings.ScriptUpdateUrl ??= "https://newbiepr.gitlab.io/shinymaskr.gitlab.io/ShinyColors.user.js";

                // Added in 1.0.9 - if null (does not exist), start with the menu open
                settings.MenuOpened ??= true;

                // Added in 1.0.17 - if null (does not exist), set the default value as false
                settings.EnableGoogleTranslate ??= false;
            }
            else if (createNew)
            {
                settings = new Settings
                {
                    EnableKoreanPatch = true,
                    ScreenshotSavePath = System.IO.Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "CYKViewer"),
                    GameScreenSize = new GameScreenSize(1.0),
                    ScriptUpdateUrl = "https://newbiepr.gitlab.io/shinymaskr.gitlab.io/ShinyColors.user.js",
                    MenuOpened = true,
                    EnableGoogleTranslate = false
                };
            }

            return settings;
        }

        public static async void UpdateSettingsFile(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // TODO: Pull the non-config data like the patch version out to a separate type?
            if (e.PropertyName == "LocalizationPatchVersion" || e.PropertyName == "ClientVersion")
            {
                return;
            }

            // Don't care what exactly changed - just serialize the object then save it to the path.
            string settingsJson = JsonSerializer.Serialize((Settings)sender, s_serializerOptions);
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

        private void AddNewEntryOnEnter(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != System.Windows.Input.Key.Enter && e.Key != System.Windows.Input.Key.Return)
            {
                return;
            }

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

        private void profileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            setDefaultCheckBox.IsChecked = _settings.DefaultProfile == (string)profileList.SelectedValue;
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
