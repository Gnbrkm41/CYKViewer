﻿using System.ComponentModel;
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

        [JsonIgnore]
        public string ClientVersion { get; set; }
    }
}
