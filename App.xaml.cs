using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;
using PureUpdate.Core.Services;
using PureUpdate.UI.Views;
using PureUpdate.Utils;
using Wpf.Ui.Appearance;

namespace PureUpdate;

public partial class App : Application
{
    public static bool ForceExit { get; set; }

    private TaskbarIcon? _trayIcon;
    private int _lastUpdateCount;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var logDir = Path.Combine(AppContext.BaseDirectory, ".logs");
        Logger.Initialize(logDir);

        // Capture toutes les exceptions non gérées
        DispatcherUnhandledException += (_, ex) =>
        {
            Logger.Error($"[Unhandled] {ex.Exception}");
            ex.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
            Logger.Error($"[Unhandled] {ex.ExceptionObject}");

        try
        {
            ApplicationThemeManager.Apply(ApplicationTheme.Dark);
        }
        catch (Exception ex) { Logger.Error($"[Theme] {ex.Message}"); }

        bool headlessScan = e.Args.Contains("--scan");

        try
        {
            var settings = AppSettingsService.Load();
            var mainWin  = new MainWindow();
            mainWin.Closing += OnMainWindowClosing;
            MainWindow = mainWin;

            PureUpdate.Core.Services.ThemeService.Apply(settings);

            if (settings.StartMinimized || headlessScan)
                mainWin.WindowState = WindowState.Minimized;

            mainWin.Show();

            if (headlessScan)
            {
                mainWin.Hide();
                Logger.Info("[App] Mode --scan headless activé");
                Dispatcher.InvokeAsync(async () =>
                {
                    try
                    {
                        if (mainWin.DashboardVm?.ScanAllCommand is { } cmd)
                            await cmd.ExecuteAsync(null);
                        Logger.Info("[App] Scan headless terminé");
                    }
                    catch (Exception ex) { Logger.Error($"[App] Scan headless: {ex.Message}"); }
                    finally { ForceExit = true; Shutdown(); }
                });
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"[Startup] {ex}");
            return;
        }

        if (!PrivilegeHelper.IsRunningAsAdministrator())
        {
            Logger.Warn("Application lancée sans droits administrateur");
            MessageBox.Show(
                "PureUpdate nécessite des droits Administrateur pour fonctionner correctement.\n" +
                "Certaines fonctions (Windows Update, installation de paquets) seront indisponibles.",
                "Droits insuffisants",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        // Tray icon après Show() pour ne pas bloquer le démarrage
        Dispatcher.InvokeAsync(() =>
        {
            try
            {
                _trayIcon = BuildTrayIcon();
                NotificationService.UpdatesFound        += OnUpdatesFound;
                NotificationService.TotalUpdatesChanged += count => UpdateTrayBadge(count);
                NotificationService.RebootRequired      += OnRebootRequired;
                NotificationService.RestorePointCreated += OnRestorePointCreated;
            }
            catch (Exception ex)
            {
                Logger.Warn($"[Tray] {ex.Message}");
            }
        }, DispatcherPriority.Background);
    }

    private TaskbarIcon BuildTrayIcon()
    {
        var icon = new TaskbarIcon
        {
            IconSource  = new BitmapImage(new Uri("pack://application:,,,/Resources/PureUpdate.ico")),
            ToolTipText = "PureUpdate — Gestionnaire de mises à jour",
            Visibility  = Visibility.Visible,
        };

        var menu = new ContextMenu();
        var open = new MenuItem { Header = "Ouvrir PureUpdate" };
        open.Click += (_, _) => ShowMainWindow();
        menu.Items.Add(open);
        menu.Items.Add(new Separator());
        var quit = new MenuItem { Header = "Quitter" };
        quit.Click += (_, _) => { ForceExit = true; Shutdown(); };
        menu.Items.Add(quit);

        icon.ContextMenu           = menu;
        icon.TrayMouseDoubleClick += (_, _) => ShowMainWindow();
        return icon;
    }

    private void ShowMainWindow()
    {
        if (MainWindow is null) return;
        MainWindow.Show();
        MainWindow.WindowState = WindowState.Normal;
        MainWindow.Activate();
    }

    private void OnMainWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (ForceExit) return;
        var settings = AppSettingsService.Load();
        if (settings.CloseToTray && sender is Window win)
        {
            e.Cancel = true;
            win.Hide();
            _trayIcon?.ShowBalloonTip("PureUpdate", "L'application est réduite dans la barre système.", BalloonIcon.Info);
        }
    }

    private void OnUpdatesFound(int count) =>
        _trayIcon?.ShowBalloonTip("PureUpdate", $"{count} mise(s) à jour disponible(s) !", BalloonIcon.Info);

    private void OnRebootRequired() =>
        _trayIcon?.ShowBalloonTip("PureUpdate", "Un redémarrage est requis pour finaliser les mises à jour.", BalloonIcon.Warning);

    private void OnRestorePointCreated(string msg) =>
        _trayIcon?.ShowBalloonTip("PureUpdate", $"Point de restauration créé : {msg}", BalloonIcon.Info);

    private void UpdateTrayBadge(int count)
    {
        if (_trayIcon is null) return;
        _lastUpdateCount = count;

        try
        {
            if (count <= 0)
            {
                _trayIcon.IconSource = new BitmapImage(new Uri("pack://application:,,,/Resources/PureUpdate.ico"));
                _trayIcon.ToolTipText = "PureUpdate — Tous les paquets sont à jour";
                return;
            }

            var baseImg = new BitmapImage(new Uri("pack://application:,,,/Resources/PureUpdate.ico"));
            var dv = new System.Windows.Media.DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawImage(baseImg, new Rect(0, 0, 32, 32));
                // Badge rouge
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(220, 40, 40)), null, new Point(25, 7), 9, 9);
                string text = count > 99 ? "99+" : count.ToString();
                double fs   = count > 9 ? 6.5 : 8.0;
                var ft = new FormattedText(text,
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Arial"),
                    fs, Brushes.White, 1.0);
                dc.DrawText(ft, new Point(25 - ft.Width / 2, 7 - ft.Height / 2));
            }

            var rtb = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            rtb.Freeze();

            // Hardcodet IconSource ne supporte pas un RenderTargetBitmap (résolution URI) :
            // passer par une vraie System.Drawing.Icon
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using var ms = new MemoryStream();
            enc.Save(ms);
            ms.Position = 0;
            using var bmp = new System.Drawing.Bitmap(ms);
            IntPtr hIcon = bmp.GetHicon();
            var newIcon  = System.Drawing.Icon.FromHandle(hIcon);

            _trayIcon.Icon        = newIcon;
            _trayIcon.ToolTipText = $"PureUpdate — {count} mise(s) à jour en attente";

            if (_lastBadgeIconHandle != IntPtr.Zero) DestroyIcon(_lastBadgeIconHandle);
            _lastBadgeIconHandle = hIcon;
        }
        catch (Exception ex) { Logger.Warn($"[Tray] Badge: {ex.Message}"); }
    }

    private IntPtr _lastBadgeIconHandle = IntPtr.Zero;

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    protected override void OnExit(ExitEventArgs e)
    {
        NotificationService.UpdatesFound        -= OnUpdatesFound;
        NotificationService.TotalUpdatesChanged -= count => UpdateTrayBadge(count);
        NotificationService.RebootRequired      -= OnRebootRequired;
        NotificationService.RestorePointCreated -= OnRestorePointCreated;
        _trayIcon?.Dispose();
        Logger.Shutdown();
        base.OnExit(e);
    }
}
