using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using AxisTune.App.Localization;
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
    private NativeMenuItem? _trayOpenItem;
    private NativeMenuItem? _trayExitItem;
    private EngineStatus _lastStatus;
    private Size _lastClientSize;
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
            Localization.Localizer.Instance.SetLanguage(_settings.Language);
            _viewModel = new MainViewModel(_engine, _settings);

            _mainWindow = new MainWindow { DataContext = _viewModel };
            RestoreWindowSize();
            _lastClientSize = _mainWindow.ClientSize;
            _mainWindow.SizeChanged += (_, e) => _lastClientSize = e.NewSize;
            _mainWindow.Closing += OnMainWindowClosing;
            desktop.MainWindow = _mainWindow;

            SetupTrayIcon();

            _engine.StatusChanged += s => Dispatcher.UIThread.Post(() => UpdateTray(s));
            desktop.Exit += (_, _) => ShutdownEngine();

            // 엔진 시작 후 드라이버 점검 / 장치 열거 / 자동 적용.
            _engine.Start();
            _viewModel.ProbeDrivers();
            _viewModel.RefreshDevices();
            _viewModel.RunStartupUpdateCheck();
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
        var loc = Localizer.Instance;

        var toggle = new NativeMenuItem(loc.Get("Tray_DriverOn"));
        toggle.Click += (_, _) =>
        {
            if (_viewModel is not null)
                _viewModel.IsDriverEnabled = !_viewModel.IsDriverEnabled;
        };
        _trayToggleItem = toggle;

        var open = new NativeMenuItem(loc.Get("Tray_Open"));
        open.Click += (_, _) => ShowMainWindow();
        _trayOpenItem = open;

        var exit = new NativeMenuItem(loc.Get("Tray_Exit"));
        exit.Click += (_, _) => ExitApplication();
        _trayExitItem = exit;

        var menu = new NativeMenu();
        menu.Items.Add(open);
        menu.Items.Add(toggle);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(exit);

        _trayIcon = new TrayIcon
        {
            Icon = LoadIcon(),
            ToolTipText = loc.Format("Tray_Tip", loc.Get("TrayState_Disabled")),
            Menu = menu,
            IsVisible = true,
        };
        _trayIcon.Clicked += (_, _) => ShowMainWindow();

        TrayIcon.SetIcons(this, new TrayIcons { _trayIcon });

        loc.LanguageChanged += () =>
        {
            if (_trayOpenItem is not null) _trayOpenItem.Header = loc.Get("Tray_Open");
            if (_trayExitItem is not null) _trayExitItem.Header = loc.Get("Tray_Exit");
            UpdateTray(_lastStatus);
        };
    }

    private static WindowIcon LoadIcon()
    {
        using var stream = AssetLoader.Open(new Uri("avares://AxisTune/Assets/app.ico"));
        return new WindowIcon(stream);
    }

    private void UpdateTray(EngineStatus status)
    {
        if (_trayIcon is null) return;
        _lastStatus = status;
        var loc = Localizer.Instance;
        string label = loc.Get(status.State switch
        {
            EngineState.Active => "TrayState_Active",
            EngineState.NoDevice => "TrayState_NoDevice",
            EngineState.DriverMissing => "TrayState_DriverMissing",
            _ => "TrayState_Disabled",
        });
        _trayIcon.ToolTipText = loc.Format("Tray_Tip", label);
        if (_trayToggleItem is not null)
            _trayToggleItem.Header = loc.Get(status.State is EngineState.Active or EngineState.NoDevice
                ? "Tray_DriverOff"
                : "Tray_DriverOn");
    }

    private void RestoreWindowSize()
    {
        if (_mainWindow is null) return;
        if (_settings.WindowWidth is double w && w >= _mainWindow.MinWidth)
            _mainWindow.Width = w;
        if (_settings.WindowHeight is double h && h >= _mainWindow.MinHeight)
            _mainWindow.Height = h;
    }

    private void SaveWindowSize()
    {
        // Width/Height는 리사이즈 후 갱신되지 않으므로 SizeChanged로 추적한 실제 크기를 저장한다.
        var size = _lastClientSize;
        if (size.Width <= 0 || size.Height <= 0)
            size = _mainWindow?.ClientSize ?? default;
        if (size.Width <= 0 || size.Height <= 0) return;
        _settings.WindowWidth = size.Width;
        _settings.WindowHeight = size.Height;
        SettingsStore.Save(_settings);
    }

    private void OnMainWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        SaveWindowSize(); // 닫기(트레이 최소화/종료) 시점의 크기를 저장
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

    /// <summary>두 번째 인스턴스가 실행됐을 때 기존 창을 앞으로 가져온다.</summary>
    public void ActivateMainWindow() => ShowMainWindow();

    /// <summary>외부(제거 관리자 등)에서 정상 종료를 요청 — 드라이버 정리 후 완전 종료.</summary>
    public void RequestQuit() => ExitApplication();

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
