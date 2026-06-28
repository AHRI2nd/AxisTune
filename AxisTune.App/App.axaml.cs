using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using AxisTune.App.Services;
using AxisTune.App.ViewModels;

namespace AxisTune.App;

public partial class App : Application
{
    private readonly TuningEngine _engine = new();
    private AppSettings _settings = new();
    private MainViewModel? _viewModel;
    private MainWindow? _mainWindow;
    private TrayIcon? _trayIcon;
    private NativeMenuItem? _trayToggleItem;
    private bool _isExiting;
    private bool _engineShutDown;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 창을 닫아도 앱이 종료되지 않도록(트레이 백그라운드 실행).
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            _settings = SettingsStore.Load();
            _viewModel = new MainViewModel(_engine, _settings);

            _mainWindow = new MainWindow { DataContext = _viewModel };
            _mainWindow.Closing += OnMainWindowClosing;
            desktop.MainWindow = _mainWindow;

            SetupTrayIcon();

            _engine.StatusChanged += s => Dispatcher.UIThread.Post(() => UpdateTray(s));
            desktop.Exit += (_, _) => ShutdownEngine();

            // 엔진 시작 후 드라이버 점검 / 장치 열거 / 자동 적용.
            _engine.Start();
            _viewModel.ProbeDrivers();
            _viewModel.RefreshDevices();
            ApplyStartupAutomation();

            bool startMinimized = HasMinimizedArg();
            if (startMinimized)
                _mainWindow.Hide();
            else
                _mainWindow.Show();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ApplyStartupAutomation()
    {
        // 마지막 장치 자동 선택은 RefreshDevices→PopulateDevices에서 처리.
        if (_settings.AutoEnableOnStartup)
        {
            // 장치 열거가 끝난 뒤 활성화되도록 UI 큐 뒤에 배치.
            Dispatcher.UIThread.Post(() => _engine.RequestSetEnabled(true),
                DispatcherPriority.Background);
        }
    }

    private void SetupTrayIcon()
    {
        var toggle = new NativeMenuItem("드라이버 켜기/끄기");
        toggle.Click += (_, _) =>
        {
            if (_viewModel is not null)
                _viewModel.IsDriverEnabled = !_viewModel.IsDriverEnabled;
        };
        _trayToggleItem = toggle;

        var open = new NativeMenuItem("열기");
        open.Click += (_, _) => ShowMainWindow();

        var exit = new NativeMenuItem("종료");
        exit.Click += (_, _) => ExitApplication();

        var menu = new NativeMenu();
        menu.Items.Add(open);
        menu.Items.Add(toggle);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(exit);

        _trayIcon = new TrayIcon
        {
            Icon = LoadIcon(),
            ToolTipText = "AxisTune — 드라이버 꺼짐",
            Menu = menu,
            IsVisible = true,
        };
        _trayIcon.Clicked += (_, _) => ShowMainWindow();

        TrayIcon.SetIcons(this, new TrayIcons { _trayIcon });
    }

    private static WindowIcon LoadIcon()
    {
        using var stream = AssetLoader.Open(new Uri("avares://AxisTune.App/Assets/app.ico"));
        return new WindowIcon(stream);
    }

    private void UpdateTray(EngineStatus status)
    {
        if (_trayIcon is null) return;
        string label = status.State switch
        {
            EngineState.Active => "동작 중",
            EngineState.NoDevice => "장치 선택 필요",
            EngineState.DriverMissing => "ViGEmBus 필요",
            _ => "드라이버 꺼짐",
        };
        _trayIcon.ToolTipText = $"AxisTune — {label}";
        if (_trayToggleItem is not null)
            _trayToggleItem.Header = status.State is EngineState.Active or EngineState.NoDevice
                ? "드라이버 끄기"
                : "드라이버 켜기";
    }

    private void OnMainWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_isExiting) return; // 실제 종료 진행 중

        if (_settings.MinimizeToTrayOnClose)
        {
            e.Cancel = true;
            _mainWindow?.Hide();
        }
        else
        {
            ExitApplication();
        }
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null) return;
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void ExitApplication()
    {
        if (_isExiting) return;
        _isExiting = true;

        ShutdownEngine();
        _trayIcon?.Dispose();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }

    private void ShutdownEngine()
    {
        if (_engineShutDown) return;
        _engineShutDown = true;
        // 가상 패드 분리 + HidHide 복원 + SDL 종료(엔진 스레드 내부에서 수행).
        _engine.Stop();
        _engine.Dispose();
    }

    private static bool HasMinimizedArg()
    {
        foreach (var arg in Environment.GetCommandLineArgs())
            if (string.Equals(arg, "--minimized", StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }
}
