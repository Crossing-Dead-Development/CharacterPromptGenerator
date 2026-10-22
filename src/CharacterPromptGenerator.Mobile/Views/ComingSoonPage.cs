using CharacterPromptGenerator.Core.Localization;

namespace CharacterPromptGenerator.Mobile.Views;

/// <summary>尚未完成的分頁。</summary>
public sealed class ComingSoonPage : ContentPage
{
    public ComingSoonPage()
    {
        Content = new Label
        {
            Text = "🚧 " + L.T("Mobile_ComingSoon"),
            Style = (Style)Application.Current!.Resources["Muted"],
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(32),
        };
    }
}
