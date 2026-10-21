using System.Windows.Markup;
using CharacterPromptGenerator.Core.Localization;

namespace CharacterPromptGenerator.App.Infrastructure;

/// <summary>XAML 取介面文字：Text="{l:T Draw_Generate}"。語言在啟動時就決定，不需要繫結。</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TExtension(string key) : MarkupExtension
{
    public string Key { get; set; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider) => L.T(Key);
}
