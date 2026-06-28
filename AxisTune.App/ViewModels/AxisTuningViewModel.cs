using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using AxisTune.Core.Axis;
using AxisTune.Core.Controls;
using AxisTune.Core.Curves;
using AxisTune.Core.Profiles;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AxisTune.App.ViewModels;

/// <summary>단일 아날로그 채널의 편집 모델. 변경 시 <see cref="Changed"/>를 발생시켜
/// 상위 VM이 실시간으로 엔진 프로파일을 재적용하도록 한다.</summary>
public partial class AxisTuningViewModel : ObservableObject
{
    public AxisChannel Channel { get; }
    public string Label { get; }
    public AxisKind Kind { get; }
    public bool IsStick => Kind == AxisKind.Bipolar;

    public ObservableCollection<CurveControlPoint> Points { get; } = new();

    [ObservableProperty] private double inputMin;
    [ObservableProperty] private double inputMax = 1;
    [ObservableProperty] private double innerDeadzone;
    [ObservableProperty] private double outerDeadzone;
    [ObservableProperty] private bool invert;
    [ObservableProperty] private CurveInterpolation interpolation = CurveInterpolation.MonotoneCubic;

    // 실시간 프리뷰(엔진 스냅샷에서 갱신, 저장/재적용 트리거 아님).
    [ObservableProperty] private double previewInput = -1;
    [ObservableProperty] private double previewOutput;

    /// <summary>편집값(곡선/데드존/범위/인버트/보간)이 바뀌면 발생.</summary>
    public event Action? Changed;

    private bool _loading;

    public AxisTuningViewModel(AxisChannel channel, string label, AxisConfigDto dto)
    {
        Channel = channel;
        Label = label;
        Kind = dto.Kind;
        LoadFrom(dto);

        Points.CollectionChanged += OnPointsChanged;
    }

    public void LoadFrom(AxisConfigDto dto)
    {
        _loading = true;

        InputMin = dto.InputMin;
        InputMax = dto.InputMax;
        InnerDeadzone = dto.InnerDeadzone;
        OuterDeadzone = dto.OuterDeadzone;
        Invert = dto.Invert;
        Interpolation = dto.Curve.Interpolation;

        foreach (var p in Points) p.PropertyChanged -= OnPointPropertyChanged;
        Points.Clear();
        if (dto.Curve.Points.Count >= 2)
            foreach (var p in dto.Curve.Points)
                Points.Add(new CurveControlPoint(p.X, p.Y));
        else
            SetLinearPoints();
        foreach (var p in Points) p.PropertyChanged += OnPointPropertyChanged;

        _loading = false;
    }

    public AxisConfigDto ToDto()
    {
        var dto = new AxisConfigDto
        {
            Kind = Kind,
            InputMin = (float)InputMin,
            InputMax = (float)InputMax,
            InnerDeadzone = (float)InnerDeadzone,
            OuterDeadzone = (float)OuterDeadzone,
            Invert = Invert,
            Curve = new CurveDto { Interpolation = Interpolation },
        };
        foreach (var p in Points)
            dto.Curve.Points.Add(new CurvePointDto { X = p.X, Y = p.Y });
        return dto;
    }

    // ---- 프리셋 ----

    [RelayCommand]
    private void PresetLinear() => ReplacePoints(CurveDefinition.Linear());

    [RelayCommand]
    private void PresetAggressive() => ReplacePoints(CurveDefinition.Aggressive());

    [RelayCommand]
    private void PresetSmooth() => ReplacePoints(CurveDefinition.Smooth());

    private void ReplacePoints(CurveDefinition def)
    {
        _loading = true;
        foreach (var p in Points) p.PropertyChanged -= OnPointPropertyChanged;
        Points.Clear();
        foreach (var p in def.Points)
        {
            var cp = new CurveControlPoint(p.X, p.Y);
            cp.PropertyChanged += OnPointPropertyChanged;
            Points.Add(cp);
        }
        Interpolation = def.Interpolation;
        _loading = false;
        RaiseChanged();
    }

    private void SetLinearPoints()
    {
        Points.Add(new CurveControlPoint(0f, 0f));
        Points.Add(new CurveControlPoint(1f, 1f));
    }

    // ---- 변경 알림 배선 ----

    private void OnPointsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
            foreach (CurveControlPoint p in e.OldItems) p.PropertyChanged -= OnPointPropertyChanged;
        if (e.NewItems is not null)
            foreach (CurveControlPoint p in e.NewItems) p.PropertyChanged += OnPointPropertyChanged;
        RaiseChanged();
    }

    private void OnPointPropertyChanged(object? sender, PropertyChangedEventArgs e) => RaiseChanged();

    partial void OnInputMinChanged(double value) => RaiseChanged();
    partial void OnInputMaxChanged(double value) => RaiseChanged();
    partial void OnInnerDeadzoneChanged(double value) => RaiseChanged();
    partial void OnOuterDeadzoneChanged(double value) => RaiseChanged();
    partial void OnInvertChanged(bool value) => RaiseChanged();
    /// <summary>ComboBox 바인딩용: 0 = Linear, 1 = MonotoneCubic.</summary>
    public int InterpolationIndex
    {
        get => Interpolation == CurveInterpolation.Linear ? 0 : 1;
        set => Interpolation = value == 0 ? CurveInterpolation.Linear : CurveInterpolation.MonotoneCubic;
    }

    partial void OnInterpolationChanged(CurveInterpolation value)
    {
        OnPropertyChanged(nameof(InterpolationIndex));
        RaiseChanged();
    }

    private void RaiseChanged()
    {
        if (_loading) return;
        Changed?.Invoke();
    }
}
