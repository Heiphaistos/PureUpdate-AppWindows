using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PureUpdate.Core.Models;
using PureUpdate.Core.Providers;
using PureUpdate.Utils;

namespace PureUpdate.UI.ViewModels;

public partial class ProviderUninstallTabViewModel : ObservableObject
{
    private readonly IUninstallProvider     _provider;
    private CancellationTokenSource?        _cts;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActive))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActive))]
    private bool _isUninstalling;

    [ObservableProperty] private string _statusText   = "Cliquez sur 'Charger' pour afficher les paquets installés";
    [ObservableProperty] private string _progressText = string.Empty;
    [ObservableProperty] private int    _selectedCount;
    [ObservableProperty] private bool   _isAvailable;

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
                FilteredItems.Refresh();
        }
    }

    public string          Name        { get; }
    public SolidColorBrush AccentBrush { get; }
    public bool            IsActive    => IsLoading || IsUninstalling;

    public ObservableCollection<UninstallableItem> AllItems { get; } = [];
    public ICollectionView                          FilteredItems { get; }

    public ProviderUninstallTabViewModel(IUninstallProvider provider, string name, string accentHex, bool isAvailable)
    {
        _provider   = provider;
        Name        = name;
        IsAvailable = isAvailable;
        AccentBrush = HexToBrush(accentHex);

        FilteredItems = CollectionViewSource.GetDefaultView(AllItems);
        FilteredItems.Filter = obj =>
            obj is UninstallableItem u &&
            (string.IsNullOrEmpty(SearchText) ||
             u.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
             u.Id.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        AllItems.CollectionChanged += (_, _) => RefreshSelectedCount();
    }

    [RelayCommand]
    private async Task LoadAsync(CancellationToken ct = default)
    {
        if (IsLoading || !IsAvailable) return;

        _cts?.Cancel(); _cts?.Dispose();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        IsLoading  = true;
        StatusText = "Chargement de la liste...";
        AllItems.Clear();

        try
        {
            var packages = await _provider.GetInstalledPackagesAsync(_cts.Token);
            foreach (var p in packages)
            {
                var item = new UninstallableItem(p);
                item.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(UninstallableItem.IsSelected))
                        RefreshSelectedCount();
                };
                AllItems.Add(item);
            }
            StatusText = packages.Count > 0
                ? $"{packages.Count} paquet(s) installé(s)"
                : "Aucun paquet trouvé";
        }
        catch (OperationCanceledException) { StatusText = "Annulé"; }
        catch (Exception ex) { Logger.Error($"[{Name}] Chargement: {ex.Message}"); StatusText = "Erreur de chargement"; }
        finally { IsLoading = false; }
    }

    [RelayCommand(CanExecute = nameof(CanUninstall))]
    private async Task UninstallAsync(CancellationToken ct = default)
    {
        var selected = AllItems.Where(i => i.IsSelected).Select(i => i.Source).ToList();
        if (selected.Count == 0) return;

        _cts?.Cancel(); _cts?.Dispose();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        IsUninstalling = true;
        StatusText     = $"Désinstallation de {selected.Count} paquet(s)...";
        var progress   = new Progress<string>(msg => ProgressText = msg);

        try
        {
            var result = await _provider.UninstallPackagesAsync(selected, progress, _cts.Token);

            // Retirer les paquets désinstallés avec succès de la liste
            if (result.UninstalledCount > 0)
            {
                var failSet = new HashSet<string>(result.Errors ?? [], StringComparer.OrdinalIgnoreCase);
                var toRemove = AllItems
                    .Where(i => i.IsSelected && !failSet.Contains(i.Title))
                    .ToList();
                foreach (var item in toRemove) AllItems.Remove(item);
            }

            StatusText = result.Success
                ? $"{result.UninstalledCount} paquet(s) désinstallé(s)"
                : $"{result.UninstalledCount} réussi(s), {result.FailedCount} erreur(s) : {string.Join(", ", result.Errors ?? [])}";

            Logger.Info($"[{Name}] Désinstallation terminée: {result.Message}");
        }
        catch (OperationCanceledException) { StatusText = "Annulé"; }
        catch (Exception ex) { Logger.Error($"[{Name}] Désinstallation: {ex.Message}"); StatusText = "Erreur"; }
        finally { IsUninstalling = false; ProgressText = string.Empty; UninstallCommand.NotifyCanExecuteChanged(); }
    }

    private bool CanUninstall() => !IsUninstalling && SelectedCount > 0;

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var item in AllItems) item.IsSelected = true;
        RefreshSelectedCount();
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var item in AllItems) item.IsSelected = false;
        RefreshSelectedCount();
    }

    private void RefreshSelectedCount()
    {
        SelectedCount = AllItems.Count(i => i.IsSelected);
        UninstallCommand.NotifyCanExecuteChanged();
    }

    private static SolidColorBrush HexToBrush(string hex)
    {
        try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
        catch { return new SolidColorBrush(Colors.Gray); }
    }
}
