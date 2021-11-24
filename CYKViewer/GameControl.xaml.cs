using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;

using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.WindowsAPICodePack.Dialogs;
using Microsoft.WindowsAPICodePack.Shell.Interop;

using NAudio.CoreAudioApi;

namespace CYKViewer
{
    /// <summary>
    /// Interaction logic for GameControl.xaml
    /// </summary>
    public partial class GameControl : UserControl
    {
        private static readonly string s_scriptPath =
            System.IO.Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CYKViewer", "localization.js");

        private MenuEntry _commsExtraction;
        private MenuEntry _enableBgm;
        private MenuEntry _devMode;

        private readonly Settings _settings;
        private readonly System.Timers.Timer _statusBarTimer = new(10000) { AutoReset = false, Enabled = false };
        public MainWindow ParentWindow { get; set; }

        public static RoutedCommand ScreenshotCommand = new RoutedCommand();

        public GameControl(MainWindow parentWindow, string userDataFolder, Settings settings)
        {
            InitializeComponent();

            _settings = settings;
            sidePanelScrollViewer.DataContext = _settings;
            menuOpenToggleButton.DataContext = _settings;
            ParentWindow = parentWindow;
            CoreWebView2CreationProperties props = new()
            {
                UserDataFolder = userDataFolder
            };
            webView.CreationProperties = props;

            webView.Source = new Uri("https://shinycolors.enza.fun");

            _statusBarTimer.Elapsed += StatusBarContentExpired;

            ScreenshotCommand.InputGestures.Add(new KeyGesture(Key.F8));
            CommandBindings.Add(new CommandBinding(ScreenshotCommand, CaptureButton_Click));
        }

        private void StatusBarContentExpired(object sender, System.Timers.ElapsedEventArgs e)
        {
            _ = Dispatcher.InvokeAsync(() => statusBarTextBlock.Text = "");
            ((System.Timers.Timer)sender).Stop();
        }

        private void DebugMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            Debug.WriteLine(e.TryGetWebMessageAsString());
        }

        private async void CaptureButton_Click(object sender, RoutedEventArgs e)
        {
            TaskCompletionSource tcs = new();
            ScreenshotObject screenshot = new(tcs);

            if (webView?.CoreWebView2 == null)
            {
                return;
            }

            webView.CoreWebView2.AddHostObjectToScript(screenshot.ObjectId, screenshot);
            string script =
$@"(function()
{{
    const screenshot = chrome.webview.hostObjects.{screenshot.ObjectId};
    const canvas = document.querySelector('canvas');
    requestAnimationFrame(function()
        {{
            var dataUrl = canvas.toDataURL(""image/png"", 1);
            screenshot.Complete(dataUrl).sync();
        }});
}})();
";
            _ = await webView.ExecuteScriptAsync(script);
            await tcs.Task;
            webView.CoreWebView2.RemoveHostObjectFromScript(screenshot.ObjectId);

            string screenshotFolder = screenshotDirTextBox.Text;
            _ = Directory.CreateDirectory(screenshotFolder);

            FileStream file;
            int retryCount = 0;
            try
            {
                DirectoryInfo directory = Directory.CreateDirectory(screenshotFolder);
            }
            catch (Exception ex)
            {
                statusBarTextBlock.Text = $"스크린샷 저장 실패: {ex.Message}";
                _statusBarTimer.Stop();
                _statusBarTimer.Start();
                return;
            }
            while (true)
            {
                try
                {
                    string pathToNewFile = System.IO.Path.Join(screenshotFolder,
                        $"Screenshot_{DateTime.Now:yyyyMMdd_HHmmss}{(retryCount > 0 ? $" ({retryCount})" : "")}.png");
                    file = new FileStream(pathToNewFile, FileMode.CreateNew);
                }
                catch (IOException ex) when (ex.Message.Contains("already exist"))
                {
                    retryCount++;
                    continue;
                }
                catch (Exception ex)
                {
                    statusBarTextBlock.Text = $"스크린샷 저장 실패: {ex.Message}";
                    _statusBarTimer.Stop();
                    _statusBarTimer.Start();
                    return;
                }
                break;
            }
            await file.WriteAsync(Convert.FromBase64String(screenshot.DataUrl.Split(',')[1]));
            statusBarTextBlock.Text = $"스크린샷 저장 완료: {file.Name}";

