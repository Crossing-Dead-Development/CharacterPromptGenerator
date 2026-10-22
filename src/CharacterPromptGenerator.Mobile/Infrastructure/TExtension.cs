using CharacterPromptGenerator.Core.Localization;

namespace CharacterPromptGenerator.Mobile.Infrastructure;

/// <summary>XAML 用的介面文字：{l:T 字串鍵}。語言在啟動時決定一次，與 Windows 版相同。</summary>
[ContentProperty(nameof(Key))]
[AcceptEmptyServiceProvider]
public sealed class TExtension : IMarkupExtension<string>
{
    public string Key { get; set; } = "";

    public string ProvideValue(IServiceProvider serviceProvider) => L.T(Key);

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => ProvideValue(serviceProvider);
}
