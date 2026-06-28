using AxisTune.Core.Profiles;

namespace AxisTune.App.Services;

public enum CaptureKind
{
    Button,
    Axis,
    Hat,
}

/// <summary>"눌러서 바인딩" 캡처 결과 — 사용자가 처음 작동시킨 물리 입력.</summary>
public readonly record struct CapturedInput(
    CaptureKind Kind,
    int Index,
    int Sign,            // 축: +1/-1 (어느 방향으로 밀었는지)
    HatDirection HatDir) // 햇 방향
{
    public string Describe() => Kind switch
    {
        CaptureKind.Button => $"버튼 {Index}",
        CaptureKind.Axis => $"축 {Index}{(Sign < 0 ? "−" : "+")}",
        CaptureKind.Hat => $"햇 {Index} {HatDir}",
        _ => "?",
    };
}