            // Reset the content of the status bar after 10 seconds
            _statusBarTimer.Stop();
            _statusBarTimer.Start();
            file.Close();
        }

        private void PrepareLocalizationPatchOnInitialLoad(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

            if (!e.IsSuccess)
            {
                Debug.WriteLine("Initialization failed?");
            }

            _commsExtraction = new MenuEntry(obj => {
                if (obj.Name == null)
                {
                    extractButton.IsEnabled = false;
                }
                else
                {
                    extractButton.Content = obj.Name;
                    extractButton.IsEnabled = true;
                }
            });

            _enableBgm = new MenuEntry(obj =>
            {
                if (obj.Name == null)
                {
                    bgmButton.IsEnabled = false;
                }
                else
                {
                    bgmButton.Content = obj.Name;
                    bgmButton.IsEnabled = true;
                }
            });

            _devMode = new MenuEntry(obj =>
            {
                if (obj.Name == null)
                {
                    devModeButton.IsEnabled = false;
                }
                else
                {
                    devModeButton.Content = obj.Name;
                    devModeButton.IsEnabled = true;
                }
            });

            webView.CoreWebView2.AddHostObjectToScript("SC_CommsExtractionMenuEntry", _commsExtraction);
            webView.CoreWebView2.AddHostObjectToScript("SC_BgmEnableMenuEntry", _enableBgm);
            webView.CoreWebView2.AddHostObjectToScript("SC_DevModeEnableMenuEntry", _devMode);
        }

        private async void BgmButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("HOST: BGM Button Clicked");
            _ = await webView.CoreWebView2.ExecuteScriptAsync($"Implementation_InvokeHandler(\"{_enableBgm.Id}\");");
        }

        private async void ExtractButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("HOST: Comms Extract Button Clicked");
            _ = await webView.CoreWebView2.ExecuteScriptAsync($"Implementation_InvokeHandler(\"{_commsExtraction.Id}\");");
        }

        private void ConfigureAlwaysOnTop(object sender, RoutedEventArgs e)
        {
            ToggleButton button = (ToggleButton)sender;
            ParentWindow.Topmost = button.IsChecked == true;
        }

        private void Control_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5 || e.Key == Key.BrowserRefresh)
            {
                webView.Reload();
                e.Handled = true;
            }
        }

        private async void OnNavigationStart(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            // Disable the BGM / Comms extract button - the script should re-enable the buttons
            // if the URL is valid and the script is loaded.
            bgmButton.IsEnabled = false;
            extractButton.IsEnabled = false;
            devModeButton.IsEnabled = false;

            // Ensure CoreWebView is initialized first
            if (webView.CoreWebView2 == null)
            {
                return;
            }

            string scriptToExecute = File.ReadAllText("scripts/pre-inject.js");

            if (e.Uri.Contains("shinycolors.enza.fun"))
            {
                string patchScript = null;
                if (locPatchCheckBox.IsChecked == true)
                {
                    try
                    {
                        patchScript = File.ReadAllText(s_scriptPath);
                    }
                    catch (IOException ex)
                    {
                        Debug.WriteLine($"Failed to read the script: {ex}");
                    }
                }
                else // Alternative script for supporting background BGM functionality without the loc. script
                {
                    try
                    {
                        patchScript = File.ReadAllText("scripts/alt-script.js");
                    }
                    catch (IOException ex)
                    {
                        Debug.WriteLine($"Failed to read the script: {ex}");
                    }
                }

                scriptToExecute += patchScript;
            }

            _ = await webView.ExecuteScriptAsync(scriptToExecute);

            if (_settings.LocalizationPatchVersion?.EndsWith("(새로고침 필요)") == true)
            {
                // Effectively refreshed, so remove the notification
                _settings.LocalizationPatchVersion = _settings.LocalizationPatchVersion.Split(' ')[0];
            }
        }

        private void DetectEnterAndUpdate(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return || e.Key == Key.Enter)
            {
                ((TextBox)sender).GetBindingExpression(TextBox.TextProperty).UpdateSource();
                e.Handled = true;
            }
        }

        private void SelectScreenshotFolder(object sender, RoutedEventArgs e)
        {
            using CommonOpenFileDialog dialog = new();
            dialog.IsFolderPicker = true;
            dialog.Multiselect = false;
            dialog.Title = "Select Folder";

            CommonFileDialogResult result = dialog.ShowDialog(ParentWindow);

            if (result != CommonFileDialogResult.Ok)
                return;

            _settings.ScreenshotSavePath = dialog.FileName;
        }

        private void Refresh(object sender, RoutedEventArgs e)
        {
            webView.Reload();
        }

        private void GoBackward(object sender, RoutedEventArgs e)
        {
            webView.GoBack();
        }

        private void GoForward(object sender, RoutedEventArgs e)
        {
            webView.GoForward();
        }

        private void ChangeWebViewMuteStatus(object sender, RoutedEventArgs e)
        {
            using MMDeviceEnumerator deviceEnumerator = new();
            MMDevice defaultPlaybackDevice;
            try
            {
                // It appears that the webview always use the default playback device.
                defaultPlaybackDevice = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            }
            catch (COMException ex) when ((uint)ex.HResult == 0x80070490) // E_NOTFOUND; returned when no default device exists
            {
                return;
            }

            AudioSessionManager manager = null;
            AudioSessionControl webView2Session = null;
            try
            {
                manager = defaultPlaybackDevice.AudioSessionManager;
                SessionCollection playbackSessions = manager.Sessions;
                for (int i = 0; i < playbackSessions.Count; i++)
                {
                    // Unfortunately, the webview creates multiple processes and the process ID we get from CoreWebView2 isn't for the process that plays the audio
                    AudioSessionControl session = playbackSessions[i];

                    try
                    {
                        using Process process = Process.GetProcessById((int)session.GetProcessID);
                        if (process.ProcessName != "msedgewebview2")
                        {
                            session.Dispose();
                            continue;
                        }
                        webView2Session = session;
                        break;
                    }
                    catch (ArgumentException ae)
                    {
                        // Somehow, there is an entry in the playbacksessions collection
                        // however it is not a valid PID for some reason (out of luck?)
                        // In which case, just dispose the session control object and continue
                        session.Dispose();
                    }
                }

                if (webView2Session == null)
                {
                    return;
                }

                webView2Session.SimpleAudioVolume.Mute = ((ToggleButton)sender).IsChecked == true;
            }
            finally
            {
                defaultPlaybackDevice?.Dispose();
                manager?.Dispose();
                webView2Session?.Dispose();
            }
        }

        private void ResolutionSelectionChanged(object sender, DataTransferEventArgs e)
        {
            UpdateScreenSize();
        }

        private async void ForceUpdateScript(object sender, RoutedEventArgs e)
        {
            statusBarTextBlock.Text = "패치 스크립트 업데이트 확인 중...";
            if (!Uri.TryCreate(scriptUpdateUrlTextBox.Text, UriKind.Absolute, out Uri updateUrl))
            {
                statusBarTextBlock.Text = "스크립트 업데이트 실패: 제공된 주소가 올바르지 않습니다.";
                _statusBarTimer.Stop();
                _statusBarTimer.Start();
                return;
            }

            using HttpClient client = new();
            string script = null;
            try
            {
                script = await client.GetStringAsync(updateUrl);
            }
            catch (HttpRequestException hrEx)
            {
                statusBarTextBlock.Text = $"스크립트 업데이트 실패 (서버 연결 실패): {hrEx.Message}";
                _statusBarTimer.Stop();
                _statusBarTimer.Start();
                return;
            }
            catch (TaskCanceledException tcEx)
            {
                statusBarTextBlock.Text = $"스크립트 업데이트 실패 (연결 중 시간 초과): {tcEx.Message}";
                _statusBarTimer.Stop();
                _statusBarTimer.Start();
                return;
            }
            catch (Exception ex)
            {
                statusBarTextBlock.Text = $"스크립트 업데이트 실패: {ex.Message}";
                _statusBarTimer.Stop();
                _statusBarTimer.Start();
                return;
            }

            try
            {
                await File.WriteAllTextAsync(s_scriptPath, script);
            }
            catch (IOException ex)
            {
                statusBarTextBlock.Text = $"스크립트 업데이트 실패 (파일 저장 실패): {ex.Message}";
                _statusBarTimer.Stop();
                _statusBarTimer.Start();
                return;
            }

            Match scriptVersionMatch = Regex.Match(script, @"\s*\/\/\s*@version\s*(?<version>.*?)\s*$", RegexOptions.Multiline);
            if (scriptVersionMatch.Success)
            {
                string versionString = scriptVersionMatch.Groups["version"].Value;
                _settings.LocalizationPatchVersion = versionString + " (새로고침 필요)";

                statusBarTextBlock.Text = $"스크립트 업데이트 성공 (새로고침 후 적용됩니다)";
                _statusBarTimer.Stop();
                _statusBarTimer.Start();
            }
            else
            {
                statusBarTextBlock.Text = $"스크립트 업데이트 경고: 스크립트 버전이 확인되지 않았습니다. 오작동의 우려가 있습니다";
                _statusBarTimer.Stop();
                _statusBarTimer.Start();
            }

            // When updating, also change the update URL.
            Match updateUrlMatch = Regex.Match(script, @"\s*\/\/\s*@updateURL\s*(?<updateUrl>.*?)\s*$", RegexOptions.Multiline);
            if (updateUrlMatch.Success)
            {
                Group updateUrlGroup = updateUrlMatch.Groups["updateUrl"];

                if (updateUrlGroup.Success && Uri.TryCreate(updateUrlGroup.Value, UriKind.Absolute, out _))
                {
                    _settings.ScriptUpdateUrl = updateUrlGroup.Value;
                }
                else
                {
                    // no clue what to do if it's invalid.
                    Debug.WriteLine($"Failed to obtain a valid URL from the new script: {updateUrlGroup.Value}");
                }
            }
        }

        private void ReturnToProfileSelection(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show(ParentWindow, "정말 프로필 선택으로 돌아갑니까?", "확인", MessageBoxButton.YesNo);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            _ = ParentWindow.Content = new StartupControl(ParentWindow);
            webView.Dispose();
        }

        private void menuOpenToggleButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateScreenSize();
        }

        private void UpdateScreenSize()
        {
            bool menuOpen = menuOpenToggleButton.IsChecked == true;
            const int menuWidth = 306;

            GameScreenSize value = (GameScreenSize)resolutionSelectionComboBox.SelectedItem;
            if (double.IsNaN(value.Multiplier))
            {
                // Set to auto-fit
                webViewBorder.Width = double.NaN;
                webViewBorder.Height = double.NaN;
                ParentWindow.MinWidth = menuOpen ? menuWidth : 0;
                ParentWindow.MinHeight = 0;
                webViewBorder.HorizontalAlignment = HorizontalAlignment.Stretch;
                webViewBorder.VerticalAlignment = VerticalAlignment.Stretch;
            }
            else
            {
                // There's a weird discrepancy between the window size and the actual control's size. 16 is the *correct* offset
                // for the window to be fully visible.
                // The 306 offset is for the menu to be fully visible (300 for the panel itself, 6 for the margin)
                ParentWindow.MinWidth = value.Width + 16 + (menuOpen ? menuWidth : 0);
                ParentWindow.MinHeight = value.Height + 60;
                webViewBorder.Width = value.Width;
                webViewBorder.Height = value.Height;
                webViewBorder.HorizontalAlignment = HorizontalAlignment.Left;
                webViewBorder.VerticalAlignment = VerticalAlignment.Top;

                if (!menuOpen)
                {
                    ParentWindow.Width -= menuWidth;
                }
            }
        }

        private async void devModeButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("HOST: DevMode Button Clicked");
            _ = await webView.CoreWebView2.ExecuteScriptAsync($"Implementation_InvokeHandler(\"{_devMode.Id}\");");
        }
    }
    public class GameScreenSize
    {
        public GameScreenSize() : this(1d) { }
        public GameScreenSize(double multiplier)
        {
            Multiplier = multiplier;
        }

        public double Multiplier { get; set; }
        [JsonIgnore]
        public int Width => (int)(1136 * Multiplier);
        [JsonIgnore]
        public int Height => (int)(640 * Multiplier);

        public override string ToString()
        {
            return double.IsNaN(Multiplier) ?
                "화면 맞춤" :
                $"{Multiplier:P0} ({Width} * {Height})";
        }
    }

    [ComVisible(true)]
    public class ScreenshotObject
    {
        public ScreenshotObject(TaskCompletionSource tcs)
        {
            _tcs = tcs;
            ObjectId = $"screenshot{DateTime.Now.Ticks}";
        }
        public string ObjectId { get; }
        private readonly TaskCompletionSource _tcs;
        public string DataUrl { get; set; }

        public void Complete(string str)
        {
            try
            {
                DataUrl = str;
                _tcs.SetResult();
            }
            catch (Exception ex)
            {
                _tcs.SetException(ex);
            }
        }
    }

    [ComVisible(true)]
    public class MenuEntry
    {
        public MenuEntry(Action<MenuEntry> callback)
        {
            _action = callback;
        }

        public string Id { get; set; }
        public string Name { get; set; }

        private readonly Action<MenuEntry> _action;
        public void Set(string id, string name)
        {
            Id = id;
            Name = name;
            _action(this);
        }
    }
}
