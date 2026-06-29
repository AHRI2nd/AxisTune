using System;
using System.IO;
using System.Threading;
using Avalonia;
using Avalonia.Threading;

namespace AxisTune.App;

class Program
{
    private const string MutexName = "AxisTune-8F3A1C2E-SingleInstance";
    private const string ShowEventName = "AxisTune-8F3A1C2E-ShowWindow";
    private const string QuitEventName = "AxisTune-8F3A1C2E-Quit";

    private static Mutex? _mutex;
    private static EventWaitHandle? _showEvent;
    private static EventWaitHandle? _quitEvent;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // 중복 실행 방지: 이미 실행 중이면 기존 창을 띄우라고 신호하고 종료.
        _mutex = new Mutex(initiallyOwned: true, MutexName, out bool isFirstInstance);
        if (!isFirstInstance)
        {
            try
            {
                if (EventWaitHandle.TryOpenExisting(ShowEventName, out var existing))
                {
                    existing.Set();
                    existing.Dispose();
                }
            }
            catch { /* 신호 실패는 무시 */ }
            return;
        }

        StartSignalListeners();
        SetupGlobalExceptionHandlers();

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            LogCrash("StartupException", ex);
            throw;
        }
        finally
        {
            GC.KeepAlive(_mutex);
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();

    /// <summary>
    /// 외부 신호 처리:
    /// - Show: 두 번째 인스턴스가 보낸 신호 → 기존 창을 앞으로.
    /// - Quit: 제거 관리자가 보낸 신호 → 드라이버 정리 후 정상 종료.
    /// </summary>
    private static void StartSignalListeners()
    {
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        _quitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, QuitEventName);

        var thread = new Thread(() =>
        {
            var handles = new WaitHandle[] { _showEvent, _quitEvent };
            while (true)
            {
                int index;
                try { index = WaitHandle.WaitAny(handles); }
                catch { break; }

                if (index == 0)
                    Dispatcher.UIThread.Post(() => (Application.Current as App)?.ActivateMainWindow());
                else if (index == 1)
                    Dispatcher.UIThread.Post(() => (Application.Current as App)?.RequestQuit());
            }
        })
        {
            IsBackground = true,
            Name = "AxisTune-Signals",
        };
        thread.Start();
    }

    private static void SetupGlobalExceptionHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            LogCrash("UnhandledException", e.ExceptionObject as Exception);
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            LogCrash("UnobservedTaskException", e.Exception);
            e.SetObserved();
        };
    }

    private static void LogCrash(string source, Exception? ex)
    {
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AxisTune");
            Directory.CreateDirectory(dir);
            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}\n{ex}\n\n";
            File.AppendAllText(Path.Combine(dir, "crash.log"), line);
        }
        catch { /* 로깅 실패는 무시 */ }
    }
}
