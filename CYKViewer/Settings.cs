using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CYKViewer
{
    public class Settings : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public bool _enableKoreanPatch;
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

        [JsonIgnore]
        public string LocalizationPatchVersion { get; set; }
    }
}
