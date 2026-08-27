using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Quick_Sab.Models;
using Quick_Sab.Services;

namespace Quick_Sab.Converters
{
    /// <summary>ActionType -> SolidColorBrush using the colours from the configuration.</summary>
    public class ActionTypeToBrushConverter : IValueConverter
    {
        public static Brush GetBrush(ActionType type)
        {
            var colors = ConfigService.Current?.Colors;
            var name = type.ToString();
            string hex = null;
            if (colors != null) colors.TryGetValue(name, out hex);
            return BrushFromHex(hex, Colors.Gray);
        }

        public static Brush BrushFromHex(string hex, Color fallback)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(hex))
                {
                    var c = (Color)ColorConverter.ConvertFromString(hex.Trim());
                    var b = new SolidColorBrush(c);
                    b.Freeze();
                    return b;
                }
            }
            catch { /* invalid colour -> fallback */ }

            var fb = new SolidColorBrush(fallback);
            fb.Freeze();
            return fb;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ActionType t) return GetBrush(t);
            if (value is string s && Enum.TryParse<ActionType>(s, true, out var t2)) return GetBrush(t2);
            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>Hex string -> Brush (preview in the configuration window).</summary>
    public class HexToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => ActionTypeToBrushConverter.BrushFromHex(value as string, Colors.Transparent);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
