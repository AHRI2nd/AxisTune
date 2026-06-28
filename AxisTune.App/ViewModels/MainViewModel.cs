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
    private readonly ProfileDocumentDto _document;
    private NamedProfileDto _activeProfile;

    private readonly DispatcherTimer _previewTimer;
    private readonly DispatcherTimer _saveTimer;

    private bool _suppressToggle;
    private bool _loadingProfile;

    public ObservableCollection<DeviceItemViewModel> Devices { get; } = new();
    public ObservableCollection<ProfileListItemViewModel> Profiles { get; } = new();
    public ObservableCollection<AxisTuningViewModel> Channels { get; } = new();

    public DriverStatusViewModel ViGEmDriver { get; }
    public DriverStatusViewModel HidHideDriver { get; }

    [ObservableProperty] private DeviceItemViewModel? selectedDevice;
    [ObservableProperty] private ProfileListItemViewModel? selectedProfile;
    [ObservableProperty] private AxisTuningViewModel? selectedChannel;
    [ObservableProperty] private MappingViewModel mapping = null!;
    [ObservableProperty] private string activeProfileName = string.Empty;
    [ObservableProperty] private bool manualMappingEnabled;
    [ObservableProperty] private int selectedTabIndex;
    [ObservableProperty] private bool isDriverEnabled;
    [ObservableProperty] private string statusText = "준비";
    [ObservableProperty] private string statusDetail = string.Empty;
    [ObservableProperty] private string statusColor = "#9AA0A6";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AnyDriverMissing))]
    private bool viGEmAvailable = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AnyDriverMissing))]
    private bool hidHideAvailable = true;

    public bool AnyDriverMissing => !ViGEmAvailable || !HidHideAvailable;
    [ObservableProperty] private bool runAtStartup;
    [ObservableProperty] private bool minimizeToTrayOnClose = true;
    [ObservableProperty] private bool autoEnableOnStartup;

    public MainViewModel(TuningEngine engine, AppSettings settings)
    {
        _engine = engine;
        _settings = settings;

        ViGEmDriver = new DriverStatusViewModel(
            DriverKind.ViGEmBus, "ViGEmBus", "가상 Xbox 360 컨트롤러 출력에 필요",
            DriverStatus.IsViGEmAvailable);
        HidHideDriver = new DriverStatusViewModel(
            DriverKind.HidHide, "HidHide", "게임으로부터 물리 컨트롤러를 숨기는 데 필요",
            () => _engine.IsHidHideInstalled);

        _document = ProfileDocumentStore.Load();
        _activeProfile = ResolveActiveProfile();

        foreach (var p in _document.Profiles)
            Profiles.Add(new ProfileListItemViewModel(p.Id, p.Name));

        _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _previewTimer.Tick += OnPreviewTick;
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _saveTimer.Tick += OnSaveTick;

        LoadActiveProfile();

        _suppressToggle = true;
        SelectedProfile = Profiles.FirstOrDefault(p => p.Id == _activeProfile.Id);
        RunAtStartup = StartupManager.IsEnabled();
        MinimizeToTrayOnClose = settings.MinimizeToTrayOnClose;
        AutoEnableOnStartup = settings.AutoEnableOnStartup;
        _suppressToggle = false;

        _engine.StatusChanged += OnEngineStatusChanged;
    }

    private NamedProfileDto ResolveActiveProfile()
    {
        var match = _document.Profiles.FirstOrDefault(p => p.Id == _document.ActiveProfileId);
        return match ?? _document.Profiles[0];
    }

    // ---- 프로파일 로드/전환 ----

    private void LoadActiveProfile()
    {
        _loadingProfile = true;

        EnsureAxisCount(_activeProfile);
        _engine.Profile = ProfileSerializer.ToProfile(_activeProfile.Axes);
        _engine.RequestSetMapping(_activeProfile.Mapping is null
            ? null
            : ProfileSerializer.ToControllerMapping(_activeProfile.Mapping));

        ActiveProfileName = _activeProfile.Name;
        ManualMappingEnabled = _activeProfile.Mapping is not null;

        BuildChannels();

        if (Mapping is not null) Mapping.Changed -= OnMappingChanged;
        Mapping = new MappingViewModel(_engine, _activeProfile.Mapping);
        Mapping.Changed += OnMappingChanged;

        _loadingProfile = false;
    }

    private static void EnsureAxisCount(NamedProfileDto profile)
    {
        var axes = profile.Axes.Axes;
        while (axes.Count < AxisChannelInfo.Count)
        {
            axes.Add(new AxisConfigDto
            {
                Kind = ProfileSerializer.DefaultKind(axes.Count),
                Curve = ProfileSerializer.FromCurveDefinition(Core.Curves.CurveDefinition.Linear()),
            });
        }
    }

    private void BuildChannels()
    {
        foreach (var ch in Channels) ch.Changed -= OnChannelChangedHandler;
        Channels.Clear();
        for (int i = 0; i < ChannelDefs.Length; i++)
        {
            var def = ChannelDefs[i];
            var vm = new AxisTuningViewModel(def.Channel, def.Label, _activeProfile.Axes.Axes[i]);
            vm.Changed += OnChannelChangedHandler;
            Channels.Add(vm);
        }
        SelectedChannel = Channels.Count > 0 ? Channels[0] : null;
    }

    private void OnChannelChangedHandler()
    {
        // 어떤 채널이 바뀌었는지 SelectedChannel 기준으로 처리(드래그/슬라이더는 현재 채널).
        if (SelectedChannel is not null) OnChannelChanged(SelectedChannel);
    }

    partial void OnSelectedProfileChanged(ProfileListItemViewModel? value)
    {
        if (_suppressToggle || value is null) return;
        var profile = _document.Profiles.FirstOrDefault(p => p.Id == value.Id);
        if (profile is null) return;

        _activeProfile = profile;
        _document.ActiveProfileId = profile.Id;
        LoadActiveProfile();
        SaveSoon();
    }

    [RelayCommand]
    private void AddProfile()
    {
        var profile = ProfileSerializer.CreateDefaultProfile($"프로파일 {_document.Profiles.Count + 1}");
        _document.Profiles.Add(profile);
        var item = new ProfileListItemViewModel(profile.Id, profile.Name);
        Profiles.Add(item);
        SelectedProfile = item; // 전환
        SaveSoon();
    }

    [RelayCommand]
    private void DeleteProfile()
    {
        if (_document.Profiles.Count <= 1) return; // 최소 1개 유지
        var current = _document.Profiles.FirstOrDefault(p => p.Id == _activeProfile.Id);
        if (current is null) return;

        _document.Profiles.Remove(current);
        var item = Profiles.FirstOrDefault(p => p.Id == current.Id);
        if (item is not null) Profiles.Remove(item);

        SelectedProfile = Profiles.FirstOrDefault();
        SaveSoon();
    }

    partial void OnActiveProfileNameChanged(string value)
    {
        if (_loadingProfile) return;
        _activeProfile.Name = value;
        var item = Profiles.FirstOrDefault(p => p.Id == _activeProfile.Id);
        if (item is not null) item.Name = value;
        SaveSoon();
    }

    partial void OnManualMappingEnabledChanged(bool value)
    {
        if (_loadingProfile) return;

        if (value)
        {
            // 현재 매핑(비어 있을 수 있음)을 활성화 → 엔진이 조이스틱 모드로 전환.
            _activeProfile.Mapping = Mapping.ToDto();
            _engine.RequestSetMapping(Mapping.ToMapping());
        }
        else
        {
            _activeProfile.Mapping = null;
            _engine.RequestSetMapping(null);
        }
        SaveSoon();
    }

    private void OnMappingChanged()
    {
        if (_loadingProfile) return;
        // 매핑이 켜져 있을 때만 프로파일에 반영(엔진은 MappingViewModel이 이미 갱신).
        if (ManualMappingEnabled)
            _activeProfile.Mapping = Mapping.ToDto();
        SaveSoon();
    }

    private void OnChannelChanged(AxisTuningViewModel channel)
    {
        if (_loadingProfile) return;
        int index = (int)channel.Channel;
        var dto = channel.ToDto();
        _activeProfile.Axes.Axes[index] = dto;

        var config = ProfileSerializer.ToAxisConfig(dto);
        _engine.Profile = _engine.Profile.WithAxis(channel.Channel, config);
        SaveSoon();
    }

    // ---- 저장(디바운스) ----

    private void SaveSoon()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void OnSaveTick(object? sender, EventArgs e)
    {
        _saveTimer.Stop();
        ProfileDocumentStore.Save(_document);
    }

    // ---- 장치 ----

    public void ProbeDrivers()
    {
        ViGEmDriver.Refresh();
        HidHideDriver.Refresh();
        ViGEmAvailable = ViGEmDriver.IsInstalled;
        HidHideAvailable = HidHideDriver.IsInstalled;
    }

    [RelayCommand]
    private void GoToDrivers() => SelectedTabIndex = 3;

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

        // 미인식 장치는 수동 매핑이 필요 → 자동으로 켠다.
        if (!value.IsGamepad && !ManualMappingEnabled)
            ManualMappingEnabled = true;

        _engine.RequestSelectDevice(value.InstanceId, value.Name, value.IsGamepad, _ => { });
        _settings.LastDeviceInstanceId = value.InstanceId;
        _settings.LastDeviceName = value.Name;
        SettingsStore.Save(_settings);
    }

    partial void OnSelectedTabIndexChanged(int value)
    {
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
