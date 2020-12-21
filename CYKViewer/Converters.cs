using System;
using System.Globalization;
using System.Windows.Data;

namespace CYKViewer
{
    public class GameScreenSizeToDoubleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            GameScreenSize size = (GameScreenSize)value;
            return size.Multiplier;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return new GameScreenSize((double)value);
        }
    }
}
