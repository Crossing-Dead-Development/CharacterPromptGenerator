using CharacterPromptGenerator.Core.Localization;
using CharacterPromptGenerator.Mobile.Services;
using CharacterPromptGenerator.Mobile.Views;

namespace CharacterPromptGenerator.Mobile;

public partial class AppShell : Shell
{
    IReadOnlyList<string>? _loadMessages;

    public AppShell(IReadOnlyList<string> loadMessages)
    {
        InitializeComponent();
        Routing.RegisterRoute(DrawSettingsPage.Route, typeof(DrawSettingsPage));
        _loadMessages = loadMessages.Count > 0 ? loadMessages : null;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // 載入時修復、從備份還原等狀況：主畫面出現後告知一次
        if (_loadMessages is not { } messages) return;
        _loadMessages = null;
        await Dialogs.Info(L.T("App_LoadNotes") + string.Join("\n", messages), L.T("App_LoadNotesTitle"));
    }
}
