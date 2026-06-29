using System.ComponentModel;

namespace AxisTune.App.Localization;

public enum AppLanguage
{
    Korean,
    English,
}

/// <summary>
/// 런타임 다국어 지원. XAML은 <c>{l:Loc Key}</c>로 인덱서에 바인딩하고, 언어 변경 시
/// 빈 문자열 알림으로 모든 바인딩을 재평가시켜 **재시작 없이 즉시 전환**한다.
/// </summary>
public sealed class Localizer : INotifyPropertyChanged
{
    public static Localizer Instance { get; } = new();

    private AppLanguage _language = AppLanguage.Korean;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>언어가 바뀐 뒤 발생(코드에서 만든 동적 문자열 갱신용).</summary>
    public event Action? LanguageChanged;

    public AppLanguage Language => _language;

    /// <summary>XAML 바인딩용 인덱서: <c>{l:Loc Key}</c>.</summary>
    public string this[string key] => Strings.Get(_language, key);

    /// <summary>코드에서 문자열을 얻는다.</summary>
    public string Get(string key) => Strings.Get(_language, key);

    /// <summary>형식 문자열을 얻어 인자를 적용한다.</summary>
    public string Format(string key, params object[] args) => string.Format(Strings.Get(_language, key), args);

    public void SetLanguage(AppLanguage language)
    {
        if (_language == language) return;
        _language = language;
        // 빈 문자열 = "모든 속성 변경" → 모든 {l:Loc} 바인딩 재평가.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        LanguageChanged?.Invoke();
    }
}
