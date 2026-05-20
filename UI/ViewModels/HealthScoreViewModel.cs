using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PureUpdate.UI.ViewModels;

public partial class HealthScoreViewModel : ObservableObject
{
    [ObservableProperty] private int    _score       = 100;
    [ObservableProperty] private string _label       = "Excellent";
    [ObservableProperty] private Brush  _scoreBrush  = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));

    public void Update(int pendingUpdates, bool rebootRequired)
    {
        int s = 100 - Math.Min(pendingUpdates * 3, 60) - (rebootRequired ? 20 : 0);
        s = Math.Clamp(s, 0, 100);
        Score = s;
        (Label, ScoreBrush) = s switch
        {
            >= 80 => ("Excellent",       new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50))),
            >= 60 => ("Bon",             new SolidColorBrush(Color.FromRgb(0xFF, 0xB8, 0x4A))),
            >= 40 => ("À mettre à jour", new SolidColorBrush(Color.FromRgb(0xFF, 0x69, 0x00))),
            _     => ("Critique",        new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50))),
        };
    }
}
