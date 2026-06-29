using Avalonia.Threading;
using AxisTune.App.Localization;
using AxisTune.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AxisTune.App.ViewModels;

/// <summary>한 드라이버(ViGEmBus/HidHide)의 상태 + 설치/다운로드 동작.</summary>
public partial class DriverStatusViewModel : ObservableObject
{
    private readonly DriverKind _kind;
    private readonly Func<bool> _probe;
    private readonly string _descriptionKey;

    public string Name { get; }
    public string Description => Localizer.Instance.Get(_descriptionKey);

    [ObservableProperty] private bool isInstalled;
    [ObservableProperty] private string statusText = string.Empty;
    [ObservableProperty] private string statusColor = "#9AA0A6";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string busyText = string.Empty;

    public DriverStatusViewModel(DriverKind kind, string name, string descriptionKey, Func<bool> probe)
    {
        _kind = kind;
        _probe = probe;
        _descriptionKey = descriptionKey;
        Name = name;
        Refresh();
    }

    public void Refresh()
    {
        IsInstalled = _probe();
        StatusText = Localizer.Instance.Get(IsInstalled ? "Drv_Installed" : "Drv_NotInstalled");
        StatusColor = IsInstalled ? "#34C759" : "#FF3B30";
    }

    public void RefreshLocalized()
    {
        Refresh();
        OnPropertyChanged(nameof(Description));
    }

    [RelayCommand]
    private async Task Install()
    {
        if (IsBusy) return;
        IsBusy = true;
        var loc = Localizer.Instance;
        try
        {
            bool ok = await DriverInstaller.DownloadAndRunAsync(
                _kind,
                key => Dispatcher.UIThread.Post(() => BusyText = loc.Get(key)),
                CancellationToken.None);

            if (!ok)
            {
                BusyText = loc.Get("Drv_Busy_OpenPage");
                DriverInstaller.OpenReleasesPage(_kind);
            }
            else
            {
                BusyText = loc.Get("Drv_Busy_Reboot");
            }
        }
        catch
        {
            DriverInstaller.OpenReleasesPage(_kind);
            BusyText = loc.Get("Drv_Busy_Failed");
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
