using Nefarius.Drivers.HidHide;

namespace AxisTune.Output.HidHide;

/// <summary>
/// HidHide 제어 래퍼. 물리 장치를 게임으로부터 숨기고, 우리 앱 프로세스를
/// 화이트리스트에 등록해 SDL이 해당 장치를 계속 읽을 수 있게 한다.
/// </summary>
public sealed class HidHideController
{
    private readonly HidHideControlService _service = new();
    private readonly List<string> _hidden = new();

    /// <summary>HidHide 드라이버 설치 여부.</summary>
    public bool IsInstalled
    {
        get { try { return _service.IsInstalled; } catch { return false; } }
    }

    /// <summary>드라이버가 정상 동작 가능한 상태인지.</summary>
    public bool IsOperational
    {
        get { try { return _service.IsOperational; } catch { return false; } }
    }

    /// <summary>이 세션에서 실제로 숨긴 장치가 있는지(=원본이 다른 앱에 숨겨졌는지).</summary>
    public bool IsHiding => _hidden.Count > 0;

    /// <summary>
    /// 앱 실행 파일을 화이트리스트에 등록(중복 방지). 등록된 앱은 숨겨진 장치를
    /// 계속 볼 수 있으므로 SDL 입력 읽기가 유지된다.
    /// </summary>
    public void EnsureWhitelisted(string applicationPath)
    {
        if (string.IsNullOrWhiteSpace(applicationPath)) return;

        foreach (var existing in _service.ApplicationPaths)
        {
            if (string.Equals(existing, applicationPath, StringComparison.OrdinalIgnoreCase))
                return;
        }
        _service.AddApplicationPath(applicationPath, throwIfInvalid: false);
    }

    /// <summary>지정한 instance id들을 숨기고 숨김 기능을 활성화한다.</summary>
    public void HideInstances(IReadOnlyList<string> instanceIds)
    {
        foreach (var id in instanceIds)
        {
            _service.AddBlockedInstanceId(id);
            _hidden.Add(id);
        }
        if (_hidden.Count > 0)
            _service.IsActive = true;
    }

    /// <summary>이 세션에서 숨긴 장치를 복원하고 숨김 기능을 비활성화한다.</summary>
    public void Restore()
    {
        try
        {
            foreach (var id in _hidden)
            {
                try { _service.RemoveBlockedInstanceId(id); }
                catch { /* 개별 항목 실패는 무시 */ }
            }
            _service.IsActive = false;
        }
        catch
        {
            // 드라이버가 사라졌거나 접근 불가 — 복원 실패는 치명적이지 않음.
        }
        finally
        {
            _hidden.Clear();
        }
    }
}
