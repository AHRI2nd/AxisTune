using Avalonia.Threading;
using AxisTune.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AxisTune.App.ViewModels;

/// <summary>한 드라이버(ViGEmBus/HidHide)의 상태 + 설치/다운로드 동작.</summary>
public partial class DriverStatusViewModel : ObservableObject
{
    private readonly DriverKind _kind;
    private readonly Func<bool> _probe;

    public string Name { get; }
    public string Description { get; }

    [ObservableProperty] private bool isInstalled;
    [ObservableProperty] private string statusText = "확인 중…";
    [ObservableProperty] private string statusColor = "#9AA0A6";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string busyText = string.Empty;

    public DriverStatusViewModel(DriverKind kind, string name, string description, Func<bool> probe)
    {
        _kind = kind;
        _probe = probe;
        Name = name;
        Description = description;
        Refresh();
    }

    public void Refresh()
    {
        IsInstalled = _probe();
        StatusText = IsInstalled ? "설치됨" : "설치 안 됨";
        StatusColor = IsInstalled ? "#34C759" : "#FF3B30";
    }

    [RelayCommand]
    private async Task Install()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            bool ok = await DriverInstaller.DownloadAndRunAsync(
                _kind,
                msg => Dispatcher.UIThread.Post(() => BusyText = msg),
                CancellationToken.None);

            if (!ok)
            {
                // 자산 탐색 실패 → 릴리스 페이지로 폴백.
                BusyText = "릴리스 페이지를 엽니다…";
                DriverInstaller.OpenReleasesPage(_kind);
            }
            else
            {
                BusyText = "설치 후 재부팅이 필요할 수 있습니다.";
            }
        }
        catch
        {
            // 네트워크/다운로드 실패 → 페이지로 폴백.
            DriverInstaller.OpenReleasesPage(_kind);
            BusyText = "다운로드 실패 — 페이지를 열었습니다.";
        }
        finally
        {
            IsBusy = false;
            Refresh();
        }
    }

    [RelayCommand]
    private void OpenPage() => DriverInstaller.OpenReleasesPage(_kind);
}
