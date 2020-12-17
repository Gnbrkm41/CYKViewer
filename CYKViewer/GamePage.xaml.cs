using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.WindowsAPICodePack.Dialogs;

using NAudio.CoreAudioApi;

namespace CYKViewer
{
    /// <summary>
    /// Interaction logic for GamePage.xaml
    /// </summary>
    public partial class GamePage : Page
    {
        private static readonly string scriptPath =
            System.IO.Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CYKViewer", "localization.js");

        private MenuEntry _commsExtraction;
        private MenuEntry _enableBgm;

        private readonly Settings _settings;
        private readonly System.Timers.Timer _statusBarTimer = new(10000);
        public MainWindow ParentWindow { get; set; }

        public GamePage(MainWindow parentWindow, string userDataFolder, Settings settings)
        {
            InitializeComponent();

            _settings = settings;
            settingsPanel.DataContext = _settings;
            ParentWindow = parentWindow;
            CoreWebView2CreationProperties props = new()
            {
                UserDataFolder = userDataFolder
            };
            webView.CreationProperties = props;

            webView.Source = new Uri("https://shinycolors.enza.fun");

            _statusBarTimer.Elapsed += StatusBarContentExpired;
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
                    _statusBarTimer.Start();
                    return;
                }
                break;
            }
            await file.WriteAsync(Convert.FromBase64String(screenshot.DataUrl.Split(',')[1]));
            statusBarTextBlock.Text = $"스크린샷 저장 완료: {file.Name}";

            // Reset the content of the status bar after 10 seconds
            _statusBarTimer.Start();
            file.Close();
        }

        private void ApplyKoreanPatchOnInitialLoad(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            webView.CoreWebView2.Settings.IsStatusBarEnabled = false;

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
            extractButton.Click += ExtractButton_Click;

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
            bgmButton.Click += BgmButton_Click;

            webView.CoreWebView2.AddHostObjectToScript("SC_CommsExtractionMenuEntry", _commsExtraction);
            webView.CoreWebView2.AddHostObjectToScript("SC_BgmEnableMenuEntry", _enableBgm);
        }

        private Task<string> EnableLocalizationPatch()
        {
            // Ensure CoreWebView is initialized first
            if (webView.CoreWebView2 == null)
            {
                return Task.FromResult<string>(null);
            }

            string prepScript = File.ReadAllText("scripts/pre-inject.js");
            string script = File.ReadAllText(scriptPath);

            return webView.ExecuteScriptAsync(prepScript + script);
        }

        private Task<string> DisableLocalizationPatch()
        {
            // Ensure CoreWebView is initialized first
            if (webView.CoreWebView2 == null)
            {
                return Task.FromResult<string>(null);
            }

            string prepScript = File.ReadAllText("scripts/pre-inject.js");

            return webView.ExecuteScriptAsync(prepScript);
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

        private void Page_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5 || e.Key == Key.BrowserRefresh)
            {
                webView.Reload();
                bgmButton.IsEnabled = false;
                extractButton.IsEnabled = false;
                e.Handled = true;
            }
        }

        private async void OnNavigationStart(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            // Disable the BGM / Comms extract button - the script should re-enable the buttons
            // if the URL is valid and the script is loaded.
            bgmButton.IsEnabled = false;
            extractButton.IsEnabled = false;

            if (locPatchCheckBox.IsChecked != true)
            {
                await DisableLocalizationPatch();
            }
            else
            {
                await EnableLocalizationPatch();
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

            screenshotDirTextBox.Text = dialog.FileName;
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

        private void muteButton_Click(object sender, RoutedEventArgs e)
        {
            using MMDeviceEnumerator deviceEnumerator = new MMDeviceEnumerator();
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

                    using Process process = Process.GetProcessById((int)session.GetProcessID);
                    if (process.ProcessName != "msedgewebview2")
                    {
                        session.Dispose();
                        continue;
                    }
                    webView2Session = session;
                    break;
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
