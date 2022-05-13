using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

using WindowPlacementNameSpace;

namespace CYKViewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly string s_scriptPath =
            System.IO.Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CYKViewer", "localization.js");

        public MainWindow()
        {
            InitializeComponent();
        }

        // https://stackoverflow.com/a/53817880
        // This method is save the actual position of the window to file "WindowName.pos"
        private void ClosingTrigger(object sender, EventArgs e)
        {
            this.SavePlacement();
        }
        // This method is load the actual position of the window from the file
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            this.ApplyPlacement();
        }

        private async void Window_Initialized(object sender, EventArgs e)
        {
            string[] commandLineArgs = Environment.GetCommandLineArgs();
            string defaultProfile = null;

            // Attempt to parse the command line arguments to see if there's a command line arg specified.
            for (int i = 0; i < commandLineArgs.Length; i++)
            {
                if (string.Equals(commandLineArgs[i], "-p", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(commandLineArgs[i], "--profile", StringComparison.OrdinalIgnoreCase)
                     && (i + 1 < commandLineArgs.Length))
                {
                    defaultProfile = commandLineArgs[i + 1];
                }
            }

            if (Microsoft.Web.WebView2.Core.CoreWebView2Environment.GetAvailableBrowserVersionString() == null)
            {
                // WebView is not installed, throw a popup that they need one
                _ = MessageBox.Show("Microsoft Edge WebView2를 발견하지 못했습니다. WebView2가 설치되어 있는지 확인해주세요.", "CYKViewer", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            // TODO: move the methods under 'StartupControl' to a better place?
            Settings settings = StartupControl.ReadSettings(false);
            defaultProfile ??= settings?.DefaultProfile;

            if (defaultProfile != null)
            {
                string pathToAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string userFolderPath = System.IO.Path.Join(pathToAppData, "CYKViewer", "userData", defaultProfile);
                if (Directory.Exists(userFolderPath))
                {
                    Version currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
                    settings.ClientVersion = currentVersion.ToString(3);
                    settings.PropertyChanged += StartupControl.UpdateSettingsFile;

                    GameControl gameControl = new(this, userFolderPath, settings);
                    Content = gameControl;

                    // Perform a simplified version check.
                    using HttpClient client = new();

                    Debug.WriteLine($"Current release is {currentVersion.ToString(3)}");
                    Version latestVersion = await StartupControl.GetLatestClientVersionAsync(client, currentVersion);

                    if (latestVersion != null)
                    {
                        Debug.WriteLine($"Latest release is {latestVersion}");
                        if (latestVersion > currentVersion)
                        {
                            settings.ClientVersion += " (업데이트 가능)";
                        }
                    }

                    // Check for localization patch's update
                    string onlineScript = null;
                    try
                    {
                        onlineScript = await client.GetStringAsync(settings.ScriptUpdateUrl);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Update check failed: {ex}");
                    }

                    Version scriptVersion = null;
                    bool onlineIsNewer = true;
                    if (onlineScript != null)
                    {
                        Version onlineScriptVersion = StartupControl.GetScriptVersion(onlineScript);
                        Debug.WriteLine($"The version of the script from update source is {onlineScriptVersion.ToString(3)}");
                        if (File.Exists(s_scriptPath))
                        {
                            string offlineScript = await File.ReadAllTextAsync(s_scriptPath);
                            Version offlineScriptVersion = StartupControl.GetScriptVersion(offlineScript);
                            Debug.WriteLine($"The version of the offline script is {offlineScriptVersion.ToString(3)}");
                            if (onlineScriptVersion > offlineScriptVersion)
                            {
                                scriptVersion = onlineScriptVersion;
                            }
                            else
                            {
                                onlineScript = offlineScript;
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

                            // When updating, also change the update URL.
                            string updateUrl = StartupControl.GetUpdateUrl(onlineScript);
                            if (updateUrl != null)
                            {
                                settings.ScriptUpdateUrl = updateUrl;
                            }
                            else
                            {
                                // no clue what to do if it's invalid.
                                Debug.WriteLine($"Failed to obtain a valid URL from the new script.");
                            }
                        }
                        Debug.WriteLine("Update logic complete.");
                    }

                    settings.LocalizationPatchVersion = scriptVersion is null ? "알 수 없음 (업데이트 실패)" : scriptVersion.ToString(3);

                    if (scriptVersion is not null && onlineIsNewer)
                    {
                        settings.LocalizationPatchVersion += " (새로고침 필요)";
                    }

                    return;
                }
            }

            Content = new StartupControl(this);
        }
    }
}
