using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace CYKViewer
{
    public class Settings : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool _enableKoreanPatch;
        public bool EnableKoreanPatch
        {
            get => _enableKoreanPatch;
            set
            {
                _enableKoreanPatch = value;
                NotifyPropertyChanged();
            }
        }

        private string _screenshotSavePath;
        public string ScreenshotSavePath 
        {
            get => _screenshotSavePath;
            set
            {
                _screenshotSavePath = value;
                NotifyPropertyChanged();
            }
        }

        // Property added in version 1.0.3
        private GameScreenSize _gameScreenSize;
        public GameScreenSize GameScreenSize
        {
            get => _gameScreenSize;
            set
            {
                _gameScreenSize = value;
                NotifyPropertyChanged();
            }
        }

        // Property added in version 1.0.4
        private string _scriptUpdateUrl;
        public string ScriptUpdateUrl
        {
            get => _scriptUpdateUrl;
            set
            {
                _scriptUpdateUrl = value;
                NotifyPropertyChanged();
            }
        }

        // Property added in version 1.0.7
        private string _defaultProfile;
        public string DefaultProfile
        {
            get => _defaultProfile;
            set
            {
                _defaultProfile = value;
                NotifyPropertyChanged();
            }
        }

        // Property added in 1.0.9
        private bool _menuOpened = false;
        public bool MenuOpened
        {
            get => _menuOpened;
            set
            {
                _menuOpened = value;
                NotifyPropertyChanged();
            }
        }

        // Those below are not configuration data

        private string _localizationPatchVersion;
        [JsonIgnore]
        public string LocalizationPatchVersion
        {
            get => _localizationPatchVersion;
            set
            {
                _localizationPatchVersion = value;
                NotifyPropertyChanged();
            }
        }

        private string _clientVersion;
        [JsonIgnore]
        public string ClientVersion
        {
            get => _clientVersion;
            set
            {
                _clientVersion = value;
                NotifyPropertyChanged();
            }
        }
    }
}
