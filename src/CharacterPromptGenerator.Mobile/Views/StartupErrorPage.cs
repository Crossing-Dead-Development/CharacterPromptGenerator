namespace CharacterPromptGenerator.Mobile.Views;

/// <summary>資料無法開啟時唯一顯示的畫面。不提供任何會寫入資料的操作。</summary>
public sealed class StartupErrorPage : ContentPage
{
    public StartupErrorPage(string title, string message)
    {
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(24, 48),
                Spacing = 16,
                Children =
                {
                    new Label { Text = "⚠ " + title, FontSize = 22, FontAttributes = FontAttributes.Bold },
                    new Label { Text = message, LineBreakMode = LineBreakMode.WordWrap },
                },
            },
        };
    }
}
