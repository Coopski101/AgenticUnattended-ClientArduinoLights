using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.Configuration;

namespace ArduinoBridge;

public partial class App : Application
{
    private MainWindow? _mainWindow;
    private BridgeService? _bridge;
    private CancellationTokenSource? _cts;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var config = LoadConfig();
            _bridge = new BridgeService(config);
            _cts = new CancellationTokenSource();
            _mainWindow = new MainWindow();

            desktop.MainWindow = _mainWindow;
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            _bridge.LogMessage += msg =>
                Dispatcher.UIThread.Post(() => _mainWindow.AppendLog(msg));

            SetupTrayIcon(desktop);
            Task.Run(() => _bridge.RunAsync(_cts.Token));
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupTrayIcon(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var showItem = new NativeMenuItem("Show Log");
        showItem.Click += (_, _) =>
        {
            _mainWindow?.Show();
            _mainWindow?.Activate();
        };

        var exitItem = new NativeMenuItem("Exit");
        exitItem.Click += (_, _) =>
        {
            _cts?.Cancel();
            _bridge?.Dispose();
            if (_mainWindow is not null)
                _mainWindow.ForceClose = true;
            desktop.Shutdown();
        };

        var trayIcon = new TrayIcon
        {
            ToolTipText = "Arduino Beacon Bridge",
            Menu = new NativeMenu { showItem, new NativeMenuItemSeparator(), exitItem },
            IsVisible = true
        };

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "icon.png");
        if (File.Exists(iconPath))
            trayIcon.Icon = new WindowIcon(iconPath);

        trayIcon.Clicked += (_, _) =>
        {
            if (_mainWindow is null) return;
            if (_mainWindow.IsVisible)
                _mainWindow.Hide();
            else
            {
                _mainWindow.Show();
                _mainWindow.Activate();
            }
        };

        var icons = new TrayIcons { trayIcon };
        SetValue(TrayIcon.IconsProperty, icons);
    }

    private static BridgeConfig LoadConfig()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var config = new BridgeConfig();
        configuration.GetSection("Bridge").Bind(config);
        return config;
    }
}
