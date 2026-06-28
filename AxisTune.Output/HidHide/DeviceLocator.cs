using Nefarius.Utilities.DeviceManagement.PnP;

namespace AxisTune.Output.HidHide;

/// <summary>
/// SDL이 보고한 HID 인터페이스 경로(symbolic link)를 HidHide가 숨길 수 있는
/// 장치 instance id로 변환한다. HID 인터페이스 자신과 그 부모 노드를 함께
/// 반환하여, 복합 장치의 모든 HID 컬렉션이 숨겨지도록 한다.
/// </summary>
public static class DeviceLocator
{
    /// <summary>
    /// SDL 장치 경로로부터 숨김 대상 instance id 목록을 best-effort로 해석한다.
    /// 실패 시 빈 목록(숨김 없이 진행) — 호출부에서 사용자에게 안내.
    /// </summary>
    public static IReadOnlyList<string> ResolveHideTargets(string? sdlInterfacePath)
    {
        if (string.IsNullOrWhiteSpace(sdlInterfacePath))
            return Array.Empty<string>();

        var ids = new List<string>();
        try
        {
            string? instanceId = PnPDevice.GetInstanceIdFromInterfaceId(sdlInterfacePath);
            if (string.IsNullOrEmpty(instanceId))
                return ids;

            ids.Add(instanceId);

            var device = PnPDevice.GetDeviceByInstanceId(instanceId, DeviceLocationFlags.Normal);
            var parent = device.Parent;
            if (parent is not null && !string.IsNullOrEmpty(parent.InstanceId))
                ids.Add(parent.InstanceId);
        }
        catch
        {
            // 장치 트리 해석 실패는 치명적이지 않다(숨김만 생략).
        }

        // 중복 제거(대소문자 무시).
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>(ids.Count);
        foreach (var id in ids)
            if (seen.Add(id))
                result.Add(id);
        return result;
    }
}
