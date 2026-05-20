using System.Windows;
using System.Windows.Media;
using PureUpdate.Core.Models;

namespace PureUpdate.Core.Services;

public static class ThemeService
{
    public static void Apply(AppSettings settings)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(settings.AccentColor);
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                Application.Current.Resources["ElectricCyan"]      = color;
                Application.Current.Resources["ElectricCyanBrush"] = brush;

                if (Application.Current.MainWindow is { } win)
                    win.FontFamily = new FontFamily(settings.FontFamily);
            }
            catch { }
        });
    }
}
