using CommunityToolkit.Mvvm.ComponentModel;

namespace AxisTune.App.ViewModels;

/// <summary>커브 에디터의 편집 가능한 제어점([0,1] 정규화). 값 변경 시 알림.</summary>
public partial class CurveControlPoint : ObservableObject
{
    [ObservableProperty] private float x;
    [ObservableProperty] private float y;

    public CurveControlPoint(float x, float y)
    {
        this.x = x;
        this.y = y;
    }
}
