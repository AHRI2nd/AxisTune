using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace AxisTune.App.Localization;

/// <summary>
/// XAML 마크업 확장. <c>{l:Loc Key}</c> 형태로 <see cref="Localizer"/> 인덱서에 바인딩한다.
/// 리플렉션 기반 <see cref="Binding"/>을 반환하므로 컴파일 바인딩 영향 없이 동작한다.
/// </summary>
public sealed class LocExtension : MarkupExtension
{
    public string Key { get; set; } = string.Empty;

    public LocExtension() { }
    public LocExtension(string key) => Key = key;

    public override object ProvideValue(IServiceProvider serviceProvider)
        => new Binding($"[{Key}]")
        {
            Source = Localizer.Instance,
            Mode = BindingMode.OneWay,
        };
}
