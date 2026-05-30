using System.Windows;
using System.Windows.Media;
using PureUpdate.Core.Models;

namespace PureUpdate.Core.Services;

public sealed record AppTheme(
    string Name,
    string Accent,
    string AppBg,
    string CardBg1,
    string CardBg2)
{
    public SolidColorBrush AccentBrush { get; } =
        new((Color)ColorConverter.ConvertFromString(Accent));
    public SolidColorBrush AppBgBrush  { get; } =
        new((Color)ColorConverter.ConvertFromString(AppBg));
}

public static class ThemeService
{
    public static readonly IReadOnlyList<AppTheme> Presets =
    [
        new("Deep Space",   "#00B7FF", "#060C18", "#091525", "#0D1C2E"),
        new("Midnight",     "#9B59B6", "#070410", "#0F081E", "#140B26"),
        new("Forest",       "#2ECC71", "#040D06", "#071308", "#0A180A"),
        new("Crimson",      "#E74C3C", "#0F0507", "#1A0808", "#1E0B0B"),
        new("Amber",        "#F59E0B", "#0E0B04", "#191005", "#1D1408"),
        new("Arctic",       "#67E8F9", "#050C12", "#071018", "#0A161E"),
        new("Obsidian",     "#A0A0B8", "#090909", "#0F0F11", "#131315"),
        new("Sakura",       "#E91E63", "#0F050A", "#18080F", "#1E0A13"),
        new("Matrix",       "#22D368", "#020A04", "#040E06", "#06120A"),
        new("Solar",        "#FF6B35", "#0F0805", "#1A0E08", "#1E120A"),
        new("Neon Purple",  "#C026D3", "#080410", "#100618", "#150A1E"),
        new("Gold",         "#F0C040", "#0C0A03", "#181205", "#1C1608"),
    ];

    public static void Apply(AppSettings settings)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            try
            {
                ApplyColors(
                    settings.AccentColor,
                    settings.AppBgColor,
                    settings.CardBg1Color,
                    settings.CardBg2Color,
                    settings.FontFamily);
            }
            catch { }
        });
    }

    public static void ApplyTheme(AppTheme theme, string fontFamily = "Segoe UI")
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            try
            {
                ApplyColors(theme.Accent, theme.AppBg, theme.CardBg1, theme.CardBg2, fontFamily);
            }
            catch { }
        });
    }

    private static void ApplyColors(
        string accentHex, string appBgHex, string card1Hex, string card2Hex, string font)
    {
        var accent  = (Color)ColorConverter.ConvertFromString(accentHex);
        var appBg   = (Color)ColorConverter.ConvertFromString(appBgHex);
        var card1   = (Color)ColorConverter.ConvertFromString(card1Hex);
        var card2   = (Color)ColorConverter.ConvertFromString(card2Hex);

        var accentBrush = new SolidColorBrush(accent);
        var appBgBrush  = new SolidColorBrush(appBg);
        accentBrush.Freeze();
        appBgBrush.Freeze();

        var res = Application.Current.Resources;
        res["ElectricCyan"]      = accent;
        res["ElectricCyanBrush"] = accentBrush;
        res["AppBgColor"]        = appBg;
        res["AppBgBrush"]        = appBgBrush;
        res["CardBg1Color"]      = card1;
        res["CardBg2Color"]      = card2;

        if (Application.Current.MainWindow is { } win)
            win.FontFamily = new FontFamily(font);
    }
}
