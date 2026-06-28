using System.Collections.ObjectModel;
using Avalonia.Threading;
using AxisTune.App.Services;
using AxisTune.Core.Axis;
using AxisTune.Core.Controls;
using AxisTune.Core.Profiles;
using AxisTune.Input.Sdl;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AxisTune.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private static readonly (AxisChannel Channel, string Label)[] ChannelDefs =
    {
        (AxisChannel.LeftStickX, "왼쪽 스틱 · X"),
        (AxisChannel.LeftStickY, "왼쪽 스틱 · Y"),
        (AxisChannel.RightStickX, "오른쪽 스틱 · X"),
        (AxisChannel.RightStickY, "오른쪽 스틱 · Y"),
        (AxisChannel.LeftTrigger, "왼쪽 트리거 (LT)"),
        (AxisChannel.RightTrigger, "오른쪽 트리거 (RT)"),
    };

    private readonly TuningEngine _engine;
    private readonly AppSettings _settings;
    private readonly ProfileDto _profile;

    private readonly DispatcherTimer _previewTimer;
    private readonly DispatcherTimer _saveTimer;

    // 엔진 상태 반영 중 토글 setter 재진입을 막기 위한 가드.
    private bool _suppressToggle;

    public ObservableCollection<DeviceItemViewModel> Devices { get; } = new();
    public ObservableCollection<AxisTuningViewModel> Channels { get; } = new();

    [ObservableProperty] private DeviceItemViewModel? selectedDevice;
    [ObservableProperty] private AxisTuningViewModel? selectedChannel;
    [ObservableProperty] private int selectedTabIndex;
    [ObservableProperty] private bool isDriverEnabled;
    [ObservableProperty] private string statusText = "준비";
    [ObservableProperty] private string statusDetail = string.Empty;
    [ObservableProperty] private string statusColor = "#9AA0A6";
    [ObservableProperty] private bool viGEmAvailable = true;
    [ObservableProperty] private bool hidHideAvailable = true;
    [ObservableProperty] private bool runAtStartup;
    [ObservableProperty] private bool minimizeToTrayOnClose = true;
    [ObservableProperty] private bool autoEnableOnStartup;

    public MainViewModel(TuningEngine engine, AppSettings settings)
    {
        _engine = engine;
        _settings = settings;
        _profile = ProfileStore.Load();

        // 저장된 프로파일을 엔진에 즉시 적용.
        _engine.Profile = ProfileSerializer.ToProfile(_profile);
        BuildChannels();

        _suppressToggle = true;
        RunAtStartup = StartupManager.IsEnabled();
        MinimizeToTrayOnClose = settings.MinimizeToTrayOnClose;
        AutoEnableOnStartup = settings.AutoEnableOnStartup;
        _suppressToggle = false;

        _engine.StatusChanged += OnEngineStatusChanged;

        _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _previewTimer.Tick += OnPreviewTick;

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _saveTimer.Tick += OnSaveTick;
    }

    private void BuildChannels()
    {
        for (int i = 0; i < ChannelDefs.Length; i++)
        {
            var def = ChannelDefs[i];
            var dto = _profile.Axes[i];
            var vm = new AxisTuningViewModel(def.Channel, def.Label, dto);
            vm.Changed += () => OnChannelChanged(vm);
            Channels.Add(vm);
        }
        SelectedChannel = Channels.Count > 0 ? Channels[0] : null;
    }

    private void OnChannelChanged(AxisTuningViewModel channel)
    {
        int index = (int)channel.Channel;
        var dto = channel.ToDto();
        _profile.Axes[index] = dto;

        // 해당 채널만 재빌드하여 원자적으로 교체(hot path 영향 최소화).
        var config = ProfileSerializer.ToAxisConfig(dto);
        _engine.Profile = _engine.Profile.WithAxis(channel.Channel, config);

        // 디스크 저장은 디바운스(드래그 중 과도한 IO 방지).
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void OnSaveTick(object? sender, EventArgs e)
    {
        _saveTimer.Stop();
        ProfileStore.Save(_profile);
    }

    public void ProbeDrivers()
    {
        ViGEmAvailable = DriverStatus.IsViGEmAvailable();
        HidHideAvailable = _engine.IsHidHideInstalled;
    }

    [RelayCommand]
    public void RefreshDevices()
    {
        _engine.RequestEnumerate(list =>
            Dispatcher.UIThread.Post(() => PopulateDevices(list)));
    }

    private void PopulateDevices(IReadOnlyList<DetectedGamepad> list)
    {
        uint? keepId = SelectedDevice?.InstanceId ?? _settings.LastDeviceInstanceId;

        Devices.Clear();
        DeviceItemViewModel? toSelect = null;
        foreach (var d in list)
        {
            var item = new DeviceItemViewModel(d);
            Devices.Add(item);
            if (keepId.HasValue && d.InstanceId == keepId.Value)
                toSelect = item;
        }

        toSelect ??= Devices.Count > 0 ? Devices[0] : null;
        if (toSelect is not null)
            SelectedDevice = toSelect;
    }

    partial void OnSelectedDeviceChanged(DeviceItemViewModel? value)
    {
        if (value is null) return;
        _engine.RequestSelectDevice(value.InstanceId, value.Name, _ => { });
        _settings.LastDeviceInstanceId = value.InstanceId;
        _settings.LastDeviceName = value.Name;
        SettingsStore.Save(_settings);
    }

    partial void OnSelectedTabIndexChanged(int value)
    {
        // 튜닝 탭(1)에서만 프리뷰 발행 + 타이머 가동(유휴 비용 절감).
        bool tuning = value == 1;
        _engine.PreviewEnabled = tuning;
        if (tuning) _previewTimer.Start();
        else
        {
            _previewTimer.Stop();
            foreach (var ch in Channels) ch.PreviewInput = -1;
        }
    }

    private void OnPreviewTick(object? sender, EventArgs e)
    {
        var channel = SelectedChannel;
        if (channel is null) return;

        if (_engine.TryGetSnapshot(out var raw, out var processed))
        {
            float inVal = raw.GetAxis(channel.Channel);
            float outVal = processed.GetAxis(channel.Channel);
            channel.PreviewInput = channel.IsStick ? Math.Abs(inVal) : Math.Max(0f, inVal);
            channel.PreviewOutput = channel.IsStick ? Math.Abs(outVal) : Math.Max(0f, outVal);
        }
    }

    partial void OnIsDriverEnabledChanged(bool value)
    {
        if (_suppressToggle) return;
        _engine.RequestSetEnabled(value);
    }

    partial void OnRunAtStartupChanged(bool value)
    {
        if (_suppressToggle) return;
        StartupManager.SetEnabled(value);
        _settings.RunAtStartup = value;
        SettingsStore.Save(_settings);
    }

    partial void OnMinimizeToTrayOnCloseChanged(bool value)
    {
        if (_suppressToggle) return;
        _settings.MinimizeToTrayOnClose = value;
        SettingsStore.Save(_settings);
    }

    partial void OnAutoEnableOnStartupChanged(bool value)
    {
        if (_suppressToggle) return;
        _settings.AutoEnableOnStartup = value;
        SettingsStore.Save(_settings);
    }

    private void OnEngineStatusChanged(EngineStatus status)
        => Dispatcher.UIThread.Post(() => ApplyStatus(status));

    private void ApplyStatus(EngineStatus status)
    {
        _suppressToggle = true;
        IsDriverEnabled = status.State is EngineState.Active or EngineState.NoDevice;
        _suppressToggle = false;

        (StatusText, StatusColor) = status.State switch
        {
            EngineState.Active => ("동작 중 — 정제된 가상 입력 전달", "#34C759"),
            EngineState.NoDevice => ("장치를 선택하세요", "#FF9F0A"),
            EngineState.DriverMissing => ("ViGEmBus 설치 필요", "#FF3B30"),
            _ => ("드라이버 꺼짐", "#9AA0A6"),
        };

        StatusDetail = status.Message ?? (status.HidHideActive ? "물리 장치 숨김 활성" : string.Empty);
    }
}
