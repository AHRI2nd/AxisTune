using System.Collections.ObjectModel;
using Avalonia.Threading;
using AxisTune.App.Localization;
using AxisTune.App.Services;
using AxisTune.Core.Controls;
using AxisTune.Core.Profiles;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AxisTune.App.ViewModels;

/// <summary>활성 프로파일의 수동 매핑 편집. "눌러서 바인딩" 캡처를 엔진과 연동한다.</summary>
public partial class MappingViewModel : ObservableObject
{
    // LabelKey는 현지화 키이거나(예: MapT_LS, Ch_LSX) 그대로 표시할 기호(A, D-Pad ↑).
    private static readonly (string LabelKey, XboxButton Button)[] ButtonTargets =
    {
        ("A", XboxButton.A), ("B", XboxButton.B), ("X", XboxButton.X), ("Y", XboxButton.Y),
        ("LB", XboxButton.LeftShoulder), ("RB", XboxButton.RightShoulder),
        ("Back", XboxButton.Back), ("Start", XboxButton.Start), ("Guide", XboxButton.Guide),
        ("MapT_LS", XboxButton.LeftThumb), ("MapT_RS", XboxButton.RightThumb),
        ("D-Pad ↑", XboxButton.DpadUp), ("D-Pad ↓", XboxButton.DpadDown),
        ("D-Pad ←", XboxButton.DpadLeft), ("D-Pad →", XboxButton.DpadRight),
    };

    private static readonly (string LabelKey, AxisChannel Axis)[] AxisTargets =
    {
        ("Ch_LSX", AxisChannel.LeftStickX), ("Ch_LSY", AxisChannel.LeftStickY),
        ("Ch_RSX", AxisChannel.RightStickX), ("Ch_RSY", AxisChannel.RightStickY),
        ("Ch_LT", AxisChannel.LeftTrigger), ("Ch_RT", AxisChannel.RightTrigger),
    };

    private readonly TuningEngine _engine;
    private readonly Dictionary<XboxButton, (CaptureKind Kind, int Index, HatDirection Dir)> _buttons = new();
    private readonly Dictionary<AxisChannel, (int Index, bool Invert)> _axes = new();

    public ObservableCollection<BindingRowViewModel> ButtonRows { get; } = new();
    public ObservableCollection<BindingRowViewModel> AxisRows { get; } = new();

    [ObservableProperty] private bool isCapturing;

    /// <summary>매핑이 바뀌면 발생(상위 VM이 저장 + 엔진 적용).</summary>
    public event Action? Changed;

    private BindingRowViewModel? _capturingRow;

    public MappingViewModel(TuningEngine engine, ControllerMappingDto? dto)
    {
        _engine = engine;
        BuildRows();
        LoadFrom(dto);
    }

    private void BuildRows()
    {
        foreach (var t in ButtonTargets)
            ButtonRows.Add(BindingRowViewModel.ForButton(t.LabelKey, t.Button, BeginBind, ClearBinding));
        foreach (var t in AxisTargets)
            AxisRows.Add(BindingRowViewModel.ForAxis(t.LabelKey, t.Axis, BeginBind, ClearBinding));
    }

    /// <summary>언어 변경 시 라벨·바인딩 텍스트를 다시 현지화.</summary>
    public void RefreshLocalized()
    {
        foreach (var r in ButtonRows) r.RefreshLocalized();
        foreach (var r in AxisRows) r.RefreshLocalized();
        RefreshAll();
    }

    public void LoadFrom(ControllerMappingDto? dto)
    {
        _buttons.Clear();
        _axes.Clear();
        if (dto is not null)
        {
            foreach (var b in dto.Buttons) _buttons[b.Target] = (CaptureKind.Button, b.PhysicalButton, default);
            foreach (var h in dto.Hats) _buttons[h.Target] = (CaptureKind.Hat, h.Hat, h.Direction);
            foreach (var a in dto.Axes) _axes[a.Target] = (a.PhysicalAxis, a.Invert);
        }
        RefreshAll();
    }

    public ControllerMappingDto ToDto()
    {
        var dto = new ControllerMappingDto();
        foreach (var kv in _buttons)
        {
            if (kv.Value.Kind == CaptureKind.Button)
                dto.Buttons.Add(new ButtonBindingDto { PhysicalButton = kv.Value.Index, Target = kv.Key });
            else if (kv.Value.Kind == CaptureKind.Hat)
                dto.Hats.Add(new HatBindingDto { Hat = kv.Value.Index, Direction = kv.Value.Dir, Target = kv.Key });
        }
        foreach (var kv in _axes)
            dto.Axes.Add(new AxisBindingDto { PhysicalAxis = kv.Value.Index, Target = kv.Key, Invert = kv.Value.Invert });
        return dto;
    }

    public ControllerMapping ToMapping() => ProfileSerializer.ToControllerMapping(ToDto());

    [RelayCommand]
    private void ClearAll()
    {
        _buttons.Clear();
        _axes.Clear();
        RefreshAll();
        RaiseChanged();
    }

    private void BeginBind(BindingRowViewModel row)
    {
        // 진행 중이던 캡처가 있으면 취소.
        if (_capturingRow is not null)
        {
            _engine.RequestCancelCapture();
            _capturingRow.IsCapturing = false;
        }

        _capturingRow = row;
        row.IsCapturing = true;
        IsCapturing = true;
        _engine.RequestCaptureInput(result =>
            Dispatcher.UIThread.Post(() => OnCaptured(row, result)));
    }

    private void OnCaptured(BindingRowViewModel row, CapturedInput? captured)
    {
        row.IsCapturing = false;
        IsCapturing = false;
        _capturingRow = null;

        if (captured is null) return;
        var cap = captured.Value;

        if (!row.IsAxis)
        {
            if (cap.Kind == CaptureKind.Button)
                _buttons[row.Button] = (CaptureKind.Button, cap.Index, default);
            else if (cap.Kind == CaptureKind.Hat)
                _buttons[row.Button] = (CaptureKind.Hat, cap.Index, cap.HatDir);
            else
                return; // 축 입력은 버튼 타깃에 매핑하지 않음
        }
        else
        {
            if (cap.Kind == CaptureKind.Axis)
                _axes[row.Axis] = (cap.Index, cap.Sign < 0);
            else
                return; // 버튼/햇은 축 타깃에 매핑하지 않음
        }

        RefreshRow(row);
        RaiseChanged();
    }

    private void ClearBinding(BindingRowViewModel row)
    {
        if (row.IsAxis) _axes.Remove(row.Axis);
        else _buttons.Remove(row.Button);
        RefreshRow(row);
        RaiseChanged();
    }

    private void RefreshAll()
    {
        foreach (var r in ButtonRows) RefreshRow(r);
        foreach (var r in AxisRows) RefreshRow(r);
    }

    private void RefreshRow(BindingRowViewModel row)
    {
        var loc = Localizer.Instance;
        if (row.IsAxis)
        {
            row.BindingText = _axes.TryGetValue(row.Axis, out var a)
                ? loc.Format("Map_Axis", a.Index, a.Invert ? "−" : "+")
                : loc.Get("Map_None");
        }
        else
        {
            row.BindingText = _buttons.TryGetValue(row.Button, out var b)
                ? (b.Kind == CaptureKind.Button ? loc.Format("Map_Btn", b.Index) : loc.Format("Map_Hat", b.Index, b.Dir))
                : loc.Get("Map_None");
        }
    }

    private void RaiseChanged()
    {
        _engine.RequestSetMapping(ToMapping());
        Changed?.Invoke();
    }
}
